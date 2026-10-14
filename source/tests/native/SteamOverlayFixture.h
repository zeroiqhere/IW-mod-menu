#pragma once
#include <windows.h>
#include <filesystem>
#include <ostream>

namespace iw::overlay::tests {

// Steam integration is optional and exists only in the owned test executable
class SteamOverlayFixture final {
public:
    SteamOverlayFixture() = default;
    SteamOverlayFixture(const SteamOverlayFixture&) = delete;
    SteamOverlayFixture& operator=(const SteamOverlayFixture&) = delete;
    void Start(const std::filesystem::path& apiPath, std::ostream& log);
    bool ObserveModule(std::ostream& log) const;
    void Shutdown() noexcept;

private:
    using InitCall = bool (__cdecl*)();
    using ShutdownCall = void (__cdecl*)();
    HMODULE api_ = nullptr;
    ShutdownCall shutdown_ = nullptr;
    bool initialized_ = false;
};

}
