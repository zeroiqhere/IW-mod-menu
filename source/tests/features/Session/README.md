# Selected teammate ownership checks

The tests link `backend/Session/TeammateGodmodeSession.cs` and the production
cleanup bridge, registry, flag feature and shutdown coordinator. Every transport
is owned in-memory state. They open no process/window, read no live game memory,
and add no writer primitive or offset.

Run the complete feature regression from the source root:

```sh
dotnet run --project tests/features/Features.Tests.csproj -c Release
```

The helper API is in `Apparition.Backend.Session`:

- Constructor: a fresh `Func<int,PlayerState>` reader and a bound
  `Func<PlayerIdentity,IPlayerGodmodeFeature>` factory
- `ReadView()`: immutable SelectedGodmodeView with captured Identity, GodmodeValue,
  Status, CanSelect, CanToggle and HasPendingCleanup
- `SelectAsync(int slot,string expectedName)`: read-only capture, returning bool
- `ToggleAsync(int slot,string expectedName)`: validated operation result, or null
  when the payload is rejected before a change
- `CloseAsync()`: joined registry close result; repeated close can retry retained
  original restoration after a failure

Only teammate slots 1..3 are accepted. Slot 0 remains outside this helper and is
not inferred to be the local Steam player or host. Selection changes metadata
without closing previous active owners. Full entity/client/server/GUID/name
identity is retained for action checks; a reused slot/name is insufficient.
Pending original cleanup blocks new selection/enabling while preserving the
original selected OFF retry. Closure immediately rejects queued select/toggle
work and retains the original resource when restoration cannot be confirmed.

The suite covers selection/switch/back/OFF ownership, stale payload and full
identity, foreign flags before and after capture, living-state rejection,
immutable views without repeated factories, pending cleanup, real monitor joins
and concurrent view/close. The held-close wrapper delays only the cleanup boundary
of the real FlagsPlayerGodmodeFeature, proving 1000 views overlap an unfinished
close; it does not replace flag behavior or claim a worker has stopped early.

Cleanup bridge checks use the production SessionShutdown coordinator: uncertain
teammate restore retains the resource but still attempts independent ammo cleanup.
A retry after restoration recovers releases only after all owners are joined.
ReadView preserves fresh validation callbacks; it does not cache process state.
These checks establish local orchestration, not Windows transport concurrency,
retail map-exit compatibility, all-player completion or a crash fix.

The thirteen selected-session/cleanup cases pass in the joined feature suite.
The initial foreign-after-capture display regression failed with UNKNOWN expected
and OFF observed before the registry added fresh read-only preflight. Final whole-
wave counts and production transport acceptance are reported separately.
