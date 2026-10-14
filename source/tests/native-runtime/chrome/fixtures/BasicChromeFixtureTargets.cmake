# Real display graphs remain separate from ordinary preview and callback inventories
add_executable(iw_basic_chrome_fixture_tests
    "${CMAKE_CURRENT_LIST_DIR}/BasicChromeFixtureMain.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/BasicChromeFixtureTests.cpp"
    "${IW_SCHEMA_ROOT}/tests/native-layout/BackendPreview.cpp")
target_compile_features(iw_basic_chrome_fixture_tests PRIVATE cxx_std_20)
target_compile_options(iw_basic_chrome_fixture_tests PRIVATE -Wall -Wextra -Werror)
if(TARGET iw_overlay_runtime)
    target_link_libraries(iw_basic_chrome_fixture_tests PRIVATE iw_overlay_runtime)
else()
    # The portable shim provides types only and never reads a real process or desktop
    target_sources(iw_basic_chrome_fixture_tests PRIVATE
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
    target_link_libraries(iw_basic_chrome_fixture_tests PRIVATE iw_portable_input)
    target_include_directories(iw_basic_chrome_fixture_tests PRIVATE
        "${IW_SCHEMA_ROOT}/tests/native-runtime/portable"
        "${IW_SCHEMA_ROOT}/third_party/json/include")
endif()
# Missing graph exports do not alter the regular schema test registration
set(IW_BASIC_CHROME_FIXTURE_DIRECTORY "" CACHE PATH "Separate actual-controller Basic chrome fixture directory")
if(IW_BASIC_CHROME_FIXTURE_DIRECTORY)
    add_test(NAME ActualBasicChromeFixtures COMMAND iw_basic_chrome_fixture_tests "${IW_BASIC_CHROME_FIXTURE_DIRECTORY}")
endif()

# The adapter sample and history contain no process or graphics calls
target_sources(iw_basic_chrome_fixture_tests PRIVATE
    "${IW_SCHEMA_ROOT}/native/render/chrome/BasicScrollerHistory.cpp"
    "${IW_SCHEMA_ROOT}/native/render/SmoothRainbow.cpp")
