# All Players God Mode source integration

This source candidate connects the source All Players **God Mode** row to the
connection's existing personal and teammate flag owners. The native row receives
only copied display state. A private, nonserialized target binds each command to
one admitted complete cohort and full local Steam account binding.

## Source behavior

The current Apparition main commit is
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`. Its
[allplayers.gsc](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Functions/allplayers.gsc)
turns everyone OFF when every participant is already ON. Otherwise it enables
the missing participants. Mixed owned state therefore displays OFF for the
source all-ON checkbox. The existing IW lobby ammo extension remains separate.

## Admission and ownership

- A complete four-slot observation must contain the authenticated local actor
  and exactly match its process, session anchors and retained account binding
- Fresh local actor and current host proof bracket every cohort observation
- Unknown slots cannot become absent slots through name, capacity or health guesses
- New ON and rollback ON require a living current host; original owned OFF cleanup
  remains available through the connection's independent cleanup route
- Successful same-cohort observations retain the exact private row target
- Busy observations retain that row while showing UNKNOWN and disabling input;
  a confirmed ineligible OFF remains visible with its action disabled
- A failed proof withdraws that target; recovery creates a new target
- A rejected stale or foreign request leaves a current valid target intact
- Group and individual actions use the same registry and original flag adapters

The registry supplies an immutable metadata receipt for each observed snapshot.
Other shared operations advance its epoch, even if their final flags match an
earlier value. Publication also checks that the copied snapshot and its metadata
describe the same owner state. Frames compare only cached owner facts and never invoke the new
cohort reader, actor reader, identity validator or feature preflight. Source reads
run in retained tasks. Shutdown disarms admission, joins those tasks, then joins
the common owners before releasing the original transport. Failed joins retain
the transport for cleanup retry. Terminal close invalidates cache receipts even
when their epoch is exhausted.

## Evidence and limits

Tests are organized under `tests/features/Godmode`,
`tests/session/Session/Cohort`, `tests/session/Session/CohortIntegration`, and
`tests/backend/Controller/CohortGodmode`. Actual controller packets also pass
through the portable native runtime and preview readers. The wave evidence
directory records evaluated build inputs, real failing regressions, passing
results, output hashes and independent review.

These checks use owned fixture state. They do not prove retail vacant-slot
semantics, graphics, physical input, co-op or quest behavior. The current retail
reader does not certify FREE or null slots as absent. A partial real lobby may
therefore remain unavailable even when its individual player controls work.
This candidate is source-only and is not installed over the saved playable tool.

Full Apparition gameplay, personal infinite ammo, all maps, Easter eggs and the
super Easter egg remain requirements of the active goal. Unsupported source
callbacks remain unavailable until their actual backend behavior is implemented
and verified.
