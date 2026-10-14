# Runtime and tests share parser and input code without linking the renderer
set(IW_RUNTIME_ROOT "${CMAKE_CURRENT_LIST_DIR}/../..")
add_library(iw_overlay_runtime STATIC
    "${CMAKE_CURRENT_LIST_DIR}/NativeInputRouter.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/SourceUseInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/SourceMonitorInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ChoiceRepeat.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/QuickExitGesture.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/InstructionPositionInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuPositionInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuCreditsInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/WidthEditorInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ControlLabels.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/RuntimeSchema.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/OpeningControlsSchema.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/LocalMenuEligibilitySchema.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/instructions/InstructionHudSchema.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/instructions/InstructionThemeSchema.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/chrome/ChromeThemeSchema.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuOptionCallbackSchema.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/MenuMonitorWaitSchema.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/MenuUseContinuationSchema.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/MenuMonitorContinuationSchema.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuBooleanPaintSchema.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/RuntimeStartup.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/scroll/SpacerScrollPlan.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/scroll/SpacerScrollReceiptTracker.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/BoundedSessionJson.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/SessionFiles.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/SnapshotHeartbeat.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/OverlayRuntime.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/PresentationFocusTracker.cpp")
target_compile_features(iw_overlay_runtime PUBLIC cxx_std_20)
target_compile_options(iw_overlay_runtime PRIVATE -Wall -Wextra -Werror)
target_include_directories(iw_overlay_runtime PUBLIC
    "${CMAKE_CURRENT_LIST_DIR}"
    "${IW_RUNTIME_ROOT}/third_party/json/include")
target_compile_definitions(iw_overlay_runtime PUBLIC WIN32_LEAN_AND_MEAN NOMINMAX)
target_link_libraries(iw_overlay_runtime PUBLIC user32)

add_executable(iw_native_runtime_tests
    "${IW_RUNTIME_ROOT}/tests/native-runtime/chrome/theme/ChromeThemeSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/instructions/theme/InstructionThemeSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/instructions/color/InstructionColorSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/instructions/InstructionHudSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/chrome/MenuChromeSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/paint/BooleanPaintSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/paint/BooleanPaintReceiptTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/callbacks/CallbackSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/quick/LocalMenuEligibilityBackendTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/quick/LocalMenuReceiptTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/quick/LocalMenuCommandTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/quick/LocalMenuEligibilitySchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/OpeningPoliciesSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/OpeningPoliciesBackendTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/OpeningControlsSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/OpeningRuntimeStartupTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/OpeningControlsBackendTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/MenuPreferenceRebuildSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/StealthBackendTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/StealthAppearanceSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/RuntimeTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/scroll/SpacerScrollBackendTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/scroll/SpacerScrollSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/scroll/SpacerScrollPlanTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/scroll/SpacerScrollReceiptTrackerTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/MenuDesignFooterSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/MenuOpenTransitionSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/MenuOpenTransitionBackendTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/MenuOpeningBackendTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/MenuOpeningSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/MenuRowCapacityTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/MenuDesignBackendTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/MenuDesignAppearanceSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/MenuDesignTransitionSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/MenuPreferenceRefreshBackendTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/MenuPreferenceRefreshSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/MenuCreditsSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/MenuPositionSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/InputTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/SchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/InstructionAppearanceSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/InstructionPositionSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/RainbowThemeSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/ScrollAnimationSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/WidthEditorSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/MenuUnlockSchemaTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/BackendStateTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/RendererStatusStub.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/ReplacementTests.cpp"
    "${IW_RUNTIME_ROOT}/tests/native-runtime/FileTests.cpp")
target_link_libraries(iw_native_runtime_tests PRIVATE iw_overlay_runtime)
target_compile_options(iw_native_runtime_tests PRIVATE -Wall -Wextra -Werror)
if(MINGW)
    target_link_options(iw_native_runtime_tests PRIVATE -static -static-libgcc -static-libstdc++)
endif()

# Input parity tests use a deterministic clock and require no game or renderer
add_executable(iw_native_input_tests
    "${IW_RUNTIME_ROOT}/tests/native-input/InputParityTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ChoiceRepeat.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/QuickExitGesture.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/InstructionPositionInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuPositionInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuCreditsInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/WidthEditorInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/NativeInputRouter.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/SourceUseInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/SourceMonitorInput.cpp")
target_include_directories(iw_native_input_tests PRIVATE "${CMAKE_CURRENT_LIST_DIR}")
target_compile_features(iw_native_input_tests PRIVATE cxx_std_20)
target_compile_options(iw_native_input_tests PRIVATE -Wall -Wextra -Werror)
if(MINGW)
    target_link_options(iw_native_input_tests PRIVATE -static -static-libgcc -static-libstdc++)
endif()

# Saved-control display checks share the real formatter without any window or game
add_executable(iw_native_control_labels_tests
    "${IW_RUNTIME_ROOT}/tests/native-controls/ControlLabelsTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ChoiceRepeat.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/QuickExitGesture.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/InstructionPositionInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuPositionInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuCreditsInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/WidthEditorInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ControlLabels.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/NativeInputRouter.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/SourceUseInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/SourceMonitorInput.cpp")
target_include_directories(iw_native_control_labels_tests PRIVATE "${CMAKE_CURRENT_LIST_DIR}")
target_compile_features(iw_native_control_labels_tests PRIVATE cxx_std_20)
target_compile_options(iw_native_control_labels_tests PRIVATE -Wall -Wextra -Werror)
if(MINGW)
    target_link_options(iw_native_control_labels_tests PRIVATE -static -static-libgcc -static-libstdc++)
endif()

# Local choice repeat checks never connect to a window, renderer or game
add_executable(iw_native_choice_input_tests
    "${IW_RUNTIME_ROOT}/tests/native-input/ChoiceInputTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/NativeInputRouter.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/SourceUseInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/SourceMonitorInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ChoiceRepeat.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/QuickExitGesture.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/InstructionPositionInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuPositionInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuCreditsInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/WidthEditorInput.cpp")
target_include_directories(iw_native_choice_input_tests PRIVATE "${CMAKE_CURRENT_LIST_DIR}")
target_compile_features(iw_native_choice_input_tests PRIVATE cxx_std_20)
target_compile_options(iw_native_choice_input_tests PRIVATE -Wall -Wextra -Werror)
if(MINGW)
    target_link_options(iw_native_choice_input_tests PRIVATE -static -static-libgcc -static-libstdc++)
endif()

# Knife hold checks use owned input and never open a game or graphics device
add_executable(iw_native_quick_exit_tests
    "${IW_RUNTIME_ROOT}/tests/native-input/QuickExitTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/NativeInputRouter.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/SourceUseInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/SourceMonitorInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ChoiceRepeat.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/QuickExitGesture.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/InstructionPositionInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuPositionInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuCreditsInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/WidthEditorInput.cpp")
target_include_directories(iw_native_quick_exit_tests PRIVATE "${CMAKE_CURRENT_LIST_DIR}")
target_compile_features(iw_native_quick_exit_tests PRIVATE cxx_std_20)
target_compile_options(iw_native_quick_exit_tests PRIVATE -Wall -Wextra -Werror)
if(MINGW)
    target_link_options(iw_native_quick_exit_tests PRIVATE -static -static-libgcc -static-libstdc++)
endif()

# Width editor checks own their keys and clock without a window or game
add_executable(iw_native_width_editor_tests
    "${IW_RUNTIME_ROOT}/tests/native-input/WidthEditorInputTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/NativeInputRouter.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/SourceUseInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/SourceMonitorInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ChoiceRepeat.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/QuickExitGesture.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/InstructionPositionInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuPositionInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuCreditsInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/WidthEditorInput.cpp")
target_include_directories(iw_native_width_editor_tests PRIVATE "${CMAKE_CURRENT_LIST_DIR}")
target_compile_features(iw_native_width_editor_tests PRIVATE cxx_std_20)
target_compile_options(iw_native_width_editor_tests PRIVATE -Wall -Wextra -Werror)
if(MINGW)
    target_link_options(iw_native_width_editor_tests PRIVATE -static -static-libgcc -static-libstdc++)
endif()

# Unlock tests own their keys and never start a game or graphics device
add_executable(iw_native_menu_unlock_tests
    "${IW_RUNTIME_ROOT}/tests/native-input/MenuUnlockInputTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/NativeInputRouter.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/SourceUseInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/SourceMonitorInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ChoiceRepeat.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/QuickExitGesture.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/InstructionPositionInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuPositionInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuCreditsInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/WidthEditorInput.cpp")
target_include_directories(iw_native_menu_unlock_tests PRIVATE "${CMAKE_CURRENT_LIST_DIR}")
target_compile_features(iw_native_menu_unlock_tests PRIVATE cxx_std_20)
target_compile_options(iw_native_menu_unlock_tests PRIVATE -Wall -Wextra -Werror)
if(MINGW)
    target_link_options(iw_native_menu_unlock_tests PRIVATE -static -static-libgcc -static-libstdc++)
endif()

# Exact position actions use owned key snapshots and never open a graphics device
add_executable(iw_native_instruction_position_tests
    "${IW_RUNTIME_ROOT}/tests/native-input/InstructionPositionInputTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/NativeInputRouter.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/SourceUseInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/SourceMonitorInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ChoiceRepeat.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/QuickExitGesture.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/WidthEditorInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/InstructionPositionInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuPositionInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuCreditsInput.cpp")
target_include_directories(iw_native_instruction_position_tests PRIVATE "${CMAKE_CURRENT_LIST_DIR}")
target_compile_features(iw_native_instruction_position_tests PRIVATE cxx_std_20)
target_compile_options(iw_native_instruction_position_tests PRIVATE -Wall -Wextra -Werror)
if(MINGW)
    target_link_options(iw_native_instruction_position_tests PRIVATE -static -static-libgcc -static-libstdc++)
endif()

add_executable(iw_native_scroll_animation_tests
    "${IW_RUNTIME_ROOT}/tests/native-input/ScrollAnimationInputTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/NativeInputRouter.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/SourceUseInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/SourceMonitorInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ChoiceRepeat.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/QuickExitGesture.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/WidthEditorInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/InstructionPositionInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuPositionInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuCreditsInput.cpp")
target_include_directories(iw_native_scroll_animation_tests PRIVATE "${CMAKE_CURRENT_LIST_DIR}")
target_compile_features(iw_native_scroll_animation_tests PRIVATE cxx_std_20)
target_compile_options(iw_native_scroll_animation_tests PRIVATE -Wall -Wextra -Werror)
if(MINGW)
    target_link_options(iw_native_scroll_animation_tests PRIVATE -static -static-libgcc -static-libstdc++)
endif()

# Menu position timing uses owned action groups and no desktop or game state
add_executable(iw_native_menu_position_tests
    "${IW_RUNTIME_ROOT}/tests/native-input/MenuPositionInputTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/NativeInputRouter.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/SourceUseInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/SourceMonitorInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ChoiceRepeat.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/QuickExitGesture.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/WidthEditorInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/InstructionPositionInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuPositionInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuCreditsInput.cpp")
target_include_directories(iw_native_menu_position_tests PRIVATE "${CMAKE_CURRENT_LIST_DIR}")
target_compile_features(iw_native_menu_position_tests PRIVATE cxx_std_20)
target_compile_options(iw_native_menu_position_tests PRIVATE -Wall -Wextra -Werror)
if(MINGW)
    target_link_options(iw_native_menu_position_tests PRIVATE -static -static-libgcc -static-libstdc++)
endif()

# Credits input checks use exact owned melee snapshots without a window or clock thread
add_executable(iw_native_menu_credits_tests
    "${IW_RUNTIME_ROOT}/tests/native-input/MenuCreditsInputTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/NativeInputRouter.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/SourceUseInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/monitor/SourceMonitorInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ChoiceRepeat.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/QuickExitGesture.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/WidthEditorInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/InstructionPositionInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuPositionInput.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MenuCreditsInput.cpp")
target_include_directories(iw_native_menu_credits_tests PRIVATE "${CMAKE_CURRENT_LIST_DIR}")
target_compile_features(iw_native_menu_credits_tests PRIVATE cxx_std_20)
target_compile_options(iw_native_menu_credits_tests PRIVATE -Wall -Wextra -Werror)
if(MINGW)
    target_link_options(iw_native_menu_credits_tests PRIVATE -static -static-libgcc -static-libstdc++)
endif()

# This deterministic router target never reads real keys or creates a window
add_executable(iw_main_opening_input_tests "${IW_RUNTIME_ROOT}/tests/native-input/MainOpeningInputTests.cpp")
target_link_libraries(iw_main_opening_input_tests PRIVATE iw_overlay_runtime)
target_compile_options(iw_main_opening_input_tests PRIVATE -Wall -Wextra -Werror)

include("${CMAKE_CURRENT_LIST_DIR}/quick/QuickMenuTargets.cmake")

# Dual policy tests use the ordinary Main router without a Quick activation adapter
add_executable(iw_opening_policies_input_tests "${IW_RUNTIME_ROOT}/tests/native-input/OpeningPoliciesInputTests.cpp")
target_link_libraries(iw_opening_policies_input_tests PRIVATE iw_overlay_runtime)
target_compile_options(iw_opening_policies_input_tests PRIVATE -Wall -Wextra -Werror)

# Direct portable input counterparts share the same closed-menu arbitration implementation
foreach(inputTarget iw_native_input_tests iw_native_control_labels_tests iw_native_choice_input_tests iw_native_quick_exit_tests iw_native_width_editor_tests iw_native_menu_unlock_tests iw_native_instruction_position_tests iw_native_scroll_animation_tests iw_native_menu_position_tests iw_native_menu_credits_tests)
    target_sources(${inputTarget} PRIVATE "${CMAKE_CURRENT_LIST_DIR}/quick/ClosedMenuOpeningArbitrator.cpp")
endforeach()

# Scoped router checks use owned facts and deterministic key samples
add_executable(iw_quick_opening_router_tests "${IW_RUNTIME_ROOT}/tests/native-input/quick-opening/QuickOpeningRouterTests.cpp")
target_link_libraries(iw_quick_opening_router_tests PRIVATE iw_overlay_runtime)

# Both Windows and portable builds exercise the same source monitor arbitration
add_executable(iw_source_monitor_priority_tests
    "${IW_RUNTIME_ROOT}/tests/native-input/menu-monitor/SourceMonitorPriorityTests.cpp")
target_link_libraries(iw_source_monitor_priority_tests PRIVATE iw_overlay_runtime)
target_compile_options(iw_source_monitor_priority_tests PRIVATE -Wall -Wextra -Werror)
add_test(NAME SourceMonitorPriority COMMAND iw_source_monitor_priority_tests)
target_compile_options(iw_quick_opening_router_tests PRIVATE -Wall -Wextra -Werror)

# Connected callback exports use a separate bounded fixture inventory
set(IW_SCHEMA_ROOT "${IW_RUNTIME_ROOT}")
include("${IW_RUNTIME_ROOT}/tests/native-runtime/callbacks/fixtures/CallbackFixtureTargets.cmake")
include("${IW_RUNTIME_ROOT}/tests/native-runtime/boolean-paint/fixtures/BooleanPaintFixtureTargets.cmake")

# Local UI caller graphs remain separate from the existing preview inventories
include("${IW_RUNTIME_ROOT}/tests/native-runtime/local-options/fixtures/LocalOptionFixtureTargets.cmake")

# Instruction caller graphs retain their own fixed inventory
include("${IW_RUNTIME_ROOT}/tests/native-runtime/instruction-options/fixtures/InstructionOptionFixtureTargets.cmake")

# Real chrome creation graphs retain their own fixed inventory
include("${IW_RUNTIME_ROOT}/tests/native-runtime/chrome/fixtures/BasicChromeFixtureTargets.cmake")

# Independent instructions use a separate actual controller graph
include("${IW_RUNTIME_ROOT}/tests/native-runtime/instructions/fixtures/InstructionHudFixtureTargets.cmake")

# Independently owned footer color graphs do not replace any earlier inventory
include("${IW_RUNTIME_ROOT}/tests/native-runtime/instructions/color/fixtures/InstructionColorFixtureTargets.cmake")

# Footer journal interpolation is a portable owned RGB adapter
include("${IW_RUNTIME_ROOT}/tests/native-instruction-theme/InstructionThemeTargets.cmake")

# Captured invocation graphs remain separate from all earlier fixed inventories
include("${IW_RUNTIME_ROOT}/tests/native-runtime/instructions/theme/fixtures/InstructionThemeFixtureTargets.cmake")

include("${IW_RUNTIME_ROOT}/tests/native-chrome-theme/ChromeThemeTargets.cmake")

# Chrome invocation graphs remain separate from every earlier inventory
include("${IW_RUNTIME_ROOT}/tests/native-runtime/chrome/theme/fixtures/ChromeThemeFixtureTargets.cmake")

# Footer withdrawal keeps a separate real controller inventory
include("${IW_RUNTIME_ROOT}/tests/native-runtime/instructions/withdrawal/fixtures/FooterWithdrawalFixtureTargets.cmake")

# Footer observation is tested through the phase used by the production renderer
include("${IW_RUNTIME_ROOT}/tests/native-instruction-observation/InstructionObservationTargets.cmake")
include("${IW_RUNTIME_ROOT}/tests/native-runtime/instructions/observation/graph/FooterObservationGraphTargets.cmake")

# Source player navigation joins its own actual controller graph
include("${IW_RUNTIME_ROOT}/tests/native-runtime/players/fixtures/PlayerNavigationFixtureTargets.cmake")

include("${IW_RUNTIME_ROOT}/tests/native-runtime/monitor/MonitorWaitFixtureTargets.cmake")

add_executable(iw_monitor_wait_input_tests "${IW_RUNTIME_ROOT}/tests/native-input/monitor-waits/MonitorWaitInputTests.cpp")
target_link_libraries(iw_monitor_wait_input_tests PRIVATE iw_overlay_runtime)
target_compile_options(iw_monitor_wait_input_tests PRIVATE -Wall -Wextra -Werror)
add_test(NAME MonitorWaitInput COMMAND iw_monitor_wait_input_tests)

# Held Use keeps the same tested implementation in Windows and portable builds
include("${IW_RUNTIME_ROOT}/tests/native-runtime/use-monitor/UseContinuationSchemaTargets.cmake")
add_executable(iw_use_continuation_input_tests "${IW_RUNTIME_ROOT}/tests/native-input/use-monitor/UseContinuationInputTests.cpp")
target_link_libraries(iw_use_continuation_input_tests PRIVATE iw_overlay_runtime)
target_compile_options(iw_use_continuation_input_tests PRIVATE -Wall -Wextra -Werror)
add_test(NAME UseContinuationInput COMMAND iw_use_continuation_input_tests)

# Lower source branches use the same deterministic router in Windows builds
add_executable(iw_use_followup_input_tests "${IW_RUNTIME_ROOT}/tests/native-input/use-followup/UseFollowupInputTests.cpp")
target_link_libraries(iw_use_followup_input_tests PRIVATE iw_overlay_runtime)
target_compile_options(iw_use_followup_input_tests PRIVATE -Wall -Wextra -Werror)
add_test(NAME UseFollowupInput COMMAND iw_use_followup_input_tests)
include("${IW_RUNTIME_ROOT}/tests/native-runtime/use-followup/UseFollowupTargets.cmake")

# Mechanical continuation tests use the same production router and explicit causal packets
add_executable(iw_mechanical_monitor_input_tests "${IW_RUNTIME_ROOT}/tests/native-input/mechanical-monitor/MechanicalMonitorInputTests.cpp")
target_link_libraries(iw_mechanical_monitor_input_tests PRIVATE iw_overlay_runtime)
target_compile_options(iw_mechanical_monitor_input_tests PRIVATE -Wall -Wextra -Werror)
add_test(NAME MechanicalMonitorInput COMMAND iw_mechanical_monitor_input_tests)

# Mechanical receipt readers and command scopes share the existing strict parser support
include("${IW_RUNTIME_ROOT}/tests/native-runtime/mechanical-monitor/MechanicalSchemaTargets.cmake")

# Held mechanical branches join the real controller packet inventory
include("${IW_RUNTIME_ROOT}/tests/native-runtime/mechanical-monitor/MechanicalGraphTargets.cmake")

# Explicit current-level opening checks do not change the legacy default sampler
include("${IW_RUNTIME_ROOT}/tests/native-input/closed-level-opening/ClosedLevelOpeningTargets.cmake")

# Windows compilation joins the same actual opening inventory as the portable runner
include("${IW_RUNTIME_ROOT}/tests/native-runtime/closed-level-opening/ClosedLevelOpeningGraphTargets.cmake")

# The Windows build uses the same production cadence runner as the portable checks
include("${IW_RUNTIME_ROOT}/tests/native-input/closed-source-cadence/ClosedSourceCadenceTargets.cmake")
include("${IW_RUNTIME_ROOT}/tests/native-input/ordinary-source-level/OrdinarySourceLevelTargets.cmake")
include("${IW_RUNTIME_ROOT}/tests/native-runtime/ordinary-source-level/OrdinarySourceLevelTargets.cmake")
include("${IW_RUNTIME_ROOT}/tests/native-runtime/preference-use-repeat/PreferenceUseRepeatTargets.cmake")
