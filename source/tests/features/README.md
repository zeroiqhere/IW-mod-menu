# Feature state regressions

This suite links the current injected backend's production feature and validation
classes. Synthetic transports record writes and controlled barriers exercise
worker ownership without opening a game process.

```sh
dotnet run --project tests/features/Features.Tests.csproj -c Release
```

The health guard rejects last stand (`health == 1`), dead values, and values above
the configured living range. Freeze configurations of one or less are rejected
before any transport read or write; two is accepted as the minimum configured
living ceiling. The first two reads test startup and readback; a subsequent
scripted read tests stopping a running loop before another write.
The source evidence is IW `cp_laststand.gsc` at CODIW-Source revision
`d1a226dbd553229503290cfb73f8051ee3b34749`. Rejecting one does not establish a
complete downed-state detector, spawn generation, or cross-map lifetime guarantee.

Ammo regressions cover exact owned byte restoration, unowned patches, missing or
ambiguous candidates, changed context, readback failure, cancellation, and close.
Teammate regressions cover owned flag restoration, identity failures, foreign
changes, read-only monitoring, pending cleanup, and overlapping operations.

The Godmode registry tests under `Godmode/` share the real flag feature between
individual and captured batch actions. They cover all-member preflight, full
identity checks, cancellation, partial write rollback, prior state preservation,
immutable snapshots, and worker shutdown before handle release. A normally active
owned flag is eligible for later individual changes; inactive unconfirmed cleanup
blocks new enables and different bindings until cleanup succeeds.

Read-only capture stages a complete roster before publishing new confirmed OFF
states. It never calls Set or Close, preserves existing ownership when another
member fails, and rejects retargeting that would require cleanup. Fresh snapshots
read flag eligibility so a later foreign flag cannot keep an old OFF display.
Rollback restores ON only after a confirmed owned OFF write; a flag cleared by
the game, or a rejected write without that proof, is never reapplied.

These tests also link the selected-teammate session helper and resource-retaining
cleanup bridge under `Session/`. The production session connects those callbacks
to its existing checked reader and writer; real Windows transport and retail
acceptance remain separate. Local-player and all-player validation are incomplete. The registry introduces no offsets or additional native writer.

The cohort checks in `Godmode/Cohort/` use immutable whole-lobby observations
with explicit fixture-owned absence facts. They cover source all-ON toggle
meaning, shared individual ownership, queued admission and cancellation,
all-member preflight, advancing timestamps, membership changes during writes
and completion, prior-ON OFF rollback, and nonliving cleanup eligibility.
Capture checks reject unknown or missing slots, duplicate accounts or storage,
and a replacement connection. Synthetic ConfirmedAbsent facts do not establish
retail empty-slot semantics. The retail reader still emits only Present/Unknown;
the All Players GUI route remains unavailable.

A controlled cleared-flag regression keeps both monitors delayed after a real
two-owner ON. Group Toggle rejects the resulting stale Active/UNKNOWN member
without disabling the still-confirmed ON owner. Explicit original cleanup can
reconcile that member, after which the mixed cohort can enable again.
