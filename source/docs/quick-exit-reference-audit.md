# Quick Exit: pinned reference behavior and candidate requirements

Audit date: 2026-10-08. This audit covers the Quick Exit wave, first checked in an isolated source copy and then integrated into main. The root confirmed 334 distinct source/isolated checks and final current-main builds. The normal installed package, game state and saved bindings are unchanged by this wave; no retail/GPU acceptance or crash fix is claimed.

The official GitHub main was fetched with curl and still resolves to `307e94c1b1d6537ba7a2a71b32a22d563d3ea25c` (2026-06-28). Fresh pinned `base.gsc` and `menu_customization.gsc` match the local reference byte-for-byte. This is the actual reference, not an inferred generic long-press design.

## Source behavior

| Context | QuickExit off | QuickExit on |
| --- | --- | --- |
| Main or Quick Menu root | Close the menu immediately when the melee branch runs | Same immediate close; no hold timer |
| Child page, melee tap | Return one parent immediately | Defer parent return until release before the goal |
| Child page, melee held | Return one parent when the branch runs | Close the menu when the hold goal is reached; do not return the parent first |

[The branch](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L119-L168) initializes count 0, increments immediately, stops at goal 10, and waits 0.01 seconds only between increments. Ten observations therefore contain nine waits: nominal 90 ms from branch entry, not 100 ms. A root close bypasses the preference check. The branch later waits 0.2 seconds and the monitor waits 0.05; these are subsequent polling delays, not part of the hold threshold. Engine scheduling can lengthen each wait.

The loop reaches its goal only while melee is observed held. A release observed before that held-goal decision takes the parent route; elapsed time on release must not invent a completed hold retroactively. Once close was emitted, release must not also return a parent. The native worker's 20 ms polling means a 90 ms elapsed threshold is commonly first observed at 100 ms, before command acknowledgement/render latency; no precise wall-clock guarantee is established.

Reference priority is scroll, then Use, then choice actions, then melee/dead-player jump. Holding another higher-priority action can prevent the melee branch from starting. The later [source monitor wave](source-monitor-priority-reference-audit.md) removes the earlier fresh-Back priority difference and implements the shared dead Quick Jump/knife branch against copied, verified life and menu facts. Fresh Use rather than held activation and one-command acknowledgment gating remain native differences. This local preference alone does not establish retail engine life state or Quick Menu acceptance.

## Defaults and storage

`main.gsc:332` calls `LoadMenuVars`. Its [default block](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L745-L806) does not set QuickExit true: a fresh undefined value is off. [QuickExit()](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L505-L509) toggles and saves it. [SaveMenuTheme](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/menu_customization.gsc#L700-L742) includes QuickExit, converts undefined to 0, and writes player-XUID-keyed dvars. Load line 778 converts the saved integer through `returnBool`: true when enabled, undefined otherwise. This does not establish disk/process-restart persistence. Candidate default should be false, controller-local, and no saved-game config writes are needed.

## Protocol and ownership requirements

At the start of this audit, the state packet exposed open state, title/context, rows/cursor, appearance and navigation revision but no authoritative root-page field. Derive an explicit root boolean from the controller's actual `MenuPage.Main`, not a displayed title, zero cursor, empty rows, roster, or absence of parent history. The reference checks actual Main/Quick Menu identity; only Main had an IW counterpart at the start of this historical wave. Later Quick Menu work adds explicit surface and local-life ownership; current ordering is documented in the source monitor audit. Native input must consume that same immutable packet's root state, preference and revision together.

Candidate state adds explicit `isRootMenu` from actual controller page identity. Missing QuickExit remains false. Missing root identity defaults true, retaining the immediate legacy Back/root path instead of arming a delayed hold against an unknown page; this conservative fallback is not proof that the producer described the root. Supplied flags require strict booleans. Changing preference/root/epoch during an armed gesture cancels it and requires release/fresh press; strict parser/producer integration now has confirmed current-main evidence.

**Quick Exit closes only local menu visibility.** Use the navigation close path, preserving controller-local page/cursor resume. It is not the existing Exit row, process termination, controller shutdown, feature disable/restoration, disconnection, DLL unload, or game exit. [Reference closeMenu1](https://github.com/CF4x99/Apparition/blob/307e94c1b1d6537ba7a2a71b32a22d563d3ea25c/Menu/base.gsc#L223-L256) destroys menu HUD and toggles menu-open state; it does not invoke the Godmode/ammo handlers. Unsupported AI-ignore/action-slot restoration in that GSC is outside this candidate.

### Close command revision guard

The native Close packet alone requires a present unsigned `navigationRevision`. The serializer refuses a missing revision for Close; existing legacy inputs keep their earlier packet shape. Zero is a valid present epoch, as is `ulong.MaxValue`; neither is a missing-value sentinel. Missing, null, string, negative, fractional, overflowing or duplicate/bad-type revisions cannot validate a Close. A recoverable invalid sequence is acknowledged without dispatch or later replay; an unparseable sequence cannot manufacture an acknowledgement.

The controller checks a present matching epoch against its actual `navigation.Revision` **after** `Snapshot()` observes asynchronous completion and updates the current navigation. It also requires an open menu, enabled QuickExit and a fresh focus check. A selection which completes between the worker snapshot and command delivery can therefore invalidate the old hold-close. Invalid/stale commands are consumed without a later close. The accepted path calls only `navigation.Close()`; it never dispatches a session feature or Exit cleanup.

This contract distinguishes the state packet's optional legacy epoch 0 from a command's mandatory present Close epoch. The 44 native-runtime checks include 11 actual-controller snapshots, strict legacy/root/preference types and Close epoch 0/max. The 93 backend checks include local-only closure and completion-before-Close epoch rejection; these join the real producers/consumers without a game.

## Confirmed native gesture evidence

The root reports 15 passing isolated native Quick Exit checks. The initial 13-check runner failed against the old immediate-Back behavior, then passed with the separate `QuickExitGesture` implementation. A late-release 90/1000 ms regression failed before release handling was fixed: release takes Back before testing elapsed hold time, so it cannot invent a Close. Startup-held aggregate melee also requires complete release; an alternate bound-key edge cannot bypass that gate.

Confirmed behavior: preference-off Back remains immediate; an enabled child tap emits one Back on release; held 90 ms emits one local Close, with no additional action on release. Focus, menu, navigation context, preference/root changes, pending commands, key-read failure and clock rollback cancel the gesture; near-uint64-limit timing is tested. Existing scroll 9 and choice 12 checks also pass. Saved-control hints now have 6 passing checks after their red→green change; root and child help use actual saved melee labels and do not describe a hold delay on the root. These are native pure key/clock checks, not game tests or controller cleanup proof.

Final root-confirmed evidence is 334 distinct checks: 93 backend, 48 features, 44 native-runtime, 65 packaging, 9 scroll, 12 choices, 15 Quick Exit, 6 saved hints, 14 geometry and 28 production CPU panel. Root main reruns cover the changed 93/44/15/9/12/6/14/28 suites with 11 fresh actual snapshots; feature 48 and packaging 65 retain root results for unchanged/hash-matching source. Root isolated 94-step and integrated 64-step native builds passed, as did the actual main Windows backend publish. The agent strict Windows build had zero errors/warnings. Three actual/sample PNGs were reviewed as legible; the actual Quick Exit snapshots preserve Text/Left/Mint and hidden instructions, while separate root/child saved-hint examples are labelled samples. No GPU/game launch, injection, new feature writer or installed deployment occurred. The prior 297-check choices/constructor candidate remains preserved.

All local source/protocol/CPU completion claims above are confirmed; no integration run is pending. Gameplay effects, hook/transition safety and full Apparition parity remain separate.

## Local gesture and protocol acceptance contract

| Case | Expected outcome |
| --- | --- |
| Root, preference off/on | Exactly one immediate local close from fresh melee |
| Child, preference off | Exactly one immediate parent return |
| Child enabled, held 89 ms / held 90 ms | No parent at 89; one close when still held at 90 |
| Child enabled, release before held-goal decision | One parent return on release, including release first observed at the boundary |
| Release after emitted close | No additional Back/close and no reopening from held melee |
| Multiple saved melee keys | One aggregate gesture; another bound key edge while already held cannot restart it |
| Held at startup/focus return/menu reopen | Reseed and require a fresh aggregate press |
| Focus lost, navigation changed, away-and-back, close-and-reopen | Cancel without a deferred action; require release/fresh press |
| Preference/root changed mid-gesture | Cancel the old gesture; no stale close on a replacement context |
| Command pending at press or during gesture | Consume/cancel, never replay a held close or deferred Back after acknowledgement |
| Clock rollback, near uint64 limit | No underflow, premature close or catch-up burst |
| Controller receives close | Menu closes, feature/session callbacks untouched, page/cursor retained; fresh stale/duplicate sequence rejected |
| Malformed/legacy packet and real producer export | Strict types/defaults and exact root/preference/revision data flow verified |

The confirmed source/isolated suites cover the local gesture, controller/parser contract and CPU presentation; they do not turn this local-close acceptance contract into gameplay proof. GPU/retail input acceptance, the known match-exit crash, full Apparition parity, and game-feature lifetime remain separate open work.
