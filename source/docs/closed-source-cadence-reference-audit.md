# Closed source monitor cadence

This source-only update restores a relative 50 ms closed-menu iteration. It does
not install or enable the injected DLL. The saved playable backup is unchanged.

## Reference and required behavior

The pinned [Apparition monitor](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L19)
checks current held opening chords and ends every completed iteration with
`wait 0.05`, including iterations rejected by the menu/control guard. Main keeps
priority over Quick; dead Quick uses saved Aim and Jump. A policy or admission
revision alone does not prove a new monitor thread. The source
[verification code](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/verification.gsc#L58)
explicitly stops and restarts that thread, an identity the current packet does
not publish.

The router adopts its first qualified closed cycle immediately. Each subsequent
completed evaluation waits at least 50 ms from the previous evaluation, using
the current keys, policy and actor facts at that deadline. A transient chord
inside the wait is discarded. Late observations anchor the next wait to the
actual observation; they do not align to a global clock or replay missed cycles.

Queued writes, an actual blocked operation and an active backend monitor wait
suspend evaluation without restarting an existing closed phase. Guard-false
facts still consume an idle iteration. An open predecessor retires the idle
phase, so an owned Back's completed 250 ms wait does not pay another 50 ms before
independently authorized reopening. A foreign Back receipt cannot restart an
existing idle phase or supply actor/policy permission.

## Input and GUI boundary

Physical input continues at the worker cadence for focus safety, key release
and heartbeat handling. One cached physical sample feeds opening arbitration;
only a due evaluation may emit an opening intent. That gate covers both context
seeding and the regular path. No packet schema, menu row, feature availability,
gameplay permission or command scope changes.

A source-sample clock detects rollback between evaluations. A separate closed
evaluation clock preserves the logical wait while input sampling continues.
Relative subtraction avoids deadline overflow at the largest timestamp.
Focus loss, invalid input and failed reads retire both clocks and keep the
existing neutral-input barrier. The legacy fresh-edge route retains its prior
behavior and has no closed source cadence.

## Evidence and limits

The first genuine regression run passed 2 of 14 cadence checks; twelve failed
on the original production implementation. The updated existing router and
actual controller graph expectations also failed before implementation.
After the fix, all 18 cadence, 26 opening and 13 actual graph checks pass.
Actual controller packet bytes remain unchanged; only native host observation
times in the authority graphs change. Producer clocks and host clocks are
separate and do not prove the original VM's physical phase.

The focused checks cover idle boundaries, transient holds, policy and permission
recovery, relative adoption, late wakes, missing-policy recovery, pending ACKs, foreign waits, completed
Back, focus and invalid-mode recovery, rollback and timestamp limits.

Two added coverage checks distinguish backend-blocked work from guard-false
idle cycles and preserve wait rollback rejection across a sampling-mode switch
when a typed owner disappears. The existing wait-only timestamp remains useful
for that transition and is retained.

A temporary nonordinary input mode while still closed does not establish an
open predecessor or a new monitor identity; it preserves the existing phase.
With that regression added, the runner passed 16 of 17 checks before narrowing
the reset, then passed 17 of 17. All 35 registered native suites pass; the Windows
DLL and six related test executables compile successfully. Windows executables
were not run. Full results are recorded outside the source tree.

These results establish offline logical cadence, not original engine scheduling,
physical input/rendering, DLL injection or retail co-op/map/quest acceptance.
Ordinary held-action adoption after opening, complete gameplay callbacks and
full Apparition parity remain unfinished.
