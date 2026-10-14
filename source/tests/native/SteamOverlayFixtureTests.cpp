#include "SteamOverlayFixtureTests.h"
#include "SteamOverlayFixture.h"
#include <array>
#include <fstream>
#include <functional>
#include <stdexcept>
#include <string>

namespace iw::overlay::tests {
namespace {
class SavedEnvironment final {
public:
    explicit SavedEnvironment(const wchar_t* name) : name_(name) {
        // Save only this fixture's two app identifiers without logging their previous values
        DWORD size = GetEnvironmentVariableW(name_, nullptr, 0);
        if (size) {
            value_.resize(size);
            DWORD length = GetEnvironmentVariableW(name_, value_.data(), size);
            if (!length || length >= size) { throw std::runtime_error("Fixture environment could not be saved"); }
            value_.resize(length); existed_ = true;
        }
    }
    ~SavedEnvironment() { SetEnvironmentVariableW(name_, existed_ ? value_.c_str() : nullptr); }
private:
    const wchar_t* name_;
    std::wstring value_;
    bool existed_ = false;
};

void Set(const wchar_t* name, const wchar_t* value) {
    if (!SetEnvironmentVariableW(name, value)) { throw std::runtime_error("Fixture environment could not be set"); }
}
}

int RunSteamOverlayGuards(const std::filesystem::path& logPath) {
    std::ofstream log(logPath);
    if (!log) { return 3; }
    unsigned checks = 0;
    try {
        SavedEnvironment app(L"SteamAppId"), game(L"SteamGameId");
        SteamOverlayFixture fixture;
        HMODULE originalApi = GetModuleHandleW(L"steam_api64.dll");
        auto reject = [&](const std::string& name, const std::filesystem::path& path) {
            bool rejected = false;
            try { fixture.Start(path, log); }
            catch (const std::runtime_error&) { rejected = true; }
            if (!rejected || GetModuleHandleW(L"steam_api64.dll") != originalApi) { throw std::runtime_error(name); }
            log << "PASS " << name << '\n'; log.flush(); ++checks;
        };
        // Each failure must happen before loading a DLL or making any Steam API call
        Set(L"SteamAppId", L"1"); Set(L"SteamGameId", L"292730");
        reject("A different app identifier rejects before DLL loading", L"steam_api64.dll");
        Set(L"SteamAppId", L"292730"); Set(L"SteamGameId", nullptr);
        reject("A missing game identifier rejects before DLL loading", L"steam_api64.dll");
        std::wstring longId(80, L'1'); Set(L"SteamGameId", longId.c_str());
        reject("An overlong game identifier rejects before DLL loading", L"steam_api64.dll");
        Set(L"SteamGameId", L"292730");
        reject("A relative API path rejects before DLL loading", L"steam_api64.dll");
        std::filesystem::path missing = std::filesystem::temp_directory_path()
            / (L"iw-steam-guard-missing-" + std::to_wstring(GetCurrentProcessId()));
        if (std::filesystem::exists(missing)) { throw std::runtime_error("The missing-file guard directory already exists"); }
        reject("An incorrect API basename rejects before DLL loading", missing / L"other.dll");
        reject("A nonexistent API DLL rejects before DLL loading", missing / L"steam_api64.dll");
        log << "PASS " << checks << " Steam fixture guard checks\n"; log.flush();
        return 0;
    } catch (const std::exception& error) {
        log << "FAIL " << error.what() << '\n'; log.flush(); return 1;
    }
}
}
