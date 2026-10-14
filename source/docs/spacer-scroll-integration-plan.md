# Typed spacer scroll presentation contract

The prior opening-controls checkpoint is sealed at 933 source files and 1,216
registered cases. This document records the next implementation contract, not
acceptance evidence. Installed files and all sealed clones remain unchanged.

## Reference branches

Fresh official `Menu/base.gsc` matches pinned commit
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`. The window calculation at 627–648
uses floor((capacity−1)/2) above and floor((capacity+1)/2) below the cursor. The
window-shift branch precedes blank validation; the final style, scroller and
counter loop follows it. Therefore final selection distance is insufficient
proof of the source path.

Current Open Controls has a typed blank at index 2. Source capacities 8–14 keep
window 0 for cursor 1, blank 2 and target 3 in both directions. A blank adds T/2
hold before the final T-length style/scroller fade. An existing counter changes
at the hold boundary, while confirmed action selection already names the target.

Stealth capacity 5 is a real supported branch and disables the counter. Down
1→blank 2→3 holds T/2, then shifts window 0→1 and starts final styles/scroller
for T. Up 3→blank 2→1 shifts window 1→0 immediately, then at T/2 redraws final
retained rows/styles/scroller without an opening fade. The detached outgoing row
can continue its original fade until T. Its identity remains bounded and unique.

Nonwrapped multiple blanks have k×T/2 only while every invalid-step window stays
unchanged. A final target may then shift by one window row before final styles.
A shifted invalid step instead waits once, advances one index, refreshes and
returns; that next index might still be blank. Long wraps refresh before blank
validation. Short-wrap undefined-variable comparison semantics remain unproved.
Unsupported ambiguous routes must not be represented as verified source paths.

## Private dispatch and acknowledgment proof

A bounded typed planner captures direction, source/target indices, source/blank/
target windows, count/capacity, topology, duration and branch. Canonical opening
action identities and spacer bits distinguish row identities without retaining
text or feature values. The planner recomputes classification rather than
trusting a supplied branch label. Menu, focus, policy and appearance belong to
one observed ordinary open snapshot. No raw key or new public command is added.

A private tracker captures only after a successful command write and MarkSent.
Promotion requires the exact raw acknowledged sequence, final target, checked
source navigation plus one, unchanged menu/focus/policy/appearance/topology and
ordinary input mode. Older ACK keeps pending; a jump or rejected/mismatched state
cancels it. Same-index ACK cannot prove acceptance and must not fabricate motion.
Closed, stale-heartbeat, changed focus/context, rollback or failed IO clears it.

Receipt dispatch, report and presentation timestamps use GetTickCount64, the
existing renderer clock. Router admission keeps its current steady clock. Late
observation fast-forwards from dispatch; completed paths do not replay. The
trusted private receipt is stamped before const snapshot publication. JSON never
creates one. Matching stays in a dedicated runtime module rather than the parser.

## Presentation and truthful selection

Render admission also requires a previously rendered source navigation, index,
menu/focus identity, layout and topology. Startup, reset or an unseen old frame
cannot animate invented history. Same-sequence reports do not restart the path;
missing, stale or mismatched descriptors retain bounded static fallback.

Frame.selected remains the confirmed final target. A separate counter paint
index is allowed only for the validated receipt phase: source before the hold
boundary, target afterwards. Counter existence remains controlled by appearance;
Stealth-plus-counter packets must not be invented to test this path. Row styles,
window movement and the bounded detached outgoing row follow their own source
branches rather than changing action identity. Arbitrary jumps retain existing
snap behavior without a verified private receipt.

The managed fixture wave will add 12 actual controller snapshots: ordinary,
counter and Stealth; Down/Up; before/after. Every changed target advances
navigation once, leaves the menu epoch unchanged and never publishes a blank
selection. Expected inventory becomes 192 profiles; exact image names and order
manifests will be updated only after the real coupled export is reviewed.

## Required regressions and limits

Practical behavioral RED precedes implementation. Pure planner/tracker tests
cover successful paths, malformed topology/metadata, clock and counter bounds,
ACK drops/jumps, stale contexts and policy changes. Presentation tests cover all
three current branches, intermediate clock samples, counter timing, repeated
heartbeats, late frames, reset and invalid receipts. Production CPU tests join
actual producer snapshots with trusted private observations; no fixture may
silently invent public wire fields or gameplay success.

No hook, graphics lifetime, gameplay writer, game process or installed launcher
change is authorized by this wave. Isolated regressions are separate from retail
input/frame timing and the unresolved saved fatal. Quick Menu, persistence,
engine gates, remaining map/quest/gameplay families and full parity remain open.
