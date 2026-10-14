# Width Editor: reference contract and prior source proof

Audit date: 2026-10-08. The prior Width Editor wave is preserved with final root-confirmed source regressions; the timing wave is now integrated with separate 441-check proof. Prior candidates, the installed package, saved IW bindings and game state are unchanged. No new gameplay writer or retail acceptance is claimed. Width editing is a dedicated modal editor, not a width value added to Preferences.

The current official Apparition main remains commit `307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`. The pinned customization file was fetched with curl and matches the local reference byte-for-byte. The prior integrated Quick Exit candidate's 334-check proof remains separate history; the integrated width wave has 392 confirmed distinct checks. Prior 334- and 297-check candidates remain separate preserved history.

## Actor and intended effect

The actor is the local menu owner. Width changes only that owner's menu body/banner/theme lines, leaving game camera, health, ammo, map, players, quests and server behavior untouched. One host DLL's local panel does not alter a friend's HUD. Inputs remain passive and still reach the game; fire/aim/slot/melee can perform their ordinary game actions while the editor reads them. There is no new input suppression or action-slot rewrite.

[MenuWidthEditor](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L356-L448) is opened from Menu Customization as an action. It updates live width during editing; no separate Use commit is required. Melee leaves the editor and keeps the edited width. It is not Cancel/rollback, Exit controller, game exit, feature disable or feature restoration.

| Item | Exact reference behavior |
| --- | --- |
| Width | Inclusive 200..500; default 260 in LoadMenuVars; existing width is retained on editor entry |
| Increment/offset | Local variable starts 1 on every entry; inclusive 1..10 |
| Increase/decrease | Actual Attack / ADS respectively; action-slot 2 and action-slot 1 are not equivalents |
| Offset smaller/larger | Actual action-slot 3 / action-slot 4 respectively |
| Priority | Attack > ADS > slot 3 > slot 4 > melee |
| Opposite input | Fire wins over aim; slot 3 wins over slot 4; no neutral-opposites rule |
| At limits | Clamp width exactly; no overshoot. A higher-priority held branch still wins at its bound and delays lower-priority exit |
| Held width cadence | Branch wait 50 ms + loop wait 25 ms = nominal 75 ms |
| Held offset cadence | Branch wait 100 ms + loop wait 25 ms = nominal 125 ms |
| Width animation | Each existing body/banner/separator/bottom-line/outer-background width animates for 50 ms |
| Exit | SoftUnlock reopens the retained original menu/cursor and saves current width through source theme storage |

Native 20 ms polling, acknowledgement gating and scheduling can delay observed repeats beyond nominal 75/125 ms. The reference editor is a held-input loop. Fresh entry/exit, context reseeding and command gating are application safeguards, not literal replication of every held-engine input side effect.

## Editor presentation and lifetime

[SoftLockMenu(120,true)](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L650-L711) disables normal menu controls, destroys option HUD groups/menuStructure and counter fields, hides the scroller, and makes the body 120 high. The Apparition banner becomes body+35; the bottom line uses body.y+height−1. The width function separately removes title/menuName. It draws five editor-action instruction lines at left+4/body.y+25, plus a white Offset Value label and green numeric offset. Normal title, options, highlight/scroller and counter must not remain behind a fake slider page. The ordinary instruction footer is independent and remains governed by its visibility preference; it is separate from the editor’s five inline controls. The offset row is 73 source units below the first instruction; inline line spacing is 14 units.

The reference’s standard footer continues to describe Scroll/Select during SoftLock because [the independent instruction loop](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/main.gsc#L444-L453) does not switch to editor controls. The candidate deliberately uses context-aware footer help instead: Width Editor, Menu navigation paused, and the actual saved melee/Exit label. The ordinary footer’s style/visibility preference remains, and normal instruction text resumes unchanged outside the editor. This is an explicit source-text difference so visible controls describe supported behavior, not inactive Scroll/Select actions.

The native contract requires parsing the five actual saved editor body hints and deriving the context footer’s Exit line from that same bounded copied vector. No remapped key is guessed from a normal-menu legend. The isolated 55-check native runtime suite confirms the parser/formatter join against all 14 fresh controller exports; root also confirms the seven saved-controls formatter checks. This establishes copied labels and local protocol behavior, not retail rendering.

SoftUnlock closes/recreates HUD without animation using preserved current-menu/page/cursor fields. `openMenu1(false)` waits 50 ms internally, then SoftUnlock waits 100 ms before reenabling controls: nominal 150 ms unlock path. The preserved 392-check candidate returned atomically, a timing difference now addressed by the local hidden/paused phases in the integrated timing wave; native GPU objects are still retained rather than destroyed/recreated. A held exit key can be reread by the source normal monitor after unlock; native reseeding must avoid a second Back/close/reentry without a fresh press.

The editor loop checks `isInMenu(true)`, which tests normal/Quick menu flags, not player alive state. If the menu is externally closed, SoftUnlock's open-menu guard returns instead of reopening it, and the subsequent SaveMenuTheme retains the width. Disconnect ends the source thread. The backend closes editor mode on hide/close, preserving live width and original page/cursor for normal reopen, and rejects delayed editor actions from its old revision. It does not resume special editor controls on reopen. Its 111-check suite also covers finish/reentry step reset, unsupported normal navigation while editing, fresh PID/epoch/focus guards and independence from pending feature operations.

The candidate retains width only for controller lifetime, resets increment to 1 for a new editor entry, and preserves width through entry/exit in backend tests. Source SaveMenuTheme stores width in XUID-keyed dvars; it does not prove disk/process-restart persistence. No game dvars/config writes are needed here. Focus return, mode changes, pending commands and changed editor revision must consume/reseed held controls instead of sending a resize/exit into another page. Closing this editor or menu must not alter existing feature ownership or start cleanup.

## Exact saved binding contract

The new producer packet adds `attack` from **all** saved `+attack` keys, never `down[0]`. `down` remains the ordinary deduplicated `+attack` + `+actionslot 2` group. Editor decrease uses actual `aim`, not combined `up`. The old aim/melee/up/down/select/previousChoice/nextChoice groups remain unchanged. New producer packets always include attack because saved `+attack` is already required. Legacy packets may omit it and remain valid; a missing/empty group cannot enable width editing.

Native/backend validation must bound keys, reject duplicates and require attack to be a subset of down. Only attack/down overlap is added to the existing aim/up exception. The editor requires actual attack/aim and both saved slot 3/slot 4 groups; no guessed keys or narrower preference substitute supplies missing editor controls. Missing capability should disable Width Editor with actionable help, while ordinary choice availability remains unchanged. Malformed new groups reject the whole packet.

## Confirmed packaging regression

Five new saved-controls cases cover remapped/multiple attack keys, earlier slot 2 exclusion, required attack despite a scroll-down binding, attack membership and unchanged legacy groups. The 16-case controls runner failed five checks before the producer change; the complete packaging regression then passed 70/70. These pure saved-text cases open no game, rewrite no config, and prove the imported binding groups rather than gameplay or full editor behavior. Source comments were reviewed for simple professional wording.

## Prior 392-check proof and current timing work

The backend agent confirms 111 checks and a strict Windows build. Root confirms 15 width-input checks after a failing-before/passing-after run; packaging 70 remains confirmed above. The renderer agent’s initial suite passed 17 geometry/32 CPU checks. The context-footer regression then failed before the fix and passed after it; the current suite is 17 geometry/33 production CPU checks, with all 14 actual backend snapshots passing fresh-context coverage and a successful renderer/fixture cross-build. Default normal-main pixels were byte-identical to the Quick Exit predecessor. The context footer is now CPU-confirmed and leaves normal snapshot instructions unchanged for resume; ordinary visibility/geometry remains independent. Root confirms the isolated full MinGW build and .NET 8 publication, 55 native runtime checks against 14 actual producer exports, and seven saved-controls formatter checks. Root confirms current-main integration, a full native rebuild and .NET 8 publication. Current-main backend 111, packaging 70, native runtime 55 against 14 fresh exports, geometry 17 and production CPU panel 33 against 14 fresh exports are green. Feature 48, scroll 9, choice 12, Quick Exit 15, width input 15 and saved hints 7 retain confirmed results on identical source: 392 distinct checks in total.

Root observed the native protocol regression fail with “Confirmed menu width was ignored”. The isolated protocol and seventh saved-controls formatter checks now pass. The parser carries actual saved body labels into the editor/footer, validates width/step/mode/revision, and preserves legacy fields/defaults. Three additional bounded-JSON regressions reject duplicate decoded object fields, more than eight nested containers, and unknown binding fields. All Width Editor commands require the exact unsigned navigation epoch; zero and the uint64 maximum remain valid. These checks are included in the final root-confirmed 392-check main-source total; this count does not include repeated reruns or snapshot coverage as additional distinct cases.

The preserved 392-check candidate applies geometry immediately and returns atomically. The integrated timing wave now adds 50 ms linear presentation interpolation plus a 0..49 ms hidden-panel rebuild and 50..149 ms visible-parent pause, unlocking from 150 ms. The source engine easing remains unproved, and native GPU objects are retained rather than destroyed/recreated. Root confirms 441 distinct current-main checks, 16 fresh exports and successful native/Windows builds; [the timing audit](width-timing-reference-audit.md) separates confirmed subsets and remaining acceptance. GPU/game, retail hook safety, the known transition crash, other Apparition features and all-map acceptance remain unproved.

The confirmed local suites cover saved key labels and context footer, suppressed normal title/options/counter, fresh snapshot/parser/CPU joining, bounded width/step, original-page/cursor return, pending/context/focus cancellation, stale revision rejection, malformed/legacy packets and untouched feature/session callbacks. That prior candidate's main-source integration/build/regression proof is complete; the timing wave has separate completed main-source proof, while retail/GPU acceptance remains unproved. A displayed width or changed row label alone does not complete the editor.


## Reproducible portable test recipes

The direct g++ recipes in the native-input and native-controls test READMEs now
link `WidthEditorInput.cpp` alongside the router, choice repeat and Quick Exit
helper. These exact production dependencies were independently compiled with
`-Wall -Wextra -Werror`; the owned-process runs passed 9 scroll, 12 choice,
15 Quick Exit, 15 width-input and 7 saved-controls checks. No window, game process,
DLL injection or feature transport was involved. This is a repeat of the same
checks, not additional distinct cases in the wave total.


The final parent-page help regression first failed with 110 passing checks and
one failing check, then passed 111/111 after the detail was changed to describe
opening the editor. Its enabled parent row says to select Width Editor and states
that edits apply immediately and melee returns here; fire/aim editor controls are
shown only once editor mode is active. This keeps the ordinary Customization
navigation legend consistent with currently supported actions.
