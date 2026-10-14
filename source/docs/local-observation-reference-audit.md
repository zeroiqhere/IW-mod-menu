# Local observation ownership and legacy Steam identity boundary

Audit date: 2026-10-08. The full Apparition port remains active. This checkpoint
prepares observation ownership and an offline ABI fixture; it does not activate
Quick opening, introduce gameplay callbacks, or identify a retail local player.

## Runtime ownership

The existing retail session exposes an immutable cached local observation and an
asynchronous refresh seam. Known life, menu permission and disabled-control facts
require a structurally supported identity. Unknown remains distinct from Dead,
denied permission and enabled controls. Validation checks bounded text, GUID shape,
slot/count consistency and linked-pointer shape. Schema validation does not
authenticate an account or prove a retail mapping.

The production runner supplies no provider. Refresh completes immediately without
a task worker, factory, health/roster reads or extra transport queries, and the
cached observation remains binding unavailable. The working Main router and
existing gameplay writers are unchanged. No observation fields were added to the
native wire protocol or GUI.

An optional future provider borrows the session's original transport. Its factory,
connection queries and reads run off the lifecycle gate in one retained task.
Concurrent refresh requests share that read; cancelling one caller only abandons
that caller's wait. Completion publishes only while the original owner and read
ticket remain current. Cached views perform no native calls and are snapshots,
not fresh engine authorization for a Quick action.

Shutdown revokes cached facts immediately and joins both the read and asynchronous
cancellation callbacks before disposal. A bounded two-second engineering timeout
uses the existing incomplete-cleanup path: the original transport stays retained,
reconnection stays blocked, and cleanup can retry after the work ends. This timeout
is not BO3 engine timing. The owner loop supervises one pending refresh, observes
failures while admission is paused, and permanently stops admission before draining.

## SteamUser016 boundary

The installed legacy proxy's getter uses a receiver in RCX, caller return storage
in RDX, and virtual entry 2. Compiling the unchanged SDK121 C++ declaration with
ordinary MinGW or Clang's Windows GNU target does not provide that return-storage
argument. Agreement between two MinGW fixtures would therefore miss this defect.

The optional bridge compiles the complete historical declaration with Clang's
Microsoft Windows x64 ABI. An independently compiled Microsoft-ABI fixture callee
writes the returned aggregate through RDX. A MinGW caller crosses a plain C scalar
boundary and checks exact ID bits, null arguments, logged-out state, invalid IDs,
non-individual accounts and invalid fixture storage. Success proves this offline
boundary, not initialization, lifetime or an engine GUID relationship.

The bridge never acquires an interface, initializes/shuts down Steam, pumps
callbacks or replaces a game DLL. It is not linked into the injected payload.
Its interface argument requires an already authorized, live, initialized and
version-verified SteamUser016 object. Production has no such acquisition path.

The fixture uses separately supplied hash-verified SDK121 headers from Valve's
[Proton source](https://github.com/ValveSoftware/Proton/tree/5b89db940e0ebe3a137a6009a3589232fe084c09/lsteamclient/steamworks_sdk_121).
Those headers are not vendored or packaged, and this work grants no SDK licence.
The [identity feasibility audit](local-steam-identity-feasibility.md) records
provenance, lifecycle, caller-thread and retail mapping limits.

## Reproducible checks

Portable commands from the source root:

```sh
dotnet run --project tests/backend/Backend.Tests.csproj -c Release
dotnet run --project tests/session/Session.Tests.csproj -c Release
dotnet run --project tests/features/Features.Tests.csproj -c Release
```

The identity fixture can be configured independently with the existing MinGW and
Clang tools and separately supplied pinned SDK headers:

```sh
cmake -S tests/native-identity -B build/identity -G Ninja   -DCMAKE_SYSTEM_NAME=Windows -DCMAKE_CXX_COMPILER=x86_64-w64-mingw32-g++   -DCMAKE_BUILD_TYPE=Release -DIW_STEAM_SDK121_INCLUDE_DIR="$iw_sdk121_include"
cmake --build build/identity --parallel 2
ctest --test-dir build/identity -R SteamIdentityStaticAbi --output-on-failure
protontricks-launch --appid 292730 build/identity/iw_steam_identity_fixture.exe
```

The main CMake project also offers `IW_BUILD_STEAM_IDENTITY_FIXTURE=ON`.
It defaults OFF, so ordinary payload configuration needs neither SDK headers nor
Clang. The optional root integration and independent fixture builds pass.

## Results and limits

This checkpoint confirms **1371 distinct registered checks**: the preceding1342
plus14 session/model cases, six polling cases, two static ABI cases and seven
Windows bridge cases. Backend418, session38 and feature103 regressions pass.
Windows .NET8.0.12 independently runs all38 session cases and six polling cases.
The actual Windows bridge passes seven cases, and two assembly-contract cases
pass. Platform reruns, profile exports and audit assertions are not extra cases.

Compiled inert implementations and targeted regressions produced genuine RED
results before GREEN. A generated private Windows polling runner initially had
an incorrect namespace import; its compile error is a harness setup issue, not a
behavioral RED. The corrected runner passes. Strict production Windows backend
publication passes without suppressed warnings.

All214 actual-controller packets remain byte-identical to the preceding candidate.
The unchanged274 previews and native renderer/loader artifacts retain their sealed
proof. No graphics lifecycle, feature offsets or gameplay writers changed, so
this wave does not repeat GPU/game trials.

The user reports that match exit returned to the lobby successfully, and confirms
being unsure which build was loaded. The injected-menu state and exact tested
build remain unidentified. This supports the reported improvement; it does not
establish this checkpoint's retail acceptance or the earlier fatal-error cause.
The installed43-file package and its existing disabled marker remain unchanged.
The new source and review package have not been deployed.

## Required next integration

Actual Quick opening still needs a validated current local identity, engine life,
menu authorization and control-state source, with justified freshness and session
lifetime. The offline account-ID boundary alone supplies none of those facts.
The Quick controller/navigation/monitor and real callbacks must then be connected.
The requested broader gameplay, roles, persistence, IW map and quest behavior also
remain incomplete. Existing personal and validated teammate paths do not prove
All-player or shared-registry self ownership.
