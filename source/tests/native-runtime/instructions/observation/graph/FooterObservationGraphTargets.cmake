# Graphics fault graphs share the exact production observation phase and immutable controller packets
add_executable(iw_footer_observation_graph_tests
    "${CMAKE_CURRENT_LIST_DIR}/FooterObservationGraphMain.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/FooterObservationGraphTests.cpp"
    "${IW_SCHEMA_ROOT}/tests/native-runtime/instructions/withdrawal/fixtures/FooterWithdrawalFixtureTests.cpp"
    "${IW_SCHEMA_ROOT}/tests/native-layout/BackendPreview.cpp"
    "${IW_SCHEMA_ROOT}/native/render/instructions/InstructionHudLifetime.cpp"
    "${IW_SCHEMA_ROOT}/native/render/instructions/color/InstructionHudColorHistory.cpp" "${IW_SCHEMA_ROOT}/native/render/instructions/color/InstructionThemeTimeline.cpp")
target_compile_features(iw_footer_observation_graph_tests PRIVATE cxx_std_20)
target_compile_options(iw_footer_observation_graph_tests PRIVATE -Wall -Wextra -Werror)
if(TARGET iw_overlay_runtime)
    target_link_libraries(iw_footer_observation_graph_tests PRIVATE iw_overlay_runtime)
else()
    target_sources(iw_footer_observation_graph_tests PRIVATE
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
    target_link_libraries(iw_footer_observation_graph_tests PRIVATE iw_portable_input)
    target_include_directories(iw_footer_observation_graph_tests PRIVATE
        "${IW_SCHEMA_ROOT}/tests/native-runtime/portable" "${IW_SCHEMA_ROOT}/third_party/json/include")
endif()
set(IW_FOOTER_OBSERVATION_FIXTURE_DIRECTORY "" CACHE PATH "Actual footer observation graph directory")
if(IW_FOOTER_OBSERVATION_FIXTURE_DIRECTORY)
    add_test(NAME ActualFooterObservationGraphs COMMAND iw_footer_observation_graph_tests "${IW_FOOTER_OBSERVATION_FIXTURE_DIRECTORY}")
endif()
