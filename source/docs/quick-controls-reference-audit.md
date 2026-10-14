# Main and Quick opening settings integration

Reference: CF4x99/Apparition commit `307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`.
Fresh official copies of `main.gsc`, `Menu/base.gsc`, `Menu/menu.gsc` and
`Menu/menu_customization.gsc` match that revision. This checkpoint integrates
the preceding paired-policy foundation with the settings controller, rows,
strict wire parser, command admission and retained renderer identity.

## Applied controls and displayed rows

Open Controls stages Menu and Bind Slot separately from their applied values.
Use applies the target or slot. Checkboxes read only the applied slot of the
applied family; sharing an action across Main and Quick is allowed. Within a
family, duplicate actions, missing predecessors and mixed offhand actions reject.
None appears after slot 1 and truncates the selected and following slots.
An accepted unchanged None preserves the tuple and still takes the source wait.
These rules follow [SetOpenButtons and selector handlers](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L1205-L1263).

The immutable pair owns independent Main and Quick tuple revisions plus one
global admission revision. The global revision changes only for a changed tuple
or Disable Quick Menu value. Applied selector changes invalidate navigation
admission once, so a delayed command cannot edit a newly selected family or slot.
Cursor, parent path and tuple revisions stay retained. Missing verified Quick
default bindings preserve the Main-only fallback and explain the unavailable
configuration rather than inventing keys.

Action attempts and Disable Quick Menu retain captured checkbox values until
180 ms, redraw once and consume ordinary input through the nominal 430 ms
boundary. Selector actions consume 250 ms. A changed target redraws once; the
same target does not redraw. Bind Slot redraws once even when unchanged.
Actual configuration publishes immediately, separately from the captured row.
The summary names both applied chords and the actual editor target and slot.
These waits adapt [source Use dispatch](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L75-L168);
exact GSC scheduling and cross-player settings observers remain unproved.

Disable Quick Menu is a policy-owned setting with the source boolean refresh.
It keeps the configured Quick tuple and leaves Main unchanged, following
[DisableQuickMenu](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L634-L638).
It is not a duplicate appearance flag. Settings remain local to the controller
lifetime; source per-XUID Dvar persistence is not implemented.

## Wire, input and presentation ownership

Optional `openingPolicies` contains exactly `revision`, `main`, `quick` and
`quickDisabled`; each tuple contains exactly `revision` and `actions`.
The existing `mainOpeningControls` is required whenever the pair is present and
must equal its Main tuple. Legacy Main-only packets remain accepted. Null,
unknown or duplicate fields, invalid bounds and unavailable private bindings
reject before runtime use. Closed and hidden frames retain applied settings.

Commands capture the global revision when a pair exists, otherwise the legacy
Main revision. Both global and navigation captures are checked before routing.
Any complete-pair change reseeds held Main input, including Quick-only edits.
Private scroll receipts and rendered-source history compare the whole bounded
policy identity, so unchanged Main cannot hide an obsolete Quick transaction.
Serialized input cannot fabricate these private receipts.

A reproduced same-target bug occurred after the opening input wait ended while
row fades still ran. Ordinary cooldown metadata rejected the retained fade.
The typed `selector` phase now preserves it without redraw or presentation
invalidation, including clock recovery. Managed producer, production parser and
preview reader require the full typed 16/17-row Open Controls context. A staged
Bind Slot value does not identify the applied slot. Other cooldown phases keep
their existing opening incompatibility.

## Regression evidence

The complete strict Windows build, backend publication and six isolated Windows
suites pass: native runtime 171 including the actual producer join, dual-policy
input 5, Main input 14, saved hints 13, pure scroll 37 and Quick foundation 19.
The four preview-order regressions pass across forward, reverse and relocated
fixtures with all 274 output artifacts equal within the current exporter.
These executions are platform reruns of registered cases, not extra cases.

The registered inventory is 1,342 distinct cases: the preceding 1,301 plus
24 managed cases, seven shared runtime cases, two preview-only cases, five
policy-input cases and three pure scroll cases. Platform reruns, exported
profiles, clock samples and assertions are not additional cases. All 412 managed
checks pass. The 24 changed managed cases also execute under Windows .NET 8.0.12.
The production portable parser joins 214 actual controller profiles in its
183-case suite; the CPU renderer produces 274 previews and passes 152 panel
checks, with 64 geometry and 17 row-text checks retained.

Of the previous 192 controller packets, 158 retain exact bytes. The other 34
have independently reviewed policy, notice, selector-phase and action-context
deltas. The former rejected Quick profile becomes an applied Quick profile.
The 22 new profiles cover actual policy epochs, 0/179/180/429/430 row capture,
duplicate and None paths, disable/re-enable, fallback, hidden/closed frames and
same-target opening preservation. The RGB pixels of all 158 unchanged profiles
match. In total 190 previous preview images retain pixels; 62 change, including
30 derived spacer clock images with the updated settings summary. PNG compression
streams differ, so decoded RGB hashes establish pixel equality independently.

Compiled behavioral RED logs precede the context, controller, GUI, selector,
strict parser, input and presentation fixes. One earlier managed compile setup
error is explicitly excluded. Strict MinGW optimization also exposed a warning
in a copied-and-reset malformed test fixture. A fresh value-initialized pair-only
fixture preserves the same missing-Main-mirror rejection and compiles with
unchanged `-O3 -Wall -Wextra -Werror`; production code and case counts are unchanged.
The [integration plan](quick-controls-integration-plan.md) records the ownership
contract and regression sequence.

## Full-port limits

Only Main is routed in the current native worker. Configured Quick controls and
Disable Quick Menu do not authorize actual Quick opening. The GUI states that
local player life and menu authorization must be verified. The source's
[opening monitor](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L20-L55)
cannot be replaced by process existence or writable health values.

Quick catalogue dispatch, timeline and retained navigation remain foundation
models without live callbacks. No new gameplay writers, engine offsets, game
operations or graphics-hook lifecycle changes occur here. The user reports a successful match-exit test. Its injection state and tested
build identity are awaiting correlation; this new settings build has not been
live-tested. The cause of the earlier fatal is not established by isolated checks.
The installed launcher still has its older disabled marker and stays unchanged.
Persistence, engine restoration, broader gameplay families, IW map quests and
the super Easter Egg remain requirements of the active full-parity goal.
