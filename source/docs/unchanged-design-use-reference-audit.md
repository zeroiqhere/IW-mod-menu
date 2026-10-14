# Selecting the current Design with held Use

Selecting the already applied Design now uses the existing plain callback wait
for a connected, admitted typed menu owner. The selection keeps its exact Use
receipt through the 250 ms caller wait. It does not change the appearance,
redraw the HUD, save preferences or invoke gameplay work. Changed designs keep
their separate rebuild path; preview and actorless compatibility paths keep
their existing transition contract.

Apparition commit `307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`
[captures the slider before dispatch and waits 200 ms plus the 50 ms outer cycle](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L75).
The [Design handler returns immediately for an unchanged value](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L450).
Current and pinned source downloads match the stored reference byte for byte.
This establishes the no-op caller wait without resolving generic threaded
submenu scheduling.

## Ownership and evidence

The former path retained a Design transition even for a no-op. That transition
prevented publication of the held-Use receipt. The replacement uses one existing
callback clock, rather than adding a second timer or a new state representation.
Normal expiry permits the next invocation. Navigation, actor replacement,
focus loss and shutdown withdraw the receipt. Clock failure and callback epoch
exhaustion reject admission before a timer is committed; clock rollback starts
one finite plain wait. A no-op does not require a redraw epoch.

Before implementation, the managed runner passed 962 checks and failed four
new regression checks. The old-controller native graph passed four of six
checks: only the same-design packet and held-Use checks failed. The candidate
passes all 966 managed checks. Five new tests cover all
five designs, first and second invocations, cancellation, rollback and admission
failures. The expanded actual-controller packet graph checks the production
readers and native router using successful command-write captures, including
rejection when a write was not captured. Native graph results are recorded in
the bound evidence for the checkpoint.

These checks establish offline controller and packet behavior. They do not
establish physical input, injection lifetime, retail Godmode restoration,
teammate authority, private ammo, all-map or Super EE readiness. The installed
menu and saved playable backups remain separate from this source checkpoint.
