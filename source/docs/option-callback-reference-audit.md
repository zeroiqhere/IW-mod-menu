# Option callback timing

The ordinary monitor now has a clock independent of the task that performs an
accepted gameplay action. This follows Apparition `Menu/base.gsc` at reference
commit `307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`: `ExeFunction` starts a
thread, then the monitor waits before it accepts another input. The current
upstream files matched the reference during this audit.

| Callback | Row redraw | Ordinary monitor resumes |
| --- | --- | --- |
| Boolean option | 180 ms | 430 ms |
| Ordinary option or slider caller | None from the generic monitor | 250 ms |

These are nominal source waits; source execution and scheduling can add time.
There is no additional generic 50 ms rebuild delay in `RefreshMenu`.

Personal and selected-teammate Godmode use the boolean timing. The personal
restore action is an IW recovery adapter for the same boolean row. Connect,
roster refresh and player selection are IW infrastructure adapters using the
ordinary caller wait. The current binary lobby-ammo extension also uses that
wait: the source Basic/Quick ammo option is a slider, and its All Players Menu
has no matching ammo BOOL declaration.

The queue starts this clock at actual dispatch, after admitting its revision,
clock sample and required redraw counter. A fast task cannot shorten the wait;
a slow or failed task cannot extend or restart it. Actual backend work keeps
its independent Busy guard and UNKNOWN feature values. The clock does not
confirm gameplay success. A completed task does not request a second generic
boolean redraw.

Queued gameplay intent also retains the accepted source owner, local admission
epoch, family, page, cursor visit and focus predicate. Pending publication cannot
retire a visit and then dispatch its old action against a replacement actor or
revoked grant. Rejection consumes the command without starting work or a clock.
The disconnected Connect bootstrap retains its explicit actorless route.

The scheduled redraw belongs to the same menu family, page, cursor visit and
private actor owner. Replacement, permission revocation, shutdown or foreign
navigation cancels it. A verified player-selection result may carry only its
own remaining ordinary wait to its child page. Focus hides the public callback
metadata while preserving the retained owner clock. Clock rollback restarts a
finite remaining stage without repeating a committed redraw. An accepted Quick
callback supersedes the completed opening monitor so a rollback cannot restart
an incompatible opening wait alongside the new callback. Its row fade remains
independent until the real redraw or cancellation.

The optional `menuOptionCallback` wire object contains a positive revision,
`boolean` or `plain` kind, and `pending` or `cooldown` phase. Plain callbacks
cannot be pending. Both native readers validate the same populated ordinary
menu boundary. Native input remains locked and selected-action help is paused;
callback identity also scopes copied frames and scroll receipts. Closed frames,
spacers and competing modal timelines cannot borrow this metadata. Older
producers that have no local admission contract keep their legacy behavior.

Tests use the real controller with an in-memory session and deterministic time;
they do not call the game's engine or write its memory. Separate connected
callback fixtures preserve the existing inert preview fixture contract.

## Remaining scope

This timing integration is not full Apparition equivalence. The later
[boolean display wave](boolean-paint-reference-audit.md) adds a separate copied
HUD sample at the actual redraw while preserving current confirmed observations.
Generic caller waits for intercepted local page/appearance callbacks remain to be
completed without double-counting their specialized timelines.

Private ammo still needs verified IW Continuous, Reload and Disable handlers.
Other unavailable source options need their real IW callbacks and data. The
injected actor binding, gameplay behavior, cooperative sessions, all maps and
final visual/input fidelity remain unproven. Offline checks do not establish
those runtime requirements. The installed payload and saved playable backup
remain separate from this candidate.
