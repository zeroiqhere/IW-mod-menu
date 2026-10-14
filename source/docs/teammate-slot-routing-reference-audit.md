# Verified player routing across all four slots

The personal production path already uses the bound local Godmode service. This
wave removes remaining restrictions that treated only slots 1–3 as teammates.
A verified local actor can occupy any slot; a different actor in slot 0 now uses
the ordinary strict teammate reader and retained teammate writer.

The saved external menu, installed DLL package and disabled launcher remain
unchanged. This is an offline candidate, not retail or co-op acceptance.

## Source and validation

The primary source audit captured Apparition commit
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c` and Aurora IW7 commit
`1b76f04e55b7e01ffb9d5a6fd9053585beb5ba27`. Apparition's Players declaration
iterates every player. Its access-level checks remain separate from local
account membership; extending the slot domain does not establish that BO3
access policy has an IW counterpart.

Only the strict reader's range changes to 0–3. Slot 0 follows the existing
entity/client/server pointer-table path with the same offsets, record spans,
map-transition checks, two client names, server backlink and GUID checks,
repeated full identity checks, command time and living-health admission. The
selected slot-zero server record is the first pointer-table entry naturally;
no contiguous-array fallback or unchecked anchor shortcut is added.

## Action and GUI mapping

The local-first selection branch still requires a fresh complete binding that
matches the original personal owner. Every remaining roster entry reaches
strict teammate capture and the existing shared target reservations. Successful
selection publishes only its captured slot, name and identity. Stale names,
changed identities, invalid slots and failed capture cannot borrow that writer.

The roster links every supported slot to the correct personal or selected-player
page and shows its detected name without an added ordinal suffix. The selected
slot-zero row now uses the actual teammate capability and confirmed value.
Preview, disconnected, busy and cleanup restrictions still apply. A roster
link is only a selection request, not permission to write flags.

The per-connection reservation and adapter caches are unchanged. A retained
writer cannot transfer between personal and teammate owners, including after
confirmed OFF. Independent actors keep independent baselines and cleanup.

## Regression evidence

Separate reader, row and actual-session regressions cover all four local-slot
routes, foreign slot-zero capture, ON/OFF/ON, separate personal flags, both
reservation orders, invalid payloads, same-name identity replacement and failed
restore/retry. The new cases first compiled and failed against exact preceding
production inputs. Existing registrations remain in the full suites.

The current source uses the same native protocol and renderer. The 233 ordinary,
14 callback, 8 game-boolean, 12 palette and 16 instruction packets exported from
the updated backend remain byte-identical to the preceding accepted packets.
Their existing native/CPU proof is inherited only after checking exact source,
fixture and artifact identities. Those packets do not cover the newly enabled
slot-zero row; managed session/row checks cover its behavior, and physical
rendering remains for later in-game testing.

## Remaining work

No injected game, GPU fixture or process-memory operation is used in this wave.
Teammate cleanup still depends on the strict living reader; death and transition
failure can leave cleanup unconfirmed. Teammate close admission also still occurs
when its cleanup phase begins, after earlier cleanup joins. These are existing
limitations and are not presented as repaired by a slot-domain change.

Full Apparition declaration and appearance parity, source scheduling and theme
fade history, per-player private ammo binding, remaining gameplay callbacks,
physical input/rendering, normal-Steam co-op, all-map and super Easter egg
acceptance remain incomplete. The full goal remains active.
