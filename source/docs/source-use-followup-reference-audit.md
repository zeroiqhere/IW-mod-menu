# Source Use follow-up and declared choice roles

This source candidate continues the original Apparition fidelity work. It is not
installed or proved in a retail match. The saved playable tool remains separate.

## Authoritative source

The reference is Apparition commit
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`:
[monitor](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc),
[menu declarations](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu.gsc), and
[Basic Scripts](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Functions/basic.gsc).

The ordinary source monitor tests scroll, Use, an exclusive choice slot, then
knife or dead Jump. Opposed choice slots fall through to the knife branch.
An exclusive slot consumes its branch even when the selected option has no
slider handler. Only a slider or increment slider incurs the inner 130 ms pause;
the outer 50 ms tick also applies. A non-slider attempt therefore pauses 50 ms,
while a slider attempt pauses 180 ms. Plain Use retains 250 ms; a stable BOOL Use
retains 430 ms. The enabled child knife-close loop counts immediately and then
waits nine times for 10 ms, reaching its tenth count at 90 ms.

## Implementation contract

A successfully written Select retains its original command sequence and the
controller's original callback receipt. Normal retirement permits current held
choice or knife predicates to resume in source order. The new branch consumes
that receipt once. The existing scoped dead Quick Jump shares the knife route.
An already admitted knife loop remains exclusive when higher buttons arrive.

Readiness does not come from a native 250/430 ms estimate or from the absence of
an unrelated descriptor. Failed writes, foreign acknowledgments, changed actor,
cohort, admission, surface, page, row topology, focus or clock direction cannot
inherit the old Use. A new branch still passes through current controller
admission before any action.

Private row metadata distinguishes Unknown, NonSlider, Slider and IncrementSlider.
It follows exact source declarations rather than labels or enabled state. Basic
Scripts has 31 declarations, including seven sliders and two increment sliders;
Quick roles follow stable source option identifiers. Selected-player copies keep
their role and original action target. Changes to role retire captured topology.
The metadata is excluded from JSON and grants no feature capability.

An unavailable source slider retains its 180 ms caller attempt pause while its
value remains UNAVAILABLE and its callback stays disabled. A proved non-slider
uses a distinct `choiceNoop` 50 ms phase with no value or action change. Unknown
IW extensions, including Connect, cannot borrow that source declaration.

## Regression evidence and rejected hypotheses

Separate tests exercise actual input routing, exact declared roles, strict schema
validation and actual controller exports. The lower-branch export has 51 packets;
the separate non-slider export has 17. Their callback, receipt, wait, value and
ownership checks are independent of agreement between the two readers.

An earlier 180 ms non-slider hypothesis was rejected after inspecting the
source's inner guard. Its passing focused tests are not accepted evidence.
The initial 17-packet export selected the IW Connect extension at the root;
that historical trace is superseded by a source-declared Basic Scripts link.
Test-driver build and oracle corrections are recorded separately from behavioral
RED failures. Acceptance requires final regressions against current source bytes.

## Remaining scope

This change does not establish general held-choice continuation after mechanical
50/180 ms waits or held Back after parent transitions. The native choice repeat
still uses its previous 180 ms schedule. Generic submenu Use scheduling depends
on source thread ordering and remains unproved. Source drawing and engine
interpolation, physical action input, settings across backend restart, private
ammo transport, remaining gameplay callbacks, complete vacant-slot authority and
normal Steam co-op across every map, quest and super Easter egg remain required.
The full goal stays incomplete until those requirements have direct evidence.
