# Normal menu opening

Official `Menu/base.gsc` and `main.gsc` were fetched with `curl` on 2026-10-08.
HEAD remains `307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`; both files match the
pinned reference byte for byte. This wave implements ordinary opening and
reopening of a retained menu. It adds no gameplay memory locations or writers.
Root confirms the independent source, protocol and rendering regressions below.

## Source behavior and observation boundary

The monitor calls `openMenu1()` synchronously. That routine marks the menu open,
waits 50 ms, restores the retained page and parent path, creates its HUD and
draws animated rows. The caller then waits 500 ms, followed by the monitor's
50 ms outer wait. Healthy observation therefore creates the HUD at 50 ms and
permits the next menu command at 600 ms.

This implementation starts the remaining 550 ms at the first actual owner
snapshot that creates the HUD, called R. A first observation at 1000 ms has
controls ready at 1550 ms. It does not claim that an unseen frame was drawn at
50 ms. This sequential owner-observation mapping is explicit; it does not prove
GSC or retail scheduling. Row fades also start at R and remain independent of
the input wait. With eight visible rows, the final fade finishes at R + 700 ms,
after controls become available. Rows start together, using the visible ordinal
for duration and the existing Spotlight Cursor target.

Opening retains the current page, cursor and parent path. Inputs received during
the wait are acknowledged and discarded rather than replayed after it. Explicit
owner shutdown remains available. Reopening after failed shutdown is an
administrative recovery path and keeps its existing immediate behavior.

## Fresh rows and commit order

Admission rechecks focus, reads the clock and reserves a checked opening event
revision before changing navigation. A zero- or one-row menu also reserves the
revision because its roster may grow before redraw. Such menus have the opening
control wait but no row-fade descriptor.

A cheap publication-phase query reports redraw readiness without reading the
session or committing the redraw. The actual snapshot builds fresh rows and
validates its clock before committing row structure and cursor clamping. Only
then does it bind the reserved event to the resulting structure, start fades
from those actual rows and begin the remaining 550 ms. Redraw adds no extra
menu revision beyond an actual structural row change. Clock or counter failure
must leave this transaction uncommitted.

After redraw, a structural change cancels the fade without restarting the input
wait. Ordinary value/capability updates retain it. Focus projection hides the
transition and fade while owner time continues. Clock rollback before redraw
restarts rebuild; rollback after redraw keeps the HUD visible, cancels its fade
and restarts a finite 550 ms cooldown.

## State and presentation contract

`menuOpenTransition` is an optional strict object containing only `phase`, whose
value is `rebuild` or `cooldown`. Missing/null packets keep legacy behavior. It
applies to ordinary root or retained child menus, including empty rows with
selection zero. Editors, credits, unlock, preference refresh and Design
transition are mutually exclusive. Rebuild excludes `menuOpening`; cooldown
permits a valid row fade. Both phases use the existing locked input mode.

Rebuild hides menu chrome and rows while retaining help and IW status. Adaptive
help uses the menu X and Design offset, and its width is at least the committed
menu width plus the Design offset. Without a HUD, help Y is the saved instruction
Y minus the actual help height. Cooldown places it below the created body. A
typed HUD-presence field distinguishes this from bypassing adaptive help.
Paused wording explains that the menu is opening; normal saved control hints
return when controls become available, even if rows are still fading. This
wording is an IW usability adaptation to the reference's ordinary legend.

## Proof and limits

Dedicated failing-before/passing-after tests cover admission, fresh roster
changes, retained navigation, input consumption, publication, clock recovery,
strict packets and rendering. Existing fixtures use a deterministic test-only
opening helper: observe the real redraw after 50 ms, then wait the larger of
550 ms or the actual visible fade duration. Timing regressions use raw inputs
and explicit observations rather than that settled helper.

Root confirms **1018 distinct checks**, forty-two more than the preceding checkpoint:
twenty managed, seven shared native, two preview-reader, five geometry and eight
CPU panel cases. Current totals are backend 300, features 103, session/history 24,
Windows runtime including actual exports 124, additional preview reader 20,
packaging 84, geometry/text 53 and production-panel CPU 123. All preceding input,
color, animation, credits and opening-helper suites also pass. Repeated runners,
individual assertions and exported files are not added as separate cases.

All 128 root controller exports match independent producer exports byte for byte.
The portable parser passes 128 checks with those exports; its shared cases overlap
Windows, so only twenty preview-reader cases add to the distinct total. The full
232-step native cross-build and Windows backend publication pass. After exact
producer epoch assertions were added, root rebuilt both native test outputs and
reran their joins. The Windows fixture uses an owned hidden window and private
files; it opens no game process and creates no graphics device.

Saved RED transcripts include genuine managed admission/input/fresh-row failures,
a structural-counter clock-commit regression, export validation, strict native
schema rejection and missing-HUD footer/panel failures. The fix stages the redraw
sample until fresh-row commit succeeds. The real Players builder has a permanent
Refresh row: its tested changing roster is one to three to one rows. Empty
Messages and one-row Players admissions have no fade. No artificial gameplay
builder was introduced to claim a zero-to-two roster transition.

Tests cover retained cursor clamping, changed labels/action identities, value and
capability retention, failed publication retry, focus projection, clock rollback,
overflow and event/navigation admission. Strict packets and held/pending input
are joined to the existing locked route. Rendering covers all five Designs,
adaptive/custom/extreme anchors, hidden/alternate/plain help, independent status,
stale layout/event/focus frames and captured scroll takeover at unlock.

Root renders 157 CPU previews and reviews six rebuild, drawn, input-ready,
retained-child, absent-HUD adaptive and explicitly labelled local-clock images.
Default main, reference-main and reference-status pixels remain byte identical
to the preceding checkpoint. Thirty-five gameplay/session/loader/hook sources,
the renderer entry point and input router remain byte identical. The preceding
976-check checkpoint and installed disabled launcher are preserved.

Engine fonts/easing, callback color history, other opening callers, retail
match-exit acceptance and full gameplay parity remain unfinished. Isolated
snapshots and CPU pixels cannot establish retail or all-map behavior.

## Reference anchors

- [Synchronous opening call and caller wait](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L32)
- [Outer monitor wait](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L168)
- [Retained opening state and HUD creation](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L200)
- [Adaptive help with an absent HUD](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/main.gsc#L544)
