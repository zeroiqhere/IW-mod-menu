# Caller metadata during Counter and Stealth rebuilds

Successful Option Counter changes and Stealth UI with an active counter have a
source-dependent caller path. They cannot safely inherit the changed Design
fix or an unconditional 430 ms BOOL wait. No production deadline is changed by
this audit.

## Established source statements

Current upstream downloads of `Menu/base.gsc`, `Menu/menu_customization.gsc`,
`Menu/menu.gsc` and `Menu/designHud.gsc` match Apparition commit
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c` byte for byte.

The [monitor](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L75)
captures slider metadata before dispatch, but reads `OPT_BOOLOPT` after
`ExeFunction` starts the handler thread. The 180 ms wait and `RefreshMenu` run
only when that read and the current row check succeed. The following 200 ms
wait and outer 50 ms wait remain on the valid Use path.

[Option Counter and Stealth UI](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L661)
close and reopen the menu when they change counter construction. Closing clears
`menuUI` and `menuStructure`. Opening sets menu-open state, waits 50 ms, then
builds HUD and rows. It retains the current page and cursor. `drawText` calls
`RunMenuOptions`, which repopulates Menu Preferences including both BOOL rows.
The new counter HUD uses the updated counter flag. Stealth with an active
counter clears that flag before reconstruction.

`GetOption` returns undefined during the cleared-structure interval. Therefore
a 250 ms caller path is supported when the post-dispatch read sees no BOOL;
a 430 ms path is supported when it still or again sees BOOL metadata. The
callback's 50 ms open wait is independent. If the monitor takes the BOOL path,
it can refresh rows again at its 180 ms boundary.

## Unresolved scheduling and next proof

The source files do not establish the order of the sibling thread and the
monitor's post-dispatch metadata read. A nominal sum of waits does not prove
which branch runs. An unconditional 430 ms replacement is therefore unproved.
The current backend's separate 50/250 ms rebuild path also lacks a held-Use
receipt; that remains incomplete behavior, not a fidelity claim.

Before implementation, establish thread semantics from an authoritative VM
contract or later reference execution. A useful trace must distinguish cleared
metadata from metadata observed before callback execution, retain page/cursor,
record the rebuilt BOOL rows, and identify whether the 180 ms refresh occurs.
A synthetic scheduler that simply chooses one order cannot prove engine order.
No offsets or gameplay writes can be derived from this source audit.
