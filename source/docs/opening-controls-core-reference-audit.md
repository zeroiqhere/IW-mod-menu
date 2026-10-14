# Main opening-controls foundation

The pinned Apparition source declares thirteen ordered logical actions: action
slots 1–4, Melee, Aim, Attack, Sprint, Use, Frag, Secondary Offhand, Stance and
Jump. Its main default is Aim plus Melee. A chord has one to three consecutive
slots; duplicates, including reselecting the current action, reject. None in
slot 2 or 3 truncates that slot and later slots. Frag and Secondary Offhand cannot
coexist, although replacing one with the other in the same slot is allowed.

The new `backend/OpenControls` core contains immutable action tuples, explicit
capability sets, independent target/slot drafts and checked positive revisions.
It copies bounded inputs and validates the whole candidate before publishing a
new owner. Rejected changes return the old owner. None at the first unused
consecutive slot is an accepted no-op and retains the revision. Exhausted
revisions reject tuple changes without partial mutation.

Quick Menu is a staged target only. Applying it rejects with UnsupportedTarget;
the main chord cannot silently become a Quick Menu chord. The product Open
Controls row remains UNAVAILABLE because controller, native input and private
IPC integration are not present. This checkpoint does not add a working editor.

## Proof and remaining integration

Eleven registered cases cover the source action inventory, capabilities, copied
ownership, default availability, slot mutation, None, offhand conflicts, stale
revisions, exhausted revisions and independent selectors. The initial missing-API
run fails compilation; the compiling scaffold fails ten functional cases. The
finished core passes the full 334-case managed suite. Independent peer review
finds no blocking pure-core issue, and all eighteen owned file hashes match.

Capabilities currently record the caller's assertion; they do not discover keys.
Expected revisions refer to the supplied immutable owner, not a shared current
store. Integration must publish the active owner and its input descriptor
coherently, using exact private saved-binding groups. Missing groups must keep
actions unavailable. Raw keys must remain private.

The source action list has a blank spacer that navigation must explicitly skip.
Disabled capability rows must remain selectable for help. A future typed refresh
cause must publish a committed input chord immediately while retaining old row
booleans until the actual deferred redraw. Focus changes, new policies and
acknowledgements must reseed held keys instead of replaying an old press.

Alive/dead source gates, Quick Menu's separate cursor/catalog, saved XUID dvars,
physical key discovery and retail input acceptance remain unimplemented. A
connected process alone does not prove that the player is alive. No gameplay
offset, writer, VM call, hook-lifecycle change or installed deployment is added.

Fresh official source copies match the pinned reference. [Action declarations
and defaults](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc)
and [slot mutation rules](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L1205)
are the primary sources.
