# Native input parity

These tests compile the production input router with an explicit monotonic clock
and a small key source. They do not create a window, read game memory, or inject.

The portable CMake project in `tests/native-runtime` builds every input runner
with the production helper dependencies. `MenuPositionInputTests.cpp` checks the
dedicated seven-action editor: exact source priority, captured 25/125 ms waits,
held movement changes at the deadline, consumed melee/pending edges and context
reseeding. It leaves ordinary menu scrolling policy unchanged.

`MenuCreditsInputTests.cpp` has ten checks for saved melee-only exit, aggregate
press/release behavior, deliberate retry after acknowledgment, pending-edge
consumption, and focus/mode/context reseeding. Other menu actions and Quick Exit
remain inactive during credits. The source's held-button loop differs from this
fresh-edge policy; no pending action is replayed automatically.

From the source root:

```sh
cmake -S tests/native-runtime -B /tmp/iw-portable-native -G Ninja -DCMAKE_BUILD_TYPE=Release
cmake --build /tmp/iw-portable-native --target iw_InputParity_tests --parallel 2
ctest --test-dir /tmp/iw-portable-native -R "^InputParity$" --output-on-failure
```

The Windows CMake target is `iw_native_input_tests`. The native runtime suite
also checks opening combinations, focus/menu reseeding, pending commands,
invalid bindings, sequence guards, heartbeat recovery, and private IPC.

The pinned Apparition `Menu/base.gsc` scrolls only when one direction is held.
Its scroll branch waits 120 ms+25 ms by default and then the monitor waits 50 ms,
for a nominal 195 ms whole-loop cycle. The current router uses that complete
195 ms threshold without catch-up bursts. The worker polls every 20 ms, and
acknowledgement gating or OS/engine scheduling can delay actual dispatch. The
source-priority candidate gives a due established scroll priority over fresh Use.
A discarded lower-priority press is consumed rather than queued.

Intentional safeguards retained: aim plus a fresh melee edge opens the menu;
feature selection and Back require fresh edges; focus return or menu-open changes
reseed held keys; pending commands consume fresh edges instead of replaying them.
A held selection never repeatedly writes a feature. Eligible branches follow the
source order: scrolling, Use, sliders, then knife or dead Quick Jump. An already
admitted knife hold remains exclusive until its own loop finishes. Freshness,
pending acknowledgment and reseeding remain recorded differences from the
reference monitor, which polls held buttons.


The separate `ChoiceInputTests.cpp` runner links the same router and `ChoiceRepeat.cpp` and has 13 choice checks; the scroll runner has 12. Saved previous/next choices repeat at 180 ms, with opposite keys neutral. Fresh Select commits before a due choice repeat, while a due choice precedes knife. Navigation revisions disarm held choices across row/page/open-state changes while draft updates preserve context. Optional missing bindings have no guessed defaults. Both runners use owned key/clock sources only; neither starts a game or window.

`menu-monitor/SourceMonitorPriorityTests.cpp` adds fifteen actual-router checks
for source branch order, consumed lower-priority edges, Main/Quick families,
active hold exclusion, the combined dead Jump/knife branch, and blocked-owner
recovery. Eight initial precedence cases and three combined-back cases reached
compiled failures before their corresponding fixes.


The timing regression first failed because the earlier 145 ms router skipped the monitor wait. The current twelve-check scroll suite covers 194/195/390 ms boundaries, pending-resume 395 ms, due scrolling before fresh Select at 195 ms, and a near-uint64-limit interval. This audit history describes the fixed source, not an installed update.


## Optional Quick Exit

`QuickExitTests.cpp` checks the production router and separate
`QuickExitGesture` with owned keys and a controlled clock. Fifteen checks cover
short release-to-Back, a held 90 ms close, release first observed at or after the
threshold, root/default-off immediate Back, one-shot behavior, simultaneous
inputs, pending-command cancellation, navigation/configuration/root changes,
focus/open-state reseeding, aggregate alternate-key release, key-reader failure,
and clock rollback/uint64 boundaries. No game, window or graphics device is used.

```sh
cmake -S tests/native-runtime -B /tmp/iw-portable-native -G Ninja -DCMAKE_BUILD_TYPE=Release
cmake --build /tmp/iw-portable-native --target iw_QuickExit_tests --parallel 2
ctest --test-dir /tmp/iw-portable-native -R "^QuickExit$" --output-on-failure
```

The Windows target is `iw_native_quick_exit_tests`. A nested enabled gesture
suppresses other input until it resolves. A cancelled or completed hold requires
all saved melee keys to be released before another gesture. Root/default-off
Back retains its fresh-edge behavior. `close` is a local navigation command;
it does not request controller shutdown or feature cleanup.


## Modal Width Editor

`WidthEditorInputTests.cpp` exercises the production router and separate
`WidthEditorInput` with owned keys and a controlled clock. Its 15 checks cover
exact saved attack/aim keys, slot 3/4 step changes, source-priority simultaneous
inputs, bounded-repeat dispatch, fresh entry/exit, pending acknowledgements,
focus/context cancellation and clock boundaries. No game or window is started.

```sh
cmake -S tests/native-runtime -B /tmp/iw-portable-native -G Ninja -DCMAKE_BUILD_TYPE=Release
cmake --build /tmp/iw-portable-native --target iw_WidthEditorInput_tests --parallel 2
ctest --test-dir /tmp/iw-portable-native -R "^WidthEditorInput$" --output-on-failure
```

The Windows target is `iw_native_width_editor_tests`. Width repeat uses nominal
75 ms and step repeat 125 ms, reflecting the source branch plus editor-loop
waits. Attack wins over aim, followed by slot 3, slot 4 and melee; opposite editor
inputs do not use the ordinary menu's neutral rule. At a bound the higher-priority
branch still consumes input. The 20 ms worker poll and command acknowledgement
can delay dispatch. Entry/context changes reseed held keys; finishing requires a
fresh melee press and cannot replay a normal Back. The separate unlock and animation suites model presentation timing; these
input checks alone do not prove retail timing or engine easing.


## Locked editor return

`MenuUnlockInputTests.cpp` has seven confirmed portable checks. It exercises the
production router with owned keys and clock: every ordinary saved input is
suppressed while locked, armed repeats are cancelled, pending/focus changes cannot
replay actions, and unlocking reseeds held selection/navigation/melee keys.

```sh
cmake -S tests/native-runtime -B /tmp/iw-portable-native -G Ninja -DCMAKE_BUILD_TYPE=Release
cmake --build /tmp/iw-portable-native --target iw_MenuUnlockInput_tests --parallel 2
ctest --test-dir /tmp/iw-portable-native -R "^MenuUnlockInput$" --output-on-failure
```

The Windows target is `iw_native_menu_unlock_tests`. Lock mode has no ordinary
Toggle/Close escape; owner shutdown is out of band. Backend phase tests separately
cover rebuild before 50 ms, pause before 150 ms, then unlock. The input tests do
not create a window, access game memory or establish exact visible-frame cadence.


## Modal instruction position editor

`InstructionPositionInputTests.cpp` has 11 checks using the production router,
owned saved keys and a controlled clock. They cover all four exact action slots,
Down-before-Up and vertical-before-horizontal priority, Right-before-Left,
one-shot melee Exit, 25 ms held repeats, aggregate alternate keys, pending
command consumption, startup/focus/phase/navigation reseeding, reader failure,
and clock rollback/unsigned boundaries. Aim, fire and Use cannot substitute for
the four movement slots.

```sh
cmake -S tests/native-runtime -B /tmp/iw-portable-native -G Ninja -DCMAKE_BUILD_TYPE=Release
cmake --build /tmp/iw-portable-native --target iw_InstructionPositionInput_tests --parallel 2
ctest --test-dir /tmp/iw-portable-native -R "^InstructionPositionInput$" --output-on-failure
```

The Windows target is `iw_native_instruction_position_tests`. Only Editing
routes movement; Entering and Exiting use the locked mode. The source movement
wait is 25 ms. A shared repeat clock prevents simultaneous directions or alternate
keys from accelerating it. The 20 ms worker poll and acknowledgements can delay
dispatch, so these checks do not establish retail or visible-frame timing. Held
keys from a replacement context require complete release; fresh edges observed
while a command is pending stay consumed until release. These safety rules are
retained differences from direct held-button polling.

All input runners and the saved-control formatter can also be built with
the portable schema project:

```sh
cmake -S tests/native-runtime -B /tmp/iw-portable-native -DCMAKE_BUILD_TYPE=Release
cmake --build /tmp/iw-portable-native -j 4
ctest --test-dir /tmp/iw-portable-native --output-on-failure
```
