# Local choice controls: prior 297-check wave

This wave is integrated into the main source. The completed preferences candidate remains preserved separately. No installed deployment or new GPU/retail-game test has occurred; saved IW key configuration is read, never rewritten. The root confirmed that prior candidate’s 297-check regression and full native CMake build green, plus a successful .NET 8 Windows publish with zero errors/warnings.

The reference uses `+actionslot 3` for previous and `+actionslot 4` for next. Those exact spaced action names appear in the observed saved IW config; numeric keyboard keys are not defaults. The protocol command strings and optional binding groups are `previousChoice` and `nextChoice`. Native input imports both optional groups without guessed fallback keys. Missing either group disables both choice rows with actionable help. Choice repeat uses the source 180 ms interval, with opposite held directions neutral. Scroll repeat separately uses 195 ms, including its default 120+25 ms branch wait and 50 ms monitor wait; 20 ms worker polling, acknowledgements and scheduling can delay actual dispatch.

Only Bool Display and Bool Box Location have choice drafts. Movement wraps the supported source choices and updates the visible candidate without changing the committed `appearance` object. Fresh Use commits that candidate. Boolean preferences and theme colors retain fresh Use actions. Page return and menu reopen retain drafts during the controller lifetime; a new controller initializes drafts from its actual appearance. There is no disk persistence.

Production availability comes from a bounded private `bindings.json` read, strict version and complete key-group validation, and both nonempty choice groups. Missing choice bindings disable the two choice rows and explain which saved actions are required. No guessed keys or immediate Use-cycle fallback is used. Choice requests outside enabled choice rows are acknowledged without dispatch to a game session. A fresh row comparison and final focus check precede every draft or committed local change.

State includes optional unsigned `navigationRevision`, default zero for generic producers and always set by the controller. Page, cursor, and open-state changes advance it, including leaving and returning to the same row. Draft moves, Use commits, and heartbeat snapshots do not advance it. The integrated native router consumes that context and disarms held choice keys when navigation changes. A fresh press is required after row/page/open-state changes, including away-and-back; draft updates retain the held-repeat context.

The reference stages slider values in `SetSlider` (`Menu/base.gsc:867–894`) and applies them with Use (`Menu/base.gsc:83–85`). Its choice repeat includes a 0.13-second branch wait and the monitor's 0.05-second loop wait. Other reference sliders, designs, and game actions remain outside this local choice wave.


## Isolated evidence and remaining scope

The preceding choices/constructor candidate reported 81 backend, 48 features, 38 native-runtime, 65 packaging, 9 scroll-input, 12 choice-input, 5 controls/hints, 14 geometry/text and 25 production-panel CPU checks passing: 297 distinct checks. The runtime suite includes nine actual-controller exports and the real epoch/router join. CPU rendering compares those nine exported snapshots with fresh-context coverage, including a draft whose applied appearance remains unchanged and a committed snapshot whose applied appearance changes.

Instructions add a fourth actual saved-controls line when both choice groups exist. Empty hints skip the legend without division by zero; long remapped keys fit the bounded text width/height. Applied appearance governs row capacity even when hints are absent. These checks establish source/protocol/CPU behavior, not GPU or retail acceptance.

Only the two local choices are implemented. Full reference sliders, designs, input suppression, in-menu binding editors, disk persistence and new gameplay features remain open. The known retail crash is not claimed fixed. Final root regression/build confirmation is recorded in the [parity audit](apparition-parity.md); it is source/isolated evidence, not an installed release or retail acceptance.


The feature count includes the final constructor-range regression: frozen-health ceiling ≤1 is rejected before transport IO; ceiling 2 constructs without activating a worker. The existing 10000 configuration is unchanged. This is consistency with the health worker's supported range, not new gameplay functionality or invulnerability proof. The root confirmed 48/48 feature checks and repeated the .NET 8 Windows publish with zero warnings/errors.


The integrated main Quick Exit extension has 334 confirmed checks, including 93 backend, 44 native-runtime, 15 gesture, 6 hints, 28 panel and 11 actual producer snapshots. Its full native build and Windows publish passed. [Quick Exit audit](quick-exit-reference-audit.md) records current evidence; integrated-main root reruns and final native rebuild/Windows publish are confirmed. The 297-check paragraph above is preserved prior-candidate history, not the latest total.
