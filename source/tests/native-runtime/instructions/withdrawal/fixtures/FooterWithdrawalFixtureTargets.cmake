# Qualified footer withdrawals use a separate actual controller chronology
add_executable(iw_footer_withdrawal_fixture_tests
    "${CMAKE_CURRENT_LIST_DIR}/FooterWithdrawalFixtureMain.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/FooterWithdrawalFixtureTests.cpp"
    "${IW_SCHEMA_ROOT}/tests/native-layout/BackendPreview.cpp"
    "${IW_SCHEMA_ROOT}/native/render/instructions/InstructionHudLifetime.cpp"
    "${IW_SCHEMA_ROOT}/native/render/instructions/color/InstructionHudColorHistory.cpp" "${IW_SCHEMA_ROOT}/native/render/instructions/color/InstructionThemeTimeline.cpp")
target_compile_features(iw_footer_withdrawal_fixture_tests PRIVATE cxx_std_20)
target_compile_options(iw_footer_withdrawal_fixture_tests PRIVATE -Wall -Wextra -Werror)
if(TARGET iw_overlay_runtime)
    target_link_libraries(iw_footer_withdrawal_fixture_tests PRIVATE iw_overlay_runtime)
else()
    target_sources(iw_footer_withdrawal_fixture_tests PRIVATE
        "${IW_SCHEMA_ROOT}/native/runtime/RuntimeSchema.cpp"
        "${IW_SCHEMA_ROOT}/native/runtime/ControlLabels.cpp"
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
        "${IW_SCHEMA_ROOT}/native/runtime/BoundedSessionJson.cpp"
        "${IW_SCHEMA_ROOT}/native/runtime/scroll/SpacerScrollPlan.cpp"
        "${IW_SCHEMA_ROOT}/native/runtime/scroll/SpacerScrollReceiptTracker.cpp")
    target_link_libraries(iw_footer_withdrawal_fixture_tests PRIVATE iw_portable_input)
    target_include_directories(iw_footer_withdrawal_fixture_tests PRIVATE
        "${IW_SCHEMA_ROOT}/tests/native-runtime/portable" "${IW_SCHEMA_ROOT}/third_party/json/include")
endif()
set(IW_FOOTER_WITHDRAWAL_FIXTURE_DIRECTORY "" CACHE PATH "Actual qualified footer withdrawal graph directory")
if(IW_FOOTER_WITHDRAWAL_FIXTURE_DIRECTORY)
    add_test(NAME ActualFooterWithdrawalFixtures COMMAND iw_footer_withdrawal_fixture_tests "${IW_FOOTER_WITHDRAWAL_FIXTURE_DIRECTORY}")
endif()
