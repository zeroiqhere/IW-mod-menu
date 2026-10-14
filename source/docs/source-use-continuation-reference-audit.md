# Source-held Use continuation

This source candidate restores repeated Select on stable ordinary rows while Use
remains held. It is offline work. It has not been installed or exercised inside
Infinite Warfare; the saved playable tool and installed products remain unchanged.
Full Apparition gameplay, rendering, input and Steam co-op acceptance are unfinished.

## Source contract

The official reference remains commit
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`.
[Menu/base.gsc](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc)
checks Use on every ordinary monitor iteration. Scrolling precedes Use, and Use
precedes slider changes and knife. It does not require release between callbacks.
A stable plain or slider callback pauses for 200 ms plus the outer 50 ms wait.
A stable nonslider BOOL row also waits 180 ms before refreshing, for 430 ms total.
The callback task's success, rejection or completion does not replace those waits.

[Menu/menu_customization.gsc](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc)
contains the instruction controls. Disable Instructions redraws immediately in its
handler and again at the caller's 180 ms refresh. The implementation therefore
retains the original invocation through both redraws rather than counting one
menu revision change as its completion.

The source rereads BOOL after threaded dispatch. Generic submenu scheduling and
its possible row-type changes are still unresolved. This candidate does not claim
that all submenu callbacks take 250 or 430 ms, or carry the original hold into a
new child. Remaining held knife/choice behavior and physical game action sampling
also need their own evidence.

## Ownership and input behavior

The backend captures the accepted Select's original command sequence, verified
local admission, connection owner, page, selected index, menu family, navigation,
complete row action topology and personal identity before mutation or queueing.
Changing row values, details or availability during its own work is allowed;
changing the original action, player, cohort or visit retires that invocation.

The public `menuUseContinuation` object contains exactly four fields: positive
unsigned `revision`, `sequence`, `admissionRevision`, and `kind` (`plain` or
`boolean`). It carries causal identity, not permission, a feature outcome, actor
addresses, elapsed time or a target. The backend publishes it only after the
original callback commits. Normal expiry retains it as ready; cancellation,
focus loss or another admitted invocation withdraws it. Ready state is private
and cannot be inferred from an absent callback without the same retained receipt.
An ignored later command ACK does not rewrite the original queued sequence.

Both state readers validate the ordinary open selected row, known local admission,
matching callback revision and kind when present, bounded unsigned fields and
exclusion of competing editor or monitor phases. Duplicate decoded keys remain
rejected by the bounded readers. Preview/connection and full private topology are
producer guards, not independently exposed wire permissions.

The native worker captures a Select sequence only after its command write succeeds.
The router then requires that exact ACK and receipt before resuming held Use. It
never estimates a backend 250/430 ms deadline, accumulates missed repeats, borrows
an older callback revision, or treats an ON checkbox as readiness. Each repeat
must receive its own successful write and new callback association. Busy, pending,
changed admission/navigation/family, lost focus and native clock rollback consume
or pause input under their existing ownership rules. Eligible held scrolling keeps
its source priority when the original Use callback becomes ready.

These ACK, focus and write guards are explicit IW transport adaptations. They are
not evidence of literal GSC VM scheduling or of full engine input parity.

## Regression evidence

The managed baseline reached seven behavioral failures before implementation;
setup errors are recorded separately. The final actual backend driver passes
879 checks, including ten new registrations. A regression in inexpensive phase
publication was caught by the full suite and fixed by reading the session only
when a continuation exists. The original six legacy tests remain unchanged.

Native held-input tests reached nine failures before implementation and now pass
14 cases. Both strict reader tests reached six failures and now pass eight cases.
Additional graph tests consume 49 real controller packets through both readers;
plain, BOOL and instruction graphs join consecutive native Select cycles, while
queued ACK and changed-player graphs check ownership retirement. The independent
literal packet inventory prevents agreement between two readers from becoming
its only oracle. Packet count is separate from test count.

Graphs that begin after admission prove producer withdrawal; they do not fabricate
an earlier native command to claim an end-to-end hold test. Backend rollback uses
its recorded source clock; it is not passed off as the native monotonic clock.
Windows payload and test builds prove compilation only. No test here proves
injected execution, GPU behavior, retail features, friend controls or quest support.
