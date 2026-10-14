# Connection liveness and final write admission

## Menu behavior

Menu, target, local-observation, and opening-admission getters read only the
retained connection's cached liveness. They do not query native `Connected` or
`ProcessId`, which share the retail transport lock with actor queries. A normal
in-flight poll leaves the last confirmed sample available rather than briefly
displaying a disconnected menu on every poll.

The cache starts unknown. Connect proves the requested PID after opening the
original transport. Retained observation workers sample that same owner before
and after source queries, including sessions without an actor provider or selected
player. An observed disconnection, different PID, or failed identity getter
permanently retires that owner. Later scalar recovery does not revive it.

Retirement withdraws connected state, player targets, local opening receipts,
cached actor facts, and new feature admission. The menu explains that the original
connection could not be confirmed and requires cleanup before reconnecting. A
fresh explicit connection is a new owner only after original cleanup succeeds.

## Physical write entry

Action and selection workers still query the original process freshly. A further
connection-owned guard runs at the final flags-ON or known-ammo-replacement write
entry, because preflight or candidate discovery can cross process loss after the
outer action check. The existing native bound actor and byte validators remain in
place; a PID sample is not a substitute for them.

Shutdown sets a permanent new-write latch before waiting for any retained worker.
The final guard checks it both before and after the potentially blocking process
query. A positive query that returns after shutdown cannot enter a new ON or ammo
replacement write. These separate observations do not make external process
reads and writes atomic.

## Original restoration

Cached retirement is not native disconnection. Raw bound `Connected`, reads, and
original OFF or ammo restoration still use the exact original transport and its
existing ownership checks. Broad lifecycle completion is not gated by cached
liveness. Cleanup can therefore join monitors and restore owned originals even
after the cache retires, without borrowing a replacement player or candidate.

The local, individual teammate, and captured-set Godmode routes share their
retained guarded writers. No new offsets, weapon mappings, vacancy interpretations,
health features, or gameplay callbacks are introduced.

## Regression evidence and limits

New tests cover shared-lock frame contention, retained source joins, wrong-PID and
disconnect retirement, getter failure, stale actor completion, process loss during
personal preflight and ammo discovery, shutdown during the final PID query for
local and peer flags and ammo, and exact original restoration after retirement.
Earlier loss tests now explicitly poll before expecting retirement; frame getters
cannot discover native changes themselves. Counter exhaustion remains covered by
a separate fresh owner inside the existing test. Existing direct fixtures provide
the original requested PID rather than leaving it unknown.

The first seven additions failed against the compiled prior source. The subsequent
two write-boundary cases failed before the final guards were added. Passing checks
are offline proofs over owned recording storage, not proof of retail stability,
physical drawing or input, Steam co-op, all maps, or the super Easter egg.

Source review and compiled candidates remain separate from installed products and
the playable backup. Full Apparition callback parity, verified empty lobby slots,
and private weapon ammo transport remain incomplete. The subsequent
[committed preferences candidate](committed-preferences-reference-audit.md)
restores the 23 supported SaveMenuTheme settings under a verified local account
without persisting drafts or old authority receipts. That memory repository ends
with the backend process; source engine lifetime across a backend restart remains
an appearance gap.
