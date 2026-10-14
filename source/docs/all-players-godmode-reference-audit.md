# All Players God Mode: source contract and IW batch scope

Audit date: 2026-10-08. This gameplay wave is an isolated source effort, not an
integrated or retail-tested feature. The prior 441-check timing candidate remains
preserved. This audit reads source only, changes documentation only, and makes
no live-memory read/write, injection, guessed target or gameplay acceptance claim.

Official Apparition main, fetched with curl, still resolves to commit
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`. Fresh pinned downloads of
Functions/allplayers.gsc, Functions/basic.gsc and Menu/menu.gsc match the local
reference byte-for-byte. The damage override was also fetched from that commit.

## Exact batch behavior

[AllClientsGodModeCheck and AllClientsGodMode](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Functions/allplayers.gsc#L125-L154)
operate on every current entry in `level.players`. Unlike the nearby generic
AllPlayersFunction, this handler excludes neither host nor developer nor menu
actor. All Players God Mode is a global summary of the same per-player flag used
by Basic Scripts and selected-player God Mode; it has no independent desired-state
latch or separate global feature instance.

| Condition when action runs | Handler effect |
| --- | --- |
| At least one participant flag is not true | Invoke Godmode only for those not true, producing all ON if calls complete |
| Every participant flag is true | Invoke Godmode only for those true, producing all OFF if calls complete |
| Some ON and some OFF | Enable only OFF players; existing ON players remain ON |
| Individual Godmode turned OFF afterwards | Aggregate check becomes false; it is not automatically turned back ON |
| A later player joins | No action on that player from the earlier batch; no follow-join thread or callback is installed |
| No players | The source check returns true vacuously; a native GUI should instead show unavailable/UNKNOWN |

The handler is one bounded pass over the current roster. It calls Godmode directly,
not a persistent all-player loop. The menu entry uses the check as a boolean;
therefore partial participation displays false/OFF in the source. A native MIXED
summary is a deliberate truthful extension: it exposes partial state while keeping
the action rule “if not everyone is ON, enable the captured participants”. A new
join or departure requires a new observed aggregate, not reuse of an old ON latch.

[Godmode](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Functions/basic.gsc#L118-L132)
first disables Demi-God if active, then toggles playerGodmode. The
[damage override](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/overrides.gsc#L36-L45)
returns zero damage while that flag is true. This is not evidence that IW health
10000 or a native entity flag has identical protection against every damage,
down, death, scripted kill, respawn or map transition.

## Proposed IW registry and safeguards

One actual feature instance per validated player identity can serve personal,
selected-player and aggregate views. A batch captures the current validated
participant identities and one desired value, then uses those existing instances.
Individual OFF afterwards must be visible to the aggregate as MIXED rather than
leaving a fake global ON. No future join should inherit the previous command.

This is implementation scope in progress, not a completed contract or test result:

- Read the current full identity and feature state before capture; reject absent,
  ambiguous, invalid, dead/transitioning or stale targets rather than inventing slots
- Keep one session/generation registry with captured immutable identity; a name or
  reused slot alone cannot authorize another player or a later spawn
- Revalidate identity/expected state before each operation and report per-target
  completion; one failed member must not turn the whole summary into ON or OFF
- Serialize aggregate and individual operations through the same instances and
  lifecycle owner; avoid competing loops, duplicate restore handles or leaked state
- A batch is not atomic across game entities: partial results need MIXED/UNKNOWN,
  clear failure status and independent guarded cleanup, not silent rollback promises
- Restore only owned changes. If a God bit was already set by another owner,
  restoring that original bit cannot truthfully promise actual OFF; retain the
  observed/owned/restored distinction and reject unsupported state changes
- Preserve native IPC sequence/PID/epoch/focus guards and current session teardown;
  batch actions must not broaden restoration into an unvalidated replacement player
- Keep existing writer primitives and exact fingerprint/state guards; no new offset
  arithmetic, damage callback installation or unsupported engine script bridge

The old self-health address is not proof of a local player. Offline constants give
`EntityArrayRva 0x3D22610 + EntityHealthOffset 0x1E0 = 0x3D227F0`, exactly the old
self-health RVA. This establishes the same numeric span for entity slot 0 only.
It does not identify the local Steam participant, establish host authority, or
prove the entity survived spawn/map changes. Slot0 must not be labelled Host or
used as a local identity bypass based on this arithmetic. Existing reader scope
and any required local-player mapping must be validated separately, without
assuming the roster's first row is the actor.

## Current aggregate binding boundary

A new source review confirms that the all-player handler includes every current
participant, including the host. It enables only OFF participants in a mixed
cohort and disables everyone only when all are ON. Later joins do not inherit an
earlier command. A complete server roster can perform this actorless operation
without mapping the local Steam account: every present participant needs its own
validated identity, but the handler does not need to distinguish the actor.
Local-account mapping remains necessary to unify the personal row with that same
per-player owner. It must not be imposed as a substitute for complete roster proof.

The current registry already accepts one to four full identities in slots 0–3
and implements captured batches, shared ownership, preflight checks and guarded
rollback/cleanup. The production boundary is narrower: PlayerStateReader and
TeammateGodmodeSession accept only slots 1–3, while CandidateRosterReader skips
failed or invalid reads. Its display list cannot prove all participants. Personal
Godmode remains a separate scalar health loop without the registry identity.
Unchanged pointers and GUIDs also lack an established per-spawn generation token.

The next aggregate integration needs a complete-roster session/transport seam.
Confirmed absence must differ from read failure, and any uncertain present slot
must reject the whole capture before writes. Aggregate and selected commands must
share the existing registry, with ON/OFF/MIXED/UNKNOWN derived from current cohort
state. Slot-0 binding and absence predicates remain unproved; God Mode stays
unavailable. This seam does not justify a new offset, a second writer or a claim
of BO3 damage-callback equivalence.

Practical session cases include a slot-0-inclusive cohort, unreadable slots,
mixed/all-ON behavior, individual changes after a batch, later joins, foreign
flags, replacement identities, reconnect/cancellation and retained cleanup.
Existing core rollback cases need not be duplicated. This audit performs no
retail memory read or write and does not establish all-map acceptance.

## Complete Basic Scripts root inventory

[PopulateBasicScripts](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Functions/basic.gsc#L1-L39)
contains exactly 31 root rows, in this order. Each operation receives the selected
player where shown; submenu population derives perks/Gobblegums/visual assets
from BO3 runtime collections. Displaying a row is not evidence of IW support.

| # | Source row | Control and exact source parameters |
| --- | --- | --- |
| 1 | God Mode | Boolean Godmode(player) |
| 2 | Demi-God | Boolean DemiGod(player); mutually disables God Mode |
| 3 | Noclip | Boolean Noclip1(player) |
| 4 | Bind Noclip To frag | Boolean BindNoclip(player); source frag action |
| 5 | UFO Mode | Boolean UFOMode(player) |
| 6 | Unlimited Ammo | Choice Continuous / Reload / Disable |
| 7 | Unlimited Equipment | Boolean UnlimitedEquipment(player) |
| 8 | Unlimited Special Weapon | Boolean UnlimitedSpecial(player) |
| 9 | Modify Score | Choice ±1000000, ±100000, ±10000, ±1000, ±100, ±10, 0; source order descends from positive to negative |
| 10 | Perk Menu | Submenu; custom runtime perks, All Perks and Retain Perks |
| 11 | Gobblegum Menu | Submenu; runtime BO3 level.bgb entries |
| 12 | Movement Speed | Numeric min 0, current 1, max 3, increment 0.5 |
| 13 | Third Person | Boolean ThirdPerson(player) |
| 14 | Invisibility | Boolean Invisibility(player) |
| 15 | Clone | Choice Clone / Dead |
| 16 | No Target | Boolean NoTarget(player) |
| 17 | Reduced Spread | Boolean ReducedSpread(player) |
| 18 | Multi-Jump | Boolean MultiJump(player) |
| 19 | Disable HUD | Boolean DisablePlayerHUD(player) |
| 20 | Visual Effects | Submenu; filtered runtime visionset/overlay keys |
| 21 | Set Vision | Choice Default, zombie_last_stand, zombie_death, flashbang, five candy/visibility BO3 names, zombie_noire |
| 22 | Zombie Charms | Choice None / Orange / Green / Purple / Blue |
| 23 | Custom Crosshairs | Choice Disable, +, @, x, o, > <, CF4_99, Extinct, Daltax, GBP, AOC, menu name, source Discord label, cleaned player name |
| 24 | No Explosive Damage | Boolean NoExplosiveDamage(player) |
| 25 | Character Model Index | Numeric min 0, current player.characterIndex, max 8, increment 1 |
| 26 | Random Character Model Index | Boolean LoopCharacterModelIndex(player) |
| 27 | Shoot While Sprinting | Boolean perk specialty_sprintfire |
| 28 | Unlimited Sprint | Boolean perk specialty_unlimitedsprint |
| 29 | Respawn | ServerRespawnPlayer(player) action |
| 30 | Revive | PlayerRevive(player) action |
| 31 | Death | Choice Down / Kill |

The row 16 No Target and No Target While In Menu preference have different scope;
Demi-God is not a second independent God Mode toggle. BO3 Gobblegums, vision keys,
charms, model indices and perk names require an explicit IW equivalent or an
honest unavailable state. Lobby-wide IW ammo is not the source's private per-player
Continuous/Reload/Disable ammo family. The prior offline Third Person research
remains only a candidate, not an implemented camera feature.

The full 31-row family, selected-player submenus, all-player families, all-map/
quest/super-EE goals, retail crash diagnosis and acceptance remain open. A guarded
batch God Mode implemented through existing validated primitives would be a
meaningful next subset; it would not complete the requested full port.
