# Saved-control help

The production formatter reads the same validated Bindings as the input router.
It shows exact saved key names and discloses additional alternatives after two
labels so instructions stay bounded. It does not change keys, write game state,
or infer bindings from the current keyboard layout.

```sh
g++ -std=c++20 -Wall -Wextra -Werror -I native/runtime \
  tests/native-controls/ControlLabelsTests.cpp native/runtime/ControlLabels.cpp \
  native/runtime/NativeInputRouter.cpp native/runtime/ChoiceRepeat.cpp \
  native/runtime/QuickExitGesture.cpp native/runtime/WidthEditorInput.cpp \
  native/runtime/InstructionPositionInput.cpp native/runtime/MenuPositionInput.cpp native/runtime/MenuCreditsInput.cpp -o /tmp/iw-control-hint-tests
/tmp/iw-control-hint-tests
```

The Windows CMake target is `iw_native_control_labels_tests`. Native runtime
checks confirm hints are copied into immutable render snapshots and leave
feature rows unchanged. CPU panel checks cover displaying custom instructions
through the same production drawing code.


A sixth regression checks enabled child tap/hold help, immediate root close help,
and unchanged scroll/Use/choice hints. Saved remapped melee keys remain explicit;
disabling the preference restores ordinary Back/close help. These strings follow
the committed appearance and explicit root identity, not a row draft or title.


The seventh regression covers five Width Editor body hints from exact saved
attack, aim, slot 3/4 and melee bindings. The editor's contextual footer copies
its Exit label from those same private hints; it does not advertise inactive
ordinary Scroll/Select controls or infer attack from the combined Down group.
Normal snapshot instructions remain unchanged for return to the original page.
The first seven formatter checks remain confirmed for the integrated width source.


Two more checks cover the five instruction-position hints from exact saved
action slots 1/2/3/4 and melee, plus rejection when any movement group is absent.
Position help cannot borrow aim or attack from combined scroll groups. Nine
formatter checks pass with the portable project in `tests/native-runtime`; no
window, game, graphics device or Windows API is used.

Two additional checks cover seven menu-reposition hints from exact saved slots
1–4, frag, secondary offhand and melee. Missing offset or movement groups reject
that editor help while retaining legacy ordinary/width/instruction help. The
contextual editor footer uses the same saved Exit key. Eleven formatter checks
now pass.

The credits formatter adds two checks for exact saved melee alternatives and
rejection of missing, duplicated or aliased keys. Optional editor binding groups
are not needed for credits. Thirteen formatter checks pass; normal navigation
help remains available unchanged when credits finishes.
