# Apparition credits presentation

The official source was checked with `curl` on 2026-10-08. Main still resolves
to `307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`; the credits utility source matches
the pinned reference byte for byte. Menu Credits is the first customization row,
and its handler is a timed presentation rather than a selectable contributor list.

## Source contract

`MenuCredits` refuses duplicate entry, calls `SoftLockMenu(220, true)` immediately,
and retains the Menu Customization title. Ordinary rows, scroller and counter
disappear. Apparition's banner height is 255 and its bottom line is at body Y+219.
The independent instruction HUD shows melee to exit and retains its visibility
preference. Closing the menu cancels credits; cancellation must not reopen it.

The source catalog has 26 entries: 21 drawn lines and five blank group separators.
Each drawn line launches after the previous entry's delay of 5/6 second; a blank
separator instead delays 5/2 seconds. These delays total 30 seconds, followed by
a 10-second tail wait. Natural completion is nominally 40 seconds. The last line
launches at 29 1/6 seconds and disappears at 39 1/6 seconds, leaving a final quiet
interval. There are at most nine simultaneous lines, and six group titles.

Each line's center starts at body Y+212 and moves to Y+12 over 10 seconds. Alpha
rises over the first 500 milliseconds, remains opaque until 9,500 milliseconds,
and fades over the final 500 milliseconds before destruction. Group titles use
the source objective font at scale 1.4; other lines use 1.1. Inline `^1` marks
red text. Exact engine font metrics, movement easing, color values and scheduler
rounding have not been measured against a BO3 frame capture.

The handler polls held melee every 25 milliseconds. Explicit or natural finish
destroys the credits HUDs, restores ordinary instructions and calls Soft Unlock:
50 milliseconds before recreating the menu, then 100 milliseconds with controls
paused. The customization cursor is retained. The native menu's fresh-edge input,
20-millisecond worker poll and ordinary owner/publication scheduling remain
separate from that source loop.

## Attribution and adaptation

The credit catalog acknowledges the original BO3 Apparition menu and its named
contributors. The two opening lines identify the BO3 reference rather than making
the source's marketing claim about IW. Contributor acknowledgments retain their
source order and cadence. Separate status text identifies the IW RTM backend and
bundled dependencies; package licenses remain authoritative. This wording is an
explicit adaptation, not literal text parity.

The source does not explicitly scissor credits to its body. Native viewport/body
clipping and fitted substitute fonts keep the panel bounded, including translated
or offscreen positions. Those drawing choices do not establish objective-font
equivalence, exact red or engine easing.

## Owner, protocol and native drawing

A controller-local monotonic visit publishes integer `elapsedMilliseconds` within
0..39999. The active page has no ordinary rows and retains the parent title.
Malformed progress and conflicting editor/unlock metadata are rejected. Input
requires a fresh saved melee press and the captured navigation revision. Releasing
all bound melee keys permits a deliberate retry after an acknowledged rejection;
pending edges cannot replay themselves later. Other menu gestures remain inactive.

Natural completion at 40 seconds uses the existing clock-only publication query
and timed parent return. Focus-hidden frames clear presentation metadata while
the owner timer continues. Toggle, close and shutdown cancel without resurrection.
Clock rollback or signed elapsed overflow restarts the local sequence. Entry
samples its clock before navigation and commits only after successful navigation,
so a clock/counter failure cannot leave a latent visit on another page.

The native renderer samples its existing owner-thread clock once per frame.
Its pure presentation helper keys progress to confirmed menu and observed private
focus identities, anchors to the backend report, and interpolates at most 100 ms
ahead of that report and never beyond 39999. Repeated stale packets therefore stop
advancing; the renderer cannot complete the owner sequence. Report/local-clock
rollback or changed identity resets the anchor. Missing legacy identity renders
the exact reported time. No cross-process clock values are compared.

The pure catalog uses integer sixths for launch positions rather than accumulating
rounded 833 ms waits. It stores at most nine live lines in a fixed array. Native
linear movement/fading, RGB 255/0/0 for `^1`, fitted substitute text and body clipping
are explicit adaptations. The contextual footer uses the saved melee binding,
respects visibility/style/background/adaptive preferences and retains ordinary
navigation hints for return. No graphics resource, hook or gameplay writer was
added by this wave.

## Regression evidence

Root reran 847 distinct checks: backend 233, features 103, session/history 24,
Windows runtime 95, CPU preview reader 12, packaging 84, scroll input 9, choice
input 12, Quick Exit input 15, Width Editor input 15, unlock input 7, instruction
position input 11, saved hints 13, width animation 8, geometry/text 42, production
panel CPU 88, rainbow clock 9, scroll timing input 12, scroll presentation 16,
menu position input 12, credits input 10, credits timeline 8 and credits clock 9.
The 91-check portable parser/export suite overlaps Windows runtime except its
12 preview-reader checks. The shared export join and seven existing input cases
are not counted twice; the 52 exports are test fixtures, not additional checks.

The full fresh native cross-build completed 210 steps; Windows .NET publication
passed. Windows runtime used an owned hidden window and private files without a
game process or graphics device. Root independently reproduced all 52 producer
exports and checked them through the parser and CPU renderer. The pure timeline
exercises all 40,000 integer millisecond samples and its nine-line bound.

Root reviewed fade-in, overlapping contributors, the final group and returned
parent CPU images. Sample saved controls are labeled as samples. Default main,
reference-main and reference-status pixels remain byte-identical to the preceding
checkpoint. RED evidence covers missing metadata/input, absent owner lifecycle,
transactional clock entry, geometry and timeline/clock stubs. A separate removed-
painter mutation proves the drawing assertion; it is not described as a prior
production TDD run. Hashes and final counts are in the candidate's evidence files.

These are isolated source/protocol/drawing checks. Full Apparition parity, private
ammo, remaining gameplay families, retail match-exit acceptance, all maps and the
super Easter Egg remain incomplete. Installed files and preceding checkpoints
remain preserved; the normal launcher remains disabled.

## Reference anchors

- [Customization declaration](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L7)
- [Credits handler and animation](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/utilities.gsc#L2063)
- [Text anchors](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/utilities.gsc#L1)
- [Soft Lock and Soft Unlock](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L650)
