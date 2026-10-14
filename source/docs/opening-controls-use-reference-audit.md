# Opening-control callback continuation

Connected typed Open Controls selections now use the same callback clock and
captured Select receipt as the ordinary monitor. Binding BOOLs and Disable Quick
apply policy immediately, retain the old checkbox through 179 ms, redraw at
180 ms and complete at 430 ms. Rejected binding edits keep their feedback and
full BOOL wait without changing the applied policy.

Menu and Bind Slot selectors use a plain 250 ms caller wait. Menu redraws only
when its applied target changes; Bind Slot redraws immediately even when equal.
Changing the target or slot advances navigation, so the old captured receipt is
retired. A callback can finish its result page without granting a new input owner.

## Source basis

The pinned Apparition revision is
`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`.
The [monitor](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L75)
provides the nominal BOOL 180 + 200 + 50 ms and plain 200 + 50 ms waits.
[Open Controls declarations](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L16)
mark Menu and Bind Slot as selectors and action bindings as BOOLs.
The binding and selector handlers in base.gsc preserve the conditional None row,
validate the tuple and refresh according to the selected editor operation.
These explicit callback bodies do not resolve the generic child-thread ordering
question for unrelated submenu rebuilds.

## Ownership and display

One callback clock owns typed waits. An immutable previous policy pair, or its
Main-only fallback, is retained solely for presentation until successful redraw.
Opening admission and serialized policy always use the actual applied state.
The pair already owns Main, so its chord is not cached again. Actorless preview
keeps the existing preference-refresh path and receives no typed Use receipt.

Clock and counter checks happen before mutation or persistence. Open Controls
and Preferences do not publish BooleanPaint, so these callbacks reserve only
their actual menu redraw epoch. Plain selectors reserve no delayed redraw epoch.
Owner, focus, navigation and row-scope changes cancel held input. Clock rollback
restarts the unfinished wait stage without undoing an already completed redraw.

## Evidence and limits

TDD first reproduced missing typed callback receipts in the real controller and
in packets read through both native readers and NativeInputRouter. The new graph
exports 36 real-controller states and nine pre-command captures, including a
second held binding invocation that captures the newly applied policy revision.
Malformed historical capture filenames are preserved as an invalid attempt;
only the corrected export is used for native RED evidence.

Backend and native checks are offline evidence. This change does not establish
physical timing, Windows runtime execution, engine action effects, DLL lifetime,
all-map quests, Super Easter Egg or original Steam co-op acceptance. The source
candidate is not installed and is not ready for gameplay testing.
