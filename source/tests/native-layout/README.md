# Apparition production panel checks

These CPU tests draw the same `native/render/ApparitionPanel.cpp` used by the DX11 renderer. They compile ImGui core, honor its 1.92 texture creation/update requests, and rasterize its actual triangles and font atlas into PPM images. The PNG export preserves those pixels. No GPU, Windows process, game, hooks, input, or feature backend is started. These images are production-panel previews, not screenshots from Infinite Warfare or proof of retail hook safety.

Run from the repository root:

```sh
cmake -S tests/native-layout -B /tmp/iw-apparition-layout-tests -G Ninja
cmake --build /tmp/iw-apparition-layout-tests
ctest --test-dir /tmp/iw-apparition-layout-tests --output-on-failure
/tmp/iw-apparition-layout-tests/ApparitionPanelTests /tmp/iw-apparition-layout-preview
python tests/native-layout/export_preview.py /tmp/iw-apparition-layout-preview/reference-main.ppm /tmp/iw-apparition-layout-preview/reference-status.ppm
```

An optional second argument to `ApparitionPanelTests` names a directory of JSON fixtures exported from the actual backend menu builder. Each fixture, including its appearance object, is rendered alongside the synthetic reference-layout cases. This reader copies only display fields and executes no menu actions. `main-saved-controls.ppm` adds clearly identified sample bindings through the production `DescribeControls` formatter; it does not infer keys from a running game.

## Source formulas and limits

The design reference is [Apparition commit 307e94c](https://github.com/CF4x99/Apparition/tree/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c). Geometry was independently implemented from `Menu/designHud.gsc`, `Menu/base.gsc`, `Menu/menu_customization.gsc`, and `Menu/utilities.gsc`; no script code or assets were copied.

The default body uses source coordinates `(0,85)`, width 260, 18-unit rows, and up to ten visible options. The banner starts 20 units above it, with the rebuilt body height plus 35. Source gray layers are 55 opaque and 25 at half opacity. One-unit theme lines divide the title and bound the body. The default theme is RGB `(57,152,254)`. The title center is `(130,73)`. Text begins four units from the left, with option centers at `85+8+18*row`. Right-hand boolean boxes are 10 units square with an eight-unit inner fill. Centered scrolling follows the source's four rows above/five rows below rule. Instructions have their own gray 42 background and outlined bottom anchor at 472.

The reference sets `menuX=0` with a comment about being close to center. Its `SetPoint` helper sets text alignment but does not establish a documented engine screen origin. The uniform 640×480 conversion and centered aspect-ratio letterbox are therefore an explicit mapping inference; exact BO3 screen placement is unverified. BO3 font assets and metrics are unavailable. ImGui's bundled font substitutes for them, with proportional title/row sizing and a bounded instruction font fit. This is not a claim of pixel-identical engine text.

Verified native controls use bounded context-specific instruction lines. Unsupported gameplay slider controls are omitted. An adjacent, adaptive status box shows connection messages and errors without moving source option rows. Long notices retain complete UTF-8 characters, fit within the viewport, and show an explicit continuation marker. UNKNOWN has a visible question mark. Source title and row text now retain literal palette RGB, including dark colors. Confirmed BOOL boxes use only the source backing and inner fill; the earlier contrasting ON tick is removed. The independent status and UNKNOWN diagnostics remain IW extensions.

Supported appearance preferences follow `base.gsc` option formulas and `menu_customization.gsc` capacity rules. Visible capacity starts at ten, gains one when instructions are hidden, gains two for non-box boolean displays, gains another one when both conditions apply, and loses two when the counter is enabled. This yields capacities from eight to fourteen, with the same centered scrolling rule. Left boxes have center x=9 and boolean label x=18; non-boolean rows keep label x=4. Text mode shows right-aligned ON/OFF. Text Color uses pure green for ON labels and ordinary row color for OFF, without a confirmed-state value. UNKNOWN remains explicit in either mode. Spotlight affects unselected option text, values, and boxes at alpha 0.4; disabling it restores full opacity. Large Cursor scales selected label and non-box value text by 1.2, while boxes stay 10/8 units. The counter uses the final footer position at body bottom+8 and white alpha 0.7; substituted font metrics can move its group slightly left to keep totals within the body width.

## Regression evidence

The initial geometry stub failed the source HUD test before implementation. The early Black ON/OFF check led to an additional tick. The later literal palette wave removes that extension and replaces the old assertion with source color and rectangle regressions. Reviewing the real CPU output exposed a normal two-line message hidden by a continuation marker; its dedicated regression failed before the adaptive padding fix. Saved-key hints, preference capacity, and Text Color each failed their new checks before implementation. The preferences suite had 14 geometry/text checks and 22 production-panel CPU checks; the integrated choice wave extends the panel suite to 25 checks.

The CPU texture fixture follows the maintained DX11 backend's declared update rectangles and delays retiring textures until `UnusedFrames>0`. The panel prepares all required text glyphs before emitting geometry. Model-switch checks compare local 16-pixel blocks against a fresh context, allowing small atlas-packing and bilinear edge rounding differences. The same comparison explicitly rejects an erased message, so its tolerance cannot conceal missing words. Actual backend fixture exports undergo this check as well. This establishes CPU drawlist and atlas coverage, not GPU or retail compatibility.

Renderer hooks, DX11 state restoration, thread ownership, deferred swapchain release, and backend operations are unchanged by this panel work. Their separate native fixture evidence remains separate from this CPU suite. The unresolved retail transition crash is not claimed fixed.

## Integrated source choice controls

This choice wave is integrated into the main source, with no installed deployment or new GPU/game launch. It extends the CPU panel suite to 25 checks alongside the existing 14 geometry/text checks. Two new regressions first failed on an empty instruction footer and on overflow from long remapped choice keys. Empty local hints now skip the legend with a guarded divisor, retaining the applied menu row capacity. Three or four saved-control lines use the same source anchors, with the substituted font fitted to both height and width. At most six local lines are rendered, matching the layout bound; excessive vectors cannot increase drawing work.

Nine snapshots exported from the actual offline controller are rendered and compared with fresh-context glyph coverage. The draft fixture shows candidate Text while the applied appearance remains Boxes and navigation revision stays 55. The committed fixture applies Text and has revision 61 after its navigation sequence; Use commit alone does not advance the revision. Rendering uses only the applied appearance object, never a candidate value inferred from a row label. The preview reader retains the exact navigation revision so this assertion cannot pass with a discarded default zero. Renderer tests do not execute Use or mutate controller state; command and navigation semantics belong to the separate backend/runtime suites.

The optional export run also produces `main-four-controls.ppm` using clearly identified sample key remappings through the production control-label formatter. No running game's keys, process state, or memory are read.

## Prior Quick Exit evidence

The preserved Quick Exit candidate was not deployed. Its suite had 28 production-panel CPU checks and the existing 14 geometry/text checks. The eighth Quick Exit preference remains visible; its confirmed row value determines its ON indicator. Changing input metadata alone cannot fabricate that display state. Root and nested hint variants use the production saved-binding formatter, and an applied hidden-instructions setting stays hidden.

All eleven actual controller snapshots render through the same production panel and pass fresh-context glyph coverage comparisons. The new Quick Exit snapshots preserve their confirmed Text/Left appearance, Mint accent and hidden instructions. Separate `nested-quick-exit-hint-demo.ppm` and `root-quick-exit-hint-demo.ppm` files explicitly enable instructions for sample-binding demonstrations; their context and notice identify that change. No game bindings or process state are read.

## Prior Width Editor evidence

The preserved 392-check Width Editor candidate is not deployed. Source `Menu/menu_customization.gsc` 360–446 enters a 120-unit soft lock, removes ordinary title/options/scroller/counter, and adds five body instructions at left+4 and body+25. `base.gsc` 650–690 retains a 155-unit banner and places the lower line at body+119. Five source instruction lines use fourteen-unit spacing; `CorrectNL_BGHeight` places the green offset value 73 units below their origin. `WidthEditorPanel.cpp` implements this distinct body; the footer geometry and its visibility preference remain independent of the editor body.

The committed width defaults to 260 and is bounded from 200 to 500. Body, banner, lines, scroller, title center, right boxes and counter follow that width. Notices are an IW extension: wide menus put their status box below the banner if at least 48 units fit before the instruction footer; taller lists use a bounded adjacent box. Message measurement uses that actual width and retains explicit continuation. This prevents the previous fixed-position notice from covering a 500-unit menu.

The prior Width Editor candidate drew committed width immediately. The integrated timing wave now interpolates presentation width linearly over 50 milliseconds; exact source engine easing remains unproved. The earlier screen-origin mapping and substituted-font limitations still apply. Width work does not change DX11 hooks, device lifetime or gameplay state.

The suite has 17 geometry checks and 33 production-panel CPU checks. Dynamic widths and every capacity style remain bounded across tested aspect ratios. The dedicated editor is unaffected by hidden ordinary rows or cursor styles. Its offset comes only from editor metadata, and its body hints remain visible when the independent ordinary footer is hidden. Long remapped editor hints and missing local hints remain finite and clipped.

The prior optional actual-export run checked all fourteen controller snapshots. The two editor snapshots have empty ordinary rows, confirmed offsets 1/10 and widths 260/500; their parent snapshot retains width 500 after editor metadata is removed. Default main-menu pixels match the preceding Quick Exit CPU preview exactly. Actual editor body previews use generic action labels, while private saved-binding injection is covered separately by the native runtime. These images remain CPU output, not retail gameplay screenshots.

The source retains ordinary navigation instructions while soft-locked. The IW editor deliberately replaces that footer text with three context lines: Width Editor, Menu navigation paused, and the matching saved melee key for Exit editor. Its geometry and visibility preference remain independent, and the ordinary menu instructions remain stored unchanged for resume. A new CPU regression first failed because changing inactive Scroll/Select text changed the editor footer; it now verifies dedicated context help, remapped exit text, and bounded empty/oversized local vectors. This is an intentional usability difference from the source, because editor input ignores normal navigation.


## Integrated timing wave

The renderer agent confirms eight animation-helper, 19 geometry and 38 production
CPU panel checks. All sixteen actual previews pass fresh-context coverage and the renderer agent
confirms its Windows cross-build. Root confirms the integrated main native build, monotonic heartbeat regressions
and sixteen fresh main-export joins. The full source wave totals 441 checks. The optional actual
producer join now has sixteen snapshots, adding hidden rebuild and visible paused
parent states to the prior fourteen. These images remain CPU previews.

Presentation width is separate from committed width. The production renderer
samples a monotonic clock and applies a 50 ms linear transition only within the
same open editor context; retargeting starts from its currently displayed edge.
Context change, editor exit and reset snap to committed geometry. The explicit
CPU clock hook tests intermediate widths, while layout checks retain bounded
float geometry. BO3 engine easing, font metrics and screen origin remain unproved.

`menuUnlock=rebuild` hides only menuUI panel/title/options/counter; independent
footer/status retain their own visibility/style. `pause` displays the retained
parent with non-actionable paused help. Missing normal hints and hidden-footer
preferences remain bounded and honest. These IW help/status choices deliberately
differ from the source's separate ordinary footer text. Native hooks, device
lifetime and gameplay feature writers are unchanged.

The backend models elapsed 0..49 ms rebuild, 50..149 ms pause and unlock from
150 ms. Changed phase publication uses its 25 ms owner loop independently of the
monotonic 100 ms heartbeat, followed by native 20 ms polling and frame scheduling. CPU
fixtures prove the phase draw contract, not exact timing on a retail swapchain.


An isolated replay of the preceding panel failed the new unlock assertion with
“Rebuild retained a supposedly destroyed menu HUD”, while its new animation
checks passed. The current panel CTest targets pass. This is a deliberately
labelled previous-panel replay, not a claim that the production implementation
was rerun chronologically before its fix. Root reviewed the midpoint, rebuild
and pause CPU PNGs; no GPU/game launch was involved.

## Instruction position checkpoint

The current source includes 33 geometry and 56 CPU panel checks, with 26 actual controller snapshots. The position editor preserves the Menu Instructions title, uses body20/banner55, and hides ordinary options, scroller and counter. Signed anchors remain unclamped while emitted pixels and notices stay bounded. Entering retains adaptive placement; editing/exiting bypass it; reset and return restore normal placement. The default main/status CPU output is unchanged. These previews prove the production drawlist contract, not live GPU or retail acceptance. See [the source audit](../../docs/instruction-position-reference-audit.md).

## Stable actual-profile export order

The [preview-order integration suite](preview-order/README.md) checks complete
image equality across opposite fixture creation orders and relocated copies.
Its exact inventories preserve all 180 actual profiles and 210 output images.
The exporter retains the production panel and persistent atlas checks, while
sorting input filenames before rendering. Four integration cases add no game
or graphics-backend dependency.
