# Closed menu opening from current held buttons

This source candidate follows the closed-menu branch in [Apparition's pinned
monitor](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc).
A fresh official HEAD check still identifies that revision. This is offline
source work; installed launchers, the saved playable backup and game files are
unchanged.

## Source behavior and its timing

`base.gsc:28–48` checks current permission and held buttons each monitor cycle.
It does not require a new press after a menu closes or permission returns. A
living player's configured Main chord takes priority over configured Quick.
A dead player uses saved Aim and Jump for Quick. Disable Quick prevents either
Quick route and leaves living Main available. `AreButtonsPressed` checks each
configured action; it stores no earlier chord edge.

Root Close and child Back wait 200 ms before the outer 50 ms wait. At normal
250 ms retirement, a closed menu may evaluate its current opening chord again.
That Back receipt proves only the earlier invocation and cannot authorize the
next opening. Knife alone still does not constitute an opening chord.

Main begins its rebuild before the 50 ms HUD creation wait. Its caller then
waits 500 ms and the monitor waits 50 ms, giving 600 ms before ordinary input.
Quick creates immediately and has a 550 ms caller/monitor pause. The adapter
retains those existing producer phases rather than adding another arbitrary
50 ms pause after Back.

The Windows worker still samples at 20 ms. These checks establish current-level
arbitration and the published producer deadlines, not literal 50 ms VM polling
or the engine's scheduling and frame timing. That remaining physical fidelity
requirement is not proved by portable tests or Windows compilation.

## Permission and recovery

`ClosedOpeningPolling::SourceLevels` is an explicit local sampling mode. It
grants no player identity, menu permission, policy or action binding. Current
typed admission and real saved policies are required independently of any
retained monitor receipt. Unknown life, missing admission or missing policy
cannot fall through to an unscoped legacy Toggle.

The worker selects this mode when a typed eligibility descriptor is present.
Omitted legacy descriptors retain their existing administrative bootstrap and
fresh-edge behavior. Default direct API calls also keep `FreshEdges`; mixed
installed versions have not been accepted.

After focus loss, malformed metadata, failed key reads, unsupported input modes
or missing editor bindings, a neutral chord is required before opening resumes.
This is a host recovery rule, not an Apparition VM rule. A benign context refresh
cannot clear that barrier. Permission recovery and ordinary closure alone do
not require release. Physical key state is read once per router sample.

Scoped opening commands retain the original navigation, policy and admission
revisions. The controller validates them before queueing and checks fresh
actor/owner/focus facts again before dispatch. An acknowledgment, receipt or
elapsed native timer cannot supply those facts.

## Regression scope

Separate arbitration and router regressions cover Main/Quick/dead priority,
current permission and controls recovery, pending requests, exact Back249/250,
missing proof, one physical sample, invalid calls, focus recovery and legacy
behavior. The tests reproduced both direct background key sampling and invalid
mode recovery before their respective fixes.

The separate actual-controller inventory covers opening, rebuild and cooldown
phases; current permission; actual Disable Quick; stale original command scopes;
and queued focus/admission/owner retirement. It is consumed unchanged by both
readers and the production native router. Stale-policy chronology also changes
navigation and is not claimed to isolate policy rejection alone.

Full parity remains incomplete: literal VM polling, generic threaded submenu
scheduling, complete gameplay callbacks, private weapon ammo transport, game
lifetime preferences, physical drawing and injection, retail player/cohort
authority, and normal Steam co-op across all maps, quests and the super Easter
egg still require their own evidence.
