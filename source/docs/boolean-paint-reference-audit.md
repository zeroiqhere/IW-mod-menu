# Boolean menu display lifetime

This wave preserves Apparition's copied boolean HUD sample independently from
current game feature observations and permission. The official main ref and
pinned files were fetched with curl on October 9, 2026. Main still matched
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`; the audited base, menu and basic
files matched the pinned source bytes.

In [Menu/base.gsc](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc),
`addOptBool` samples `Is_True(boolVar)` into `OPT_BOOL` (1295–1314).
`createOption` consumes that copied value when creating its boolean HUD
(413–453). Same-window scrolling reads the copied option; it does not re-read
the feature. The monitor starts the callback thread, waits 180 ms, then redraws
through `RefreshMenu`, `drawText`, `DestroyOpts` and `RunMenuOptions`. That actual
redraw samples the current feature once. Later callback completion and the
430 ms monitor retirement do not request another redraw.

The IW controller captures eligible personal and selected-teammate Godmode rows
at their actual visible draw. Main's 50 ms rebuild captures on its first visible
frame, including a retained gameplay page. Quick captures immediately while its
opening cooldown and fade remain independent. The private scope includes the
admitted actor owner, menu family, page, presentation epoch and complete row
identities. Cursor movement, heartbeat and worker completion retain the sample;
actual redraw, changed topology, reopening and actor replacement retire it.

`StateRow.Value` remains the current confirmed value or UNKNOWN. `Enabled`,
queued action admission and feature ownership never consume cached paint.
The optional `menuBooleanPaint` field has exactly positive unsigned `revision`
and nonempty ordered `rows`; each row has exactly `index` and `value`.
Indices must be unique, ascending, within 0–127 and target existing nonspacer
ON/OFF/UNKNOWN rows. Ordinary visible menus require a supplied menu epoch;
opening cooldown, row fade and option callback stages may retain paint.
Editors, hidden rebuild, credits, unlock and preference/design transitions
cannot publish it. Both native readers share the strict parser. Omitted metadata
keeps legacy behavior; supplied null and malformed shape are rejected.

The display resolver feeds boxes, boolean text, text color, glyph preparation
and row ink. Copied requests, frames and private scroll receipts carry paint
identity so an old frame cannot paint a replacement sample. A current UNKNOWN
always remains visibly UNKNOWN, regardless of a cached known value. This is an
explicit native safety extension; the source has no UNKNOWN equivalent. When a
known current value differs from the retained display, the selected-row notice
puts that current result first so even a full-length status cannot hide it.

Dispatch preflights the callback and future paint/menu epochs before gameplay
work. Early structural changes preserve room for the scheduled invalidation and
possible topology commit, even when life changes remove every eligible boolean.
Counter failures reject before row/cache commits; rejected queued work cannot
replay. Clock recovery keeps the finite pending stage and does not resample a
committed display. No actor pointer or private account identity reaches the
public descriptor. Focus hides public paint while retaining the owner cache.

## Evidence and remaining scope

Tests drive the real controller with an inert connected session, controlled
time and separate current results. The eight producer packets include ON
current state with retained OFF/UNKNOWN display, exact redraw timing, late work,
scrolling, reopening and focus hiding. The two native readers consume those
unmodified packets. CPU tests compare production pixels against an independent
effective-value oracle across all five designs and three boolean styles.
Genuine compiled RED runs cover the original missing cache, delayed Main
capture, hidden disclosure and exhausted epoch paths before their fixes.

This is not full Apparition equivalence or a game-ready release. Private ammo
transport, remaining local callback waits, unavailable engine callbacks, exact
fonts/origins and final visual/physical-input fidelity still need completion.
The later [local caller wait wave](local-caller-waits-reference-audit.md) integrates
palette/slider/Reset timing and an inert Continuous/Reload/Disable refill plan. Actual injected binding/gameplay, cooperative sessions,
all maps and the super Easter egg remain unproven. The installed disabled DLL
and saved playable backup are separate and unchanged. This wave performs no
game injection, process-memory work, Steam API calls or GPU execution.
