# Adaptive help during Design rebuild

Official `Menu/menu_customization.gsc`, `Menu/base.gsc` and `main.gsc` were fetched
with `curl` on 2026-10-08. The reference remains commit
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`. This wave concerns the local help
rectangle during changed-Design rebuild. Independent isolated proof is complete; no gameplay binding or writer is added.

## Two Design meanings in the brief gap

The handler commits `MenuDesign`, clears incompatible flags, destroys `menuUI`
and calls `openMenu1()`. The latter marks the menu open before its 50 ms wait.
Closing does not destroy the independent instruction HUD. Its loop calls
`SetInstructionsPosition` every 10 ms, which reads the committed new Design.
While the menu background is absent, adaptive X is menu X plus that Design's
offset, width is at least committed menu width plus its offset, and Y is the
saved instruction Y minus the actual help height.

| Target Design | X offset | Minimum-width offset |
| --- | ---: | ---: |
| Apparition / Native | +1 | −2 |
| Classic / Basic | 0 | 0 |
| AIO | −1 | +2 |

Nonadaptive and alternate help retain saved anchors. Hidden and plain help keep
their existing visibility and decoration rules. The body/title/chrome remain
absent during rebuild. The independent IW status remains bounded and visible.

Footer channels choose fill, alpha and text properties when created. The handler
requests instructions refresh only after `openMenu1()` returns. Existing channels
can therefore retain earlier paint during the gap, while placement uses the new
Design. Missing channels may instead be created from the new Design before
redraw. Rainbow callbacks also read current state. A two-Design descriptor cannot
prove arbitrary paint history or the exact refresh frame; this wave preserves
the current bounded paint model and corrects geometry only.

## State and legacy contract

`menuDesignTransition.footerDesign` is an optional exact Design name. A modern
producer supplies it only during changed-Design `rebuild`; it omits null values.
`appearance.design` remains the existing captured presentation Design. The
descriptor changes no input mode, clock read, publication phase or public epoch.
Rows and capability flags already reflect the committed normalized Design.
Classic/Native require Colored Cursor off; AIO/Basic require Option Counter off.

Missing/null metadata keeps legacy captured gap geometry without inventing a
committed target. Non-null metadata is rejected outside rebuild, including
cooldown even if the target equals the displayed Design. Its object accepts only
the required phase and optional footer Design; wrong names/types, extra fields,
decoded duplicates and incompatible contexts or target flags are rejected.

The native immutable snapshot appends an optional typed Design. Layout appends
an optional instruction-geometry Design and reuses typed HUD absence. Only known
rebuild descriptors activate this geometry; old packets retain their existing
fallback. The override affects adaptive X/minimum width, while absent-HUD Y uses
the saved anchor. The HUD's captured Design still drives existing paint. Initial
opening behavior and the normal Design cooldown remain unchanged.

## Isolated proof and remaining limits

Root independently confirms **1040 distinct registered checks**: backend 304,
feature backend 103, session/history 24, owned Windows runtime including the
actual producer join 128, additional preview reader 22, packaging 84, geometry
59 and production panel CPU 129. Retained independent input, hint and animation
suites account for the remaining 187 checks. The 130 actual producer profiles
are joined evidence, not 130 additional registered checks.

Full 234-step MinGW Release cross-build and Windows .NET8 publication pass.
All portable CTest suites pass. The owned hidden-window Windows runtime fixture
passes without a game process or graphics device. Independently produced 130
JSON profiles match the managed agent byte for byte. The preceding 128 profiles
change only eight explicit rebuild descriptors; row identities, epochs,
transition clocks and presentation fields remain unchanged.

TDD evidence preserves real failing managed, exporter, schema, layout and panel
transcripts before the fix. The final checks cover exact targets, normalization,
null/missing legacy behavior, unsupported types/fields/duplicates, draft/same
Design, focus and clock recovery. Layout covers all twenty-five captured/target
pairs, widths 200/260/500, saved/extreme anchors, alternate/plain/hidden help and
unchanged body/counter geometry. Real adaptive AIO producer states at 0 and
49 ms join the parser and production renderer.

Root reviewed the two adaptive rebuild PNGs, the AIO cooldown and an explicitly
labelled legacy descriptor-removal demo. Both rebuild frames have identical
pixels; their adaptive footer pixels also match the renderer agent. The normal
main/reference-main/reference-status pixels match the preceding checkpoint
exactly. Root retains 160 CPU PNGs. Whole-image equality between independently
ordered fixture runs is not claimed: a small number of sampled glyph pixels
vary while the targeted footer geometry and registered glyph-coverage checks
pass. That difference is recorded separately in the preview comparison.

A temporary disk quota interrupted early output capture. Empty/partial logs
are excluded from passing evidence. Root removed six reproducible older build
caches only, preserved all seventeen previous sealed checkpoints and reran the
affected suites using a private cache under the home directory. Complete recovery
logs are included in the new checkpoint.

All thirty-five protected feature/session/loader/hook files, the renderer entry
and native input router are byte-identical to the preceding checkpoint. The
installed package remains unchanged and disabled. These results replace the
preceding 1018-check checkpoint as current isolated proof without changing its
sealed artifacts.

Engine fonts/origin/easing, partial HUD creation, callback color history and
independent refresh scheduling remain unfinished. This local geometry correction
does not establish retail crash acceptance, all-map gameplay or full Apparition
parity. Installed files remain unchanged and the launcher remains disabled.

## Reference anchors

- [Design commit and deferred instructions refresh](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L450)
- [Open wait and menu-only destruction](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L200)
- [Independent instruction channel creation](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/main.gsc#L372)
- [Committed Design geometry and absent-HUD fallback](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/main.gsc#L506)
