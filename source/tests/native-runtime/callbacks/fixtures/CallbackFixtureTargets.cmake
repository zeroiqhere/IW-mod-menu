# This inventory remains separate from the ordinary preview and schema fixtures
add_executable(iw_callback_fixture_tests
    "${CMAKE_CURRENT_LIST_DIR}/CallbackFixtureMain.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/CallbackFixtureTests.cpp"
    "${IW_SCHEMA_ROOT}/tests/native-layout/BackendPreview.cpp")
target_compile_features(iw_callback_fixture_tests PRIVATE cxx_std_20)
target_compile_options(iw_callback_fixture_tests PRIVATE -Wall -Wextra -Werror)
if(TARGET iw_overlay_runtime)
    target_link_libraries(iw_callback_fixture_tests PRIVATE iw_overlay_runtime)
else()
    # The portable shim supplies Windows types only, with no platform behavior
    target_sources(iw_callback_fixture_tests PRIVATE
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
        "${IW_SCHEMA_ROOT}/native/runtime/BoundedSessionJson.cpp"
        "${IW_SCHEMA_ROOT}/native/runtime/scroll/SpacerScrollPlan.cpp"
        "${IW_SCHEMA_ROOT}/native/runtime/scroll/SpacerScrollReceiptTracker.cpp")
    target_link_libraries(iw_callback_fixture_tests PRIVATE iw_portable_input)
    target_include_directories(iw_callback_fixture_tests PRIVATE
        "${IW_SCHEMA_ROOT}/tests/native-runtime/portable"
        "${IW_SCHEMA_ROOT}/third_party/json/include")
endif()
# Actual controller exports are opt-in and cannot change the legacy inventory
set(IW_CALLBACK_FIXTURE_DIRECTORY "" CACHE PATH "Separate real-controller callback fixture directory")
if(IW_CALLBACK_FIXTURE_DIRECTORY)
    add_test(NAME ActualCallbackFixtures COMMAND iw_callback_fixture_tests "${IW_CALLBACK_FIXTURE_DIRECTORY}")
endif()
