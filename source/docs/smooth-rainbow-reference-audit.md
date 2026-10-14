# Apparition Smooth Rainbow theme

The official main commit was checked with `curl` on 2026-10-08 and remains
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`. Fresh customization and utilities
files match the pinned source byte for byte. This wave adds the source's
thirty-seventh Main Design Color row after its thirty-six fixed colors.

## Menu and private state

Smooth Rainbow enables a theme mode; selecting it again while ON does nothing.
It is not an on/off toggle. Selecting any fixed color stops the mode and commits
that color. While active, all fixed-color indicators are OFF and only the
Smooth Rainbow indicator is ON. The static color catalog remains thirty-six
entries, and the mode preserves the stored fixed color as its fallback.
The row explains that choosing a fixed color stops the cycle.

The optional top-level `smoothRainbowTheme` field defaults false for legacy
packets. Supplied nulls, nonbooleans and duplicate decoded property names are
rejected. The existing required `accent` still needs a valid `#RRGGBB` value in
both modes. Owned immutable snapshots retain navigation, feature values and
appearance independently. Local focus/page guards apply before mode selection.
The controller adds no worker, clock, gameplay action or feature dispatch.

## Source cycle and drawing

The source level-wide producer begins at a random hue in [0,1), uses brightness
0.95 and six linear RGB sectors, and increments hue by 0.001 after each nominal
10 ms wait. This gives a nominal ten-second cycle. The menu's own 10 ms loop
samples that global color independently of navigation or menu visibility.

The native renderer samples an independent monotonic uptime phase. It takes the
remainder before conversion, uses the source's 10 ms steps and sector formulas,
and converts channels deterministically to eight-bit color. Brightness 0.95
quantizes to a maximum channel of 242. It does not accumulate frame deltas,
restart a cycle on selection or mutate the committed snapshot. Navigation,
closure, resize and enabling/disabling the mode cannot reset the sample clock.
The source's random level phase and scheduler are unavailable; phase alignment
and exact retail timing are not claimed.

For the Apparition design, title, separator, bottom line, ON boxes, colored
selected text and the instruction outline use the sampled color. The scroller,
menu background and instruction fill stay gray. Instruction text stays white,
OFF boxes stay black, and the Text Color boolean's ON label stays pure green.
The prior static-theme contrast substitution remains unchanged; rainbow-colored
text uses the source sample even in dark parts of the cycle.

The inherited contrasting ON checkmark remains an IW usability difference.
Pure clock and CPU panel checks establish mathematics and drawlist propagation.
They do not establish GPU transitions, engine easing, retail map support or
match-exit stability. The separate IW status panel remains a presentation
extension rather than a BO3 gameplay HUD. No unsupported HUD or feature writer
is added.

## Evidence and limits

Regression evidence includes failures against the earlier menu, runtime parser
and preview parser before their respective fixes. The actual controller exports
cover exclusive mode selection, repeated ON selection and fixed-color stop,
retaining the preceding twenty-six position and appearance profiles. The checkpoint passes **654 distinct checks**: controller 183, features 103,
session/history 24, Windows native runtime 75, preview reader 6, packaging 73,
scroll 9, choices 12, Quick Exit 15, width input 15, unlock input 7, position input
11, saved hints 9, animation 8, geometry 33, CPU panel 62 and source-color clock 9.
The 58 portable schema checks overlap Windows parser/export checks except for
six separate preview-reader cases. Twenty-nine producer snapshots are checked
within the existing joins. Full native cross-build and Windows backend
publication pass; the Windows fixture opens its own hidden test window and
private files, with no game process or graphics device. The final candidate's
`evidence.json` records builds and source hashes. Repeated exports are not counted as independent tests.

This is a source checkpoint, without installed deployment or live-game testing.
Full Apparition parity, private ammo, remaining gameplay families, all-map and
super-EE behavior, and the retail crash remain unfinished.

## Reference anchors

- [Theme rows and enable/stop handlers](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc)
- [Global RGB producer](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/utilities.gsc#L1234)
