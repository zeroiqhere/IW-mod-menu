# Retail Steam local-player identity feasibility

Later [retail player lookup evidence](retail-player-lookup-audit.md) supersedes
the on-disk code limitation below: bounded live code and data now establish
entity health and session-state semantics. The later
[controller-binding investigation](retail-controller-binding-audit.md) establishes
the current engine account, selected session and retail server-member relationship.
The compiled reader matches the inspected solo actor. The later
[production observation integration](production-observation-reference-audit.md)
connects that route to the backend provider and menu-owned access state; runtime
Quick opening and new feature integration remain unfinished. The engine route does not
require a separate Steam interface call or numeric Steam64-to-GUID equality.
This historical static audit is retained for provenance; its original blocked
decision and bridge requirements do not describe the new engine route.

Update, 2026-10-08: the [local observation checkpoint](local-observation-reference-audit.md)
adds an offline bridge fixture. Static comparison demonstrates that standard
MinGW/Windows GNU aggregate-return code disagrees with the installed proxy;
Clang's Microsoft Windows x64 ABI matches its receiver and return-buffer layout.
Two assembly checks and seven Windows fixture cases pass. No live interface is
acquired, and initialization/lifetime, thread ownership and retail GUID mapping
remain unproved. The bridge is optional and is not linked into the payload.

Read-only audit, 2026-10-08. Scope: the normal Steam retail game, app 292730, in custom Zombies sessions with friends. This audit inspected installed PE files and primary source documents without loading a DLL, calling Steam APIs, launching the game, reading live game memory, changing account settings, or reading login/credential files. Only this document was added.

## Decision

The installed game DLL has the structure of a historical Steam client proxy exposing the `SteamUser016` C++ interface. Its modern flat getter is absent. Original retail-depot provenance has not been established. A version-matched Windows C++ bridge is a feasible candidate for obtaining an account identifier, but neither a successful runtime binding nor the mapping from that identifier to a retail player GUID has been validated.

**Local-player selection for the shared flag registry remains blocked.** No slot number, name match, cached Steam account file, appmanifest owner, or IW7-Mod surrogate identity provides that missing proof. A new shared-registry self action and an All operation that promises to include self must remain unavailable until local identity is established. The existing personal health loop remains unchanged and separate; its earlier host-session observation does not establish a local identity binding for the registry. Existing validated teammate operations do not establish self identity either.

## Installed binary evidence

The appmanifest identifies installed build `2715288`. Files were inspected in `$HOME/.local/share/Steam/steamapps/common/Call of Duty - Infinite Warfare`.

| File | SHA-256 |
| --- | --- |
| `steam_api64.dll` | `fc20547408a7c34f0bd4946a34c21aab48a75e3b98dce9e55969f486d37b212f` |
| `iw7_ship.exe` | `a883ee28ee9f552c63d8ed1e352f60501b2e256d8857ca178c6dd3aae622723b` |

These fingerprints identify inspected files, not every retail build or an authenticated running session.

### DLL provenance follow-up

The installed `steam_api64.dll` was audited offline for metadata, imports, export bodies, target-library strings, and backup candidates. Its PE timestamp is 2013-02-25, its linker version is 10, and its resources identify `Valve Corporation` / `Steam Client API`. One localized resource reports file version `01.68.08.89` and product version `01.00.00.01`; another reports `1, 0, 0, 1`. Its debug record names a Valve `steam_rel_client_win64` buildslave path. Imports are from `KERNEL32.dll`, `ADVAPI32.dll`, and `SHELL32.dll`; dynamic-loading imports include `LoadLibraryExA/W` and `GetProcAddress`. Strings target `steamclient64.dll` / `steamclient.dll`, `SteamClient012`, and Steam's active-process client-library registry values.

The `SteamUser` export at RVA `0x2740` reads a cached interface pointer and returns it. Ordinary initialization passes that cache to a routine at RVA `0x3040`, which obtains an interface through a client virtual method using the literal `SteamUser016` and stores the returned pointer. Its account-ID use passes return storage to virtual entry 2 before reading the returned 64-bit data, consistent with the historical ABI below. This is client-proxy behavior rather than the local auth-key-hash implementation shown in IW7-Mod's source.

A PE security directory is present. Offline OpenSSL parsing of its PKCS#7 record succeeds and shows an embedded Valve code-signing certificate plus VeriSign/Symantec certificates. **Parsing certificates is not signature verification**: the PE digest, signature, timestamp, certificate trust, and revocation status were not validated. Version resources and paths are also self-reported metadata. These observations strongly support a legacy Valve SDK proxy interpretation, but do not prove that this is the DLL supplied by the installed IW Steam depot or that a mod installer never replaced it with another SDK build. No authoritative official hash was obtained. The SHA-256 above is an installed-file fingerprint only.

Filename inventories under the game directory and `$HOME/Downloads`, including task-related backup trees, found the installed `steam_api64.dll` and companion `steam_api.dll`, with no separate `steam_api64` / `steamclient` original-or-backup file. Backup archive contents and unrelated locations were not exhaustively inspected. No backup was restored, Steam verification was not run, and no network binary was downloaded.

Offline `objdump -p` lists 57 named DLL exports. Relevant exports include `SteamUser` (ordinal 51), `SteamClient`, `SteamAPI_Init`, `SteamAPI_InitSafe`, `SteamAPI_Shutdown`, `SteamAPI_RunCallbacks`, `SteamAPI_GetHSteamUser`, and `SteamAPI_GetHSteamPipe`. DLL strings contain `SteamUser016` and `SteamClient012`. The installed executable imports `SteamUser`, `SteamAPI_Init`, `SteamAPI_Shutdown`, and `SteamAPI_RunCallbacks` from `steam_api64.dll`.

The DLL does **not** export `SteamAPI_ISteamUser_GetSteamID`, a `SteamAPI_SteamUser_vNNN` accessor, `SteamInternal_ContextInit`, `SteamInternal_CreateInterface`, or `SteamAPI_ReleaseCurrentThreadMemory`. A direct P/Invoke of the modern getter cannot work against this file. The exported `SteamGameServer_GetSteamID` identifies a server and is not a local-player substitute. Steam user/pipe handles are not Steam account IDs.

Reproducible inspection commands, with no DLL execution:

```bash
iw_game_dir="$HOME/.local/share/Steam/steamapps/common/Call of Duty - Infinite Warfare"
objdump -p "$iw_game_dir/steam_api64.dll"
objdump -p "$iw_game_dir/iw7_ship.exe"
strings "$iw_game_dir/steam_api64.dll" | rg '^Steam(User|Client)[0-9]+$'
sha256sum "$iw_game_dir/steam_api64.dll" "$iw_game_dir/iw7_ship.exe"
```

## Verified API shapes

Valve documents [`SteamUser()->GetSteamID()`](https://partner.steamgames.com/doc/api/ISteamUser#GetSteamID) as returning a `CSteamID` for the account currently logged into the Steam client. This identifies an account; it does not identify an IW entity or prove that a separate helper is associated with a particular game process.

Historical headers in Valve's Proton repository, pinned at `5b89db940e0ebe3a137a6009a3589232fe084c09`, resolve the old interface shape:

- [`steamworks_sdk_121/steam_api.h`](https://github.com/ValveSoftware/Proton/blob/5b89db940e0ebe3a137a6009a3589232fe084c09/lsteamclient/steamworks_sdk_121/steam_api.h) declares the exported accessor `ISteamUser *SteamUser()` and distinguishes ordinary initialization from version-safe context acquisition
- [`steamworks_sdk_121/isteamuser.h`](https://github.com/ValveSoftware/Proton/blob/5b89db940e0ebe3a137a6009a3589232fe084c09/lsteamclient/steamworks_sdk_121/isteamuser.h) declares `SteamUser016`, with `GetHSteamUser`, `BLoggedOn`, then `CSteamID GetSteamID` as its first three virtual methods
- [`steamworks_sdk_121/steamclientpublic.h`](https://github.com/ValveSoftware/Proton/blob/5b89db940e0ebe3a137a6009a3589232fe084c09/lsteamclient/steamworks_sdk_121/steamclientpublic.h) defines the 64-bit representation and `CSteamID::ConvertToUint64()`
- [`winISteamUser.c`](https://github.com/ValveSoftware/Proton/blob/5b89db940e0ebe3a137a6009a3589232fe084c09/lsteamclient/winISteamUser.c#L3205) implements the Windows getter as `CSteamID * __thiscall ... (struct w_iface *_this, CSteamID *_ret)`, with caller-provided return storage

The Windows C++ method is therefore not equivalent to a guessed managed delegate returning `ulong` from one `this` pointer. The verified interface places the getter at zero-based virtual entry 2, after the handle and logged-on methods, without a virtual destructor entry. That source fact does not authorize an unvalidated indirect call. The compiler must handle the historical interface and Windows aggregate-return ABI. A future bridge should expose a small plain C result after calling the version-matched C++ declaration and converting the returned `CSteamID`; it must validate its interface and failure paths before integration. A fixture whose caller and callee are both compiled by MinGW would only establish agreement within that fixture, not compatibility with the game's MSVC ABI. No bridge or vtable caller was implemented in the original audit; the later optional offline fixture is described above.

For comparison, Valve's [`steamworks_sdk_165/steam_api_flat.h`](https://github.com/ValveSoftware/Proton/blob/5b89db940e0ebe3a137a6009a3589232fe084c09/lsteamclient/steamworks_sdk_165/steam_api_flat.h) declares `uint64_steamid SteamAPI_ISteamUser_GetSteamID(ISteamUser *self)` and `SteamAPI_SteamUser_v023()`. That plain C API has a different boundary and is unavailable in the inspected retail DLL. The current header is not a reason to replace the game's DLL.

## Initialization and thread ownership

Valve requires successful [`SteamAPI_Init`](https://partner.steamgames.com/doc/api/steam_api#SteamAPI_Init) before interface access. It depends on a running Steam client, correct App ID context, matching OS user/access level, and account ownership. Shutdown releases interface pointers. [`SteamAPI_RunCallbacks`](https://partner.steamgames.com/doc/api/steam_api#SteamAPI_RunCallbacks) dispatches listeners on the pumping thread; multi-threaded pumps can move callback execution between threads. Its thread-memory cleanup helper is absent from this old DLL.

No consulted documentation imposes a specific main-thread-only rule on `GetSteamID`. A serialized API-owner context is a proposed lifecycle policy, not an observed engine guarantee.

For a bridge sharing the game's initialized context, borrowing its interface must not independently initialize, shut down, or pump the game's global callbacks. This is an ownership precaution inferred from Valve's lifecycle semantics. The game already imports those lifecycle functions. For a separate helper, its own successful initialization and Steam ID still require a validated relationship to the captured retail game session; being in the same prefix or seeing an `iw7_ship` process is insufficient. API pointers are local to their process and cannot be passed to another process for dereferencing. A Linux Vulkan layer also cannot treat this Windows PE interface as an ordinary Linux ABI function.

No `steam_appid.txt`, app-ID environment override, replacement Steam DLL, relaunch API, account switch, or alternate client was introduced. Those are not established solutions to the session-association problem.

## GUID derivation evidence and limits

IW7-Mod source was inspected at `c0a1c6dacd33c86320b308656d9ba895291c2198`. Its [`user.cpp`](https://github.com/auroramod/iw7-mod/blob/c0a1c6dacd33c86320b308656d9ba895291c2198/src/client/steam/interfaces/user.cpp#L12) sets its surrogate Steam ID bits to `auth::get_guid()`. [`auth.cpp`](https://github.com/auroramod/iw7-mod/blob/c0a1c6dacd33c86320b308656d9ba895291c2198/src/client/component/auth.cpp#L288) returns a generated/loaded key hash for ordinary clients and a fabricated session ID for dedicated servers. Its GUID console output formats those surrogate bits as hexadecimal. Equality there exists by construction and does not prove equality to a Valve Steam ID in retail.

The pinned [`symbols.hpp`](https://github.com/auroramod/iw7-mod/blob/c0a1c6dacd33c86320b308656d9ba895291c2198/src/client/game/symbols.hpp) names `Live_GetXuid`, `Session_GetXuid`, `StringToXUID`, `XUIDToString`, `SV_GameMP_GetGuid`, and `SV_IsLocalClient`. These declarations are research leads, not validated retail calls or a local-controller-to-server-slot mapping. Offline disassembly at the corresponding installed-executable addresses did not yield coherent function bodies. The on-disk bytes therefore do not establish their retail implementation; no attempt was made to obtain decrypted live code.

CODIW-Source at `d1a226dbd553229503290cfb73f8051ee3b34749` adds script-level evidence:

- [`cp/utility.gsc`, `getuniqueid`](https://github.com/AuroraDoesCode/CODIW-Source/blob/d1a226dbd553229503290cfb73f8051ee3b34749/scripts/scripts/cp/utility.gsc#L417) calls engine `getguid()`, caches its string, and substitutes `scriptN` for an all-zero GUID
- [`mp/utility.gsc`](https://github.com/AuroraDoesCode/CODIW-Source/blob/d1a226dbd553229503290cfb73f8051ee3b34749/scripts/scripts/mp/utility.gsc#L5902) uses the same pattern
- [`mp/playerlogic.gsc`](https://github.com/AuroraDoesCode/CODIW-Source/blob/d1a226dbd553229503290cfb73f8051ee3b34749/scripts/scripts/mp/playerlogic.gsc#L1869) assigns that value to `self.guid` and uses it in match-data fields named `xuid`

These scripts neither expose the engine getter implementation nor convert Steam64 to GUID text. Hexadecimal formatting of an unsigned 64-bit value would only encode that value; it would not prove what account namespace supplied it. This community source dump is evidence of script behavior, not an official retail identity contract.

The current `PlayerStateReader` accepts only teammate slots 1–3 and validates linked entity/client/server records plus a bounded 16-digit hexadecimal GUID. Its existing observations establish an identity guard for those records, not GUID-to-SteamID equality. `GameIdentity` protects process lifetime and PID reuse; it does not authenticate a Steam account or find the local player.

## Required evidence before integration

1. Validate a version-matched account-ID bridge against the installed DLL with explicit initialization ownership, Windows x64 return ABI, null-interface handling, shutdown behavior, and no game callback interference
2. Establish the retail connection from that account ID to the engine GUID/XUID, or validate an independent engine local-controller-to-player mapping; include host and non-host custom sessions and reject ambiguous or multiple local players
3. Resolve exactly one current roster identity and revalidate its session and entity/client links before each shared-registry operation; clear it on transitions, disconnect, or conflicting observations
4. Add regression coverage for missing API exports, initialization failure, no local match, duplicate matches, identity changes, slot reuse, and shared self/selected/all ownership; retain fail-closed behavior while evidence is missing

A later, separately scoped read-only runtime probe could validate the historical API binding and compare an account ID with a captured retail roster without feature writes. It would need to report missing or duplicate matches instead of selecting a fallback slot. That probe is outside this audit and was not run; no game trials are required to complete this documentation.

No gameplay, Steam API runtime call, local identity match, or feature write was tested here. This documentation changes no backend behavior and does not claim the baseline's 497 checks validate Steam identity. No software or packages were installed.
