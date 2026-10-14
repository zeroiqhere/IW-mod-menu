#include "SteamOverlayFixture.h"
#include <array>
#include <bit>
#include <stdexcept>

namespace iw::overlay::tests {
namespace {
void RequireAppEnvironment(const wchar_t* name, std::ostream& log) {
    std::array<wchar_t, 64> value{};
    DWORD length = GetEnvironmentVariableW(name, value.data(), static_cast<DWORD>(value.size()));
    // Refuse to initialize a different Steam app or infer a missing app identity
    if (length == 0 || length >= value.size() || std::wstring_view(value.data(), length) != L"292730") {
        throw std::runtime_error("The fixture requires SteamAppId and SteamGameId to be exactly 292730");
    }
    log << "INFO Verified Steam app environment 292730\n"; log.flush();
}
}

void SteamOverlayFixture::Start(const std::filesystem::path& apiPath, std::ostream& log) {
    RequireAppEnvironment(L"SteamAppId", log);
    RequireAppEnvironment(L"SteamGameId", log);
    // The DLL is used from its existing installation and is never copied into the package
    if (!apiPath.is_absolute() || _wcsicmp(apiPath.filename().c_str(), L"steam_api64.dll") != 0
        || !std::filesystem::is_regular_file(apiPath)) {
        throw std::runtime_error("An existing absolute steam_api64.dll path is required");
    }
    api_ = LoadLibraryExW(apiPath.c_str(), nullptr, LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    if (!api_) { throw std::runtime_error("The existing Steam API DLL could not be loaded"); }
    auto init = std::bit_cast<InitCall>(GetProcAddress(api_, "SteamAPI_Init"));
    shutdown_ = std::bit_cast<ShutdownCall>(GetProcAddress(api_, "SteamAPI_Shutdown"));
    if (!init || !shutdown_) { throw std::runtime_error("Documented Steam API lifecycle exports are unavailable"); }
    log << "TRACE Before SteamAPI_Init, before any fixture D3D device\n"; log.flush();
    initialized_ = init();
    log << "TRACE After SteamAPI_Init result=" << initialized_ << '\n'; log.flush();
    if (!initialized_) { throw std::runtime_error("SteamAPI_Init is unsupported in this fixture environment"); }
}

bool SteamOverlayFixture::ObserveModule(std::ostream& log) const {
    HMODULE overlay = GetModuleHandleW(L"GameOverlayRenderer64.dll");
    log << "INFO GameOverlayRenderer64 module present=" << (overlay != nullptr)
        << "; presence alone does not prove active Present hooks\n";
    log.flush();
    return overlay != nullptr;
}

void SteamOverlayFixture::Shutdown() noexcept {
    // The fixture destroys its D3D resources first and never unloads the API or overlay DLL
    if (initialized_ && shutdown_) { shutdown_(); initialized_ = false; }
}

}
