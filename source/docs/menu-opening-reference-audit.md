# Changed-design opening fades

Fresh official files were fetched with `curl` on 2026-10-08. HEAD remains
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`. The monitor, row drawing,
customization, HUD helpers and player-recovery sources match the pinned local
reference byte for byte. This wave concerns local presentation after applying
a different Design. It adds no game memory locations, player writers or hooks.

## Source meaning and scope

`drawText(true)` creates every displayed row at once. All row HUD fades start
together; visible ordinal `i` determines duration `i × 100 ms`, rather than a
delay before starting the row. Ordinal zero is immediate. Other rows approach
alpha 1, or alpha .4 when Spotlight Cursor is enabled and the row is unselected.
The ordinal is relative to the initial visible window, not the absolute option
index. At source capacity 14, the last duration is 1300 ms.

The same fade applies to labels, submenu markers, choice/numeric text, both
checkbox layers and ON/OFF text. Text Color changes RGB without changing this
alpha. An OFF checkbox keeps its black interior. Title, panel, scroller, counter
and instructions are outside the row fade. The source's blank/spacer marker is
not the IW renderer's unavailable-feature strike; these have different meanings.

Changing Design calls `openMenu1()` after destroying the old ordinary HUD. The
50 ms rebuild and the nominal 250 ms input cycle remain separate from row fades.
Opening animation does not lock controls. A row can still be fading when the
player scrolls. The source then replaces current-row alpha fades, starting from
the alpha currently displayed. A correct local mapping blends captured alpha
linearly to the new target, instead of multiplying opening visibility by a
second spotlight interpolation.

This checkpoint intentionally arms only changed-Design opens. Normal initial
opening also calls this source routine, but its synchronous 50 ms call and
500 ms monitor wait need a separate state/input change. Option Counter's threaded
handler has two possible monitor paths: a retained boolean can trigger an
180 ms refresh and nominal 430 ms cycle; a cleared menu structure can skip that
refresh and retain the 250 ms cycle. Source text alone does not settle GSC thread
ordering. Quick Menu, Stealth UI and selected-player recovery are other separate
callers. Ordinary page/parent refresh and modal return use nonanimated drawing.
This wave must not silently assign them the changed-Design fade.

## State and clock contract

The optional `menuOpening` object contains exactly a positive unsigned `revision`
and integer `elapsedMilliseconds`. It is valid only for open ordinary rows with
at least two displayed options. The elapsed value is nonnegative and strictly
below `(shown − 1) × 100`; shown comes from actual row count and normalized
appearance capacity, bounded to 8–14. It may accompany Design cooldown, never
Design rebuild, preference refresh, editors, credits or Soft Unlock. Legacy
packets without it retain the existing fully displayed rows.

The owner admits a new opening revision before committing a changed Design.
The existing transition clock sample also arms opening. The first observed
redraw starts elapsed zero. A late observation at 250 ms therefore starts a
fresh fade while controls are already available; it does not pretend a native
frame was drawn at an unobserved nominal 50 ms boundary. This is a documented
owner-observation mapping, not proof of BO3 or retail scheduler timing.

Accepted cursor movement cancels the owner opening event without changing the
menu revision, allowing the renderer to capture current opacity for scrolling.
Page/modal/close/shutdown and real structural redraw cancel it. Draft movement,
same-Design selection and value updates that do not redraw retain it. Focus projection
hides the metadata while owner time continues. Publication observes additions
and removals separately from the normal 100 ms heartbeat.

Before redraw, a clock fault restarts the armed Design rebuild coherently. After
redraw, recovery keeps the applied design and single presentation invalidation;
the fade safely completes rather than reverting a committed state. Native time
uses an independent bounded presentation clock. It cannot extrapolate more than
100 ms beyond the latest owner report or beyond the final active millisecond.
Only the owner removes a completed event. Missing
legacy identities use exact reported timing. No controller/native clock epoch
is assumed to be shared.

The renderer retains only bounded row identities and presentation weights, never
row text or game feature values. Explicit alpha is separate from selection/font
and visible-window weights. The IW unavailable marker has its own decoration
weight, fading toward its established .4 endpoint. This preserves its meaning
without multiplying two simultaneous fades or borrowing the source blank-spacer
rules. Supplied frames must match event, exact report, menu/focus revisions and
layout; invalid or stale samples use the current reported-state fallback.

## Proof and remaining limits

Root confirms **976 distinct checks**: backend 280, features 103,
retail-session/history 24, Windows native runtime including actual exports 117,
additional CPU preview reader 18, packaging 84, scroll input 9, choice input 12,
Quick Exit input 15, width input 15, unlock input 7, instruction-position input 11,
saved hints 13, width animation 8, geometry/text 48, production-panel CPU 115,
rainbow clock 9, scroll-animation input 12, scroll presentation 16, menu-position
input 12, credits input 10, credits timeline 8, credits clock 9, opening clock 9
and opening opacity/scroll takeover 12. Repeated runners and assertions within
each grouped case are not counted again.

The portable schema runner passes 119 checks with all 110 actual controller
exports. Its shared parser/input/export cases overlap Windows; only its eighteen
preview-reader cases are additional. Root producer exports are byte identical to
the independent managed exports. Full MinGW build passes all 230 steps, and the
actual Windows backend publishes successfully. The Windows runtime fixture owns
a hidden test window and private files; it reads no game process and creates no
graphics device.

Saved controller, export, schema, clock, opacity and panel RED logs precede their
passing implementations. Tests cover source capacity and visible ordinals,
49/50/249/250 ms boundaries, late redraw, draft/same-Design retention, ordinary
input during fades, scroll interruption and a missing intermediate unlock frame,
cancellation, focus return, clock rollback/overflow, revision admission and stale
draw frames. GUI assertions cover text and both checkbox layers, black OFF fill,
green Text Color, ON/OFF text, choices/numeric/submenu values and the independent
unavailable marker. Default legacy frames retain their established pixels.

All 110 producer packets join production parsing and CPU rendering. Root produces
137 previews and visually reviews six opening/progress/unlocked/final/scroll
and reduced-capacity images. These are offline CPU previews, not retail frames.
Default main, reference main and reference status pixels remain byte identical to
the preceding checkpoint. Warm font textures remain bounded through repeated
opening and interrupted scrolling. Thirty-five gameplay/session/loader/hook
sources, the renderer entry point and input router remain byte identical.

Linear native alpha is an explicit mapping. The engine's `FadeOverTime` easing,
concurrent color-tween interaction and rendered retail timing remain unproved.
Source callback color history and the brief HUD-absent adaptive footer placement
also remain unfinished. No isolated parser or CPU image establishes retail
match-exit acceptance, all-map behavior or full Apparition parity. The installed
launcher remains disabled and unchanged.

## Reference anchors

- [Opening and drawing](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L200)
- [Concurrent visible-ordinal fades](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L322)
- [Row alpha and checkbox creation](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L414)
- [Scrolling replaces alpha fades](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L459)
- [HUD fade helper](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/utilities.gsc#L421)
- [Design handler](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L450)
