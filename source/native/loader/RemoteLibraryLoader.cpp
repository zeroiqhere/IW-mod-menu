#include "RemoteLibraryLoader.h"

#include <psapi.h>
#include <array>
#include <cstdint>
#include <cstring>
#include <limits>
#include <stdexcept>

namespace iw::loader {
namespace {

class Handle {
public:
    explicit Handle(HANDLE handle) : value_(handle) {
        if (!handle || handle == INVALID_HANDLE_VALUE) throw std::runtime_error("A loader handle could not be opened");
    }
    ~Handle() { CloseHandle(value_); }
    Handle(const Handle&) = delete;
    Handle& operator=(const Handle&) = delete;
    HANDLE Get() const { return value_; }
private:
    HANDLE value_;
};

std::filesystem::path FullPath(const std::filesystem::path& path) {
    std::array<wchar_t, 32768> buffer{};
    DWORD count = GetFullPathNameW(path.c_str(), static_cast<DWORD>(buffer.size()), buffer.data(), nullptr);
    if (count == 0 || count >= buffer.size()) throw std::runtime_error("A module path could not be resolved");
    return buffer.data();
}

bool SamePath(const std::filesystem::path& left, const std::filesystem::path& right) {
    return _wcsicmp(FullPath(left).c_str(), FullPath(right).c_str()) == 0;
}

template<class T> T ReadRemote(HANDLE process, const void* address) {
    T value{};
    SIZE_T count = 0;
    if (!ReadProcessMemory(process, address, &value, sizeof(value), &count) || count != sizeof(value)) {
        throw std::runtime_error("The remote system image could not be read completely");
    }
    return value;
}

bool Executable(DWORD protection) {
    return !(protection & (PAGE_GUARD | PAGE_NOACCESS))
        && (protection & (PAGE_EXECUTE | PAGE_EXECUTE_READ | PAGE_EXECUTE_READWRITE | PAGE_EXECUTE_WRITECOPY));
}

}

std::optional<MODULEENTRY32W> FindExactModule(DWORD processId, const std::filesystem::path& path,
    bool rejectOtherPathsWithSameName) {
    for (unsigned attempt = 0; attempt < 4; ++attempt) {
        HANDLE raw = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, processId);
        if (raw == INVALID_HANDLE_VALUE && GetLastError() == ERROR_BAD_LENGTH) {
            // A moving module list receives a short bounded retry, never a partial result
            Sleep(5);
            continue;
        }
        Handle snapshot(raw);
        MODULEENTRY32W entry{};
        entry.dwSize = sizeof(entry);
        std::optional<MODULEENTRY32W> result;
        if (!Module32FirstW(snapshot.Get(), &entry)) {
            if (GetLastError() == ERROR_BAD_LENGTH) { Sleep(5); continue; }
            throw std::runtime_error("The target module list could not be read");
        }
        do {
            bool exact = SamePath(entry.szExePath, path);
            if (rejectOtherPathsWithSameName && !exact
                && _wcsicmp(std::filesystem::path(entry.szExePath).filename().c_str(), path.filename().c_str()) == 0) {
                throw std::runtime_error("Another DLL with the payload's name is already loaded; restart before another attempt");
            }
            if (exact) {
                if (result) throw std::runtime_error("The exact module path is ambiguous");
                result = entry;
            }
        } while (Module32NextW(snapshot.Get(), &entry));
        DWORD error = GetLastError();
        if (error == ERROR_BAD_LENGTH) { Sleep(5); continue; }
        if (error != ERROR_NO_MORE_FILES) throw std::runtime_error("The target module list was incomplete");
        return result;
    }
    throw std::runtime_error("The target module list did not become stable within the retry bound");
}

void* ResolveRemoteLoadLibrary(HANDLE process, DWORD processId) {
    BOOL wow64 = FALSE;
    SYSTEM_INFO system{};
    GetNativeSystemInfo(&system);
    if (system.wProcessorArchitecture != PROCESSOR_ARCHITECTURE_AMD64
        || !IsWow64Process(process, &wow64) || wow64) {
        throw std::runtime_error("The target must be a native AMD64 process");
    }
    HMODULE kernel = GetModuleHandleW(L"kernel32.dll");
    FARPROC loadLibrary = kernel ? GetProcAddress(kernel, "LoadLibraryW") : nullptr;
    MEMORY_BASIC_INFORMATION localProtection{};
    if (!loadLibrary || !VirtualQuery(reinterpret_cast<const void*>(loadLibrary), &localProtection, sizeof(localProtection))
        || localProtection.State != MEM_COMMIT || localProtection.Type != MEM_IMAGE || !Executable(localProtection.Protect)) {
        throw std::runtime_error("The local LoadLibraryW export is not executable image code");
    }
    // Forwarded exports are resolved to the system image that actually owns the code
    auto owner = static_cast<HMODULE>(localProtection.AllocationBase);
    std::array<wchar_t, 32768> modulePath{};
    std::array<wchar_t, 32768> systemPath{};
    DWORD moduleCount = GetModuleFileNameW(owner, modulePath.data(), static_cast<DWORD>(modulePath.size()));
    UINT systemCount = GetSystemDirectoryW(systemPath.data(), static_cast<UINT>(systemPath.size()));
    if (moduleCount == 0 || moduleCount >= modulePath.size() || systemCount == 0 || systemCount >= systemPath.size()) {
        throw std::runtime_error("The system loader image path could not be confirmed");
    }
    std::filesystem::path localPath(modulePath.data());
    auto name = localPath.filename();
    if (!SamePath(localPath.parent_path(), systemPath.data())
        || (_wcsicmp(name.c_str(), L"kernel32.dll") != 0 && _wcsicmp(name.c_str(), L"kernelbase.dll") != 0)) {
        throw std::runtime_error("LoadLibraryW resolves outside its supported system images");
    }
    MODULEINFO localInfo{};
    if (!GetModuleInformation(GetCurrentProcess(), owner, &localInfo, sizeof(localInfo))
        || localInfo.lpBaseOfDll != owner || localInfo.SizeOfImage == 0) {
        throw std::runtime_error("The local system module bounds could not be confirmed");
    }
    auto base = reinterpret_cast<std::uintptr_t>(owner);
    auto function = reinterpret_cast<std::uintptr_t>(loadLibrary);
    constexpr std::size_t CodeBytes = 16;
    if (function < base || function - base > localInfo.SizeOfImage
        || CodeBytes > localInfo.SizeOfImage - (function - base)) {
        throw std::runtime_error("The system loader export exceeds its owning image");
    }
    auto remoteModule = FindExactModule(processId, localPath);
    if (!remoteModule || remoteModule->modBaseSize != localInfo.SizeOfImage) {
        throw std::runtime_error("The exact remote system loader image was not found");
    }
    // Matching PE identity and code bytes validate the RVA before applying a remote base
    auto localDos = ReadRemote<IMAGE_DOS_HEADER>(GetCurrentProcess(), owner);
    auto remoteDos = ReadRemote<IMAGE_DOS_HEADER>(process, remoteModule->modBaseAddr);
    if (localDos.e_magic != IMAGE_DOS_SIGNATURE || remoteDos.e_magic != IMAGE_DOS_SIGNATURE
        || localDos.e_lfanew < static_cast<LONG>(sizeof(localDos)) || localDos.e_lfanew != remoteDos.e_lfanew
        || static_cast<std::size_t>(localDos.e_lfanew) + sizeof(IMAGE_NT_HEADERS64) > localInfo.SizeOfImage) {
        throw std::runtime_error("The system module headers do not match");
    }
    auto localNt = ReadRemote<IMAGE_NT_HEADERS64>(GetCurrentProcess(), reinterpret_cast<const std::byte*>(owner) + localDos.e_lfanew);
    auto remoteNt = ReadRemote<IMAGE_NT_HEADERS64>(process, remoteModule->modBaseAddr + remoteDos.e_lfanew);
    if (localNt.Signature != IMAGE_NT_SIGNATURE || remoteNt.Signature != IMAGE_NT_SIGNATURE
        || localNt.FileHeader.Machine != IMAGE_FILE_MACHINE_AMD64 || remoteNt.FileHeader.Machine != IMAGE_FILE_MACHINE_AMD64
        || localNt.OptionalHeader.Magic != IMAGE_NT_OPTIONAL_HDR64_MAGIC || remoteNt.OptionalHeader.Magic != IMAGE_NT_OPTIONAL_HDR64_MAGIC
        || localNt.FileHeader.TimeDateStamp != remoteNt.FileHeader.TimeDateStamp
        || localNt.OptionalHeader.SizeOfImage != remoteNt.OptionalHeader.SizeOfImage
        || remoteNt.OptionalHeader.SizeOfImage != remoteModule->modBaseSize) {
        throw std::runtime_error("The remote system DLL differs from the local loader image");
    }
    void* remoteEntry = remoteModule->modBaseAddr + (function - base);
    MEMORY_BASIC_INFORMATION remoteProtection{};
    if (!VirtualQueryEx(process, remoteEntry, &remoteProtection, sizeof(remoteProtection))
        || remoteProtection.State != MEM_COMMIT || remoteProtection.Type != MEM_IMAGE
        || remoteProtection.AllocationBase != remoteModule->modBaseAddr || !Executable(remoteProtection.Protect)) {
        throw std::runtime_error("The remote system loader address is not executable image code");
    }
    auto localBytes = ReadRemote<std::array<std::byte, CodeBytes>>(GetCurrentProcess(), reinterpret_cast<const void*>(loadLibrary));
    auto remoteBytes = ReadRemote<std::array<std::byte, CodeBytes>>(process, remoteEntry);
    if (localBytes != remoteBytes) throw std::runtime_error("The remote system loader code does not match");
    return remoteEntry;
}

void LoadRemoteLibrary(HANDLE process, DWORD processId, const std::filesystem::path& payload) {
    if (WaitForSingleObject(process, 0) != WAIT_TIMEOUT) throw std::runtime_error("The target process is no longer alive");
    if (FindExactModule(processId, payload, true)) throw std::runtime_error("The payload is already loaded; restart before another attempt");
    void* entry = ResolveRemoteLoadLibrary(process, processId);
    std::wstring path = FullPath(payload).wstring();
    if (path.empty() || path.size() >= 32767) throw std::runtime_error("The DLL path exceeds supported bounds");
    SIZE_T size = (path.size() + 1) * sizeof(wchar_t);
    void* remote = VirtualAllocEx(process, nullptr, size, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    if (!remote) throw std::runtime_error("The DLL path buffer could not be allocated");
    SIZE_T written = 0;
    if (!WriteProcessMemory(process, remote, path.c_str(), size, &written) || written != size) {
        VirtualFreeEx(process, remote, 0, MEM_RELEASE);
        throw std::runtime_error("The DLL path buffer could not be written completely");
    }
    HANDLE thread = CreateRemoteThread(process, nullptr, 0,
        reinterpret_cast<LPTHREAD_START_ROUTINE>(entry), remote, 0, nullptr);
    if (!thread) {
        VirtualFreeEx(process, remote, 0, MEM_RELEASE);
        throw std::runtime_error("The checked LoadLibraryW call could not be started");
    }
    Handle worker(thread);
    DWORD waited = WaitForSingleObject(worker.Get(), 15000);
    if (waited != WAIT_OBJECT_0) {
        // A running loader still owns its buffer, so timeout never frees or kills that memory
        throw std::runtime_error("The DLL loader did not finish; restart before another attempt");
    }
    DWORD code = 0;
    BOOL reported = GetExitCodeThread(worker.Get(), &code);
    VirtualFreeEx(process, remote, 0, MEM_RELEASE);
    // A 64-bit HMODULE cannot be recovered from a thread's 32-bit exit code
    if (!reported || !FindExactModule(processId, payload)) {
        throw std::runtime_error("The exact DLL path was not loaded into the target");
    }
}

}
