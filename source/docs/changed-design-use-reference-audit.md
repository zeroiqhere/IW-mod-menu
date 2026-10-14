# Held Use during a Design rebuild

A connected typed Design selection now retains its exact admitted Use receipt
through the existing redraw clock. The controller rebuilds the HUD at 50 ms and
releases the plain caller at 250 ms. Held Use can then select the already applied
value through its normal no-op path, without another redraw. Row fades continue
independently of caller readiness.

## Source and scope

Apparition commit `307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`
[captures the slider and starts its handler thread](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L75),
then waits 200 ms before the outer 50 ms monitor wait. The
[Design handler commits the selection and rebuilds the menu](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L450).
The menu opening waits 50 ms before constructing HUD elements; row fades start
as separate threads. The monitor does not end on `menuClosed`. Current source
files match the pinned downloads byte for byte.

These statements establish nominal independent waits and retained logical
selection. They do not prove exact sibling-thread scheduling in the source VM.
The portable controller uses its existing deterministic 50/250 ms model.

## One clock and one caller

Previously, an active Design transition excluded the receipt from publication,
so native input discarded the held invocation. The Design state now links its
existing clock to the admitted callback revision. The plain callback view is
derived from that link and the validated receipt; it has no second timer or
stored duplicate callback state. Normal clock completion releases only that
caller. Counter, clock and redraw admission precede the appearance mutation.

Cursor, topology, navigation, actor, life, focus and shutdown changes withdraw
the receipt. Rollback preserves a finite remaining wait and does not redraw an
already constructed HUD again. Preview and actorless paths retain their legacy
contract. Both production readers require an exact plain cooldown callback and
matching receipt whenever Design shares its wait with Use; missing halves,
wrong kinds and revisions remain invalid.

## Regression evidence

Before implementation, the five new controller checks failed. The expanded old
producer failed six managed checks and two of nine native graph checks: the
changed-Design receipt was missing and held Use could not repeat at 250 ms.
The final candidate passes all 971 backend checks. An intermediate native test
incorrectly required a fixed opening revision; that rejected diagnostic is
preserved separately as a reconstructed transcript. The final test captures
the observed revision and verifies stable ownership as elapsed time advances.

The actual producer exports 69 packets and 14 captured Select commands. The
original 56 packet files remain byte-identical. The graph exercises both
production readers and native input using actual controller frames and captured
successful command writes. Independent clean build results and artifact hashes
are recorded in the checkpoint evidence.

These checks establish offline controller, protocol and input behavior. They
do not establish retail Godmode OFF restoration, personal ammo, teammate or
aggregate authority, physical input, injection lifetime, all-map or Super EE
readiness. Installed products and playable backups remain separate.
