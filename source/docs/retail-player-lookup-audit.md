# Retail player lookup and engine state

This checkpoint corrects the teammate reader's server-record lookup using bounded,
read-only captures from the normal Steam executable. It also establishes the
engine health predicate and the named native `sessionstate` field. It does not
activate Quick opening, install a new DLL, or enable new gameplay actions.

## Selected server records

The captured `SV_GameMP_GetGuid` player branch reads the selected pointer from
the image-relative server table at `0x6B22950 + slot * 8`, then adds `0x9F340`
to that record. The preceding reader instead added a fixed record stride to the
first pointer. That arithmetic could reject a valid separated record or accept
a prepared lookalike while ignoring an invalid selected pointer.

`PlayerStateReader` now follows and checks the selected table pointer. It retains
the first record as the existing shared session anchor and stores the selected
record separately in `PlayerIdentity.ServerClient`. A changed selected pointer
therefore changes identity even when the name, GUID and other links remain equal.
The farthest required field is checked before the record is read. The reader
still supports teammate slots 1–3 and retains its count, transition, connection,
name, GUID, entity backlink, movement and living-state checks.

Eight regression registrations use fixture-owned memory. Four failed against
the preceding lookup and all eight pass with the correction. The cases cover
noncontiguous records, fabricated contiguous records, invalid and truncated
pointers, pointer replacement, session transitions and the retained state guards.
No test opens or writes a game process. This correction has not been exercised
against a spawned teammate and is not proof that teammate toggling is fixed live.

## Engine life and script session state

The exact captured executable fingerprint is
`a883ee28ee9f552c63d8ed1e352f60501b2e256d8857ca178c6dd3aae622723b`.
The capture validates loaded PE metadata, process creation identity and repeated
matching bounded reads. It does not call any captured address.

The original retail builtin dispatch uses `builtinId - 1` in its function table.
The captured entry for ID `0xFE` points to the observed `isalive` body, with
neighboring `abs`, `acos` and `isarray` entries agreeing with the pinned
[primary builtin catalog](https://github.com/xensik/gsc-tool/blob/05d212f5059f7e2b08d95be3e083eb1d6bb83ffc/src/gsc/engine/iw7_func.cpp).
The VM argument must be a current entity object. Its class-zero reference reaches
the native entity array with stride `0x3F8`; the body tests signed health at
entity offset `0x1E0` greater than zero. It publishes through VM state and is not
a directly callable `bool(gentity*)` function.

The original client descriptor at index 2 resolves to the string `sessionstate`.
Its getter reads client offset `0x4A94` and publishes these exact decoded strings:

| Native value | Script string |
| --- | --- |
| 0 | `playing` |
| 1 | `dead` |
| 2 | `spectator` |
| 3 | `intermission` |

Other values publish no result. The setter independently maps the same four
string handles back to those native values. Names were decoded using the saved
string-conversion implementation and bounded table reads, without calling it.
The class-zero field path follows entity client offset `0x170`. The original
registration loop's name-to-encoded-field-ID edge remains uncaptured; the named
descriptor, getter, native offset and string meanings are directly observed.

Apparition's [source wrapper](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/utilities.gsc)
adds defined session state and spectator exclusion to engine `IsAlive`.
For a valid known entity and recognized session state, health 1 passes that engine
test; a spectator fails the wrapper. A `dead` or `intermission` label alone does
not make the wrapper false when health remains positive. These observations do
not relax the stricter teammate writer admission checks. Unreadable fields,
unknown identity and reused entities must not be classified as authenticated dead.

## Remaining runtime binding

`Live_GetXuid` uses a controller index. `Session_GetXuid` uses a session client
index and returns zero for an unregistered member. The captured server GUID
getter uses a server-player index. These are different index domains.

The [primary declarations](https://github.com/auroramod/iw7-mod/blob/c0a1c6dacd33c86320b308656d9ba895291c2198/src/client/game/symbols.hpp)
and script/UI dump supplied the active-controller and server-session leads.
The later [controller-binding investigation](retail-controller-binding-audit.md)
now establishes those code edges and the retail member-to-server relationship,
with a stable linked solo observation and a compiled production-reader match.
The production provider remains unfinished. Numeric GUID equality with Steam64
is not required.

The local observation factory remains absent. Authenticated local selection,
coherent generation and shutdown handling, menu permission, controls state and
personal feature targeting remain required before Quick can be activated.
The complete Apparition port, maps, quests and super Easter Egg remain unfinished.
