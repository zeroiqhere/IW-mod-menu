# Literal palette presentation

The source assigns MainTheme directly to Apparition and Basic titles, selected colored row text and confirmed ON box interiors. The previous painter replaced six dark static palettes with white text and added a contrasting ON tick. This wave removes those substitutions from source widgets. IW connection and error messages retain their independent readable color; UNKNOWN remains an explicit diagnostic state.

## Source contracts

Official Apparition commit `307e94c1b1d6537ba7a2a71b32a22d563d3ea25c` remains the reference. `Menu/utilities.gsc` lines 558–566 defines 36 paired palette names and RGB values. `Menu/designHud.gsc` lines 35 and 96 gives Apparition and Basic titles MainTheme. `Menu/base.gsc` lines 421 and 432–453 sets row text, box backing and interior colors without a luminance replacement or check mark.

| Existing source object | Creation or settled row color |
| --- | --- |
| Apparition or Basic title | Chosen palette RGB |
| Classic, Native or AIO title | Source white with its existing alpha |
| Selected ordinary row with Colored Cursor | Chosen palette RGB; Classic and Native creation stays white |
| Unselected row or Colored Cursor disabled | Source white |
| True BOOL label in Text Color mode | Source green |
| ON box interior | Chosen palette RGB |
| OFF box interior | Black |
| Box backing | Source gray |

Blue, Black, Deep Blue, Midnight Blue, Purple and Indigo were affected by the old luminance threshold. All 36 palette colors remain paired with their original labels and copied source BOOL facts. For Black, the source ON interior and OFF interior have identical RGB; the source Text and Text Color display choices remain available. No extra tick changes those source pixels.

The direct Rainbow callback has no design exemption for selected row color, but the supported source preferences and production schema reject Colored Cursor under Classic and Native. This wave preserves those restrictions rather than exporting an impossible accepted combination. Native sampled Rainbow timing and phase remain explicit adaptations.

## Controller and renderer evidence

The new exporter uses actual connected inert controller navigation and accepted palette actions. Its independent pinned palette oracle does not derive expected values from the production MenuThemes table. It includes all palette selections in all five designs, real cached and settled callback boundaries, page creation and supported BOOL display/cursor variants. Exported profiles do not dispatch gameplay actions.

Production CPU primitive and pixel regressions check source titles, labels, right values, two-rectangle BOOL boxes, green Text Color labels and separately readable diagnostics. The native preview reader consumes actual producer packets for a separate aggregate comparison. Profile members and platform repeats do not count as extra logical test cases.

The source and report manifests in the matching offline evidence directory identify the final accepted inputs and test results. CPU previews use the same panel code as the DX11 renderer; they are not live game screenshots.

## Remaining fidelity work

Removing the palette substitution does not bind the broader theme callback planner or implement historical per-widget fades. The source requests 500 and 1000 ms fades for different widget families; numerical engine easing, exact callback scheduling, unsampled Rainbow history, font metrics and physical screen-origin mapping remain unproved. In particular, source-fixed callbacks over existing Classic/Native rows must not be confused with their later white row recreation.

The complete objective also retains personal ammo binding, remaining gameplay callbacks, injected input/graphics acceptance, normal Steam co-op, every IW map and the requested super Easter egg. This candidate is offline source work. The installed disabled package and saved playable backup remain unchanged.
