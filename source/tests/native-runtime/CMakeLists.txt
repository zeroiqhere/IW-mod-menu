cmake_minimum_required(VERSION 3.20)
project(IWPortableSchemaTests LANGUAGES CXX)
enable_testing()
set(IW_SCHEMA_ROOT "${CMAKE_CURRENT_LIST_DIR}/../..")
# The test header supplies types only and never loads Windows, a game or graphics
add_executable(iw_native_schema_tests
    chrome/theme/ChromeThemeSchemaTests.cpp chrome/theme/ChromeThemePreviewTests.cpp
    instructions/theme/InstructionThemeSchemaTests.cpp instructions/theme/InstructionThemePreviewTests.cpp
    instructions/color/InstructionColorPreviewTests.cpp instructions/color/InstructionColorSchemaTests.cpp instructions/InstructionHudSchemaTests.cpp instructions/InstructionHudPreviewTests.cpp
    chrome/MenuChromeSchemaTests.cpp chrome/MenuChromePreviewTests.cpp
    paint/BooleanPaintSchemaTests.cpp
    paint/BooleanPaintReceiptTests.cpp
    paint/BooleanPaintPreviewTests.cpp
    callbacks/CallbackSchemaTests.cpp callbacks/CallbackPreviewTests.cpp portable/SchemaTestsMain.cpp quick/LocalMenuEligibilitySchemaTests.cpp quick/LocalMenuCommandTests.cpp quick/LocalMenuReceiptTests.cpp quick/LocalMenuEligibilityBackendTests.cpp quick/LocalMenuEligibilityBackendPreviewTests.cpp quick/LocalMenuEligibilityPreviewTests.cpp OpeningPoliciesSchemaTests.cpp OpeningPoliciesBackendTests.cpp scroll/SpacerScrollBackendTests.cpp scroll/SpacerScrollSchemaTests.cpp scroll/SpacerScrollPlanTests.cpp scroll/SpacerScrollReceiptTrackerTests.cpp OpeningControlsSchemaTests.cpp OpeningControlsPreviewTests.cpp OpeningRuntimeStartupTests.cpp OpeningControlsBackendTests.cpp StealthBackendTests.cpp StealthPreviewTests.cpp MenuPreferenceRebuildSchemaTests.cpp StealthAppearanceSchemaTests.cpp MenuDesignFooterSchemaTests.cpp MenuDesignFooterPreviewTests.cpp MenuOpenTransitionSchemaTests.cpp MenuOpenTransitionBackendTests.cpp MenuOpenTransitionPreviewTests.cpp MenuOpeningSchemaTests.cpp MenuOpeningBackendTests.cpp MenuOpeningPreviewTests.cpp MenuRowCapacityTests.cpp MenuDesignAppearanceSchemaTests.cpp MenuDesignBackendTests.cpp MenuDesignPreviewTests.cpp MenuDesignTransitionSchemaTests.cpp MenuPreferenceRefreshSchemaTests.cpp MenuPreferenceRefreshBackendTests.cpp MenuPreferenceRefreshPreviewTests.cpp MenuCreditsSchemaTests.cpp MenuCreditsPreviewTests.cpp MenuPositionSchemaTests.cpp MenuPositionPreviewTests.cpp InstructionAppearanceSchemaTests.cpp InstructionPreviewTests.cpp
    InstructionPositionSchemaTests.cpp RainbowThemeSchemaTests.cpp RainbowThemePreviewTests.cpp SchemaTests.cpp
    WidthEditorSchemaTests.cpp MenuUnlockSchemaTests.cpp ScrollAnimationSchemaTests.cpp InputTests.cpp BackendStateTests.cpp
    "${IW_SCHEMA_ROOT}/native/runtime/RuntimeSchema.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/OpeningControlsSchema.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/LocalMenuEligibilitySchema.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/instructions/InstructionHudSchema.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/instructions/InstructionThemeSchema.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/chrome/ChromeThemeSchema.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/MenuOptionCallbackSchema.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/monitor/MenuMonitorWaitSchema.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/monitor/MenuUseContinuationSchema.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/monitor/MenuMonitorContinuationSchema.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/MenuBooleanPaintSchema.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/RuntimeStartup.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/scroll/SpacerScrollPlan.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/scroll/SpacerScrollReceiptTracker.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/BoundedSessionJson.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/NativeInputRouter.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/monitor/SourceUseInput.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/monitor/SourceMonitorInput.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/quick/ClosedMenuOpeningArbitrator.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/ChoiceRepeat.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/QuickExitGesture.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/WidthEditorInput.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/InstructionPositionInput.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/MenuPositionInput.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/MenuCreditsInput.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/ControlLabels.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/PresentationFocusTracker.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/SnapshotHeartbeat.cpp")
target_sources(iw_native_schema_tests PRIVATE "${IW_SCHEMA_ROOT}/tests/native-layout/BackendPreview.cpp")
target_compile_features(iw_native_schema_tests PRIVATE cxx_std_20)
target_compile_options(iw_native_schema_tests PRIVATE -Wall -Wextra -Werror)
target_include_directories(iw_native_schema_tests PRIVATE portable
    "${IW_SCHEMA_ROOT}/native/runtime" "${IW_SCHEMA_ROOT}/third_party/json/include")
add_test(NAME NativeSchemaTests COMMAND iw_native_schema_tests)

# Every existing pure input runner shares the changed validator and router implementation
set(IW_PORTABLE_INPUT_SOURCES
    "${IW_SCHEMA_ROOT}/native/runtime/NativeInputRouter.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/monitor/SourceUseInput.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/monitor/SourceMonitorInput.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/quick/ClosedMenuOpeningArbitrator.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/ChoiceRepeat.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/QuickExitGesture.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/WidthEditorInput.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/InstructionPositionInput.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/MenuPositionInput.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/MenuCreditsInput.cpp")
add_library(iw_portable_input STATIC ${IW_PORTABLE_INPUT_SOURCES})
target_compile_features(iw_portable_input PUBLIC cxx_std_20)
target_compile_options(iw_portable_input PRIVATE -Wall -Wextra -Werror)
target_include_directories(iw_portable_input PUBLIC "${IW_SCHEMA_ROOT}/native/runtime")
foreach(case InputParity ChoiceInput QuickExit WidthEditorInput MenuUnlockInput InstructionPositionInput ScrollAnimationInput MenuPositionInput MenuCreditsInput MainOpeningInput OpeningPoliciesInput)
    add_executable("iw_${case}_tests" "${IW_SCHEMA_ROOT}/tests/native-input/${case}Tests.cpp")
    target_link_libraries("iw_${case}_tests" PRIVATE iw_portable_input)
    target_compile_options("iw_${case}_tests" PRIVATE -Wall -Wextra -Werror)
    add_test(NAME "${case}" COMMAND "iw_${case}_tests")
endforeach()
add_executable(iw_portable_controls_tests "${IW_SCHEMA_ROOT}/tests/native-controls/ControlLabelsTests.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/ControlLabels.cpp")
target_link_libraries(iw_portable_controls_tests PRIVATE iw_portable_input)
target_compile_options(iw_portable_controls_tests PRIVATE -Wall -Wextra -Werror)
add_test(NAME SavedControlLabels COMMAND iw_portable_controls_tests)

# Scoped opening checks share the production router and copied caller facts
add_executable(iw_QuickOpeningRouter_tests "${IW_SCHEMA_ROOT}/tests/native-input/quick-opening/QuickOpeningRouterTests.cpp")
target_link_libraries(iw_QuickOpeningRouter_tests PRIVATE iw_portable_input)
target_compile_options(iw_QuickOpeningRouter_tests PRIVATE -Wall -Wextra -Werror)
add_test(NAME QuickOpeningRouter COMMAND iw_QuickOpeningRouter_tests)

# Source monitor precedence uses the actual router and copied owner facts
add_executable(iw_SourceMonitorPriority_tests
    "${IW_SCHEMA_ROOT}/tests/native-input/menu-monitor/SourceMonitorPriorityTests.cpp")
target_link_libraries(iw_SourceMonitorPriority_tests PRIVATE iw_portable_input)
target_compile_options(iw_SourceMonitorPriority_tests PRIVATE -Wall -Wextra -Werror)
add_test(NAME SourceMonitorPriority COMMAND iw_SourceMonitorPriority_tests)

include("${CMAKE_CURRENT_LIST_DIR}/callbacks/fixtures/CallbackFixtureTargets.cmake")
include("${CMAKE_CURRENT_LIST_DIR}/boolean-paint/fixtures/BooleanPaintFixtureTargets.cmake")

# Local UI caller graphs remain separate from the existing preview inventories
include("${CMAKE_CURRENT_LIST_DIR}/local-options/fixtures/LocalOptionFixtureTargets.cmake")

# Instruction caller graphs retain their own fixed inventory
include("${CMAKE_CURRENT_LIST_DIR}/instruction-options/fixtures/InstructionOptionFixtureTargets.cmake")

# Real chrome creation graphs retain their own fixed inventory
include("${CMAKE_CURRENT_LIST_DIR}/chrome/fixtures/BasicChromeFixtureTargets.cmake")

# Independent instructions use a separate actual controller graph
include("${CMAKE_CURRENT_LIST_DIR}/instructions/fixtures/InstructionHudFixtureTargets.cmake")

# Independently owned footer color graphs do not replace any earlier inventory
include("${CMAKE_CURRENT_LIST_DIR}/instructions/color/fixtures/InstructionColorFixtureTargets.cmake")

# Footer journal interpolation is a portable owned RGB adapter
include("${IW_SCHEMA_ROOT}/tests/native-instruction-theme/InstructionThemeTargets.cmake")

# Captured invocation graphs remain separate from all earlier fixed inventories
include("${IW_SCHEMA_ROOT}/tests/native-runtime/instructions/theme/fixtures/InstructionThemeFixtureTargets.cmake")

include("${IW_SCHEMA_ROOT}/tests/native-chrome-theme/ChromeThemeTargets.cmake")

# Chrome invocation graphs remain separate from every earlier inventory
include("${IW_SCHEMA_ROOT}/tests/native-runtime/chrome/theme/fixtures/ChromeThemeFixtureTargets.cmake")

# Footer withdrawal keeps a separate real controller inventory
include("${IW_SCHEMA_ROOT}/tests/native-runtime/instructions/withdrawal/fixtures/FooterWithdrawalFixtureTargets.cmake")

include("${IW_SCHEMA_ROOT}/tests/native-instruction-observation/InstructionObservationTargets.cmake")
include("${IW_SCHEMA_ROOT}/tests/native-runtime/instructions/observation/graph/FooterObservationGraphTargets.cmake")

# Source player navigation joins its own actual controller graph
include("${CMAKE_CURRENT_LIST_DIR}/players/fixtures/PlayerNavigationFixtureTargets.cmake")

# Mechanical source waits preserve the accepted choice repeat without modal reseeding
add_executable(iw_MonitorWaitInput_tests "${IW_SCHEMA_ROOT}/tests/native-input/monitor-waits/MonitorWaitInputTests.cpp")
target_link_libraries(iw_MonitorWaitInput_tests PRIVATE iw_portable_input)
target_compile_options(iw_MonitorWaitInput_tests PRIVATE -Wall -Wextra -Werror)
add_test(NAME MonitorWaitInput COMMAND iw_MonitorWaitInput_tests)

include("${CMAKE_CURRENT_LIST_DIR}/monitor/MonitorWaitFixtureTargets.cmake")

# Held Use checks consume copied invocation proof without opening a game or renderer
add_executable(iw_UseContinuationInput_tests "${IW_SCHEMA_ROOT}/tests/native-input/use-monitor/UseContinuationInputTests.cpp")
target_link_libraries(iw_UseContinuationInput_tests PRIVATE iw_portable_input)
target_compile_options(iw_UseContinuationInput_tests PRIVATE -Wall -Wextra -Werror)
add_test(NAME UseContinuationInput COMMAND iw_UseContinuationInput_tests)
include("${CMAKE_CURRENT_LIST_DIR}/use-monitor/UseContinuationSchemaTargets.cmake")

# Lower source branches resume only after the original causal Use callback is ready
add_executable(iw_use_followup_input_tests "${IW_SCHEMA_ROOT}/tests/native-input/use-followup/UseFollowupInputTests.cpp")
target_link_libraries(iw_use_followup_input_tests PRIVATE iw_portable_input)
target_compile_options(iw_use_followup_input_tests PRIVATE -Wall -Wextra -Werror)
add_test(NAME UseFollowupInput COMMAND iw_use_followup_input_tests)
include("${CMAKE_CURRENT_LIST_DIR}/use-followup/UseFollowupTargets.cmake")

# Mechanical continuation tests use the same production router and explicit causal packets
add_executable(iw_mechanical_monitor_input_tests "${IW_SCHEMA_ROOT}/tests/native-input/mechanical-monitor/MechanicalMonitorInputTests.cpp")
target_link_libraries(iw_mechanical_monitor_input_tests PRIVATE iw_portable_input)
target_compile_options(iw_mechanical_monitor_input_tests PRIVATE -Wall -Wextra -Werror)
add_test(NAME MechanicalMonitorInput COMMAND iw_mechanical_monitor_input_tests)

# Mechanical receipt readers and command scopes share the existing strict parser support
include("${IW_SCHEMA_ROOT}/tests/native-runtime/mechanical-monitor/MechanicalSchemaTargets.cmake")

# Held mechanical branches join the real controller packet inventory
include("${IW_SCHEMA_ROOT}/tests/native-runtime/mechanical-monitor/MechanicalGraphTargets.cmake")

# Explicit current-level opening checks do not change the legacy default sampler
include("${IW_SCHEMA_ROOT}/tests/native-input/closed-level-opening/ClosedLevelOpeningTargets.cmake")

# Actual opening phases and permissions use their own unchanged controller inventory
include("${IW_SCHEMA_ROOT}/tests/native-runtime/closed-level-opening/ClosedLevelOpeningGraphTargets.cmake")

# Closed source cycles keep their logical fifty wait separate from transport and callback pauses
include("${IW_SCHEMA_ROOT}/tests/native-input/closed-source-cadence/ClosedSourceCadenceTargets.cmake")
include("${IW_SCHEMA_ROOT}/tests/native-input/ordinary-source-level/OrdinarySourceLevelTargets.cmake")
include("${CMAKE_CURRENT_LIST_DIR}/ordinary-source-level/OrdinarySourceLevelTargets.cmake")
include("${CMAKE_CURRENT_LIST_DIR}/preference-use-repeat/PreferenceUseRepeatTargets.cmake")
include("${CMAKE_CURRENT_LIST_DIR}/opening-controls-use/OpeningControlsUseTargets.cmake")
include("${CMAKE_CURRENT_LIST_DIR}/rejected-preferences/RejectedPreferencesTargets.cmake")
include("${CMAKE_CURRENT_LIST_DIR}/recovery/OwnedCleanupGraphTargets.cmake")
