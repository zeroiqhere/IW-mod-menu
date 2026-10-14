# Menu Instructions BOOL callbacks

The official Apparition main commit was fetched with curl on October 9, 2026.
It remains `307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`; current and pinned
`Menu/base.gsc`, `Menu/menu_customization.gsc`, `main.gsc` and
`Menu/utilities.gsc` match byte-for-byte.

## Source facts and implementation

[Menu Instructions](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc)
declares Disable, Alternate Style, Disable Background and Adaptive Position as
unconditional BOOL options at lines 40–43. In
[the monitor](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc),
all four therefore receive the 180 ms checked-row redraw, followed by 200 ms
caller and 50 ms outer waits. An unchanged or rejected handler still consumes
the nominal 430 ms cycle.

Disable changes the actual visibility flag and calls `RefreshMenu` inside its
handler (customization 511–518). It redraws checked options immediately and
again at 180 ms. Alternate Style, Disable Background and Adaptive Position
change actual settings without an immediate options redraw. Their checked
indicators retain the last drawn sample until 180 ms. Disable labels continue
to represent the inverse of stored visibility/background fields.

Alternate clears Adaptive Position on either toggle and resets the saved anchor
to the new style's default (521–529). Adaptive remains selectable under Alternate;
its handler reports the conflicting setting and returns without mutation
(538–545). The controller now follows that rejection path instead of disabling
the row. Its error remains visible while the ordinary BOOL wait completes.

Actual appearance fields, copied checked values and action permission stay
separate. Only a source-admitted Instructions visit publishes the four-row paint
sample. The default cache scope still excludes these local controls. Palette
paint remains a separate opt-in. Full row topology, actor, family, page and menu
epochs own the cache; focus hides public descriptors without transferring them
to another actor. Legacy previews retain their preceding immediate local timing.

Disable reserves two future paint epochs and three menu epochs before changing
visibility. This covers its immediate redraw, scheduled redraw and potential
structural commit. Failed clock or epoch admission leaves flags, paint and wait
unchanged. The other three handlers suppress only their immediate checked-row
redraw; actual settings still commit before the caller wait.

## Reasoning and evidence

The source has two independent timelines: its option monitor owns checked-row
samples and input waits, while its instruction display thread observes current
settings. Using one delayed appearance object would hide confirmed settings;
using only live checkbox values would erase the source's copied HUD lifetime.
The controller therefore retains current appearance and a separate owned paint
sample, then invalidates that sample only on an actual redraw.

Compiled behavior tests failed before implementation. Eight controller cases
cover all four controls, both redraw clocks, clearing two indicators in one
Alternate callback, rejected Adaptive, two-toggle restoration, focus/actor
retirement and clock/paint/menu failure admission. Seven cache cases cover exact
scope, inversion, immutable copies, full topology and invalid values. Two export
cases preserve the actual sixteen-packet graph and prevent overwriting an
existing output directory. Two earlier managed tests and one native preview
aggregate were corrected from disabled-row expectations to the source's
selectable rejection behavior. The native aggregate's original failure is
preserved separately from its passing rerun.

All 685 backend and 107 session regressions pass on the host and Windows .NET 8.
The isolated Windows proof uses the exact current projects: 300 backend compile
inputs and three content inputs, 148 session compile inputs, 379 distinct
source/content/project files total. Before/after hashes match. Runtime is
.NET 8.0.31, Windows 10.0.19045, X64. Production backend publication passes with
warnings treated as errors.

Sixteen unmodified connected controller packets cover default state, Disable
0/179/180/430 ms, the other three controls at 0/180 ms, Adaptive rejection at
0/180/430 ms and Alternate clearing Adaptive at 0/180 ms. Sessions are inert and
perform no gameplay dispatch. The prior 233-preview graph changes only its
Alternate instruction row's selectable state/detail; the separate 14 callback
and eight gameplay-paint graphs retain their bytes. The prior twelve local-option
packets change only Reset's two packets, adding the four current instruction
paint values without changing its plain 250 ms wait.

The full MinGW build completes 319 steps, followed by a two-step rebuild of the
corrected native preview expectation. Windows passes all 191 native runtime
cases, including the 233-preview aggregate. Separate 14 callback, eight paint,
twelve local-option and sixteen instruction graphs each pass one aggregate case.
Portable schema passes 208 cases with the actual previews; all 16 configured
portable CTest executables pass. CPU rendering passes 159 cases with both local
graph options enabled and renders all sixteen actual instruction packets.
Geometry passes 64 cases. This wave adds exactly two native/CPU case registrations;
profiles, clock samples, key loops and individual assertions are not extra cases.

The independent pixel oracle supplies fixed checked-row masks while preserving
actual visibility, style, background, Adaptive and accent fields. It catches
discarded checkbox samples. Current flags, copied values and rejected-setting
feedback agree across both readers. All 145 native production source files are
unchanged. Windows console fixture entry wrappers reuse the full build's exact
test/parser/router objects and write explicit owned pass reports; they add no
product behavior or test cases.

## Remaining proof

The source instruction display thread polls at a nominal 10 ms interval.
The controller exposes confirmed settings immediately and CPU drawing uses them;
this does not prove exact GSC thread/HUD interleaving or a 10 ms engine redraw.
Source theme fade history, submenu thread scheduling, engine fonts/origins and
final injected visual/physical input remain unfinished. Private ammo binding,
remaining gameplay callbacks, co-op, all maps and the super Easter egg remain
unfinished or unproven. The full 1:1 goal stays active.

This is offline review work. No game, injection, process-memory, GPU or Steam
API operation belongs to this wave. The installed disabled payload and the
separate playable backup remain unchanged.
