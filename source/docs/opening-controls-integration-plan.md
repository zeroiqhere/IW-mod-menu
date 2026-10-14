# Main opening-controls integration contract

This wave starts from the sealed row-text/core checkpoint: 897 source files and
1,157 distinct cases. The installed package remains unchanged. This document retains the pre-implementation contract; reviewed evidence is in
[the integration audit](opening-controls-reference-audit.md).
Fresh official base/customization files match the pinned Apparition commit.

## Applied controls and private bindings

An optional global `mainOpeningControls` object contains a positive uint64
`revision` and one to three canonical integer `actions` in source order. Both
come from the same immutable applied chord owner. It remains present while the
menu is closed, focus-projected, paused or displaying another modal page.
Unsupported legacy profiles omit it. Supplied null or malformed objects reject;
an absent object retains the legacy Aim plus fresh Melee path.

Exact saved action groups determine individual capability IDs. Slots 1/2 use
moveUp/moveDown, slots 3/4 use previousChoice/nextChoice, and Attack uses attack
rather than the combined Down group. Optional sprint, stance and jump groups
come only from exact saved actions. Missing groups remain unavailable. Existing
alias rules remain strict; physical keys remain inside private startup bindings.

The explicit policy observes aggregate action-group state and the full chord's
rising edge. Another physical key for an already-held action is not a new action.
Full chords held across policy installation, focus recovery, closure or pending
acknowledgement cannot replay. A genuinely fresh action completing a previously
partial chord is a separate path; the contract does not require releasing every
constituent action. Legacy input behavior stays separately tested.

## Rows and command admission

Optional row `openingAction` carries a canonical ID for private saved-key
captions. A missing group retains a readable logical caption and UNAVAILABLE;
the renderer cannot invent a key or interpret a BO3 engine placeholder.
Optional `isSpacer` defaults to false and is omitted for legacy rows. A declared
spacer is empty, disabled and has no action metadata. Only spacers are skipped;
ordinary disabled capability rows remain selectable for explanatory help.
Action-row ON means that action occupies the applied selected slot, rather than
membership anywhere in the chord. An action assigned to another slot is OFF and
remains selectable so the source duplicate rejection can explain the conflict.
None is ON exactly when the selected slot is absent. Final-target packets reject
selected spacers; nonempty all-spacer rows cannot describe a valid selection.

Open Controls has separate candidate and applied target/slot selectors. Arrows
stage candidates; Use commits. Quick Menu can be staged, but its unsupported
commit retains applied Main and reports the distinction. Each mutation command
captures both `navigationRevision` and `openingControlsRevision`. Missing or
stale captured values are acknowledged and dropped before routing on every page. The backend
must never fill expected revisions from its current owner. Legacy admission
elsewhere remains intact.

## Source refresh phases

Action-row attempts publish accepted input changes immediately. A typed
MainOpeningControls refresh captures the previous chord for old row booleans
through 179ms, redraws once at 180ms and unlocks at 430ms. Duplicate/reselect,
conflict, rejected attempts and accepted consecutive None no-ops retain the tuple
revision but still own that source boolean refresh.

Selector Use has a distinct MainOpeningSelector cause with a 250ms locked
cooldown. Bind Slot redraws immediately and invalidates presentation once,
including an unchanged slot. Selecting the already applied Menu target returns
without source RefreshMenu, so it retains the caller cooldown without inventing
a redraw. Unsupported Quick retains applied Main and similarly cannot claim a
successful source target redraw. Appearance refresh cannot accidentally supply
chord history. Rollback and late clock observation must preserve the correct
number of redraws and truthful rows.

The runtime derives directional consecutive spacer counts from the same typed
snapshot and captures an additional half-animation wait per skipped blank.
Legacy/no-spacer dispatch retains the existing animation plus 75ms wait. This
implements admission cadence. In an unchanged visible window, the source skips
the invalid blank before the style/scroller loop: at a 120ms animation, it holds
the old visual for 60ms, then animates the final target for 120ms. It does not
animate an intermediate blank scroller in that path. Current Distance>1 snapping
must not be described as this source hold/fade. A validated typed-spacer path
must preserve that distinction, while arbitrary jumps retain legacy snapping.
Visible-window changes execute different code before the invalid-row branch and
require a separate audit.
Alive/dead gates, Quick Menu's independent catalog/cursor, saved XUID dvars,
retail input acceptance and all remaining gameplay families remain open.
