# Shared teammate Godmode ownership

This source wave separates player selection from feature ownership. Selecting
another teammate no longer closes the previous teammate's feature. Each full
validated identity retains one adapter, and switching back reads that owner's
actual confirmed state. Selection itself uses read-only capture and never seeds
the display by issuing an OFF command.

The new portable registry supports individual changes and bounded captured
batches. A batch validates every member before writing, shares the same owners as
individual changes, and rolls back its own confirmed changes after failure or
cancellation. It never follows later joins. The reference contract and its 31
Basic Scripts entries are documented in
[the source audit](all-players-godmode-reference-audit.md).

## Production connection

`TeammateGodmodeSession` is connected to the existing checked retail reader and
flag binding for slots 1 through 3. Its selector payload carries a slot and name;
the retained full identity, original transport, and fresh reader authorize the
operation. Names alone cannot authorize a replacement player. The existing
primitives and their offsets remain unchanged.

The session closes every retained teammate owner independently. Unconfirmed
worker stopping or flag restoration keeps the original process transport for a
cleanup retry. Ammo restoration still receives an independent attempt. Published
frames retain their references once, so asynchronous cleanup cannot null a field
between its check and dereference.

Known display states undergo fresh read-only preflight. A foreign flag is UNKNOWN
and cannot be adopted, cleared, or advertised as OFF. An inactive owned change
awaiting cleanup is UNKNOWN and permits only cleanup of its original binding.
Changing selection is blocked while any retained owner needs cleanup.

## Deliberately unavailable all-player action

The retail menu does not enable the new batch endpoint. Its All Players page
keeps the working lobby ammo action and displays God Mode as UNAVAILABLE, with
directions to the existing individual controls. Personal or selected-player ON
never supplies a fabricated all-player ON value.

Slot zero's health address equals the old personal health address, but that does
not prove the local Steam player, host authority, or a private match. No new
slot-zero binding, guessed predicate, offset, writer, or automatic join handler
is added. The old personal health loop remains separate; it is not equivalent to
Apparition's damage callback. Private ammo and full map/Easter Egg parity remain
unfinished.

## Regressions and acceptance limits

The tests use recording transports and real existing flag features. They cover
shared ownership, read-only capture, prior state preservation, foreign flags,
identity replacement, failed writes, cancellation, partial cleanup, selection
changes, serialized shutdown, and immutable views. OFF rollback must not re-enable
a flag the game cleared when the OFF operation made no confirmed owned write.
An unconfirmed worker join must not return successful OFF with pending cleanup.

The real controller's sixteen preview exports pass the native parser and CPU
panel join. The updated All Players CPU preview was inspected. Native hooks,
graphics resource lifetime, and injected input controls are unchanged.

These checks do not establish atomicity against the game, an engine spawn token,
all-map retail behavior, or a fixed match-exit crash. No gameplay trial or installed
launcher update is part of this source wave.


## Confirmed source checkpoint

The integrated wave has 497 distinct checks: backend 134, feature/session 103,
native runtime 60, packaging 70, scroll 9, choice 12, Quick Exit 15, width input
15, unlock input 7, saved hints 7, animation 8, geometry 19 and CPU panel 38.
The 73 input/hint/animation checks retain their previous confirmed results for
byte-identical production and test sources; the affected feature, controller,
packaging, native runtime and CPU suites were run for this wave. Repeated runs
and the sixteen producer snapshots are not counted as extra checks.

Root confirmed 103 feature checks and 134 controller checks in authoritative main,
a full Windows backend publication and a full native cross-build of identical
main native sources. Sixteen fresh main-controller exports byte-match the set
that passed the native parser and CPU panel join. Packaging passed 70 checks;
geometry passed 19 and CPU panel passed 38. The previous timing candidate remains
sealed and the installed launcher is unchanged.

TDD caught cancellation after the final write, adapter reuse across identities,
retained cleanup after a thrown rollback, unrelated flag baseline drift, a foreign
flag falsely shown as OFF, source-cleared OFF rollback, and unconfirmed shutdown
reporting success. The corresponding regressions now pass. Concurrent view/close
checks exercise the new portable session boundary, not Windows transport release
or the full retail renderer.
