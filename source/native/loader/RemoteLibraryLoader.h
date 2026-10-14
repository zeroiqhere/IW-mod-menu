#pragma once

#include <windows.h>
#include <tlhelp32.h>
#include <filesystem>
#include <optional>

namespace iw::loader {

// Paths identify exact loaded images instead of choosing the first matching DLL name
std::optional<MODULEENTRY32W> FindExactModule(DWORD processId, const std::filesystem::path& path,
    bool rejectOtherPathsWithSameName = false);
void LoadRemoteLibrary(HANDLE process, DWORD processId, const std::filesystem::path& payload);

// This checked address can also be exercised against the isolated helper process
void* ResolveRemoteLoadLibrary(HANDLE process, DWORD processId);

}
