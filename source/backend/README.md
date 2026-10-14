# Headless menu backend

This Windows executable owns the existing retail Steam feature operations. It creates no WPF window or console. The injected renderer communicates through an existing private session directory and never requests an automatic connection or feature enable.

Launch with the original game's Windows PID:

```text
iw_apparition_backend.exe --game-pid 1234 --session-directory C:\private\session
```

The argument order is fixed. The directory must already exist. Its access restrictions are supplied by the launcher. The backend retains the original process identity and exits when that process ends.

`command.json` contains `version: 1`, a positive unsigned `sequence`, the matching `processId`, and one lowercase `input`: `toggle`, `up`, `down`, `select`, or `back`. Every recoverable fresh sequence is acknowledged exactly once. Invalid versions, foreign PIDs, unsupported inputs, and lost focus never dispatch a game action. Malformed JSON without one usable sequence cannot supply an acknowledgment token.

`state.json` is replaced atomically every 100 ms and after commands. It contains `version`, `processId`, `generation`, `lastSequence`, `isOpen`, `accent`, `title`, `context`, `notice`, `selectedIndex`, and `rows` with `label`, `value`, and `enabled`. Each committed frame increments `generation`, including unchanged frames. State is bounded to 32 KiB, 128 rows, and a 1,500-character notice. Readers must allow file deletion sharing on Windows so replacement can succeed.

The renderer sends one command at a time until `lastSequence` acknowledges it. It should stop displaying cached feature states when heartbeat generations stop. The command directory must be unique for each launch; commands from an earlier backend instance must not be reused.

`stop-request.json` with `version: 1` and the exact `processId` requests graceful cleanup, including when game focus is elsewhere. `status.json` records bounded errors. Exit code 0 confirms stopped workers and successful restoration; 2 reports startup or runtime failure; 3 reports stopped workers with unconfirmed restoration; 4 reports incomplete cleanup after the game ended.

Personal and selected-player Godmode share one connection registry. Captured-set operations reuse the same local service and retained peer adapters; individual toggles choose their desired state inside that registry's gate. Preflight checks are read-only, and fresh ownership failures withdraw cached personal capability. Pending cleanup blocks new enables while retaining original restoration routes. All Players Godmode dispatches only with a verified complete cohort; partial retail cohorts remain unavailable while absent-slot semantics are unverified.

The cohort reader now preserves four explicit slot observations. Fully linked actors are Present even when life or movement blocks a new enable; missing, free, inactive and unreadable slots are Unknown. The retail reader never reports ConfirmedAbsent because retail empty-slot semantics have not been established. Four records describe storage capacity, not player population. Observations are repeated and copied; they do not establish an atomic engine snapshot or spawn generation.

The registry's cohort Set and Toggle APIs recheck the entire captured membership before binding, preflight, every member operation and completion. They retain advancing command-time watermarks. A join, departure, unknown slot or replaced connection rejects the operation, with rollback limited to its original owned writes. Fresh read-only flag preflight must confirm each member before choosing the toggle direction; stale monitor Active or unknown pending flags cannot supply an all-ON decision. Toggle turns everyone off only when every participant is freshly confirmed owned ON; otherwise it enables missing members. The All Players Menu dispatches this toggle only with a private cohort receipt, current host proof and complete membership; any unknown retail slot keeps the row unavailable. The same personal and selected-player adapters remain the only feature owners. The read-only transport port does not establish authenticated local account or host authority by itself.

Personal infinite ammo remains unavailable. Lobby ammo keeps its existing shared instruction patch. Teammate Godmode keeps the selected full player identity, guarded flag write, pending restoration lock, and installed-build check. Menu colors work through `accent`; the controls row documents the renderer's aim, melee, fire, and use inputs.

Portable regression checks live in `../tests/backend`. These tests never attach to a game. The copied portable feature and memory-layout helpers originate in the reviewed desktop project; `dependencies/Memory.dll` remains its bundled upstream binary, with its separate LGPL license.

## Main opening controls

The [integrated editor contract](../docs/opening-controls-reference-audit.md) covers private saved-action
availability, copied applied chord descriptors, typed spacers and captured
navigation/policy epochs. Portable checks cover the controller join and backend behavior; the saved
test evidence identifies the inputs and profiles used. Legacy omitted descriptors retain the Aim plus fresh Melee gesture;
explicit descriptors use a full aggregate chord edge. Main and Quick navigation and
account-scoped preferences have offline source coverage. General held Select across
submenu transitions and physical retail acceptance remain unfinished.
