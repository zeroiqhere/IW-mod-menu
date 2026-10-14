# Ordinary source monitor waits

The controller now owns the source's finite pause after a slider branch and
after the final Back decision. The native router consumes ordinary input while
that ownership is published. A closed root can still be waiting, so an opening
gesture cannot bypass Back's pause. This checkpoint does not enable a gameplay
callback, add a native offset, or change installed products.

## Fresh source contract

The official main commit checked with curl remains
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`. The literal
[monitor chain](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L61-L169)
checks scrolling, Use, sliders, then Melee or dead Jump. A valid slider branch
waits 130 ms before the outer 50 ms wait: the ordinary monitor resumes at
180 ms. The Back branch waits 200 ms after its actual parent/close decision,
then the same outer 50 ms: it resumes at 250 ms. The existing child knife loop
decides short Back versus held close before this final Back pause starts.

The [threaded callback dispatcher](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L172-L197)
does not make feature completion the monitor's deadline. Existing classified
plain and BOOL callback waits retain their separate 250 ms and 430 ms schedules;
this checkpoint does not stack another pause onto them. The source's
[drawText](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L301-L409),
[RefreshMenu](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L724-L752),
and [newMenu](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L927-L1016)
have no additional explicit 50 ms wait. Generic submenu Use and its threaded
row-type reread need their own scheduling implementation; no blanket delay is
claimed for those callbacks.

## Controller and wire ownership

The optional `menuMonitorWait` object contains only a positive unsigned revision
and `kind`, either `choice` or `back`. It has no elapsed-time, account, feature
result, or permission field. Absence retains the legacy contract. A choice wait
requires a populated ordinary page with a valid nonspacer cursor. Back can own
an open parent or a closed root. Both require the original verified local menu
owner, known life, menu permission, and a connected nonpreview visit.

Counter and clock acquisition happen before a slider draft or navigation changes.
The wait records navigation after its own action, so that action does not cancel
it. Independent navigation, replacement or lost proof, shutdown, or a competing
modal retires it. A backward controller clock restarts the full finite interval
without transferring the original owner. Focus loss withdraws the published
descriptor while the controller timeline continues. Retirement has a separate
publication identity, including when the menu body is closed.

This pause does not set `ControlsDisabled` or grant menu access. Both strict native
readers reject malformed fields, unavailable local admission, an empty or spacer
choice, and competing editors, callback or rebuild phases. Existing aggregate
field order is preserved; the new field is appended. Parsing occurs before
acknowledgment tracking so malformed ownership cannot advance that receipt.

## Input behavior and retained adaptations

The router samples each configured key once. During a wait it consumes ordinary
edges and disarms knife; it does not estimate readiness from a native deadline.
The original same-direction slider hold can resume once when the authoritative
choice wait disappears. Release or an opposite direction cancels that repeat,
while the wait itself still runs. Back interrupts an inherited choice repeat.
Closed-menu gestures observed during Back's pause cannot replay afterward; a
fresh post-wait gesture is required. A backward native clock retires inherited
input instead of fabricating an unsigned readiness time.

Fresh-edge selection and Back, acknowledgment gating, focus/context reseeding,
and the opposite-direction release requirement remain native adapters. Literal
GSC held-button coroutine behavior, row-type checks and action-slot writes are
not fully ported. These tests establish the implemented ownership and deadlines,
not identical physical input scheduling.

## Regression evidence

Eleven new managed cases compiled against reached missing-wait, focus-boundary,
strict empty-choice, actual exporter-dispatch and graph-predecessor failures
before their fixes. The actual standard backend project now passes 869 cases;
the session project passes 210. Existing high-level fixture navigation explicitly
waits for source readiness; raw command submission remains unchanged. Existing
profile names are retained rather than rewriting exported packets.

The native router contract first reached nine failures among twelve cases with
an ignored wait parameter. All twelve now pass. The shared-schema contract first
reached six failures among eight cases before the readers copied the field.
All eight pass, and two further cases join the actual 26-packet controller graph
to both readers and the router. Root's independent actual Program exports match
the agent's 26 packets byte for byte. Sixteen existing boolean-paint packets also
pass their original native reader assertions.

The broader native run passes nineteen selected suites. Its two schema invocations
repeat eight cases; logical counts must deduplicate them. The current Windows
DLL and both new test executables compile with the project's vendored headers.
No Windows executable, injected DLL, GPU, or game was run for this checkpoint.
Setup failures from a missing project source, incorrect fixture setup, a wrong
vendored include, and a missing test link source remain separate from behavioral
RED evidence.

## Full objective remains open

This is a source checkpoint, not a complete or installed Apparition port.
Remaining work includes generic Use and held-button schedules, source engine
interpolation and physical appearance, restart-safe game-lifetime preferences,
private per-player ammo, unsupported IW gameplay callbacks, complete retail
lobby authority, normal Steam co-op, every map and the requested super Easter
egg. The saved playable backup and installed disabled DLL remain separate and
unchanged. No new user live test is required to accept this source checkpoint.
