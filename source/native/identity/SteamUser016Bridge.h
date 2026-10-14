#pragma once

// Only C scalar types cross the compiler boundary
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

enum IwSteamIdentityStatus {
    IwSteamIdentitySuccess = 0,
    IwSteamIdentityNullOutput = 1,
    IwSteamIdentityNullInterface = 2,
    IwSteamIdentityNotLoggedOn = 3,
    IwSteamIdentityInvalidId = 4,
    IwSteamIdentityNotIndividual = 5
};

// The caller must already own a live, version-verified SteamUser016 interface
// This function cannot validate pointer lifetime or acquire a Steam session
int IwReadSteamUser016Identity(void* authorizedInterface, uint64_t* result);

#ifdef __cplusplus
}
#endif
