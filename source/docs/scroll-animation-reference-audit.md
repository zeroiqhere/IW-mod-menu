# Apparition scroll animation and timing

The official main commit was checked with `curl` on 2026-10-08 and remains
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`. The freshly fetched `Menu/base.gsc`
matches the pinned reference. This checkpoint connects the local timing control
to input cadence and the production panel's cursor, row and selection animation.
It introduces no game-state address or gameplay writer.

## Units, drafts and committed state

The source labels its slider `Scroll Animation Time (ms)` but displays integers
10..25, default 12. Its handler multiplies by 0.01 seconds: the real range is
100..250 ms, default 120 ms, in 10 ms steps. The IW menu displays that real duration
explicitly. Exact saved action slots 3/4 decrease/increase a separate numeric
candidate, wrapping at both bounds; a fresh Use commits it. Candidate and applied
values remain distinct, and missing bindings disable editing with help.

The draft persists for the controller lifetime, including navigation and closure.
The reference's destination-indexed back cleanup does not provide the same clean
draft lifetime; that difference is deliberate. Numeric commits do not route
through the boolean preference handler or dispatch a gameplay action.

The optional `appearance.scrollAnimationMilliseconds` field defaults to 120 for
legacy packets. Supplied values must be integers in the supported range and step.
Null, floating-point, duplicate decoded names and other invalid forms are rejected.
`menuRevision` is an optional unsigned integer with absence distinct from zero;
the production controller supplies it. It changes for open/close, page changes,
structural row changes and explicit presentation rebuilds. Cursor movement,
heartbeat, row values, availability and draft text do not change that context.
Checked counters are evaluated before committing affected navigation state.

## Input cadence

The source scroll branch waits for the selected duration plus 25 ms, then the
monitor adds its normal 50 ms wait. Normal held scrolling therefore uses duration
plus 75 ms: 175..325 ms, default 195 ms. The router captures this interval when
dispatching movement; a later duration commit affects the next movement.

The cooldown remains active after a direction is released. Observed Select,
melee, choice and direction edges during it are consumed, rather than deferred.
An inherited knife hold cannot block an established scroll at the deadline.
Pending acknowledgements rebase repeats to avoid bursts. Focus loss, changed menu
context and modal transitions reset/reseed held inputs. Opposite directions remain
neutral. Invalid direct duration arguments fail before reading keys.

Fresh input and one command in flight remain IW safety differences from the source
held-button loops. The later [source monitor wave](source-monitor-priority-reference-audit.md) restores eligible expired scrolling before fresh Select.
Native 20 ms polling, acknowledgement and scheduling quantize observed timing.
The source's separate post-Use wait is not newly reproduced by this checkpoint.

## Presentation behavior

Within a stable menu context, adjacent selection moves the scroller and interpolates
selection color, Spotlight opacity and Large Cursor font size for the captured
duration. A one-row viewport shift moves common rows by one row height. The outgoing
edge stays in place and fades out; the incoming edge fades in at its destination,
matching the source's creation behavior. Short-list wrap moves across the visible
list. Long-list wrap, skipped windows and missing intermediate anchors snap to the
current confirmed window. Disabled capability rows stay selectable for discovery,
as the source skips empty labels rather than unavailable actions.

Retargeting starts from the sampled displayed positions and style weights. A fixed
fifteen-row bound includes retiring edges; overflow snaps. The helper stores row
indices and weights, with no retained player names, old text or feature values.
Drawing resolves those indices against the current immutable snapshot. A duration
change cannot stretch an active transition. Clock rollback, changed page/structure,
layout, editors, unlock states, closure and renderer reset discard old motion.

Focus transitions observed by the runtime worker stamp a private native epoch before
snapshot publication. JSON cannot supply this epoch. The same foreground observation
feeds input. This resets animation on observed focus gaps without changing graphics
hooks; extremely brief gaps between worker observations are not claimed detectable.
Legacy snapshots without menu/focus identity draw statically.

Interpolation is linear; exact BO3 engine easing and concurrent rainbow/color-fade
interaction are unproved. Changed boolean/style controls still rebuild immediately
in the existing IW controller, while the source can delay a generic boolean rebuild
by 180 ms. This checkpoint does not claim that delay. Font sizes use the pinned
ImGui renderer's integer baking behavior; bounded-cache regression evidence is
recorded with the final checkpoint rather than inferred from screenshots.

## Evidence and remaining work

Regression checks cover managed state/navigation, strict native parsing, input
cooldowns, pure animation geometry/clocks and production CPU drawlists. Actual
controller exports join the producer, reader and renderer. Final counts, binaries,
previews and source hashes are recorded in the candidate's `evidence.json`.
The checkpoint passes **715 distinct checks**: backend 199, features 103,
session/history 24, Windows native runtime 82, separate preview reader 8,
packaging 73, existing scroll input 9, choice input 12, Quick Exit 15, width
input 15, unlock input 7, instruction-position input 11, saved hints 9,
width animation 8, geometry/text 33, production panel CPU 72, rainbow clock 9,
new scroll timing 12 and pure scroll presentation 14. The 74 portable schema
checks include the actual-export join and seven existing Windows input checks;
all overlap the Windows fixture except the eight separate preview-reader cases.
The 39 snapshots and repeated runs are not extra independent checks.

Root confirms the full native cross-build and Windows backend publication. The
Windows fixture uses its own hidden test window and private files, with no game
process or graphics device. A thousand CPU scrolling frames exercise font sizes
13 through 16; the warmed bake count, active texture dimensions and texture list
remain stable. Default main and reference main/status pixels match the preceding
rainbow checkpoint byte for byte. Reviewed midpoint/window-complete previews
state their injected clock and private focus context explicitly.
CPU previews and owned Windows fixtures are not retail-game acceptance.

The installed launcher and previous backups remain untouched. Private ammo,
remaining gameplay families, all-map/super-EE behavior, full Apparition parity and
the prior retail match-exit crash remain unfinished.

## Reference anchors

- [Slider bounds, duration conversion and default](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc)
- [Input waits, scrolling, numeric wrapping and refresh](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc)
