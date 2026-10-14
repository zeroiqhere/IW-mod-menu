# Source menu designs: state and geometry

Fresh official sources were fetched with `curl` on 2026-10-08. HEAD remains
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`; customization, HUD constructors,
monitor and instruction sources match the pinned reference byte for byte.
This checkpoint concerns local presentation and design selection. It adds no
IW gameplay fields, writer, injected hook or retail acceptance claim.

## Design choice and state contract

The source's first Menu Preferences row is a StringSlider with choices
Apparition, Classic, Native, AIO and Basic. A saved action-slot 3/4 changes the
draft and Use applies it. The source slider starts at index zero independently
of the applied theme value. The local draft follows that lifetime, wraps at the
bounds and survives page visits and menu close/reopen. Missing either saved
choice binding disables the row with useful help. Settings remain within the
controller lifetime; no process-restart storage is introduced.

A changed design immediately changes the underlying preference. Classic and
Native clear Colored Cursor; AIO and Basic clear Option Counter. Returning to
Apparition does not restore a value cleared by a previous design. Later attempts
to enable an incompatible flag must remain unavailable. The source itself rejects
them; the local row explains the restriction instead of silently ignoring Use.
A same-design application returns before these mutations and HUD rebuilds.

Changed-design selection destroys the ordinary HUD and `openMenu1` waits 50 ms
before creating its replacement. The independent monitor's Use wait is 200 ms
plus the outer 50 ms: a nominal 250 ms input cycle. It is not the modal return's
150 ms Soft Unlock. The local owner exposes `menuDesignTransition` with an
exclusive `rebuild` or `cooldown` phase. It retains the old declared design for
the independent footer before 50 ms, then publishes the new design and invalidates
presentation once. A same-design Use has a visible cooldown only and no rebuild
invalidation. Cursor, page and parent path are preserved.

The strict optional `appearance.design` field accepts only the five exact source
names and defaults to Apparition for older producers. Explicit incompatible
flag/design combinations are rejected even in a closed packet. Transition
metadata requires an open child with ordinary rows and a valid selected index;
it cannot coexist with editors, credits, Soft Unlock or a preference refresh.
The existing paused input route consumes commands and reseeds fresh edges on
resume. No new IPC action or shortcut is required.

## Source geometry

For a body origin `(X,Y)`, width `W` and `N` displayed rows, source row spacing
is 18 units. Geometry changes by design:

| Design | Body height | First row center | Chrome and scroller |
| --- | --- | --- | --- |
| Apparition | `18N` | `Y+8` | Gray banner begins `Y−20`, final height `H+35`; one-unit accent lines; gray `W×18` scroller |
| Classic | `18N+7` | `Y+11` | Accent frame `(X−1,Y−13,W+2,H+14)`; gray body alpha `.85`; accent `W×18` scroller |
| Native | `18N` | `Y+8` | Accent 38-unit banner at `Y−55`, black 17-unit separator at `Y−17`; accent scroller alpha `.7` |
| AIO | `18N+13` | `Y+15` | Accent 25-unit header/footer, black outer frame; accent `2×23` side scroller |
| Basic | `18N+22` | `Y+30` | Black 20-unit banner inside body; black `W×18` scroller |

Titles are Apparition centered/accent at scale 1.5; Classic left/white at 1.2;
Native left/white at 1 with alpha .7 and a separate centered Apparition brand at
1.6; AIO left/white at 1.4; Basic centered/accent at 1.4. Native's root title is
Main Menu. Classic and Native counters sit beside the header title; Apparition's
final counter sits below the body. AIO's status is BO3 verification state, for
which there is no proved IW equivalent. The local implementation must identify
its actual local menu status rather than invent Host/Admin/Developer authority.

Adaptive instruction offsets `(xOffset,yOffset,widthOffset)` are Apparition
`(1,18,−2)`, Classic/Basic `(0,5,0)`, Native `(1,5,−2)` and AIO `(−1,30,2)`.
The ordinary footer background starts gray42/opaque for Apparition, gray25/alpha
.85 for Classic and black/opaque for the other designs. Source body/viewport
coordinates and substitute font metrics remain explicit mapping limits.

Soft Lock fixes bodies at 20/120/122/220 for instruction positioning, width,
menu positioning and credits. Rows, scroller and counter disappear. Only
Apparition and Classic banners resize with that body; Native's header/separator
stay fixed, AIO's bottom band/status moves with the body and Basic's top banner
stays fixed. Width editing destroys both the title and secondary menuName;
other editors and credits retain them. Reposition translates all design chrome;
ordinary instructions keep their separate anchor and adaptive instructions follow
the body's design offset. Scroll context must include the exact design.

## Deliberately unfinished source behavior

This wave is a state/geometry checkpoint, not complete design parity. Opening
`drawText(true)` fades each displayed row concurrently over `rowIndex×100 ms`;
that can continue for 1.3 seconds after the 50 ms rebuild at capacity14. A 250 ms
input cooldown alone does not reproduce this presentation. Ordinary modal return
uses `openMenu1(false)` and must not be assigned that fade. Opening fade metadata
and engine tween interactions need their own tests before a fidelity claim.

Source color callbacks also retain history. AIO's instruction background begins
black and its outline captures the accent; fixed/rainbow callbacks recolor the
background rather than the outline. A footer rebuild resets that history. Basic's
static color handler does not recolor its scroller, while rainbow does; stopping
rainbow can retain the last scroller color until HUD rebuild. Stateless design
plus current accent cannot reproduce all these transitions. This checkpoint
uses initial per-design chrome and records callback history as unfinished.

During the 50 ms HUD-absent rebuild, the source footer can already use the new
design's width/x offsets and fall back to its ordinary saved vertical anchor,
while its existing constructed colors remain old until forced refresh. Retaining
the old declared design in the local rebuild packet does not encode both halves
of that state. This brief placement difference remains unfinished; no target
design is inferred from row strings and no exact transition-frame claim is made.

## Completed regression evidence

Root confirms **922 distinct checks**: backend 266, features 103,
retail-session/history 24, Windows native runtime including actual exports 109,
additional CPU preview reader 16, packaging 84, scroll input 9, choice input 12,
Quick Exit input 15, width input 15, unlock input 7, instruction-position input 11,
saved hints 13, width animation 8, geometry/text 48, production-panel CPU 106,
rainbow clock 9, scroll-animation input 12, scroll presentation 16, menu-position
input 12, credits input 10, credits timeline 8 and credits clock 9.

The portable runner passes 109 checks with the same 98 actual producer exports.
Its parser/export and seven existing input cases overlap Windows; only the sixteen
preview-reader cases are additional. CTest runners and the 98 joined files are
not counted again. Root exports are byte identical to the independent producer
exports. The complete native cross-build passes all 218 steps and actual Windows
backend publication succeeds. The Windows fixture owns a hidden test window and
private files, without a game process or graphics device.

New tests cover five choices, draft persistence, typed forbidden flags, row
availability/help, 0/49/50/249/250 boundaries, same-design no-op, exactly one
presentation invalidation, stable navigation, focus loss, held/pending input,
unchanged epochs on same-design cooldown, shutdown, pending session completion,
clock faults/overflow and transactional counter admission. Native readers reject
malformed names, types, decoded duplicate keys, invalid contexts and every modal
conflict. All 98 producer states join production parsing and CPU rendering.

The six new geometry groups cover every design's body/rows/scroller, headers,
secondary labels, counters, four modal sizes, adaptive footer offsets, signed
origins and bounded notices. Ten new CPU groups cover design painting and source
colors, restrictions, modal chrome, paused help, equal-size design context/stale
frame rejection, repeated switching and font-atlas stability. Long UTF-8 headers
retain the mapped source font and use complete-character ellipsis; counter ink is
identical to a short title at widths 200/260 for Classic and Native. This is an IW
readability adaptation. The repeated design-switch test leaves the font atlas
unchanged.

Root reviewed four alternate cooldown layouts, Native's retained root cursor and
Main Menu title, Classic width editing and the hidden rebuild with paused help.
All 123 generated images are offline CPU previews. Normal main/reference main
and reference status PPMs remain byte identical to the preceding checkpoint.
Thirty-five gameplay/session/loader/hook sources remain byte identical. The
installed package is preserved and its launcher remains disabled.

Retail match-exit investigation, all-map acceptance, private ammo, shared self
flag identity and complete source families remain open. The opening, callback
history and brief footer-placement limits above remain real gaps; this checkpoint
does not claim full 1:1 parity.

## Reference anchors

- [Design choices and handlers](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L59)
- [Design change and capability normalization](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L450)
- [Five HUD constructors](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/designHud.gsc#L27)
- [Drawing, row offsets, fades and final geometry](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L301)
- [Soft Lock geometry](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L650)
- [Instruction creation and adaptive offsets](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/main.gsc#L372)
