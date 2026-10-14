# Verified local actor Godmode candidate

This is a historical checkpoint. The current production path already uses the
bound personal service; the later [player-routing audit](teammate-slot-routing-reference-audit.md)
records the current local-first routing contract.

This checkpoint implements the personal flag owner and its view contract. The
production action path is not migrated yet. No current game bytes were changed,
no DLL was injected and no new package was installed. The original Steam account,
current saved backup and installed disable marker are preserved.

## Account and feature admission

`LocalActorFlagsReader` composes the verified production actor reader before
reading command time, movement and flags. It accepts only session state zero,
health 2 through 10000, normal movement and nonnegative command time. Every local
slot comes from the full account/member/actor proof; slot zero is not assumed.
The complete actor must match before and after the copied feature fields, with
an additional final scalar consistency pass. Callback-owned buffers are copied.

`LocalOwnedActorFlagsReader` preserves the same complete identity proof and field
rechecks but allows dead, nonplaying, changed-movement and unknown-life snapshots
for an already bound cleanup owner. Its snapshot exposes enable eligibility
separately. These copied cleanup facts alone authorize neither an ON nor a writer.
The stricter teammate reader and the general native life predicate are unchanged.

## Writer and ownership

`BoundLocalActorFlagsTransport` retains the complete account/session binding and
observed command time. Binding or time replacement, failed reads and writer-side
validation exceptions permanently retire this writer. Original process lifetime
remains separate from actor validity, so retirement cannot pretend that pending
restoration vanished with a process exit.

Only bit zero may change. New ON additionally requires fresh normal play; owned
OFF retains exact flag/identity checks after death. The native factory rechecks
selected flags while binding and repeats the full identity, command-time and
flag proof inside its existing native access lock before the checked write. It
uses the same fingerprinted read-only connection as observation and the existing
checked process writer. These repeated reads and expected-byte comparisons are
not atomic against game threads and do not establish an engine spawn generation.
Native admission branches were reviewed and compiled; this checkpoint does not
claim a successful live native feature write or restoration.

`LocalGodmodeSession` owns one full binding for one connection and uses the
existing flag feature and receipt observer. It rejects foreign protection,
records actual writer baselines, retains partially applied changes, joins the
real monitor and preserves the original cleanup owner through failure/retry.
OFF and close use the retained owner without a new strict living read. A proven
actor retirement propagates to that owner even if later scalars recover. A
read-only retained-adapter check prevents a recovered source from advertising a
writer that was already retired. Cached frame views do not perform native reads.

Normal active ownership includes a restoration lease: `HasPendingCleanup` can
be true during a confirmed ON. Production integration must distinguish that lease
from failed inactive cleanup. An unknown failed change needs an explicit cleanup
route rather than a permanently disabled toggle alone.

## View mapping

`OverlayMenuState.PersonalGodmodeIdentity` is optional view data supplied by the
personal owner. Quick requires it to match the verified observed local identity
when supplied. A matched target can use slots zero through three; a mismatch
cannot borrow a value or action, including for slot zero. Missing target data
keeps the older slot-zero path during migration. This field does not authenticate
an account or bypass the action owner's fresh validation.

Four view/controller regressions cover all slots, mismatched targets, replacement
between display and selection, and the exact personal action for a nonzero slot.
The production session does not supply this field yet, so these checks establish
the future view contract without claiming that runtime personal routing changed.

## Evidence

The current backend suite passes 554 distinct checks on Linux and in an actual
Windows .NET 8 process under the isolated Proton prefix. The session suite passes
62 checks. Its 21 new personal service checks also pass under Windows .NET 8 using
the actual bound transport, existing flag primitive and owned fixture storage.
The production Windows backend publishes with warnings treated as errors.

Focused RED/GREEN evidence covers ten strict-reader checks, nine cleanup-reader
checks, eight bound-owner checks and the service/UI behavior. Three service peer
findings gained compiled regressions: permanent retirement propagation,
completed-close status, and capability after adapter retirement. The initial
Quick-target compile setup error is retained separately from its genuine
550-pass/three-fail behavioral RED and subsequent 554-pass GREEN.

The first final Windows guest run completed all 554 checks but its private Steam
shim lingered. Only that confirmed isolated AppId-zero shim was terminated, with
launcher exit 241 preserved. The installed Proton script was inspected, and a
repeat through its `runinprefix` branch completed the same 554 checks with exit
zero. The separate 21-case service run used that branch and also exited zero.
These test launches did not use the production Steam prefix or account settings.

Evidence is retained under
`$HOME/.cache/iw-apparition-tests-20261008-personal-actor-flags`. Earlier active
Steam-prefix read failures and the isolated five-case native memory proof remain
in the preceding observation checkpoint; this candidate does not resolve that
active-prefix access condition or replace its evidence with an isolated success.

## Required integration

Replace the production fixed-slot health owner with this retained personal
service, including shutdown restoration and cached capability publication.
Arbitrate local and teammate ownership across the whole connection in both
acquisition orders, retaining target reservations through unresolved cleanup.
Remove remaining slot-zero self routing, expose failed-change cleanup, and connect
owner-bound observations to the native Main/Quick opening monitor and protocol.

Continue the full Apparition callback port, private per-player ammo, lobby/access
behavior and map/quest controls. Runtime original-Steam multiplayer, all maps,
super-EE progression, protection coverage and exact match-exit build acceptance
remain unproved. This candidate is not the completed 1:1 menu.
