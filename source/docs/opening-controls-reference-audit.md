# Main opening-controls integration

This checkpoint integrates the Main menu's opening-control editor into the
controller, private saved bindings and native input router. Quick Menu remains
unavailable. It changes local menu controls only; gameplay writers, graphics
hooks and the disabled installed launcher are unchanged.

## Source and action identity

The reference is Apparition commit
[307e94c](https://github.com/CF4x99/Apparition/tree/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c).
Fresh official `Menu/base.gsc` and `Menu/menu_customization.gsc` reads matched that
pin. Canonical IDs preserve the source order: action slots 1–4, Melee, Aim,
Attack, Sprint, Use, Frag, Secondary Offhand, Stance and Jump.

Physical keys stay in the existing private bindings file. Individual saved groups
prove action availability; combined scrolling groups cannot prove an opening
action. IW's saved `+melee_zoom` maps to source Melee. Optional Sprint, Stance and
Jump use their exact saved action tokens. No key, default binding or game-engine
placeholder is invented for a missing action. One bounded bindings read supplies
both action availability and selector capability to the production runner.

## Applied and candidate controls

The editor offers Menu and Bind Slot selectors, a typed blank separator and the
13 source actions. Slots 2 and 3 also offer None. Arrows stage selectors; Use
commits them. The Bind Slot selector wraps between 1 and 3. Main's applied tuple
contains one to three distinct actions; the first action cannot be removed.
None truncates the selected optional slot and all later slots. Repeated actions
and the source offhand conflict are rejected without changing the applied tuple.

An action checkbox is ON only when that action occupies the applied selected
slot. Membership elsewhere in the chord does not make this slot ON. Duplicate
choices remain selectable for their rejection explanation. None is ON exactly
when the selected optional slot is absent. Selector drafts and refresh history
cannot fabricate confirmed checkbox values. The notice reports the complete
actual applied chord even while rows show captured old values. Quick Menu can be
staged, but committing it reports unsupported and preserves applied Main.

Action callbacks publish an accepted chord immediately, hold captured row values
through 179 ms, redraw once at 180 ms and unlock at 430 ms. Rejected or unchanged
bool callbacks still own that refresh without changing policy revision. Selector
Use locks for 250 ms. Bind Slot always redraws, even when unchanged; reselecting
Main does not redraw. These are source callback distinctions rather than a shared
appearance refresh. Clock rollback and late observation retain bounded phases.

## Native input and admission

An optional global `mainOpeningControls` packet carries a positive uint64
revision and one to three canonical IDs from the same immutable chord owner.
It remains present when closed, paused, hidden by focus or on another modal page.
A supplied null, malformed descriptor, duplicate action or unavailable private
action rejects before publishable runtime state is returned. Production startup
uses the tested preparation helper to validate bindings before parsing state.
Legacy packets omitting the descriptor retain Aim held plus fresh Melee.

Explicit chords open on the full aggregate rising edge, independent of press
order. A second key for an already-held action is not a new action. Policy,
focus, context and pending-command boundaries reseed held states. A fresh action
completing a partial chord remains admissible; every constituent need not be
released. Single Melee Back cannot immediately reopen a closed menu.

Native Select and choice commands capture the observed navigation and opening
policy revisions. The controller acknowledges and drops stale supplied captures
before any page routing, including after leaving the editor. Missing captures
cannot mutate Open Controls. Legacy omitted fields retain ordinary-page
admission. A genuine regression reproduced the previously missed away-only
Select and stale-policy ordinary-choice cases before the global scope fix.

`openingAction` row metadata selects a saved-key caption privately. Missing
bindings retain a logical caption with UNAVAILABLE. Typed `isSpacer` rows must
be empty, disabled and action-free; a selected spacer or all-spacer nonempty
packet rejects. Disabled ordinary rows remain selectable for useful help.

## Spacer cadence and remaining presentation

The native router captures consecutive skipped spacers in each direction,
including wrap, and adds half the configured animation time per spacer to the
existing animation-plus-75-ms admission interval. Invalid geometry rejects;
disabled ordinary rows do not add delay.

This establishes admission cadence only. `ScrollPresentation` remains unchanged.
For an unchanged visible window, source T=120 ms holds the old cursor/styles for
60 ms, then animates the final nonblank target for 120 ms. Current distance-greater-
than-one snapping does not implement that hold/fade. If the first step changes
the visible window, source window movement begins before blank validation; the
recursive final step can recreate rows. These branches need a separate validated
presentation path and regressions. No intermediate blank selection is published.

## Evidence and limits

The reviewed suites contain 361 managed controller cases, 148 Windows runtime
cases including the actual producer join, 26 portable-only preview-reader cases,
14 dedicated opening-input cases, 147 production panel CPU cases and 90 packaging
cases. The complete retained logical inventory is 1,216 distinct cases. Portable
runs of shared tests, repeated exports and graphics assertions are not added a
second time. Session and feature suites retain 24 and 103 cases respectively.

There are 180 actual offline controller profiles and 210 production drawlist
previews. All old 158 packet bytes remain identical. Four old PNGs change by
1, 1, 13 and 11 pixels respectively after introducing editor glyphs into the
persistent atlas; the complete filename/hash order check covers the new 210 set.
Replaying the old 158 profiles with the new compiled exporter reproduces all
188 preceding PPMs byte-for-byte, isolating these differences to the expanded
profile inventory. These images are CPU previews, not gameplay screenshots. Fresh-context glyph
coverage, global epoch regression, startup availability and malformed-packet
checks have separate evidence. Invalid setup and stale-binary failures are
retained as such rather than claimed behavioral RED.

The independent full Windows build, strict .NET 8 Windows publish and four owned
DX11 modes are separate from retail acceptance. The graphics modes exercise the
test process only, without a game or SteamAPI initialization. Their 42/60/47/69
assertion executions do not add distinct logical cases or establish a retail
crash fix. Previous intermittent fixture failures remain unexplained.

Alive/dead source gating, Quick Menu's independent catalog and cursor, saved
XUID/dvar persistence, exact BO3 assets and engine easing, live opening-control
acceptance, all remaining gameplay families and the saved fatal match-exit crash
remain open. Full Apparition parity is still the active goal. Installed files
remain unchanged and disabled; testing in the actual game is deferred as requested.
