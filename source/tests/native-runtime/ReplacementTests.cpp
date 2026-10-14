#include "TestSupport.h"
#include "SessionFiles.h"

#include <future>
#include <thread>

namespace iw::overlay::tests {
namespace {
HANDLE OpenTarget(const FixtureDirectory& fixture, DWORD sharing) {
    HANDLE file = CreateFileW((fixture.path + L"\\command.json").c_str(), GENERIC_READ,
        sharing, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr);
    Check(file != INVALID_HANDLE_VALUE, "Fixture target reader could not be opened");
    return file;
}

class HeldTarget {
public:
    HeldTarget(const FixtureDirectory& fixture, DWORD sharing) : handle(OpenTarget(fixture, sharing)) {}
    ~HeldTarget() { CloseHandle(handle); }
    HeldTarget(const HeldTarget&) = delete;
    HeldTarget& operator=(const HeldTarget&) = delete;
    HANDLE handle;
};

void ReplaceAfterReaderRelease(DWORD sharing, bool mustWait) {
    FixtureDirectory fixture;
    fixture.Write(L"command.json", "old frame");
    runtime::SessionFiles files(fixture.path);
    HANDLE held = OpenTarget(fixture, sharing);
    const auto started = GetTickCount64();
    {
        // The reader holds a real target handle while replacement retries the completed frame
        std::jthread release([held] { Sleep(30); CloseHandle(held); });
        files.WriteCommand("new frame");
        if (mustWait) Check(GetTickCount64() - started >= 20, "Replacement did not wait for a blocking target reader");
    }
    Check(fixture.Read(L"command.json") == "new frame", "Released target did not receive the complete replacement");
    Check(GetFileAttributesW((fixture.path + L"\\command.tmp").c_str()) == INVALID_FILE_ATTRIBUTES,
        "Successful replacement retained a temporary frame");
}
}

void SessionReplacementRetriesUntilSharedReaderReleases() {
    // Wine denies an opened replacement target even when this reader allows delete sharing
    ReplaceAfterReaderRelease(FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, false);
}

void SessionReplacementRetriesUntilDeleteDenyingReaderReleases() {
    // Omitting delete sharing also exercises the retry on native Windows
    ReplaceAfterReaderRelease(FILE_SHARE_READ | FILE_SHARE_WRITE, true);
}

void SessionReplacementPersistentBlockFailsAndPreservesFrame() {
    FixtureDirectory fixture;
    fixture.Write(L"command.json", "old frame");
    runtime::SessionFiles files(fixture.path);
    {
        HeldTarget held(fixture, FILE_SHARE_READ | FILE_SHARE_WRITE);
        const auto started = GetTickCount64();
        Reject([&] { files.WriteCommand("complete pending frame"); });
        const auto elapsed = GetTickCount64() - started;
        // Allow scheduler delay while checking that the production retry has a hard deadline
        Check(elapsed >= 80 && elapsed < 250, "Persistent target blockage did not respect the bounded retry");
    }
    Check(fixture.Read(L"command.json") == "old frame", "Persistent blockage changed the committed target");
    Check(fixture.Read(L"command.tmp") == "complete pending frame", "Persistent blockage discarded or rewrote the pending frame");
}

void SessionReplacementReadonlyTargetIsRejected() {
    FixtureDirectory fixture;
    fixture.Write(L"command.json", "old frame");
    const auto target = fixture.path + L"\\command.json";
    Check(SetFileAttributesW(target.c_str(), FILE_ATTRIBUTE_READONLY), "Readonly fixture target could not be set");
    try {
        runtime::SessionFiles files(fixture.path);
        Reject([&] { files.WriteCommand("new frame"); });
        Check(fixture.Read(L"command.json") == "old frame", "Readonly rejection changed the target");
    } catch (...) {
        SetFileAttributesW(target.c_str(), FILE_ATTRIBUTE_NORMAL);
        throw;
    }
    Check(SetFileAttributesW(target.c_str(), FILE_ATTRIBUTE_NORMAL), "Readonly fixture cleanup failed");
}

void SessionReplacementDirectoryAndReparseTargetsAreRejected() {
    FixtureDirectory fixture;
    runtime::SessionFiles files(fixture.path);
    const auto target = fixture.path + L"\\command.json";
    Check(CreateDirectoryW(target.c_str(), nullptr), "Fixture destination directory could not be created");
    Reject([&] { files.WriteCommand("new frame"); });
    Check(RemoveDirectoryW(target.c_str()), "Fixture destination directory cleanup failed");
    fixture.Write(L"link-target.json", "unchanged");
    const auto linked = fixture.path + L"\\link-target.json";
    // The link remains inside the own fixture and must not receive any protocol bytes
    Check(CreateSymbolicLinkW(target.c_str(), linked.c_str(), SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE),
        "Fixture destination link could not be created");
    Reject([&] { files.WriteCommand("new frame"); });
    Check(fixture.Read(L"link-target.json") == "unchanged", "A linked target received protocol bytes");
}

void SessionReplacementRechecksDirectoryDuringRetry() {
    FixtureDirectory fixture;
    fixture.Write(L"command.json", "old frame");
    runtime::SessionFiles files(fixture.path);
    HeldTarget held(fixture, FILE_SHARE_READ | FILE_SHARE_WRITE);
    auto changed = std::async(std::launch::async, [&] {
        Sleep(20);
        // Replacement of the private directory during a blocked retry must be detected
        if (!MoveFileW(fixture.path.c_str(), (fixture.path + L"-old").c_str())) return false;
        return CreateDirectoryW(fixture.path.c_str(), nullptr) != 0;
    });
    std::string error;
    try { files.WriteCommand("new frame"); }
    catch (const std::exception& exception) { error = exception.what(); }
    Check(changed.get(), "The fixture directory could not be replaced during the retry");
    Check(error == "The session directory identity changed", "Replacement retry did not recheck directory identity");
    Check(GetFileAttributesW((fixture.path + L"\\command.json").c_str()) == INVALID_FILE_ATTRIBUTES,
        "Retry committed into a replacement directory");
}
}
