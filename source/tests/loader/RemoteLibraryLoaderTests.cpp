#include "loader/RemoteLibraryLoader.h"

#include <windows.h>
#include <array>
#include <iostream>
#include <stdexcept>

namespace {
template<class Action> void Reject(const char* label, Action action) {
    try { action(); } catch (const std::runtime_error&) {
        std::cout << "PASS " << label << '\n';
        return;
    }
    throw std::runtime_error(label);
}
}

int main() {
    try {
        // Only this isolated test process is queried or supplied a remote thread
        auto pid = GetCurrentProcessId();
        auto process = GetCurrentProcess();
        void* resolved = iw::loader::ResolveRemoteLoadLibrary(process, pid);
        auto expected = GetProcAddress(GetModuleHandleW(L"kernel32.dll"), "LoadLibraryW");
        if (!resolved || resolved != reinterpret_cast<void*>(expected)) {
            throw std::runtime_error("A checked self-process loader address must equal the actual export");
        }
        std::cout << "PASS Checked owning system image resolves the real LoadLibraryW address\n";
        std::array<wchar_t, 32768> path{};
        DWORD count = GetModuleFileNameW(GetModuleHandleW(L"kernel32.dll"), path.data(), static_cast<DWORD>(path.size()));
        if (!count || count >= path.size()) throw std::runtime_error("The own system module path is unavailable");
        auto module = iw::loader::FindExactModule(pid, path.data());
        if (!module || module->modBaseAddr != reinterpret_cast<BYTE*>(GetModuleHandleW(L"kernel32.dll"))) {
            throw std::runtime_error("The exact own system module must be enumerated");
        }
        std::cout << "PASS Exact path enumeration returns the owning module without a guessed address\n";
        Reject("An already loaded DLL is refused before another loader call", [&] {
            iw::loader::LoadRemoteLibrary(process, pid, path.data());
        });
        Reject("An invalid process module snapshot is rejected", [&] {
            iw::loader::FindExactModule(MAXDWORD, path.data());
        });
        std::array<wchar_t, 32768> temporary{};
        DWORD temporaryCount = GetTempPathW(static_cast<DWORD>(temporary.size()), temporary.data());
        if (!temporaryCount || temporaryCount >= temporary.size()) throw std::runtime_error("The test temporary directory is unavailable");
        auto missing = std::filesystem::path(temporary.data()) / (L"iw-missing-loader-test-" + std::to_wstring(pid) + L".dll");
        Reject("The same DLL name from another path is refused before loading", [&] {
            iw::loader::FindExactModule(pid, std::filesystem::path(temporary.data()) / L"kernel32.dll", true);
        });
        if (std::filesystem::exists(missing)) throw std::runtime_error("The missing-library fixture path already exists");
        if (iw::loader::FindExactModule(pid, missing)) throw std::runtime_error("An absent exact DLL must not be guessed from another module");
        std::cout << "PASS An absent exact path is never substituted with another module\n";
        Reject("A failed LoadLibraryW call is rejected by exact post-load enumeration", [&] {
            iw::loader::LoadRemoteLibrary(process, pid, missing);
        });
        std::cout << "Passed 7 checked native loader regressions\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "FAIL " << error.what() << '\n';
        return 1;
    }
}
