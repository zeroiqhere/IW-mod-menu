# Family navigation and Basic Scripts reference audit

Audit date: 2026-10-08. Reference: Apparition commit
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`.
The full retail IW port remains active. This update corrects production navigation
and migrates the real Basic Scripts view; it does not add the unavailable game
callbacks or activate Quick opening.

## Navigation contract

Apparition keeps independent Main and Quick current pages and parent stacks.
Parents contain page names. Cursor state is shared by exact page name, so Back
reads the newest cursor for the returning page instead of a cursor captured
earlier in its path. Close preserves both histories. Source slider storage is also
shared by page and row; its narrow default-clear behavior is outside this update.

Production `OverlayNavigation` now retains those independent paths.
`Open(MenuSurface.Main)` and `Open(MenuSurface.Quick)` restore their respective
page and shared cursor. Existing `Open()` requests Main. The Quick root enum is
appended without renumbering existing pages. The controller and native input
router still open Main only; this API is not Quick gameplay admission.

An already open family cannot be switched by a second Open request. Same-family
Open is a no-op. Closed-family switches clear dispatchable current rows after
checked epoch preflight. Page and Back transitions retain prior immutable
published rows but reject their selection until fresh rows are published.
Fresh rows clamp the cursor and skip typed blanks while keeping unavailable help
inspectable. Counter failures leave the original owner, path and rows unchanged.

The source opening monitor admits a family only while both are closed. Explicit
cross-family API requests are therefore a local preparation boundary, not evidence
of a source runtime opening route. No player selection or life/permission fact
is fabricated.

## Actual Basic Scripts view

The pinned [basic.gsc](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Functions/basic.gsc)
declares 31 rows in fixed order for a valid selected player. The real Personal
page now follows that order. The first label is God Mode and retains the existing
personal capability, preview/disconnected/busy value guards and action handler.
Its help identifies personal scope.

The other 30 rows show UNAVAILABLE, are disabled and carry no action intent.
They do not show invented ON/OFF or applied slider choices. Submenus are also
inactive until their real IW data and callbacks exist. This view migration is
not implementation of those features.

Unlimited Ammo on this page is explicitly per-player and unavailable. It cannot
borrow the working lobby patch; All Players Menu Infinite Ammo remains a separate
command affecting everyone. The source grenade token is not exposed as product
syntax: Bind Noclip To Lethal Action is a binding-neutral caption, with no guessed
key. All other source labels keep their declaration order. Unavailable help uses
player-visible scope rather than memory or callback implementation terms.

The source choices, numeric ranges, dynamic crosshair values and missing IW
equivalents are recorded in the accompanying source audit. They are not represented
as interactive choices before callbacks exist. BO3 assets such as Gobblegums
remain visibly unavailable; their IW equivalents need separate implementation.

## TDD and integration

Two real-controller regressions first failed against the immutable preceding
source: nested page reuse and close/reopen returned row 1 instead of the latest
shared row 4. Twelve navigation cases recorded a compiled 4-pass/8-fail RED before
GREEN, with 11 retained boundary cases also passing independently.

Eight Basic Scripts cases recorded compiled 0-pass/8-fail RED before GREEN.
Caption and product-help regressions strengthened those same cases; they do not
increase registration counts. The cases check exact ordering, retained Godmode
guards, inert unsupported rows, private/lobby separation, read-only copies and
disabled selection.

Combined compilation caught exporter assertions referencing action and detail
fields absent from the display-only StateRow. Wire assertions now check display
state; actual source rows establish intent and help. This compile failure is
an integration-test setup correction, not a behavioral RED or wire-schema change.
One retained counter fixture selected stale rows immediately after a page change.
It now republishes the same child rows first, preserving the original counter
boundary and failure/rollback assertions.

Three new actual-controller profiles show Back before/after the shared-cursor
return and the fresh reopened child. The top-level exporter owns the combined
217-profile inventory; individual older exporters retain their own totals.
Native and CPU joins enumerate that full producer set. Canonical preview-order
inventories include the corresponding three images.

## Confirmed results

Root confirms **1394 distinct registered checks**: preceding 1371 plus 12
navigation, eight Basic Scripts, two controller and one actual-export case.
All 441 backend checks pass. The 23 new cases also pass under Windows .NET 8.0.12.
Windows native 171 and portable native 183 cases consume all 217 actual exports.
Both CPU build configurations pass 152 production-panel checks. All four preview-order
cases pass across forward/reverse/relocated input sets. Platform reruns, RED variants,
exported packets and preview images are not extra registrations.

The three new parent-history profiles bring the actual producer inventory to 217
and the preview inventory to 277. Exactly 213 preceding packets remain byte-identical;
Basic Scripts is the only changed preceding packet. The renderer source is
unchanged. A same-binary control reproduces all 274 old-corpus images pixel-exactly.
With the expanded corpus, 211 preceding images remain pixel-exact; Basic Scripts
changes intentionally, and 62 other images differ by at most 34 pixels each.

Matching optimization settings did not remove those minor differences. The
same-binary old-corpus control establishes input-corpus dependence. Shared glyph
atlas warmup and bilinear sampling are the inferred cause, supported by the
exporter's shared font context and unchanged renderer source. The existing local
glyph-coverage comparisons against fresh contexts pass for all 217 profiles; their
erased-message regression still rejects missing text. No tolerance was widened.
The matched unoptimized previews are packaged, and the Release comparisons remain
explicit evidence rather than a claim of byte or pixel identity.

The renderer, loader, graphics hook lifetime, IPC production schema, feature
writers, offsets and Steam identity code are unchanged. Native production binaries
retain their sealed bytes; changed test inventory adapters are rebuilt.
The strict Windows backend publication succeeds. CPU rendering and Windows/portable
parser joins verify source display coherence, not retail graphics safety.

## Remaining work

Quick admission still needs validated local identity, engine life, menu permission,
controls state and justified freshness. Its catalogue/timeline/controller callbacks
and native route still need integration. The production observation provider remains
absent and Unknown. The 30 Basic Scripts callbacks, wider game families, roles,
persistence and IW maps/quests/super EE remain required full-port work.

The user reports successful match exit but is unsure which build was loaded.
That report remains recorded; it does not identify this update as tested or
establish the earlier fatal-error cause. Installed files and the existing disable
marker remain unchanged. No game process, injection or live feature write was
used for this checkpoint.
