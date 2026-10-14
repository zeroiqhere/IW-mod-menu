# Retail account and local-player binding

The read-only runtime investigation now establishes an engine route from the
current controller account to a registered session member and its server record.
This evidence applies to the inspected executable fingerprint
`a883ee28ee9f552c63d8ed1e352f60501b2e256d8857ca178c6dd3aae622723b`.
It does not activate an installed menu, grant gameplay permission, or prove the
complete Apparition port ready for testing.

## Controller and session selection

The named `GetFirstActiveController` binding reaches a leaf reading the current
controller index at image RVA `0x2204464`. `GetMaxControllerCount` publishes one
on this PC build. The account getter reads the nonzero qword at image RVA
`0x779C7A8 + controller * 0x70`; it does not check the index itself.

The PC `HasActiveLocalClient` and `IsAnyLocalClientActive` helpers return constant
successful results through their wrappers. They do not establish a spawned,
connected, or living player and cannot replace map and identity checks.

`SV_MainMP_GetServerLobby` reads its selector through a pointer at image RVA
`0x6005468`, then a byte at pointer offset `0x10`. A nonzero byte selects image
RVA `0x6B50B30`; zero selects `0x6B4E080`. The setting's primary name remains
unclassified. Its exact byte-load and branch suffice to select the actual buffer
without guessing the setting's purpose or calling the game function.

`Session_GetXuid` checks the registered byte at `session + member * 0x38 + 0x60`
and reads its account qword at `+0x68`. The original registration code uses
eighteen entries. A local join requires exactly one registered match to the
current nonzero controller account, with a member inside the supported server
player range. Empty and duplicate matches cannot choose a fallback slot.

## Retail member-to-server relationship

The original routine at `0x140C531E0` spans seven contiguous unwind ranges,
ending at `0x140C5351A`. Its entry keeps the supplied server record in RSI and
selects the actual server session into R14. It computes a server index from
`(serverRecord - firstServerRecord) / 715416`, then uses the same index for
`Session_GetXuid` and registration argument five. The same server record supplies
the remote address. The magic signed division agrees with the asserted
[`client_t` size](https://github.com/auroramod/iw7-mod/blob/1b76f04e55b7e01ffb9d5a6fd9053585beb5ba27/src/client/game/structs.hpp).

The registration routine spans five contiguous unwind ranges, 545 bytes. It
preserves argument five as the target member, computes `session + member * 0x38`,
and writes that target's registered/account fields. Duplicate removal operates
on old entries rather than remapping the requested target. Neither routine is
called by the diagnostic or reader.

A local reader must still follow the selected pointer table entry, compare it
with the retail index arithmetic, and validate the full entity, client, server
backlinks, indices, names and bounded GUID. The separate teammate reader remains
valid for noncontiguous selected records and keeps its stronger writer guards.
The new account join must reject a record layout inconsistent with its own
observed retail registration rule.

## Live evidence and limits

One stable read-only snapshot in the normal Steam solo Zombies process found
controller zero, one matching registered member at index zero, an active map,
clear transition flags, matching linked indices and names, and the server entity
backlink. The registered member was selected by account equality before reading
its server actor. No account identifier or player label is published in the
report. The diagnostic performs no injection, game write, engine call or Steam
call.

Bounded code captures were repeated and matched. Executable-region searches only
located candidate call sites; the large main code region changed between search
passes, so those search hashes are not stable code proof. Each used call site was
subsequently captured and decoded separately with matching repeated bytes.

Identity is separate from engine life, menu ownership, control state and feature
permission. A read-only binding result alone does not authorize an action or
prove that stale observations survive a map change. Production provider and
feature integration remain unfinished; the installed package remains disabled.

## Reader and life regression results

`LocalAccountSessionReader` now performs the account join and linked identity
checks. It requires the original nonzero session-handle byte at `session + 0x10`
and rereads every observed field, including unregistered entries that could
become duplicate matches. It does not read health or grant menu access. The
owning transport must still authenticate the executable and process lifetime.

Eleven grouped reader cases failed against the inert scaffold and pass with the
implementation. Review exposed an additional session-handle regression: ten
passed and one failed before the handle guard, then all eleven passed after it.
Four native life cases reproduce the pinned source wrapper, including downed
health one and positive health with dead/intermission labels. Unsupported native
session values remain Unknown. The complete backend suite passes 477 checks;
the production Windows .NET 8 backend publishes successfully.

An isolated compiled fixture ran the actual reader against the original normal
Steam process. Two complete binding results matched; separately repeated life
fields classified the bound actor Alive. It used 180 bounded reads and performed
no game writes, injection, engine calls or Steam calls. This is a local read-only
proof, not a teammate, feature-toggle, match-exit or all-map acceptance test.

## Menu ownership and session host

Pinned Apparition initializes `accessLevel` as its own state; `HasMenu` means
verification greater than None. Its control-disable variable is also mod-owned.
A native implementation must initialize and retain equivalent policy rather than
reading nonexistent BO3 menu variables from IW memory.

The original `Session_IsHost` selects a party matching the actual session. An
active party compares the member to the observed host-index field at offset
`0x2C50`; otherwise the original fallback tests member zero. Both operations
require the authenticated member/session relationship. The published primary
host-number layout disagrees with that captured offset, so its field name is not
borrowed. One repeated solo observation has an active matching party and host
member zero matching the selected account. That establishes the named session
host result without calling the function. The later
[production observation integration](production-observation-reference-audit.md)
now implements the exact session-field replica and menu-owned host access policy.
It does not claim complete script-method equivalence or new gameplay support.

The separate script `ishost` method candidate also has an unnamed override branch
and still-unverified original method dispatch attribution. It is not directly
called or used to claim a complete VM method port. Neither these host observations
nor the successful local binding activates unavailable gameplay callbacks.
