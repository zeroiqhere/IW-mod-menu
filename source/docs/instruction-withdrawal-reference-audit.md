# Instruction footer during uncertain local observations

The independent instruction footer must retain its creation and accepted color
history when the same player temporarily has unknown life state. Drawing and
input authority remain withdrawn. A later known observation must not recreate
the footer, recapture its theme, or restart its fades merely because observation
was unavailable.

## Source contract

Fresh official source fetched with curl on 2026-10-09 still matches commit
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`.
[The instruction loop](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/main.gsc#L372)
owns its widgets separately from menu chrome. Its destruction causes include
disabled instructions, lost menu permission, the first actual dead episode,
explicit force refresh, and loop exit. Ordinary menu close and row redraw do not
destroy this independent panel.

[The source life predicate](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/utilities.gsc#L1296)
has a binary engine result. The port's `Unknown` observation does not prove that
predicate returned false. Preserving private state during uncertainty is an
observation adaptation; it does not prove what happened during missed engine
ticks or unobserved life changes.

## Ownership and publication

Retention is restricted to the existing footer owner, a controller-validated
current process, positive menu permission, enabled instructions, and no stop or
cleanup retirement. It keeps the last known life, creation identity, captured
design/style/background/accent/Rainbow facts, pending force cause, and matching
private instruction theme journal.

The current packet still publishes explicit `instructionHud` absence and omits
`instructionTheme`. This prevents native legacy footer fallback and supplies no
drawable or actionable player proof. It does not grant old admission authority.
Known return updates the admission revision while retaining creation facts and
immutable event bodies. Event ages are measured from original invocation time.

The existing footer absence shape withdraws drawing. It differs from the newer
menu chrome absence descriptor, which retires a creation. Omitting the footer
descriptor would permit the existing legacy open-menu footer adapter, so omission
must not replace explicit absence for this current producer.

## Independent recreation causes

An observed Alive-to-Dead edge remains meaningful across an Unknown interval and
creates its required replacement. Dead-to-Alive and Dead-to-Dead recovery retain
the existing creation unless another cause was observed. No unseen life episode
is inferred.

A changed Design reserves a force cause until actual menu creation completes.
Recovery before that draw retains the original footer; the draw creates its one
required replacement. If the cause becomes ready during withdrawal, known return
must commit that replacement once. Alternate/background refresh and actual
disable/re-enable remain independent causes. Permission, owner or process loss
must not borrow the old creation or journal.

At the creation counter limit, uncertainty followed by same-owner known return
must not need another identity. A real next creation still fails before mutation.
State construction failure must leave staged lifetime and journal work uncommitted
so a corrected retry cannot lose or duplicate a reserved force cause.

## Native behavior and remaining acceptance

Existing native lifetime and color owners already suppress explicit absent
footers before sampling colors or advancing their histories. Graphics resource
reset and focus withdrawal retain logical ownership. Actual controller packets
must establish that managed restoration joins those retained histories through
both production and preview readers and the CPU panel painter.

Backend report-clock recovery and native presentation time are separate domains.
Actual snapshot replay keeps its native clock monotone when a backend fixture
rolls back; it does not substitute that decreasing report time for
`GetTickCount64`. Fixed fades still use the documented linear adapter.

The CPU helper checks do not prove every production renderer early-return path.
That checkpoint's renderer observes footer lifetime before graphics preparation,
but observes footer colors after those graphics gates. The later
[observation candidate](instruction-observation-reference-audit.md) addresses
that separate integration gap through the production preparation and paint phase.

Acceptance of this checkpoint needs registered failing managed regressions before
the fix, final source-bound managed and native checks, actual controller exports,
and independent logical review. Passing coverage of unchanged native production
behavior is recorded as such, without inventing native failing-before evidence.

No game test or installation is part of this wave. Exact engine fade curves,
Rainbow phase, 10 ms coroutine timing, remaining widgets and gameplay callbacks,
private ammo transport, complete-cohort actions, normal Steam co-op, all-map quests
and the super Easter egg, and physical injected rendering/input remain open.
