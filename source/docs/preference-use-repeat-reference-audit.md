# Preference held-Use continuation

Quick Exit, Spotlight Cursor, Colored Cursor, Large Cursor and Stealth UI now
use the existing Boolean callback clock when a connected typed menu owner
selects them. Their original Select receipt survives the scheduled redraw and
normal expiry, so held Use can invoke the same row again after 430 ms. The
actorless compatibility path keeps its existing preference-refresh contract.

The source is Apparition commit
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`:
[BOOL monitor](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L75)
and [preference declarations and callbacks](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L57).
These callbacks leave the row structure unchanged. The monitor waits 180 ms
before refreshing, then 200 ms plus its 50 ms outer cycle before reading input
again. This establishes their caller cadence without deciding the separate
threaded-submenu scheduling question.

## Presentation and ownership

One callback clock owns the wait and its command receipt. A callback-owned
previous appearance retains the source HUD's dependent fields until redraw;
the typed and legacy paths share one projection. Each toggle retains its
previous checkbox value through 179 ms, including the second invocation.
The committed Quick Exit policy is published immediately for native knife
input. Stealth retains its related Boolean Display and Option Counter fields
together, so pending frames remain valid.

Normal redraw, expiry and cancellation retire the retained appearance.
Changed actor, page, topology, focus and shutdown cannot transfer a ready
receipt. Clock rollback restarts only the remaining presentation stage.
Stealth with an active Option Counter still uses its distinct HUD rebuild;
that path receives no generic callback receipt.

Preferences do not produce the separate Boolean indicator cache. Their
callback reserves its one actual menu redraw epoch without spending an
unrelated BooleanPaint epoch. A saturated paint counter therefore does not
block a preference, one remaining menu epoch permits the redraw, and an
exhausted menu or callback epoch rejects the mutation before commitment.

## Offline evidence and remaining scope

Before implementation, the full managed runner had 910 passes and eight
expected failures out of 918; the actual native preference graph passed one
of three checks. The final managed runner passes all 918 checks and all 39
native suites pass. Eleven new controller checks cover timing, two toggles,
cancellation, rollback, transactional failures, compatibility and rebuild
separation. Two exporter checks cover fresh atomic exports and overwrite
rejection.

Five independent actual controller visits produce 45 serialized state packets
and a manifest with ten actual Select captures. Both production readers and
the current-level native router consume those unchanged packets. Held Use
repeats at 430 and 860 ms only after its own captured write; a native command
without a captured write cannot borrow the same ready receipt. No process
memory writer is used in these graphs. Windows artifacts are compile-only.

The declaration audit also establishes that all represented selectable
nonspacer source rows have a defined callback. Omitted callbacks default to a
defined `EmptyFunction`; availability and `Action.None` cannot establish a
missing function. No stable represented row needs an invented no-function
Select branch. Generic submenu timing remains unresolved because the source
rereads BOOL after starting a child thread.

This source checkpoint is not installed or ready for gameplay testing. Full
source callback coverage, resident game-lifetime settings, retail player and
cohort authority, physical drawing/input/injection lifetime, and normal Steam
co-op across all maps, quests and the Super EE remain incomplete. The saved
playable backup and installed products remain unchanged.
