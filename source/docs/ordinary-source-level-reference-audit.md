# Ordinary source-level input checkpoint

This offline update extends the monitor's relative 50 ms cycle to ordinary open
menus. A held action can run on the first qualified visit after Main or Quick
opening becomes ready. Each iteration evaluates scrolling, Use, choice slots,
then knife or dead Quick Jump in that order. The physical sampler still runs at
the existing worker cadence; these tests do not establish the original GSC VM's
physical polling phase.

The reference is Apparition commit
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`, [Menu/base.gsc](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc).
The monitor's open branch is at lines 51–163 and its outer wait at line 168.
The source closes Main when its actor dies before reading ordinary input, so
this update retains the backend's Quick-only dead Jump scope.

## Clock and ownership

One evaluation timestamp and interval now cover open and closed ordinary
visits. A guard-false cycle consumes 50 ms. Pending transport, current backend
work, callback locks and published mechanical waits suspend the phase.
Permission, row and navigation changes cannot erase an existing scroll wait.
Scrolling retains animation + 75 ms, plus half the animation duration for each
skipped spacer. Knife and Jump results retain 250 ms after their terminal
result. An admitted child knife loop samples its own nine 10 ms waits before
returning to the ordinary branch chain.

Current typed actor facts authorize each SourceLevels command. A continuation receipt
explains only its original written invocation; it is not actor permission.
Actual slider and nonslider choice waits remain producer-owned at 180 and
50 ms. The same qualified current input can run when that wait retires without
an added native delay. The worker retains its legacy fresh-edge route for
snapshots without typed local eligibility; the SourceLevels claims do not
cover those snapshots.

Focus loss, key-reader failure, invalid calls and backward time retire state.
Ordinary recovery requires all ordinary action groups to become neutral. That
neutral sample starts a fresh 50 ms wait. Opening chords keep their separate
release barrier. All predicates use one physical key sample per poll.

## Select timing still incomplete

A held Use button can dispatch its first qualified Select after opening. A
further held Select requires its exact captured callback to become ready.
Without that proof, Use remains consumed until physical release and continues
to shadow lower-priority branches. A row or navigation change cannot clear it.

This is a conservative limit, not complete source parity. Native row metadata
does not describe every source function, slider or BOOL declaration. Generic
submenu Select repeats still need exact producer-derived wait classification
before their caller behavior can be implemented. A newer
[declaration audit](preference-use-repeat-reference-audit.md) found that all
represented selectable source rows have a defined callback; omitted callbacks
default to defined `EmptyFunction`. No represented row establishes the
missing-function 50 ms branch. Row
availability, an ACK and a changed label cannot supply that classification.

## Evidence

The original held-through-opening checks failed 0/2 before implementation.
The new ordinary input suite failed 8/21 before implementation. Updated idle
knife assertions and current held Use/Jump assertions also failed against the
old router. The final suite includes 25 ordinary checks, the original 2 held
opening checks and 3 checks joining actual controller packets to native input,
commands and result paint. The full portable run covers 38 suites.

The independent living Quick graph contains 11 actual controller packets and a
capture manifest. The inert session records one local God Mode action, keeps
its result unknown while pending, then publishes ON. Retained BOOL paint stays
OFF until the 180 ms redraw; the callback locks input until 430 ms. Native
Select fields match the producer's original command fields. An uncaptured
write cannot borrow the ready receipt. No process-memory writer runs in this
graph.

The backend suite executes 905 checks from current compiled source. Unchanged
session and feature results may be reused only after exact evaluated input
membership and byte comparisons. Earlier 271 producer packets remain immutable
and separately inventoried. Windows compilation covers the DLL and nine test
executables; none is executed in Wine or a game.

This source checkpoint is not installed or ready for gameplay testing. Full
source callback coverage, resident game-lifetime preferences, retail player and
cohort authority, physical drawing/input/injection lifetime, and normal Steam
co-op across all maps, quests and the Super EE remain incomplete. The saved
playable backup and installed products remain unchanged.
