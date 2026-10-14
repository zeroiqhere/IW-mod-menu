#include "../../native/identity/SteamUser016Bridge.h"
#include "FixtureSteamUser.h"
#include <array>
#include <cstdio>
#include <stdexcept>

namespace {
constexpr uint64_t ValidIndividual = 0x011000010000002aULL;
void Require(bool condition, const char* message) {
    if (!condition) throw std::runtime_error(message);
}
struct Fixture {
    // MinGW owns only bytes while the Microsoft-ABI callee owns object construction
    alignas(16) std::array<unsigned char, 256> storage{};
    void* object;
    Fixture(bool loggedOn, uint64_t id) : object(nullptr) {
        Require(IwFixtureStorageSize() <= storage.size(), "fixture exceeds bounded storage");
        Require(IwFixtureStorageAlignment() <= 16, "fixture alignment exceeds storage");
        object = IwFixtureConstruct(storage.data(), storage.size(), loggedOn, id);
        Require(object != nullptr, "fixture construction failed");
    }
    ~Fixture() { IwFixtureDestroy(object); }
    Fixture(const Fixture&) = delete;
    Fixture& operator=(const Fixture&) = delete;
};
void ValidIndividualIdCrossesThePlainBoundaryExactly() {
    Fixture fixture(true, ValidIndividual);
    uint64_t result = 123;
    Require(IwReadSteamUser016Identity(fixture.object, &result) == IwSteamIdentitySuccess, "valid ID rejected");
    Require(result == ValidIndividual, "aggregate return lost ID bits");
    Require(IwFixtureLoggedOnCalls(fixture.object) == 1 && IwFixtureGetterCalls(fixture.object) == 1, "wrong interface slots called");
}
void NullOutputNeverTouchesTheInterface() {
    Fixture fixture(true, ValidIndividual);
    Require(IwReadSteamUser016Identity(fixture.object, nullptr) == IwSteamIdentityNullOutput, "null output not rejected");
    Require(IwFixtureLoggedOnCalls(fixture.object) == 0 && IwFixtureGetterCalls(fixture.object) == 0, "null output called interface");
}
void NullInterfaceClearsTheOutput() {
    uint64_t result = 123;
    Require(IwReadSteamUser016Identity(nullptr, &result) == IwSteamIdentityNullInterface && result == 0, "null interface retained output");
}
void LoggedOutInterfaceNeverCallsTheGetter() {
    Fixture fixture(false, ValidIndividual);
    uint64_t result = 123;
    Require(IwReadSteamUser016Identity(fixture.object, &result) == IwSteamIdentityNotLoggedOn && result == 0, "logged out retained output");
    Require(IwFixtureLoggedOnCalls(fixture.object) == 1 && IwFixtureGetterCalls(fixture.object) == 0, "logged out called getter");
}
void StructurallyInvalidIdsAreRejectedAndClearTheOutput() {
    // Invalid universe, type, account number and individual instance are distinct SDK checks
    for (uint64_t id : {0ULL, 0x001000010000002aULL, 0x010000010000002aULL, 0x0110000100000000ULL, 0x011000050000002aULL}) {
        Fixture fixture(true, id);
        uint64_t result = 123;
        Require(IwReadSteamUser016Identity(fixture.object, &result) == IwSteamIdentityInvalidId && result == 0, "invalid ID retained output");
    }
}
void ValidNonIndividualAccountIsNotReportedAsLocalUser() {
    Fixture fixture(true, 0x017000000000002aULL);
    uint64_t result = 123;
    Require(IwReadSteamUser016Identity(fixture.object, &result) == IwSteamIdentityNotIndividual && result == 0, "clan ID accepted as user");
}
void FixtureRejectsInvalidStorageWithoutConstructingAnObject() {
    alignas(16) std::array<unsigned char, 256> storage{};
    Require(IwFixtureConstruct(nullptr, storage.size(), 1, ValidIndividual) == nullptr, "null fixture storage accepted");
    Require(IwFixtureConstruct(storage.data(), 1, 1, ValidIndividual) == nullptr, "short fixture storage accepted");
    Require(IwFixtureConstruct(storage.data() + 1, storage.size() - 1, 1, ValidIndividual) == nullptr, "unaligned fixture storage accepted");
}
}
int main() {
    const std::array tests{ValidIndividualIdCrossesThePlainBoundaryExactly, NullOutputNeverTouchesTheInterface,
        NullInterfaceClearsTheOutput, LoggedOutInterfaceNeverCallsTheGetter,
        StructurallyInvalidIdsAreRejectedAndClearTheOutput, ValidNonIndividualAccountIsNotReportedAsLocalUser,
        FixtureRejectsInvalidStorageWithoutConstructingAnObject};
    unsigned failures = 0;
    for (auto test : tests) {
        try { test(); } catch (const std::exception& error) { ++failures; std::fprintf(stderr, "FAIL: %s\n", error.what()); }
    }
    std::printf("SteamUser016 bridge: %zu checks, %u failures\n", tests.size(), failures);
    return failures == 0 ? 0 : 1;
}
