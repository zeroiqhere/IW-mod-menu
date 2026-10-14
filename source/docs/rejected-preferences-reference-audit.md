# Rejected preference callbacks

Option Counter and Colored Cursor remain selectable source BOOL rows when
their current configuration makes the handler reject the request. The menu
reports the source reason, refreshes the unchanged rows at 180 ms and ends the
caller wait at 430 ms. The rejection never changes appearance, writes saved
settings or dispatches a game operation.

## Primary source

Fresh Apparition main still resolves to
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`.
[Menu Preferences](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L57)
declares both settings with `addOptBool`, which retains a defined handler and
Boolean option type. The
[Colored Cursor handler](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L646)
rejects Classic and Native. The
[Option Counter handler](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L661)
rejects Stealth first, then AIO and Basic. Each early return precedes mutation,
saving and menu reconstruction.

These rejected handlers leave the menu structure present, so the
[source monitor](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L75)
takes its Boolean branch: wait 180 ms, refresh, wait 200 ms and complete the
50 ms outer cycle. This path does not require resolving the separate threaded
close/reopen scheduling question.

## Controller and presentation

Both rows now use the existing selectable preference factory. The obsolete
design-disabled factory has no remaining consumer. `MenuAppearance.TryChange`
and `IsSupported` retain their incompatibility checks.

The rejection handler runs only after fresh selected-row and focus admission.
It reserves the existing callback epoch, redraw epoch and clock before changing
feedback. Typed connected callers retain the same captured Select receipt
through redraw and normal expiry; the next invocation requires its own receipt.
Legacy actorless callers keep the existing preference-refresh clock and never
publish a typed continuation. No new protocol field or gameplay owner is added.

The successful Counter rebuild and Stealth with an active Counter keep their
separate existing reconstruction path. This change does not establish a source
deadline for that threaded close/reopen combination.

## Evidence limits

The development evidence preserves the original managed and joined native RED,
plus the later actorless regression found during independent review. Actual
controller exports use an inert session with no process memory writer. Fixed
older packets remain compatibility inputs; current selectable-row behavior is
checked against fresh controller output.

This source candidate is not installed or ready for gameplay testing. Per-row
palette fades, game-lifetime restart ownership, remaining retail gameplay
bindings, physical rendering/input/injection lifetime and normal Steam co-op
across all IW maps, quests and the Super EE remain incomplete. The saved
playable backup and installed disabled package are unchanged.
