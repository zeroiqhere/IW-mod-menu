# Apparition menu repositioning

The official main commit was checked with `curl` on 2026-10-08 and remains
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`. Fresh customization, HUD, main and
utility sources match the pinned reference. This checkpoint implements the
`Reposition Menu` handler, independently of Menu Instructions Reposition.
It changes local presentation and adds no gameplay writer or engine address.

## Reference behavior

The handler immediately calls `SoftLockMenu(122, true)`. Ordinary options,
scroller and counter disappear; the existing Menu Customization title remains.
The Apparition body is 122 units tall and its banner is 157. Seven body hints
describe action slots 1–4, frag, secondary offhand and melee. The single multiline
text has a source anchor at body Y + 22. `CorrectNL_BGHeight` estimates 101 for
seven lines, placing the separate offset anchor at body Y + 113; its 14-unit
height estimate does not prove engine text line spacing. Mapping source anchors
to native text centers with a 13-unit line advance is a font/anchor inference
rather than a measured BO3 font equivalence.

The movement increment starts at 1 on each entry and saturates within 1..10.
The source checks vertical movement before horizontal movement, with Down and
Right winning their respective conflicts. Secondary offhand decreases the
increment before frag increases it; melee exits only when higher-priority
controls are absent. Movement has a nominal 25 ms loop wait. Changing the
increment adds a 100 ms wait, giving 125 ms even at a bound.

Movement translates the menu HUD collection and updates `menuX`/`menuY`.
The ordinary instruction HUD is a separate collection and keeps its saved
anchor. Adaptive instructions instead follow the current menu's left + 1 and
body bottom + 18. Default menu coordinates are X 0, Y 85. The reference does
not clamp repositioning to the screen or provide a Reset Menu Position row.

Melee calls the same Soft Unlock used by Width Editor: close/rebuild, 50 ms
before opening HUDs, then 100 ms with controls paused. The IW controller models
elapsed 0..49 ms hidden menu, 50..149 ms visible parent with paused controls and
unlock from 150 ms. It retains the customization cursor and edited coordinates.
Unlike instruction repositioning, this handler has no separate 100 ms entry wait.

## Managed state and protocol

A nullable typed `menuPosition` holds signed 32-bit X/Y, with absence/null
resolving to the source defaults. Movement uses widened, range-checked arithmetic and refuses
overflow before changing state. Coordinates remain unclamped, including offscreen
positions. Reopening preserves the controller-local position; entering resets
only the movement increment. This lifetime does not claim source XUID/dvar
persistence across process restarts.

`menuPositionEditor` is separate modal metadata with `step` 1..10, empty ordinary
rows, selected index zero and an open non-root context. It cannot coexist with
another editor or unlock phase. Native saved hints are private presentation
data; backend JSON supplies only the validated increment. Old packets retain
the source defaults and advertise no missing editor capability.

Movement and finish commands require the captured navigation epoch, current
editor, exact binding capability and foreground context. They execute on the
existing controller owner and dispatch no game action. Stale or unsupported
commands cannot change another page or move a replacement editor.

## Saved input and drawing

The binding reader imports exact `+actionslot 1` through `+actionslot 4`,
`+frag`, `+smoke` and the existing melee binding. No frag/tactical keys are
guessed. Both offset groups and all four movement groups are required for
entry; partial or legacy bindings leave it unavailable with help. Normal aim,
fire and Use do not become editor movement commands.

One command remains in flight, and observed pending edges are consumed. Movement
and increment keys held through a captured wait can act at its deadline; releasing
them clears eligibility. Melee edges observed during the wait are consumed until
release. Held actions use captured waits without catchup bursts. Focus, context,
mode and clock rollback reset/reseed input. Fresh-key requirements, native
20 ms polling and acknowledgement delay remain differences from source held
loops. Inputs still reach ordinary gameplay, as in the existing passive menu.

The committed menu origin moves ordinary and editor panel geometry, title,
counter, scroller and rows. Changed origins invalidate row animation context.
The ordinary footer retains its own anchor; adaptive help follows the menu.
The editor's independent footer uses contextual paused-navigation and saved
Exit help, retaining ordinary instructions for resume. That footer wording is
an intentional IW usability difference from the reference's ordinary hints.

Source coordinates remain intact while drawing stays viewport-clipped. The IW
status panel searches bounded free space for moved menus/footers. If no readable
free region exists, a bounded viewport status fallback may overlap another HUD;
it does not change saved anchors. Current coordinates and increment remain
available in editor status, including when the menu is offscreen. No invented
reset row or silent coordinate clamp is introduced.

## Evidence and remaining work

Regression evidence covers managed arithmetic/modal lifecycle, strict parsing,
exact bindings/input priority, translated geometry, CPU drawlists and actual
controller exports. Final counts and source/artifact hashes are recorded in the
candidate's `evidence.json`. These are isolated checks rather than retail
gameplay, GPU-transition, all-map or super-EE acceptance.

Root reran 783 distinct checks: backend 216, features 103, session/history 24,
Windows runtime 89, CPU preview reader 10, packaging 84, scroll input 9, choice
input 12, Quick Exit input 15, Width Editor input 15, unlock input 7, instruction
position input 11, saved hints 11, width animation 8, geometry/text 41, production
panel CPU 79, rainbow clock 9, scroll timing input 12, scroll presentation 16 and
menu position input 12. The 83-check portable parser suite overlaps Windows
runtime except its 10 preview-reader checks; its actual-export join and seven
existing input cases are not counted twice. The 45 exports are fixtures used
inside those checks rather than 45 additional tests.

The fresh native cross-build completed 178 steps, and Windows .NET publication
passed. Windows runtime tests used an owned hidden window and private files,
without a game process or graphics device. Root reviewed editor, saved-control,
translated-parent and offscreen CPU PNGs. Sample saved-control images explicitly
identify sample mappings. Default main, reference-main and reference-status
pixels match the preceding checkpoint byte for byte.

Full Apparition parity, private ammo, remaining gameplay families and the prior
retail match-exit crash remain unfinished. The installed launcher and preceding
sealed checkpoints remain preserved.

## Reference anchors

- [Reposition handler](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L264)
- [Soft Lock and Soft Unlock](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L650)
- [Apparition HUD geometry](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/designHud.gsc#L27)
- [Adaptive instruction tracking](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/main.gsc#L544)
- [Multiline height calculation](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/utilities.gsc#L2361)
