# IW Apparition injected menu

This project builds an Apparition-style menu inside Infinite Warfare for normal Steam custom Zombies matches. The source is still under development. Offline checks do not establish retail gameplay readiness, and this source checkpoint does not update the installed menu or the saved playable backup.

## Current gameplay controls

| Control | Scope and requirements |
| --- | --- |
| Your God Mode | The authenticated local actor, using its original owned flags |
| Selected player God Mode | One captured teammate with full identity checks; original OFF cleanup can remain available after life or movement changes |
| All Players God Mode | A complete proved roster and current host authority; unknown slots keep it unavailable |
| Infinite Ammo (IW lobby) | The existing shared instruction patch affects the entire match |
| Personal or selected-player Unlimited Ammo | Unavailable until actor-specific clip/reserve access is established |

Disabled gameplay rows have no implemented IW action. Their presence records the reference inventory; it is not evidence that the feature works. Shared owners keep personal, teammate and aggregate Godmode changes from creating competing writers. A replacement player cannot inherit the previous actor's cleanup identity.

Background personal Godmode recovery now shares coordination with menu actions and rejects a busy registry before reading local state. Explicit refresh of a retained personal owner also waits for shared action admission before checking fresh eligibility. Fixture regressions cover OFF rollback, stale refresh proof and cancellation; they do not establish that every retail OFF failure is resolved. Failed personal restore messages now preserve the operation reason and observed ON/OFF/UNKNOWN state, including when an earlier cached status remains ON.

The [cleanup recovery regression](docs/personal-cleanup-recovery-audit.md) now covers the actual personal menu retry followed by read-only observation and a later deliberate ON through the same writer. This adds evidence for existing behavior, without changing the runtime build.

## Menu interaction

The default Main opening chord uses the game's Aim and Melee actions. Saved opening controls can change that chord. Aim and Fire move through ordinary rows, Use selects, and Melee returns to the parent or closes the root. These refer to game actions rather than fixed keyboard keys.

The four represented Main pages follow [Apparition's declaration order](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu.gsc): Basic Scripts, Menu Customization, Players Menu, then All Players Menu. The IW connection and status rows remain separate extensions. This relative-order correction does not implement the remaining reference families or their callbacks.

Connect after spawning is explicit. Opening the menu does not connect or enable features automatically. Displayed ON/OFF values depend on confirmed state; UNKNOWN and disabled controls withdraw action permission. When session cleanup fails, the administrative Exit route retries the original cleanup owner. An unconfirmed restoration can require a full game restart.

The current source also provides a deliberate, Exit-only cleanup menu when the cached local admission disappears while this connection still owns active or pending changes. After releasing the controls, a fresh configured Main opening chord requests this menu using the displayed navigation and policy revisions. Changes to the offered cleanup owner invalidate that opening scope. Back or knife closes it without restoring features; selecting Exit uses the existing guarded shutdown. Returning actor proof, removal of the owned changes, or replacement of the connection withdraws this visit. Typed blocked or unknown actor admissions keep their existing opening restrictions. This is an IW administrative extension, not an additional Apparition gameplay callback.

Owned session and compiled native packet/input regressions cover this cleanup path offline. Retail acceptance and incorporation into a saved test package remain pending; the saved test build and installed shortcuts are unchanged.

Main and Quick navigation, held ordinary input and several owned callback waits have offline source coverage. Held Select across general submenu transitions is not complete. Successful Counter and Stealth rebuild callers also require [source thread-order proof](docs/rebuild-caller-interleaving-audit.md) before their timing and continuation can be changed. Physical input, rendering and injection lifetime still need later retail acceptance with a known loaded build.

Design selections preserve held Use through their caller wait. A changed design redraws at 50 ms and becomes ready at 250 ms while row fades continue; selecting the applied value uses the plain no-op wait. The [changed Design audit](docs/changed-design-use-reference-audit.md) and [unchanged Design audit](docs/unchanged-design-use-reference-audit.md) record source evidence and offline limits.

## Work remaining

- Private self/teammate ammo and the remaining IW engine gameplay callbacks
- Verified empty-slot semantics for partial-lobby aggregate Godmode
- Complete Apparition callback and interaction fidelity
- IW map, quest and Super EE operations
- Normal Steam co-op, all-map, disconnect, respawn and match-exit acceptance

Color work is deferred while gameplay and controls take priority.

## Source and checks

The [backend contract](backend/README.md) describes commands, feature ownership and cleanup. Portable controller checks are under [tests/backend](tests/backend), and real session/reader fixture checks are under [tests/session](tests/session). Tests operate on owned fixtures and do not attach to a game. Windows compilation is separate from Windows or retail execution.

The [private-ammo evidence diagnostic](tools/retail-code-evidence/README.md) is ready for offline fixture use and a later separately scheduled read-only capture. It captures six published code candidates with bounded repeated reads and process/image checks. It has not been run against the game and does not enable personal or teammate ammo.

The [parity requirements](docs/apparition-parity.md) retain the reference inventory and evidence limits. [Historical README checkpoints](docs/historical-readme-checkpoints.md) preserve earlier progress reports; their status and counts are historical.

## Dependencies and attribution

- Dear ImGui v1.92.9b, commit `f1cc2ae15e53a861a874c3034aae6798fde194ab`
- MinHook v1.3.4, commit `c3fcafdc10146beb5919319d0683e44e3c30d537`
- nlohmann/json v3.12.0
- Existing Memory.dll with provenance and license in [backend/dependencies](backend/dependencies)

The feature backend derives from SyndiShanX's GPL-3.0 IW RTM Tool. Its license remains in [backend/LICENSE.md](backend/LICENSE.md); dependency licenses remain in their respective directories. The Apparition reference is pinned in the parity audit.
