# Real display graphs remain separate from ordinary preview and callback inventories
add_executable(iw_monitor_wait_schema_tests
    "${CMAKE_CURRENT_LIST_DIR}/MonitorWaitSchemaMain.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MonitorWaitSchemaTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MonitorWaitBackendTests.cpp"
    "${IW_SCHEMA_ROOT}/tests/native-layout/BackendPreview.cpp")
target_compile_features(iw_monitor_wait_schema_tests PRIVATE cxx_std_20)
target_compile_options(iw_monitor_wait_schema_tests PRIVATE -Wall -Wextra -Werror)
if(TARGET iw_overlay_runtime)
    target_link_libraries(iw_monitor_wait_schema_tests PRIVATE iw_overlay_runtime)
else()
    # The portable shim provides types only and never reads a real process or desktop
    target_sources(iw_monitor_wait_schema_tests PRIVATE
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
    target_link_libraries(iw_monitor_wait_schema_tests PRIVATE iw_portable_input)
    target_include_directories(iw_monitor_wait_schema_tests PRIVATE
        "${IW_SCHEMA_ROOT}/tests/native-runtime/portable"
        "${IW_SCHEMA_ROOT}/third_party/json/include")
endif()
add_test(NAME MonitorWaitSchema COMMAND iw_monitor_wait_schema_tests)
set(IW_MONITOR_WAIT_FIXTURE_DIRECTORY "" CACHE PATH "Separate actual controller mechanical wait packets")
if(IW_MONITOR_WAIT_FIXTURE_DIRECTORY)
    add_test(NAME ActualMonitorWaitFixtures COMMAND iw_monitor_wait_schema_tests "${IW_MONITOR_WAIT_FIXTURE_DIRECTORY}")
endif()
