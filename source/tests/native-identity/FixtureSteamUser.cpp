#include "FixtureSteamUser.h"
#include "steamclientpublic.h"
#include "isteamuser.h"

// Placement construction needs no allocation or C++ runtime across compilers
void* operator new(decltype(sizeof(0)), void* storage) noexcept { return storage; }

namespace {
// Implement the complete real interface rather than inventing a vtable prefix
class FixtureSteamUser final : public ISteamUser {
public:
    FixtureSteamUser(bool loggedOn, uint64_t id) : loggedOn_(loggedOn), id_(id) {}
    HSteamUser GetHSteamUser() override { return 17; }
    bool BLoggedOn() override { ++loggedOnCalls; return loggedOn_; }
    CSteamID GetSteamID() override { ++getterCalls; return CSteamID(id_); }
    int InitiateGameConnection(void*, int, CSteamID, uint32, uint16, bool) override { return 0; }
    void TerminateGameConnection(uint32, uint16) override {}
    void TrackAppUsageEvent(CGameID, int, const char*) override {}
    bool GetUserDataFolder(char*, int) override { return false; }
    void StartVoiceRecording() override {}
    void StopVoiceRecording() override {}
    EVoiceResult GetAvailableVoice(uint32*, uint32*, uint32) override { return k_EVoiceResultNotRecording; }
    EVoiceResult GetVoice(bool, void*, uint32, uint32*, bool, void*, uint32, uint32*, uint32) override { return k_EVoiceResultNotRecording; }
    EVoiceResult DecompressVoice(const void*, uint32, void*, uint32, uint32*, uint32) override { return k_EVoiceResultNotRecording; }
    uint32 GetVoiceOptimalSampleRate() override { return 0; }
    HAuthTicket GetAuthSessionTicket(void*, int, uint32*) override { return 0; }
    EBeginAuthSessionResult BeginAuthSession(const void*, int, CSteamID) override { return k_EBeginAuthSessionResultInvalidTicket; }
    void EndAuthSession(CSteamID) override {}
    void CancelAuthTicket(HAuthTicket) override {}
    EUserHasLicenseForAppResult UserHasLicenseForApp(CSteamID, AppId_t) override { return k_EUserHasLicenseResultNoAuth; }
    bool BIsBehindNAT() override { return false; }
    void AdvertiseGame(CSteamID, uint32, uint16) override {}
    SteamAPICall_t RequestEncryptedAppTicket(void*, int) override { return 0; }
    bool GetEncryptedAppTicket(void*, int, uint32*) override { return false; }

    // Counters prove that rejected arguments do not reach the SDK interface
    uint32_t loggedOnCalls = 0;
    uint32_t getterCalls = 0;
private:
    bool loggedOn_;
    uint64_t id_;
};
}

extern "C" uint32_t IwFixtureStorageSize() { return sizeof(FixtureSteamUser); }
extern "C" uint32_t IwFixtureStorageAlignment() { return alignof(FixtureSteamUser); }
extern "C" void* IwFixtureConstruct(void* storage, uint32_t bytes, int loggedOn, uint64_t id) {
    // Construction is rejected before touching undersized or misaligned storage
    if (!storage || bytes < sizeof(FixtureSteamUser) ||
        reinterpret_cast<uintptr_t>(storage) % alignof(FixtureSteamUser) != 0) return nullptr;
    return new (storage) FixtureSteamUser(loggedOn != 0, id);
}
extern "C" void IwFixtureDestroy(void* object) {
    // The same compiler that constructs the complete object also destroys it
    if (object) static_cast<FixtureSteamUser*>(object)->~FixtureSteamUser();
}
extern "C" uint32_t IwFixtureLoggedOnCalls(void* object) { return static_cast<FixtureSteamUser*>(object)->loggedOnCalls; }
extern "C" uint32_t IwFixtureGetterCalls(void* object) { return static_cast<FixtureSteamUser*>(object)->getterCalls; }
