# Independent Apparition instructions HUD

The instructions panel belongs to the local player, independently of the open
menu. Closing the menu retains the panel and changes its text to opening hints.
This candidate connects that lifetime from the controller to the native renderer.
It is offline work; current retail rendering, input and gameplay acceptance remain
unproved. The saved backup and the installed disabled DLL remain separate.

## Reference and wire contract

The reference is [Apparition main.gsc](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/main.gsc),
lines 372–470 and 506–595. The repository HEAD was fetched with curl on
2026-10-09 and still matches that commit. Its instructions thread starts before
menu opening, continues through menu locks and destroys its widgets separately
from menu chrome. Closing the menu or redrawing rows does not create a new footer.

The optional `instructionHud` object distinguishes legacy omission from a
current producer explicitly withdrawing its panel. The base descriptor has six
fields. The [later color candidate](instruction-color-reference-audit.md) adds the
paired optional creationAccent and creationRainbow fields; older descriptors remain
supported:

| Field | Meaning |
| --- | --- |
| `exists` | Whether this producer declares a current footer instance |
| `creationRevision` | Positive checked unsigned 64-bit creation identity |
| `admissionRevision` | Revision of the current verified local eligibility |
| `creationDesign` | Design at creation, used for retained footer paint |
| `creationAlternate` | Alternate style captured at creation |
| `creationBackground` | Background visibility captured at creation |

An explicit absent instance has `exists:false` and null remaining fields.
An active instance requires known Alive or Dead life, positive menu permission,
matching current eligibility and enabled instructions. Busy and monitor control
locks do not revoke its lifetime. Unknown facts cannot select the source's Dead
branch. The private actor owner remains outside the wire packet.

Missing or null `instructionHud` retains compatibility: the old open-menu footer
can draw, and no closed footer is inferred. Current production publishes explicit
absence when proof is unavailable. Focus and stale/error projections withdraw
the visible declaration; they do not create a replacement actor or authorize input.

## Creation and placement

The controller retains the instance across menu close/open, navigation, row
redraw, palette changes, position changes and soft locks. Enable, changed Alternate
or Background settings, actor replacement and the first observed Alive-to-Dead
edge create a replacement. Dead-to-Alive updates the available hints while
retaining the instance. Changed Design refreshes after successful actual menu
construction at the observed 50 ms boundary, rather than its 250 ms input unlock.

Creation revisions are reserved before related local setting mutations and
committed after successful snapshot construction. This is an owner-observed
lifetime mapping. The original coroutine can destroy a footer and recreate it on
its next 10 ms iteration; sparse native/controller observations do not prove that
interleaving or its literal creation count. The source can also restart duplicate
instruction threads after a verification change. This port retains a single owner
instead of reproducing that race.

An observed death during a pending Design change has its own creation identity
and preserves the later post-draw refresh. Both future identities are checked
before committing the earlier creation. Qualified same-owner Unknown withdrawal
retains private creation and last known life instead of inventing a death or
refresh cause. Recovery before the Design draw retains the original footer;
the actual draw creates its required replacement once. The later
[withdrawal audit](instruction-withdrawal-reference-audit.md) records this correction
to the original observation policy.
Permission loss or actor replacement cancels the existing body transition and its
pending footer cause. A canceled visit cannot revive that cause on recovery.

Closed placement uses the saved instructions anchor even when Adaptive is enabled.
Open placement retains the separate present-HUD and absent-HUD rules. Creation
style affects paint; current geometry settings continue to control placement.
The closed path draws only instructions, without menu rows, title or status chrome.

## Hints and remaining fidelity

Opening hints come from saved action bindings and configured Main/Quick tuples.
Dead opening uses the saved Aim and Jump groups. An unsupported group cannot
invent a physical key. The source advertises Dead Quick opening even when Quick
is disabled, although its monitor rejects that route; native help marks the route
unavailable. Displayed hints do not grant menu admission or gameplay authority.

Living closed help omits disabled Quick, as the reference does. Dead ordinary
Quick help uses saved Attack/Aim, choice slots, Use and Jump. The current native
router supports Jump exit on Quick only; Dead Main help marks that route
unavailable. Modal and paused help retains precedence where those actual input
modes differ from the reference, and the router's additional saved scroll aliases
remain an explicit input adaptation.

General theme fades, exact engine easing, AIO retained creation color and source
coroutine transient scheduling remain unfinished. This checkpoint must not be
used as evidence of complete instructions/theme parity, actual GPU behavior,
injection safety or co-op/all-map feature acceptance.

## Original checkpoint evidence

The complete actual backend driver registers and passes 742 checks on Linux and
Windows .NET 8, including 18 new instruction cases. Both session runs pass 114
checks. The actual driver exports all 35 new controller packets; an independent
root export matches their bytes. All eight current controller exports contain
526 packets, which are inputs rather than additional test cases.

The native gates pass 175 production CPU panel checks and 65 geometry checks.
Current portable and Windows runtime suites and separate actual-graph checks
cover both readers, private saved hints and input gates. The strict MinGW Release
build and production Windows backend publication pass. Four CPU images were
reviewed for closed help, disabled Quick, Design rebuild and disabled instructions;
they are not game screenshots or GPU acceptance.

Compiled failing-before regressions include missing controller ownership, closed
rendering, ignored closed placement, overlapping death/Design and Unknown/Design
causes under the original observation policy, and the actual test-driver export
route. Later withdrawal regressions correct that policy against the source
destruction causes. Incorrect test-directory setup
and compiler/setup failures are excluded from that evidence. Platform repeats,
multiple profiles and inner assertions do not add distinct cases.
