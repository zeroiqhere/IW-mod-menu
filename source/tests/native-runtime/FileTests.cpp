#include "TestSupport.h"
#include "SessionFiles.h"
#include "RendererStatusStub.h"

#include <algorithm>
#include <filesystem>

namespace iw::overlay::tests {

FixtureDirectory::FixtureDirectory() {
    wchar_t temporary[1024]{};
    DWORD count = GetTempPathW(1024, temporary);
    Check(count > 0 && count < 1024, "Test temporary path is unavailable");
    for (int attempt = 0; attempt < 128; ++attempt) {
        path = std::wstring(temporary) + L"iw-runtime-fixture-" + std::to_wstring(GetCurrentProcessId())
            + L"-" + std::to_wstring(GetTickCount64()) + L"-" + std::to_wstring(attempt);
        if (CreateDirectoryW(path.c_str(), nullptr)) return;
        Check(GetLastError() == ERROR_ALREADY_EXISTS, "Test directory creation failed");
    }
    throw std::runtime_error("No test fixture directory was available");
}

FixtureDirectory::~FixtureDirectory() {
    // Cleanup only the directory created by this fixture, after runtime threads have stopped
    std::error_code error;
    std::filesystem::remove_all(path, error);
    std::filesystem::remove_all(path + L"-old", error);
}

void FixtureDirectory::Write(const wchar_t* name, const std::string& bytes) const {
    HANDLE file = CreateFileW((path + L"\\" + name).c_str(), GENERIC_WRITE, 0,
        nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    Check(file != INVALID_HANDLE_VALUE, "Test file could not be created");
    DWORD written = 0;
    bool complete = WriteFile(file, bytes.data(), static_cast<DWORD>(bytes.size()), &written, nullptr)
        && written == bytes.size();
    CloseHandle(file);
    Check(complete, "Test file write was incomplete");
}

void FixtureDirectory::PublishState(const std::string& bytes) const {
    Write(L"fixture-state.tmp", bytes);
    Check(MoveFileExW((path + L"\\fixture-state.tmp").c_str(), (path + L"\\state.json").c_str(),
        MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH), "Fixture state replacement failed");
}

std::string FixtureDirectory::Read(const wchar_t* name) const {
    HANDLE file = CreateFileW((path + L"\\" + name).c_str(), GENERIC_READ,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING,
        FILE_ATTRIBUTE_NORMAL, nullptr);
    Check(file != INVALID_HANDLE_VALUE, "Test output file is missing");
    LARGE_INTEGER length{};
    bool sized = GetFileSizeEx(file, &length) && length.QuadPart >= 0 && length.QuadPart <= 32768;
    if (!sized) {
        CloseHandle(file);
        throw std::runtime_error("Test file size is invalid");
    }
    std::string bytes(static_cast<std::size_t>(length.QuadPart), '\0');
    DWORD received = 0;
    bool complete = ReadFile(file, bytes.data(), static_cast<DWORD>(bytes.size()), &received, nullptr)
        && received == bytes.size();
    CloseHandle(file);
    Check(complete, "Test output read was incomplete");
    return bytes;
}

void SessionFilesAtomicReplacementAndBounds() {
    FixtureDirectory fixture;
    fixture.Write(L"state.json", StateJson().dump());
    fixture.Write(L"bindings.json", BindingsJson().dump());
    runtime::SessionFiles files(fixture.path);
    runtime::ParseState(files.ReadState(), GetCurrentProcessId());
    runtime::ParseBindings(files.ReadBindings());
    files.WriteCommand(runtime::CommandJson(GetCurrentProcessId(), 1, runtime::Input::Toggle));
    files.WriteCommand(runtime::CommandJson(GetCurrentProcessId(), 2, runtime::Input::Back));
    const auto output = Json::parse(fixture.Read(L"command.json"));
    Check(output.at("sequence") == 2 && output.at("input") == "back", "Atomic replacement did not retain the last complete command");
    Check(GetFileAttributesW((fixture.path + L"\\command.tmp").c_str()) == INVALID_FILE_ATTRIBUTES, "Protocol temporary file survived replacement");
    files.WriteReady(runtime::ReadyJson(GetCurrentProcessId()));
    Check(Json::parse(fixture.Read(L"runtime-ready.json")).at("ready") == true, "Ready file was not published");
    fixture.Write(L"state.json", std::string(runtime::MaximumFileBytes + 1, 'X'));
    Reject([&] { files.ReadState(); });
    fixture.Write(L"state.json", "");
    Reject([&] { files.ReadState(); });
    DeleteFileW((fixture.path + L"\\state.json").c_str());
    Reject([&] { files.ReadState(); });
    Reject([&] { runtime::SessionFiles rejected(fixture.path + L"\\missing"); });
    Reject([&] { runtime::SessionFiles rejected(L"relative"); });
}

void SessionDirectoryReplacementIsRejected() {
    FixtureDirectory fixture;
    fixture.Write(L"state.json", StateJson().dump());
    runtime::SessionFiles files(fixture.path);
    Check(MoveFileW(fixture.path.c_str(), (fixture.path + L"-old").c_str()), "Fixture directory rename failed");
    Check(CreateDirectoryW(fixture.path.c_str(), nullptr), "Replacement fixture directory creation failed");
    fixture.Write(L"state.json", StateJson().dump());
    Reject([&] { files.ReadState(); });
    Reject([&] { files.WriteCommand(runtime::CommandJson(GetCurrentProcessId(), 1, runtime::Input::Select)); });
}

void SessionReparseDirectoriesAndFilesAreRejected() {
    FixtureDirectory fixture;
    fixture.Write(L"target.json", StateJson().dump());
    std::wstring stateLink = fixture.path + L"\\state.json";
    std::wstring target = fixture.path + L"\\target.json";
    // Links point only to this fixture's data and never to another process or session
    Check(CreateSymbolicLinkW(stateLink.c_str(), target.c_str(), SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE),
        "The fixture file link could not be created");
    runtime::SessionFiles files(fixture.path);
    Reject([&] { files.ReadState(); });
    std::wstring directoryLink = fixture.path + L"\\directory-link";
    Check(CreateSymbolicLinkW(directoryLink.c_str(), fixture.path.c_str(),
        SYMBOLIC_LINK_FLAG_DIRECTORY | SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE),
        "The fixture directory link could not be created");
    Reject([&] { runtime::SessionFiles rejected(directoryLink); });
}

namespace {
class OwnWindow {
public:
    OwnWindow() {
        // A hidden built-in window validates process ownership without taking foreground focus
        handle = CreateWindowExW(0, L"STATIC", L"Runtime test fixture", WS_POPUP,
            0, 0, 1, 1, nullptr, nullptr, GetModuleHandleW(nullptr), nullptr);
        Check(handle != nullptr, "Own fixture window creation failed");
    }
    ~OwnWindow() { if (handle) DestroyWindow(handle); }
    HWND handle{};
};

BootstrapConfig Config(const FixtureDirectory& fixture, HWND window) {
    BootstrapConfig config{};
    config.size = sizeof(config);
    config.version = ProtocolVersion;
    config.expectedProcessId = GetCurrentProcessId();
    config.gameWindow = window;
    Check(fixture.path.size() < 1024, "Fixture path does not fit the bootstrap packet");
    std::copy(fixture.path.begin(), fixture.path.end(), config.sessionDirectory);
    return config;
}
}

void RuntimeOwnFixtureStartsAndStopsWithoutGameFeatures() {
    FixtureDirectory fixture;
    fixture.Write(L"state.json", StateJson().dump());
    fixture.Write(L"bindings.json", BindingsJson().dump());
    OwnWindow window;
    auto config = Config(fixture, window.handle);
    std::string error;
    Check(RuntimeStart(config, error), "Runtime rejected its own fixture");
    try {
        auto snapshot = GetOverlaySnapshot();
        Check(snapshot && snapshot->title == "Main Menu", "Runtime did not publish its initial snapshot");
        Check(Json::parse(fixture.Read(L"runtime-ready.json")).at("processId") == GetCurrentProcessId(), "Runtime readiness lost process identity");
        Check(!RuntimeStart(config, error), "A second runtime start was allowed");
        Check(GetFileAttributesW((fixture.path + L"\\command.json").c_str()) == INVALID_FILE_ATTRIBUTES,
            "A hidden unfocused fixture emitted a command");
    } catch (...) {
        RuntimeStop();
        throw;
    }
    RuntimeStop();
    Check(!GetOverlaySnapshot(), "Runtime stop retained a render snapshot");
    Check(GetFileAttributesW((fixture.path + L"\\runtime-error.json").c_str()) == INVALID_FILE_ATTRIBUTES,
        "Normal runtime shutdown published an error packet");
    RuntimeStop();
}

void RuntimeWrongConfigNeverStarts() {
    FixtureDirectory fixture;
    fixture.Write(L"state.json", StateJson().dump());
    fixture.Write(L"bindings.json", BindingsJson().dump());
    OwnWindow window;
    for (int variation = 0; variation < 5; ++variation) {
        auto config = Config(fixture, window.handle);
        if (variation == 0) config.version = 2;
        if (variation == 1) config.expectedProcessId++;
        if (variation == 2) config.gameWindow = nullptr;
        if (variation == 3) config.size--;
        if (variation == 4) std::fill(std::begin(config.sessionDirectory), std::end(config.sessionDirectory), L'X');
        std::string error;
        Check(!RuntimeStart(config, error) && !error.empty() && !GetOverlaySnapshot(), "Invalid runtime configuration was accepted");
    }
}

void RuntimeFailureClosesCachedSnapshot() {
    FixtureDirectory fixture;
    auto initial = StateJson();
    initial["isOpen"] = true;
    fixture.Write(L"state.json", initial.dump());
    fixture.Write(L"bindings.json", BindingsJson().dump());
    OwnWindow window;
    std::string error;
    Check(RuntimeStart(Config(fixture, window.handle), error), "Runtime fixture did not start");
    try {
        DeleteFileW((fixture.path + L"\\state.json").c_str());
        bool closed = false;
        for (int attempt = 0; attempt < 100; ++attempt) {
            auto snapshot = GetOverlaySnapshot();
            if (snapshot && !snapshot->isOpen && !snapshot->notice.empty()) {
                closed = true;
                break;
            }
            Sleep(10);
        }
        Check(closed, "Missing state did not close the cached menu");
    } catch (...) {
        RuntimeStop();
        throw;
    }
    RuntimeStop();
    const auto packet = Json::parse(fixture.Read(L"runtime-error.json"));
    Check(packet.at("version") == ProtocolVersion && packet.at("processId") == GetCurrentProcessId()
        && packet.at("error").is_string() && !packet.at("error").get<std::string>().empty(),
        "Worker failure did not publish an identified error packet");
    Check(GetFileAttributesW((fixture.path + L"\\runtime-error.tmp").c_str()) == INVALID_FILE_ATTRIBUTES,
        "Runtime error temporary file survived successful atomic replacement");
}

void RuntimeMalformedStatePublishesOnlySanitizedError() {
    FixtureDirectory fixture;
    fixture.Write(L"state.json", StateJson().dump());
    fixture.Write(L"bindings.json", BindingsJson().dump());
    OwnWindow window;
    std::string error;
    Check(RuntimeStart(Config(fixture, window.handle), error), "Private error fixture did not start");
    try {
        // Invalid input includes a marker that must never appear in public diagnostics
        fixture.PublishState("{\"private\":\"PRIVATE_TEST_MARKER_0x12345678\"");
        bool failed = false;
        for (int attempt = 0; attempt < 100; ++attempt) {
            auto snapshot = GetOverlaySnapshot();
            if (snapshot && !snapshot->isOpen && snapshot->context == "RUNTIME ERROR") {
                Check(snapshot->notice.find("PRIVATE_TEST_MARKER") == std::string::npos,
                    "The cached failure snapshot echoed private input");
                failed = true;
                break;
            }
            Sleep(10);
        }
        Check(failed, "Malformed state did not close the runtime");
    } catch (...) {
        RuntimeStop();
        throw;
    }
    // Joining the worker waits for its best-effort diagnostic publication
    RuntimeStop();
    const auto bytes = fixture.Read(L"runtime-error.json");
    Check(bytes == runtime::RuntimeErrorJson(GetCurrentProcessId())
        && bytes.find("PRIVATE_TEST_MARKER") == std::string::npos,
        "Runtime error packet echoed private input or exception contents");
}

void RuntimeErrorPublicationFailureStillClosesAndStops() {
    FixtureDirectory fixture;
    fixture.Write(L"state.json", StateJson().dump());
    fixture.Write(L"bindings.json", BindingsJson().dump());
    // A directory at the fixed destination makes error publication fail without touching other data
    const auto outputPath = fixture.path + L"\\runtime-error.json";
    Check(CreateDirectoryW(outputPath.c_str(), nullptr), "Blocked error destination could not be created");
    OwnWindow window;
    std::string error;
    Check(RuntimeStart(Config(fixture, window.handle), error), "Error IO fixture did not start");
    try {
        Check(DeleteFileW((fixture.path + L"\\state.json").c_str()), "Failure fixture state could not be removed");
        bool closed = false;
        for (int attempt = 0; attempt < 100; ++attempt) {
            auto snapshot = GetOverlaySnapshot();
            if (snapshot && !snapshot->isOpen && snapshot->context == "RUNTIME ERROR") {
                closed = true;
                break;
            }
            Sleep(10);
        }
        Check(closed, "Failed error IO prevented the menu from closing");
    } catch (...) {
        RuntimeStop();
        throw;
    }
    RuntimeStop();
    Check(!GetOverlaySnapshot(), "Failed error IO prevented runtime cleanup");
    const auto attributes = GetFileAttributesW(outputPath.c_str());
    Check(attributes != INVALID_FILE_ATTRIBUTES && (attributes & FILE_ATTRIBUTE_DIRECTORY) != 0,
        "Best-effort diagnostics replaced an invalid destination");
}

void RuntimeStaleHeartbeatClosesAndRecoversCachedSnapshot() {
    FixtureDirectory fixture;
    auto state = StateJson();
    state["isOpen"] = true;
    fixture.Write(L"state.json", state.dump());
    fixture.Write(L"bindings.json", BindingsJson().dump());
    OwnWindow window;
    std::string error;
    Check(RuntimeStart(Config(fixture, window.handle), error), "Heartbeat fixture did not start");
    try {
        bool closed = false;
        for (int attempt = 0; attempt < 400; ++attempt) {
            auto snapshot = GetOverlaySnapshot();
            if (snapshot && !snapshot->isOpen && snapshot->notice.find("heartbeat") != std::string::npos) {
                closed = true;
                break;
            }
            Sleep(10);
        }
        Check(closed, "Stale heartbeat did not close the HUD");
        state["generation"] = 2;
        state["title"] = "Fresh snapshot";
        fixture.PublishState(state.dump());
        bool recovered = false;
        for (int attempt = 0; attempt < 100; ++attempt) {
            auto snapshot = GetOverlaySnapshot();
            if (snapshot && snapshot->isOpen && snapshot->title == "Fresh snapshot") {
                recovered = true;
                break;
            }
            Sleep(10);
        }
        Check(recovered, "New generation did not restore the cached HUD");
    } catch (...) {
        RuntimeStop();
        throw;
    }
    RuntimeStop();
}

void RuntimePublishesInitialAndChangedRendererStatus() {
    FixtureDirectory fixture;
    fixture.Write(L"state.json", StateJson().dump());
    fixture.Write(L"bindings.json", BindingsJson().dump());
    OwnWindow window;
    SetRendererStatusForTest({});
    std::string error;
    Check(RuntimeStart(Config(fixture, window.handle), error), "Renderer status fixture did not start");
    auto awaitStatus = [&](bool initialized, std::uint64_t frames, std::uint64_t faults) {
        for (int attempt = 0; attempt < 100; ++attempt) {
            DWORD attributes = GetFileAttributesW((fixture.path + L"\\renderer-status.json").c_str());
            if (attributes != INVALID_FILE_ATTRIBUTES) {
                // Any published packet must already be complete JSON, even between updates
                auto packet = Json::parse(fixture.Read(L"renderer-status.json"));
                Check(packet.at("version") == ProtocolVersion
                    && packet.at("processId") == GetCurrentProcessId(), "Renderer status publication lost identity");
                if (packet.at("initialized") == initialized && packet.at("drawnFrames") == frames
                    && packet.at("faults") == faults) {
                    return packet;
                }
            } else {
                Check(GetLastError() == ERROR_FILE_NOT_FOUND, "Renderer status path could not be inspected");
            }
            Sleep(10);
        }
        throw std::runtime_error("Renderer status did not publish the expected sample");
    };
    try {
        awaitStatus(false, 0, 0);
        RendererStatus drawn{true, 10, 0, 0xb000, 1920, 1080};
        SetRendererStatusForTest(drawn);
        auto packet = awaitStatus(true, 10, 0);
        Check(packet.at("featureLevel") == 0xb000 && packet.at("width") == 1920
            && packet.at("height") == 1080, "Renderer dimensions were not published");
        SetRendererStatusForTest({false, 10, 1, 0xb000, 1920, 1080});
        awaitStatus(false, 10, 1);
        Check(GetFileAttributesW((fixture.path + L"\\renderer-status.tmp").c_str()) == INVALID_FILE_ATTRIBUTES,
            "Renderer status temporary file survived atomic replacement");
        // A stopped renderer is sampled once more during runtime cleanup
        SetRendererStatusForTest({false, 10, 1, 0xb000, 0, 0});
    } catch (...) {
        RuntimeStop();
        SetRendererStatusForTest({});
        throw;
    }
    RuntimeStop();
    auto final = Json::parse(fixture.Read(L"renderer-status.json"));
    Check(final.at("initialized") == false && final.at("width") == 0 && final.at("height") == 0,
        "Runtime stop did not publish the final renderer sample");
    SetRendererStatusForTest({});
}
}
