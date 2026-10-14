# Portable retail session regressions

Run `dotnet run --project tests/session/Session.Tests.csproj` from the source root.

This project links the actual retail session, player binding, feature primitives,
coordinators, and cleanup code. It excludes the native transport factory, Memory.dll,
and foreground/process APIs. Every byte, health value, and player flag belongs to
an owned recording transport; no test can attach to a game process.

The suite covers rejected and valid connections, exact process identity, queued
requests crossing shutdown, retained resources after failed cleanup, real player
flag switching, worker joining, and reconnect status. A held synchronization
context pauses only the retail continuation after real feature work completes,
then allows shutdown and a fresh connection to finish before that continuation
runs. This checks the original late selection and status races without replacing
the feature implementations. The diagnostic tests exercise the bounded shared
message history and its concurrent readers.

The local Godmode desired-state checks exercise the same retained native flag
primitive as the personal menu toggle. Repeated enable preserves owned ON;
explicit disable never becomes an enable or binds an uncaptured actor. Transition
receipts describe an accepted, confirmed write from that exact gated operation.
An observed game-cleared flag, no-op, failed write, or rejected admission cannot
produce an owned transition. Held source and binding callbacks exercise queued
requests and shutdown without timing guesses.

One connection registry now reuses that exact local service and the existing
peer adapters for individual and captured-set operations. Toggle determines its
desired state inside the same gate as batch changes. Shared cleanup joins each
registered owner once, with a fallback for a retained local adapter whose
read-only registration failed. Held callbacks check queued toggles, monitor
joining, cancellation rollback, and cached personal frame publication.

Display identity checks cannot authorize writes or completion. Fresh proof
failures withdraw the personal checkbox capability; pending peer cleanup blocks
a new personal enable and explains why in the row detail. Restoring original
owned flags remains available where its identity proof succeeds.

All Players Godmode remains disabled until a complete, freshly validated player
cohort and authoritative absent-slot proof exist. Captured fixture sets do not
prove whole-lobby detection. These offline checks do not prove retail graphics,
normal Steam co-op, or all-map quest behavior.

Shared cohort tests feed complete fixture-owned observations through the actual
connection registry for each verified local slot. They check that personal,
selected-peer and group changes reuse the same adapters and state, and that a
changed local account still rejects group writes even when membership labels
and pointers look unchanged. These fixtures do not validate retail absence or
host authority and do not enable the All Players GUI route.
