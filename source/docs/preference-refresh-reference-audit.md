# Local boolean refresh timing

Fresh official monitor and customization sources were fetched with `curl` on
2026-10-08 and match pinned Apparition commit
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c` byte for byte. This wave concerns only
Quick Exit, Spotlight Cursor, Colored Cursor and Large Cursor in Menu Preferences.
It adds no game feature timing or writer.

## Source behavior and scope

The monitor dispatches the option handler on a thread. If the resulting selected
option still has boolean metadata, it waits 180 milliseconds, refreshes the
cached menu options, waits another 200 milliseconds and reaches the outer
50-millisecond monitor wait. The next input poll is nominally 430 milliseconds
after dispatch. This is a monitor wait rather than a hidden-panel Soft Unlock;
the footer and other workers continue running.

The four scoped handlers change their preference variables and save the theme.
They leave the existing option values and cursor HUDs untouched until the generic
redraw. `RefreshMenu(menu, cursor)` calls `drawText`, which destroys and recreates
options without the opening animation by default. It preserves the selected row.

Other implemented handlers take different paths. Instruction visibility refreshes
immediately; alternate style and background use the separate footer refresh;
adaptive placement is handled by that footer loop; Option Counter closes and
reopens the menu. Fixed colors and Smooth Rainbow update colors directly, even
though their cached indicators can later receive the generic redraw. Bool Display
and Box Location are sliders, and numeric scroll timing has its own variable
update. This wave does not impose the four-toggle schedule on those handlers or
claim their complete source timing parity.

Source redraw can also update another player's menu when its option references
the source actor. The local native panel does not reproduce that propagation.
Fresh-edge inputs and private IPC scheduling differ from the GSC held-button loop.

## Implemented state contract

The controller commits the four local preferences immediately. For the pending
interval it retains the old displayed checkbox and three cursor style flags.
At 180 milliseconds it presents the committed checkbox/style and invalidates
menu presentation once. Input remains paused through 429 milliseconds and resumes
from 430. The panel stays visible throughout. Quick Exit remains the committed
input preference, while the paused input route prevents it acting early.

Separate optional `menuPreferenceRefresh` metadata carries only a typed
`pending` or `cooldown` phase. It requires an open child menu with ordinary rows,
a valid selected index and no editor, credits or Soft Unlock metadata. It does
not fabricate a rebuild phase or a new game action. Contextual help describes
the brief input pause and retains normal hints for return.

Before redraw, clock rollback or elapsed overflow restarts the pending interval.
After the single redraw, clock faults retain the applied style and restart only
the 250-millisecond cooldown. They must not revert the checkbox or redraw twice.
The existing owner loop advances time even while focus hides the panel. Owner
shutdown cancels the visit; monitor inputs received during the wait are consumed.

## Regression and preview evidence

Root confirms **876 distinct checks**: backend 247, feature backends 103,
retail-session/history 24, Windows native runtime including the actual exports
100, additional CPU preview reader 14, packaging 84, scroll input 9, choice input
12, Quick Exit input 15, width-editor input 15, menu-unlock input 7,
instruction-position input 11, saved control hints 13, width animation 8,
geometry/text 42, production-panel CPU 96, rainbow color clock 9,
scroll-animation input 12, scroll presentation 16, menu-position input 12,
credits input 10, credits timeline 8 and credits presentation clock 9.

The portable schema runner passes 98 checks with the same 72 actual controller
exports. Its parser, export and seven existing input cases overlap Windows;
only the fourteen preview-reader cases are additional. Export joins and CTest
suite totals are not counted again. The native cross-build completes all 212
steps and the actual Windows backend publication passes. The Windows fixture
uses an owned hidden test window and private files without a game or graphics
device.

Regression tests cover each toggle at 0, 179, 180, 429 and 430 milliseconds,
consumed inputs, exactly one presentation invalidation, Quick Exit's committed
input policy, clock faults before and after redraw, late owner polls, shutdown,
strict phase validation, malformed combinations and router reseeding on resume.
A pre-refresh caller animation frame cannot override the declared paused
snapshot. Repeated refresh/resume rendering leaves the font atlas unchanged over
900 cycles. Root reviewed paused/resumed footer and checkbox/style PNGs; normal
main and reference main/status pixels remain byte identical to the preceding
credits checkpoint.

The refresh uses the existing owner publication loop, input lock and render
resources. No gameplay writer, retail session, loader or hook lifecycle changes
are included. These checks model nominal elapsed-time boundaries: a late owner
poll may skip a phase already elapsed, and do not prove exact GSC scheduling or
retail rendered-frame timing. Full Apparition parity, all-map acceptance and the
match-exit crash investigation remain incomplete. The installed launcher remains
disabled and its files are preserved.

## Reference anchors

- [Monitor timing and dispatch](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L80)
- [Option redraw](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L301)
- [Generic refresh](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L724)
- [Quick Exit and instruction visibility](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L505)
- [Cursor and counter handlers](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L640)
- [Independent footer loop](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/main.gsc#L372)
