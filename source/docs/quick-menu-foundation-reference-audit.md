# Quick Menu foundation checkpoint

This checkpoint adds tested prerequisites for Apparition's Quick Menu. It does
not connect them to the current controller, protocol, input router or renderer.
It adds no gameplay callback. Quick Menu integration and the full parity goal
remain open; the installed launcher remains unchanged and disabled.

## Reference and implemented contracts

The reference is CF4x99/Apparition at commit
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`. Fresh official raw copies of
`Menu/base.gsc`, `Menu/menu.gsc`, `Menu/menu_customization.gsc` and `main.gsc` match the
pinned files byte for byte. Their hashes and URLs accompany the checkpoint.

Main and Quick own separate immutable opening tuples. Quick defaults to Aim plus
Secondary Offhand, with verified saved action groups required. Both families
share the existing one-to-three-action validation rules: duplicates and paired
offhands reject, None trims slots two or three and later slots, and overlap
between families is allowed. Each tuple has its own revision. A global admission
revision advances once for an actual tuple or Disable Quick Menu change. Stale,
rejected, empty-slot no-op and exhausted-revision edits retain the old owners.

The pure native arbitrator models closed-menu priority. Living held Main wins
over configured Quick, even after the Main completion was consumed. Dead Quick
uses fixed Aim plus Jump. Unknown life grants neither route. Source menu
authorization, controls-disabled state, focus, pending commands and menu state
are explicit inputs. All bound keys are sampled once per poll. Policy or context
changes seed current holds and prevent replay; a later partial-to-full chord can
open. Invalid metadata and callback failures reset the arbitrator and throw.
Both configured owners need verified bindings. Missing fixed Jump removes the
dead fallback without inventing a key. The fresh aggregate-edge rule is an IW
adapter for the source held monitor, not identical BO3 scheduling.

The immutable catalogue retains all fourteen declared source IDs, labels and
choice orders. Living membership includes twelve ordinary rows; dead and unknown
membership includes the unconditional respawn row. Verified host or developer
authority adds the final two rows. Unknown authority never grants those rows.
Every descriptor remains unavailable and cannot dispatch. Membership alone is
not permission to open or execute. The source frag action token remains a token
until a verified saved-key caption adapter exists.

The pure opening timeline makes the HUD visible immediately and models the
nominal 550 ms input wait independently of visible-ordinal row fades. Fourteen
rows can fade beyond input unlock. Rollback, invalid or saturated elapsed time,
and elapsed arithmetic overflow retain the HUD, cancel fading and restart a
finite conservative wait. Timestamp and unexpected clock exceptions propagate
before state replacement. Cancellation cannot revive an old timeline. Recovery
is a local adaptation, not an engine timing claim.

The immutable navigation model keeps Main and Quick pages and parent-name stacks
separate across close/open. Exact page names share cursor history; page and row
share slider drafts. Returning to a parent reads its latest shared cursor rather
than an obsolete captured index. The local bounds of 128 rows, 128 parent names,
256 characters per page and 128 choices are implementation limits, not BO3
limits. The model does not establish a reachable menu graph or callback mapping.

## Regression evidence

Behavioral RED logs precede implementation for policy, arbitration and
catalogue/presentation cases. The elapsed-overflow refinement also records a
specific failing case before its fix. Two policy runner setup failures are
labelled separately and are not behavioral RED evidence. A preliminary source
argument extraction mistake was corrected before creating the independent
catalogue oracle and is not counted as a product regression.

There are **1,301 distinct registered logical cases**: the preceding 1,256 plus
26 managed cases and 19 native foundation cases. The managed additions comprise
12 policy cases, 13 catalogue/timeline/navigation cases and one independent
pinned-source oracle. Profiles, clock samples, repeated executions and graphics
assertions do not increase that count.

The complete backend suite passes 388 cases. A dedicated runner passes the 26
new cases and 11 retained opening cases under the existing Windows
Microsoft.NETCore.App 8.0.12 runtime. The Windows native foundation passes 19
cases, and the existing native runtime passes 164. The portable foundation also
passes its CTest target. A fresh full MinGW build and strict Windows .NET 8
backend publication succeed.

All 192 freshly exported controller snapshots match the preceding checkpoint
byte for byte. There are no Quick wire fields or controller integration in this
wave. The preceding 252 preview PNGs are retained unchanged; no new renderer
behavior or graphics acceptance is claimed. Other unchanged subsystem checks
retain their preceding evidence rather than being described as newly executed.

## Integration requirements and remaining work

`GameIdentity.IsAlive` proves process lifetime, not player life. The teammate
reader covers slots one through three and does not prove local self or death.
Personal health is feature-writable and cannot independently authenticate the
source life predicate. Those values must not be substituted for `Is_Alive`,
`hasMenu`, controls-disabled state or coherent host/developer authority.

Integration still needs a justified local entity/session/life source, strict
family and policy protocol, controller histories, runtime dual-chord routing,
exact-key instructions and controller/parser/renderer joins. Each gameplay row
also needs a proved IW callback mapping. Selected-player restoration, action-slot
and ignoreme restoration, persistence, remaining gameplay families, all-map
quests and the super Easter egg remain part of the full goal. The saved retail
fatal and intermittent fixture failures remain unresolved. Standalone success
does not establish a retail crash fix.

The [integration plan](quick-menu-foundation-plan.md) retains the next-step
requirements. This checkpoint is a foundation, not completion of Quick Menu or
the complete mod menu.
