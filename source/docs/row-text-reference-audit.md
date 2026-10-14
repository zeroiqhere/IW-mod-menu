# Row label and value separation

Root review of the sealed Stealth previews prompted a measurement check. The
default 260-width No Target While In Menu row fits: at 960×720 its label advance
is 246.675 pixels, value advance 117.975, and available label width 254.025.
That packet does not reproduce overflow. The initial test assumed it did; its
setup failure is preserved and excluded as bug evidence. The earlier visual
follow-up description must not be treated as a measured default-profile defect.

The production row renderer limits reserved status width to half the body, then
places the full right-aligned value using its actual measured width. Those bounds
can disagree for a wider legal status. Narrower menus and larger selected text
also need direct checks for clipped labels. Explicit owned presentation variants
keep those cases separate from the original, unchanged controller packets.

## Reference and intended scope

The pinned Apparition `base.gsc:411–451` uses separate HUD elements for full
option labels and values at source anchors. It does not implement label ellipsis.
The injected menu uses a substitute font and adds IW capability values such as
UNAVAILABLE. Reserving actual value ink before fitting the displayed label is
therefore an IW readability adaptation, not a newly claimed source behavior.

The isolated wave keeps source inventory, stored labels/values, font size,
appearance flags, action identity and controller packets unchanged. A separate
row-layout helper reserves the measured value or existing box bounds plus the
source spacing. A separate bounded UTF-8 fitting helper adds an ellipsis only
when a label overflows. Fitting labels retain their original pixels. Glyphs must
be warmed before drawing so this change cannot leave triangles pointing at an
outdated atlas. No gameplay writer, runtime schema or hook lifecycle changes
belong to this work.

A value wider than the entire body cannot fit at an unchanged font size. Its
data remains intact and drawing stays clipped; the wave must not hide that limit
by silently truncating stored values or reducing the font.

## Regression evidence

The actual Stealth ON and Counter ON packets from the sealed 158-profile export
remain passing baselines. Clearly labeled owned variants exercise width 200,
selected Large Cursor and wider allowed scalar values. Nine new CPU cases check
complete fitting status ink, separate label/value bounds, a drawn UTF-8 ellipsis,
all five Designs, widths 200/260/500, moving/fading rows, unchanged fitting rows,
bounded offscreen drawing and font-atlas updates. Seventeen pure cases cover
exact fit, tiny width, both box locations, invalid geometry and oversized values.

A genuine preproduction run reproduces label overflow. An early value oracle
drew behind the panel and is excluded. The corrected foreground oracle was then
replayed against an owned copy of the unchanged sealed row source: it reproduces
missing value ink and label overflow, but is not a chronological pre-fix run.
The corrected oracle excludes the capability strike band, whose draw order
otherwise differs. Both invalid setups and the corrected replay provenance are
retained separately.

Root independently passes 64 geometry, 145 production CPU and 17 pure row checks.
The original glyph-loading pass remains before fitted-prefix measurement; new
ellipsis glyphs are warmed before geometry. The six owned before/after images
show complete fitting status text and visible label ellipses. They are additional
visual evidence, not producer packets or extra registered checks. A value wider
than the body remains clipped as described above.

The adjacent opening-controls core adds eleven pure managed cases, without UI or
input integration. Combined managed regression passes 334 cases. The new wave
adds 37 distinct cases to the preserved 1,120-check checkpoint. Installed files
remain unchanged and disabled. Retail crash acceptance and full parity remain
incomplete. Root joins all 158 fresh packets through Windows runtime parsing and
CPU rendering; every one of the 188 PPM and PNG artifacts is byte-identical to
the sealed Stealth checkpoint. The four preview-order cases, full 249-step native
build and strict Windows .NET8 publication pass. Four owned graphics modes pass
42/60/47/69 assertion executions separately. A first orchestration stop occurred
after the normal fixture passed because its process was briefly still listed;
bounded waiting resumed the remaining modes without overlap. This is a harness
handoff correction, not a reproduced rendering failure or a retail crash fix.

[Source option creation](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L411-L451)
is the primary reference. A fresh official file matches the pinned local copy.
