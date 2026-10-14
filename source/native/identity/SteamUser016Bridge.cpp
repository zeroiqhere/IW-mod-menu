#include "SteamUser016Bridge.h"

// The build supplies the historical declarations from an external licensed SDK
#include "steamclientpublic.h"
#include "isteamuser.h"

// A different target ABI must never compile this aggregate-return call
#if !defined(_MSC_VER) || !defined(_M_X64) || !defined(__clang__)
#error SteamUser016Bridge requires Clang targeting the Microsoft x64 ABI
#endif
static_assert(sizeof(CSteamID) == sizeof(uint64_t));
static_assert(alignof(CSteamID) == 1);

extern "C" int IwReadSteamUser016Identity(void* authorizedInterface, uint64_t* result) {
    // A missing output must never touch the caller's interface
    if (!result) return IwSteamIdentityNullOutput;
    *result = 0;
    if (!authorizedInterface) return IwSteamIdentityNullInterface;

    // The caller supplies lifetime ownership; this cast does not establish it
    auto* user = static_cast<ISteamUser*>(authorizedInterface);
    if (!user->BLoggedOn()) return IwSteamIdentityNotLoggedOn;

    // Only the Microsoft-ABI compiler handles this historical aggregate return
    const CSteamID id = user->GetSteamID();
    if (!id.IsValid()) return IwSteamIdentityInvalidId;
    if (id.GetEAccountType() != k_EAccountTypeIndividual) return IwSteamIdentityNotIndividual;

    // Success reports observed scalar shape rather than game-player identity
    *result = id.ConvertToUint64();
    return IwSteamIdentitySuccess;
}
