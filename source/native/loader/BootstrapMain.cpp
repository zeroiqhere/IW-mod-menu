#include "PeExport.h"
#include "RemoteLibraryLoader.h"
#include "BootstrapPacket.h"
#include "../shared/OverlayProtocol.h"

#include <windows.h>
#include <psapi.h>
#include <shellapi.h>
#include <tlhelp32.h>
#include <nlohmann/json.hpp>
#include <array>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <memory>
#include <stdexcept>
#include <string>
#include <vector>

namespace {
using json = nlohmann::json;

class Handle {
public:
    explicit Handle(HANDLE value) : value(value) {
        if (!value || value == INVALID_HANDLE_VALUE) {
            throw std::runtime_error("A required Windows handle could not be opened");
        }
    }
    ~Handle() { CloseHandle(value); }
    Handle(const Handle&) = delete;
    Handle& operator=(const Handle&) = delete;
    HANDLE Get() const { return value; }
private:
    HANDLE value;
};

std::filesystem::path FullPath(const std::filesystem::path& path) {
    std::array<wchar_t, 32768> buffer{};
    DWORD count = GetFullPathNameW(path.c_str(), static_cast<DWORD>(buffer.size()), buffer.data(), nullptr);
    if (count == 0 || count >= buffer.size()) {
        throw std::runtime_error("The supplied path could not be resolved");
    }
    return std::filesystem::path(buffer.data());
}

void WriteResult(const std::filesystem::path& directory, const char* name, const json& result) {
    auto temporary = directory / (std::string(name) + ".tmp");
    auto destination = directory / name;
    std::ofstream output(temporary, std::ios::binary | std::ios::trunc);
    output << result.dump();
    output.close();
    if (!output || !MoveFileExW(temporary.c_str(), destination.c_str(), MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH)) {
        throw std::runtime_error("The bootstrap result could not be published");
    }
}

bool IsGame(HANDLE process) {
    std::array<wchar_t, 32768> image{};
    DWORD size = static_cast<DWORD>(image.size());
    if (!QueryFullProcessImageNameW(process, 0, image.data(), &size)) {
        return false;
    }
#if defined(IW_BOOTSTRAP_FIXTURE)
    // The test helper cannot open the actual game, even when both processes are running
    return _wcsicmp(std::filesystem::path(image.data()).filename().c_str(), L"Dx11Fixture.exe") == 0;
#else
    return _wcsicmp(std::filesystem::path(image.data()).filename().c_str(), L"iw7_ship.exe") == 0;
#endif
}

DWORD FindGame() {
    std::array<DWORD, 4096> pids{};
    DWORD bytes = 0;
    if (!EnumProcesses(pids.data(), static_cast<DWORD>(sizeof(pids)), &bytes) || bytes >= sizeof(pids)) {
        throw std::runtime_error("The game process list could not be bounded");
    }
    DWORD selected = 0;
    for (std::size_t index = 0; index < bytes / sizeof(DWORD); ++index) {
        HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pids[index]);
        if (!process) {
            continue;
        }
        bool game = IsGame(process);
        CloseHandle(process);
        if (game) {
            if (selected != 0) {
                throw std::runtime_error("Multiple Infinite Warfare processes cannot be resolved");
            }
            selected = pids[index];
        }
    }
    if (!selected) {
        throw std::runtime_error("Launch the normal Steam game and spawn first");
    }
    return selected;
}

struct WindowSearch { DWORD pid; HWND selected = nullptr; bool ambiguous = false; };
BOOL CALLBACK FindWindow(HWND window, LPARAM parameter) {
    auto& search = *reinterpret_cast<WindowSearch*>(parameter);
    DWORD pid = 0;
    RECT rectangle{};
    GetWindowThreadProcessId(window, &pid);
    // The existing visible game surface is the only permitted render target
    if (pid == search.pid && IsWindowVisible(window) && GetClientRect(window, &rectangle)
        && rectangle.right >= 640 && rectangle.bottom >= 480) {
        if (search.selected) {
            search.ambiguous = true;
        }
        search.selected = window;
    }
    return TRUE;
}

HWND GameWindow(DWORD pid) {
    WindowSearch search{pid};
    if (!EnumWindows(FindWindow, reinterpret_cast<LPARAM>(&search)) || !search.selected || search.ambiguous) {
        throw std::runtime_error("Exactly one visible game render window is required");
    }
    return search.selected;
}

std::vector<std::byte> ReadPayload(const std::filesystem::path& path) {
    auto size = std::filesystem::file_size(path);
    if (size == 0 || size > 64 * 1024 * 1024) {
        throw std::runtime_error("The payload exceeds supported file bounds");
    }
    std::vector<std::byte> bytes(static_cast<std::size_t>(size));
    std::ifstream input(path, std::ios::binary);
    input.read(reinterpret_cast<char*>(bytes.data()), static_cast<std::streamsize>(bytes.size()));
    if (!input || input.gcount() != static_cast<std::streamsize>(bytes.size())) {
        throw std::runtime_error("The payload file could not be read completely");
    }
    return bytes;
}

MODULEENTRY32W FindModule(DWORD pid, const std::filesystem::path& payload) {
    auto selected = iw::loader::FindExactModule(pid, payload);
    if (!selected) {
        throw std::runtime_error("The exact payload path has not been loaded into the game");
    }
    return *selected;
}

template<class T> T ReadRemote(HANDLE process, const void* address) {
    T value{};
    SIZE_T count = 0;
    if (!ReadProcessMemory(process, address, &value, sizeof(value), &count) || count != sizeof(value)) {
        throw std::runtime_error("The loaded DLL metadata could not be confirmed");
    }
    return value;
}

DWORD Initialize(DWORD pid, HWND window, const std::filesystem::path& directory,
                 const std::filesystem::path& payload) {
    auto bytes = ReadPayload(payload);
    auto metadata = iw::loader::InspectExport(bytes, "InitializeOverlay");
    auto config = iw::loader::MakeBootstrapPacket(pid, window, directory.wstring());
    Handle process(OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_CREATE_THREAD
        | PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE | SYNCHRONIZE, FALSE, pid));
    if (!IsGame(process.Get()) || GameWindow(pid) != window) {
        throw std::runtime_error("The original game identity changed before bootstrap");
    }
    // The checked Windows loader runs first; initialization remains a separate validated call
    iw::loader::LoadRemoteLibrary(process.Get(), pid, payload);
    auto module = FindModule(pid, payload);
    auto dos = ReadRemote<IMAGE_DOS_HEADER>(process.Get(), module.modBaseAddr);
    if (dos.e_magic != IMAGE_DOS_SIGNATURE || dos.e_lfanew < static_cast<LONG>(sizeof(dos))
        || static_cast<std::size_t>(dos.e_lfanew) + sizeof(IMAGE_NT_HEADERS64) > module.modBaseSize) {
        throw std::runtime_error("The loaded image has invalid bounded headers");
    }
    auto nt = ReadRemote<IMAGE_NT_HEADERS64>(process.Get(), module.modBaseAddr + dos.e_lfanew);
    if (nt.Signature != IMAGE_NT_SIGNATURE || nt.FileHeader.Machine != IMAGE_FILE_MACHINE_AMD64
        || nt.OptionalHeader.Magic != IMAGE_NT_OPTIONAL_HDR64_MAGIC
        || nt.OptionalHeader.SizeOfImage != metadata.imageSize
        || nt.FileHeader.TimeDateStamp != metadata.timeStamp || module.modBaseSize != metadata.imageSize) {
        throw std::runtime_error("The loaded DLL differs from the inspected payload");
    }
    void* entry = module.modBaseAddr + metadata.entryRva;
    MEMORY_BASIC_INFORMATION protection{};
    if (!VirtualQueryEx(process.Get(), entry, &protection, sizeof(protection))
        || protection.State != MEM_COMMIT || protection.Type != MEM_IMAGE
        || protection.AllocationBase != module.modBaseAddr
        || !(protection.Protect & (PAGE_EXECUTE | PAGE_EXECUTE_READ | PAGE_EXECUTE_READWRITE | PAGE_EXECUTE_WRITECOPY))
        || (protection.Protect & (PAGE_GUARD | PAGE_NOACCESS))) {
        throw std::runtime_error("The bootstrap export is not executable loaded-image code");
    }
    // Only this allocated packet is written; no game field or code offset is guessed
    void* remote = VirtualAllocEx(process.Get(), nullptr, sizeof(config), MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    if (!remote) {
        throw std::runtime_error("The bootstrap packet could not be allocated");
    }
    SIZE_T count = 0;
    if (!WriteProcessMemory(process.Get(), remote, &config, sizeof(config), &count) || count != sizeof(config)) {
        VirtualFreeEx(process.Get(), remote, 0, MEM_RELEASE);
        throw std::runtime_error("The bootstrap packet could not be written completely");
    }
    HANDLE thread = CreateRemoteThread(process.Get(), nullptr, 0,
        reinterpret_cast<LPTHREAD_START_ROUTINE>(entry), remote, 0, nullptr);
    if (!thread) {
        VirtualFreeEx(process.Get(), remote, 0, MEM_RELEASE);
        throw std::runtime_error("The explicit bootstrap could not be started");
    }
    Handle worker(thread);
    DWORD waited = WaitForSingleObject(worker.Get(), 15000);
    if (waited != WAIT_OBJECT_0) {
        // A live bootstrap retains its packet and DLL until the original game exits
        throw std::runtime_error("Bootstrap did not finish; restart the game before another attempt");
    }
    DWORD code = 0;
    BOOL reported = GetExitCodeThread(worker.Get(), &code);
    VirtualFreeEx(process.Get(), remote, 0, MEM_RELEASE);
    if (!reported || code != 0) {
        throw std::runtime_error("Overlay initialization failed; restart the game before another attempt");
    }
    return code;
}

int Run(int count, wchar_t** args) {
    std::filesystem::path directory;
    std::filesystem::path payload;
    bool discover = false;
    DWORD requestedPid = 0;
    for (int index = 1; index < count; ++index) {
        std::wstring option(args[index]);
        if (option == L"--discover") {
            discover = true;
        } else if (option == L"--session-directory" && index + 1 < count) {
            directory = FullPath(args[++index]);
        } else if (option == L"--payload" && index + 1 < count) {
            payload = FullPath(args[++index]);
        } else if (option == L"--game-pid" && index + 1 < count) {
            auto value = std::stoull(args[++index]);
            if (value == 0 || value > MAXDWORD) {
                throw std::runtime_error("The requested process identifier is invalid");
            }
            requestedPid = static_cast<DWORD>(value);
        } else {
            throw std::runtime_error("Unsupported bootstrap argument");
        }
    }
    if (directory.empty() || !std::filesystem::is_directory(directory)) {
        throw std::runtime_error("An existing private session directory is required");
    }
    try {
        DWORD pid = FindGame();
        HWND window = GameWindow(pid);
        if (requestedPid != 0 && requestedPid != pid) {
            throw std::runtime_error("The captured game process is no longer current");
        }
        if (!discover) {
            if (requestedPid == 0 || payload.empty()) {
                throw std::runtime_error("Explicit game identity and payload are required");
            }
            Initialize(pid, window, directory, payload);
        }
        WriteResult(directory, discover ? "bootstrap-target.json" : "bootstrap-result.json",
            {{"version", 1}, {"processId", pid}, {"window", reinterpret_cast<std::uintptr_t>(window)},
             {"ready", !discover}});
        return 0;
    } catch (const std::exception& error) {
        WriteResult(directory, "bootstrap-error.json", {{"version", 1}, {"error", error.what()}});
        return 1;
    }
}
}

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int) {
    int count = 0;
    wchar_t** args = CommandLineToArgvW(GetCommandLineW(), &count);
    if (!args) {
        return 2;
    }
    int result = 2;
    try {
        result = Run(count, args);
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
    }
    LocalFree(args);
    return result;
}
