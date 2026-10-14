# Production local-player observation

This source checkpoint connects the verified retail account/actor route to the
backend. It leaves the installed injected package and its disable marker intact.
The requested full Apparition port remains unfinished.

## Data and ownership

`LocalActorSnapshotReader` reads a complete account/session/actor binding before
using its health and session-state addresses. It copies signed health, native
session state and the exact named `Session_IsHost` fields, repeats the complete
binding, requires equality, and rechecks every copied scalar and host field.
The callback may reuse buffers; retained bytes are cloned. Failed, incomplete or
observably changing reads never return an actor. These reads are bounded but
cannot establish an atomic engine snapshot or a spawn generation.

The host replica compares the verified session against the two captured party
session pointers in source order. A matched active party compares the verified
member with captured host field +0x2C50. Otherwise it preserves the original
member-zero fallback, after account/member verification. It does not select
player zero or infer the full script `ishost` method's unnamed override branch.

`RetailSteamTransport` implements `ILocalActorSnapshotSource` under its existing
access lock. It retains a separate `PROCESS_VM_READ` handle with no write API,
checks the inspected executable fingerprint and mapped PE header, and reads only
through that owned handle. File fingerprint and mapped-header rejections are
retained for the immutable connection; transient reads can retry. Failed
candidate validation disposes the candidate. Normal disposal releases the
observation handle only after the session has joined reads and cancellation work.

`BackendRunner` supplies the production provider factory. The session constructs
one provider for each retained connection on its worker. The provider checks the
original PID/connection before and after the copied read, validates the binding
shape, honors cancellation, and reports Unknown after source failure. Cached
views do not start native reads. Existing owner/ticket checks prevent an old
completion from publishing into a replacement connection.

## Menu policy and display

The pinned Apparition source owns `accessLevel` and `DisableMenuControls` itself;
these are not fields to discover in retail IW memory. The native port initializes
Host level 6 from a positive verified session-host result under its host policy.
HasMenu means the retained level exceeds None at level 1. A negative native role
alone does not prove the script override or a saved access level, so that policy
remains Unknown. The grant belongs to one complete account/actor binding and one
connection. Life changes and transient unavailable reads do not transfer it;
another binding resets it, and a new provider starts without the old grant.

The controller overlays ordinary control locks from its actual opening, unlock,
refresh/rebuild, modal and shutdown owners. Dedicated editor input keeps its
existing route. Quick views receive the same composed context during initial
publication and fresh action checks. Known life declarations remain visible
during a lock, while actions remain disabled. Unknown identity, permission or
life cannot expose a living action. Busy and focus checks remain separate.

The current personal health adapter still targets slot zero. A nonzero verified
local slot therefore keeps its living Quick catalog but shows God Mode as
UNAVAILABLE, with no borrowed slot-zero ON/OFF value or action. A target-bound
personal feature adapter is the next required gameplay change. Personal ammo
cannot borrow the lobby-wide instruction patch. Runtime Quick opening is still
unimplemented; these views do not advertise it as available.

## Evidence

The focused suites include genuine compiled RED/GREEN sequences for the actor
reader, host replica, provider, owner locks and Quick context. Peer review found
an initially missing full-binding shape guard before permission commit; its
regression reproduced seven passes and one failure, then passed all eight after
the fix. The complete suites now pass 523 backend checks and 41 session checks,
including three checks joining the actual reader, provider and session lifetime.
The same 523 backend checks pass in an actual self-contained Windows .NET 8
process under the isolated Proton prefix. A framework-specific timer-overflow
precondition in an older test was replaced with the actual required menu restart
behavior; production already handles both framework results. The Windows .NET 8
backend publishes with warnings treated as errors.

Three additional RED/GREEN cache regressions prove that a native-read retry does
not rehash the executable file, that buffers cannot mutate the retained header,
that module changes reject before reuse, and that disposal permits a fresh read.
Failed or malformed file loads are not retained. The connection owns this cache;
the native adapter still validates mapped headers for each actor read.

A bounded Linux fixture compiled the actual production actor reader and inspected
the original Steam game. Two complete actor snapshots matched: selected member
and linked slot zero, native life Alive, named session-host role true. It used
370 bounded reads and performed zero game writes, injections or engine/Steam
calls. It did not grant menu permission in the fixture. This confirms copied
player facts in one solo match, not native Windows transport operation, gameplay
callbacks, teammate toggles, match exit or all-map fidelity.

The new native memory fixture uses only its own process and allocations. All
five .NET 8 checks passed under Proton in an isolated test prefix: exact reads,
returned-array independence, actual write denial for the read-only handle,
protected/partial/released-memory errors, input bounds and handle disposal.

In the existing active Steam prefix, the same fixture had three passes and two
read failures with error 5. Comparing read-only, query, operation and write masks
still produced read denial; no extra process rights were adopted. ProtonInformer
module enumeration of the original game also reported error 5. The isolated
passing run does not erase these failures or establish their cause. The original
game prefix, Steam account and launch settings were not modified. A future fresh
normal-Steam runtime check is still required; no user game trial is requested by
this checkpoint.

Evidence is retained under
`$HOME/.cache/iw-apparition-tests-20261008-production-observation`. Native adapter
failure-path coverage remains narrower than the portable lifetime tests: failed
candidate release, cached header rejection and transient native retry branches
were reviewed but are not fully exercised by the own-memory fixture.

## Remaining implementation

Bind personal features to the current verified actor rather than a fixed health
address, and connect the composed menu state to the native opening monitor.
Implement and validate the remaining source callbacks, complete the All Players
and access-management behavior, and confirm multiplayer/all-map/super-EE and
original Steam progression behavior. Complete the actual renderer/control/UI
joins before installing a new package. The current tests do not prove the full
1:1 port or resolve the unknown tested build in the user's match-exit report.
