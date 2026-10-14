# Owned fixed-theme callbacks for the instruction HUD

This candidate joins accepted palette actions to the independent help panel's
outline and background histories. A selection is an event even when the selected
RGB is unchanged. Its target belongs to the design at invocation, rather than the
design when a later renderer frame arrives. Full Apparition parity remains open.

## Reference contract

The official [MenuTheme callback](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc)
always stops Smooth Rainbow, applies the selected theme, and asks the existing
instruction widget to fade over 500 ms. The current AIO design targets its
background; other current designs target its outline. There is no same-color
early return. Ordinary row redraw, Back and closing menu chrome retain the
independent instruction widgets; replacing those widgets ends their histories.

The palette's nominal 430 ms input wait is shorter than the requested fade.
Successive selections can therefore interrupt one another. Eight stored events
cannot describe an indefinitely interrupted history to a reader that missed its
prefix. An older event being 500 ms old does not prove that its endpoint was
reached before a later event interrupted it.

Fresh official source remains at commit
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`. Source establishes the targets and
requested durations. It does not establish the engine's numerical fade curve,
interruption sampling, or the interaction between FadeOverTime and a concurrent
Rainbow assignment.

## Controller and wire ownership

The optional `instructionTheme` descriptor belongs to one admitted instruction
creation. It carries that creation's positive revision, a `prefixComplete` flag,
and at most eight ordered, consecutive events. Each event contains an immutable positive
invocation revision, target, RGB, stopped-Rainbow fact, and a predecessor gap for
the same target channel. Its elapsed milliseconds advance with reports and are
not clamped at 500. A predecessor gap of 500 means at least 500 ms of quiet time;
a smaller gap preserves the millisecond interval. A null gap means no earlier
invocation on that channel.

The actual controller captures the target and prior Rainbow mode at the accepted
palette action. Event creation does not imply a row, menu, chrome or instruction
recreation. A valid explicit empty descriptor disables scalar-accent inference.
Legacy omission retains the earlier compatibility adapter. Current descriptors
are withdrawn with their display authority; they grant no gameplay permission.
An explicitly supplied JSON null is rejected. A no-background creation may carry
an explicit empty journal, but cannot carry a widget-color event.

Invocation counters and clock reads must succeed before a selection commits.
Snapshot projection commits journal ownership only after state construction
succeeds. Pruning makes the prefix incomplete for the remainder of that creation.
An incomplete prefix still requires consecutive IDs inside the retained window;
it cannot hide missing events between stored entries. The first ID may be any
positive global invocation revision.
Replacing the parent HUD or actor retires its journal rather than replaying it on
another owner. Same-identity event bodies cannot mutate, and elapsed time cannot
run backwards.

## Native presentation and limits

The native renderer keeps separate outline and background segments, preserving
each widget's alpha. Its 500 ms linear interpolation is an explicit adapter,
not evidence of the source engine's easing. Current design cannot redirect a
previously accepted event. Graphics resource resets and temporary focus
withdrawal retain observed histories.
An observed Rainbow sample proves a seed only for the channel that received it.
Observation of the other channel cannot authorize an interrupted fade's start.

Complete fixed histories can be replayed in chronological order. A retained
native owner can also continue from its already observed state. A late observer
with a missing interrupted prefix or a missing Rainbow seed cannot reconstruct
the starting RGB. It preserves the last proved color until an uninterrupted
latest callback reaches its endpoint. This recovery is deliberately documented
as incomplete source fidelity, rather than presented as a recovered animation.
If a late journal contains only one channel and has pruned the other channel's
last event, that omitted channel's endpoint remains unavailable until a later
proved callback. A missing prefix cannot be treated as a two-channel checkpoint.

## Evidence required for this candidate

Acceptance requires genuine failing regressions before implementation, actual
controller and serialization checks, strict native schema and lifetime tests,
independent interpolation oracles, and actual controller packets consumed by the
production parser and CPU painter. Normal, interrupted, same-color, channel,
pruned-prefix, invalid-clock, replacement and withdrawal cases must be covered.
The final candidate must also pass the existing managed and native regression
suites and production Windows cross-builds with matching source identities.

No live game test is part of this wave. The saved backup and installed disabled
DLL must remain byte-identical. General widget fades, source engine interpolation,
remaining gameplay callbacks, private ammo transport, normal Steam co-op,
all-map and super-Easter-egg acceptance, and final injected appearance and input
remain requirements of the full port.
