# Retail session ownership and menu state

This wave makes the actual retail session testable with an owned recording
transport. The production factory still opens the existing native transport;
the tests link the real session, feature primitives, player bindings and cleanup
code without linking Memory.dll or any process-opening implementation. No
gameplay offset, patch bytes, health target or flag mask changed.

## Connection and shutdown

Each connection publishes one complete resource bundle. That bundle is also the
generation token for asynchronous results. Status and selection changes commit
under the lifecycle gate only while the original bundle remains current.
An old successful or failed operation cannot overwrite a fresh connection.

Shutdown invalidates queued requests immediately, before its worker starts.
A Connect request admitted before shutdown cannot reopen the session afterward.
Feature workers join before transport disposal. Failed stopping or disposal
retains the original resources and blocks reconnect until cleanup succeeds.
An ammo failure schedules cleanup without awaiting it under the lifecycle gate.

## Display and diagnostics

A frame that observes its resource bundle retire during a player-state read
publishes UNKNOWN values and disables game actions. The controller preserves
both its own pending state and the session's busy state. Local appearance
changes remain available while gameplay controls are disabled.

The bounded message history now serializes queue access and returns owned
snapshots. Rendering works from a snapshot outside the lock. Truncation avoids
splitting a complete UTF-16 surrogate pair at the length limit.

## Evidence and limits

The new session suite has 24 checks. Four delayed completion regressions, queued
Connect after shutdown, a retired frame, and concurrent history corruption were
reproduced before their fixes. Two new controller checks cover backend busy
projection, rejected-input non-replay and local appearance controls.

The integrated checkpoint has 523 distinct checks: controller 136, features
103, retail session/history 24, native runtime 60, packaging 70, scroll 9,
choice 12, Quick Exit 15, width input 15, unlock input 7, saved hints 7,
animation 8, geometry 19 and CPU panel 38. Unchanged native suites retain their
previous results only after source comparison; the sixteen fresh controller
exports are byte-identical to the previously checked parser/panel input.
Affected managed suites and packaging were rerun, and the Windows backend
published successfully with warnings treated as errors.

These checks establish behavior against owned test buffers. They do not prove
atomicity against the game, retail transitions, all-map acceptance, private
ammo or full Apparition parity. No live process was opened, DLL injected or
game launched. The earlier match-exit crash remains unresolved, the installed
launcher is unchanged, and the normal-launch guard is retained.

The separate [Steam identity audit](local-steam-identity-feasibility.md) explains
why a new shared-registry local/all-player binding is still unavailable. It
does not change the existing personal health loop.
