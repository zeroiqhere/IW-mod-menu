# The renderer has no engine offsets, input sender, or game feature backend
set(IW_RENDER_ROOT "${CMAKE_CURRENT_LIST_DIR}/../..")
add_library(iw_overlay_renderer STATIC
    "${CMAKE_CURRENT_LIST_DIR}/chrome/theme/ChromeColorHistory.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/chrome/BasicScrollerHistory.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/Dx11Hooks.cpp" "${CMAKE_CURRENT_LIST_DIR}/Renderer.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/RendererSwapChain.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/designs/DesignChrome.cpp" "${CMAKE_CURRENT_LIST_DIR}/designs/DesignText.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/opening/OpeningPresentationClock.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/rows/RowTextLayout.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ScrollRows.cpp" "${CMAKE_CURRENT_LIST_DIR}/ScrollPresentation.cpp" "${CMAKE_CURRENT_LIST_DIR}/spacer/SpacerScrollPresentation.cpp" "${IW_RENDER_ROOT}/native/runtime/scroll/SpacerScrollPlan.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/SmoothRainbow.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/InstructionGeometry.cpp" "${CMAKE_CURRENT_LIST_DIR}/InstructionPanel.cpp" "${CMAKE_CURRENT_LIST_DIR}/instructions/ClosedInstructionPanel.cpp" "${CMAKE_CURRENT_LIST_DIR}/instructions/InstructionHudLifetime.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/instructions/color/InstructionHudColorHistory.cpp" "${CMAKE_CURRENT_LIST_DIR}/instructions/color/InstructionThemeTimeline.cpp" "${CMAKE_CURRENT_LIST_DIR}/WidthAnimation.cpp" "${CMAKE_CURRENT_LIST_DIR}/MenuPositionEditorPanel.cpp" "${CMAKE_CURRENT_LIST_DIR}/WidthEditorPanel.cpp" "${CMAKE_CURRENT_LIST_DIR}/credits/CreditsCatalog.cpp" "${CMAKE_CURRENT_LIST_DIR}/credits/CreditsTimeline.cpp" "${CMAKE_CURRENT_LIST_DIR}/credits/CreditsPanel.cpp" "${CMAKE_CURRENT_LIST_DIR}/credits/CreditsPresentationClock.cpp" "${CMAKE_CURRENT_LIST_DIR}/ApparitionPanel.cpp" "${CMAKE_CURRENT_LIST_DIR}/PanelGeometry.cpp" "${CMAKE_CURRENT_LIST_DIR}/PanelText.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/designs/DesignGeometry.cpp" "${CMAKE_CURRENT_LIST_DIR}/layout/ApparitionLayout.cpp" "${CMAKE_CURRENT_LIST_DIR}/layout/MenuPositionEditorLayout.cpp" "${CMAKE_CURRENT_LIST_DIR}/layout/MenuNoticePlacement.cpp" "${CMAKE_CURRENT_LIST_DIR}/layout/NoticePlacement.cpp")
target_include_directories(iw_overlay_renderer PUBLIC
    "${IW_RENDER_ROOT}/native" "${IW_RENDER_ROOT}/third_party/imgui" "${IW_RENDER_ROOT}/third_party/minhook/include")
target_link_libraries(iw_overlay_renderer PUBLIC iw_imgui minhook d3d11 dxgi)
target_compile_options(iw_overlay_renderer PRIVATE -Wall -Wextra -Werror)

# A separate payload accepts only the standalone fixture process name
add_library(iw_overlay_fixture SHARED
    "${IW_RENDER_ROOT}/native/Payload.cpp" "${IW_RENDER_ROOT}/tests/native/FixtureRuntime.cpp")
target_compile_definitions(iw_overlay_fixture PRIVATE IW_OVERLAY_TEST_BUILD)
target_link_libraries(iw_overlay_fixture PRIVATE iw_overlay_renderer)
target_compile_options(iw_overlay_fixture PRIVATE -Wall -Wextra -Werror)
set_target_properties(iw_overlay_fixture PROPERTIES PREFIX "")

add_executable(Dx11Fixture
    "${IW_RENDER_ROOT}/tests/native/Dx11Fixture.cpp" "${IW_RENDER_ROOT}/tests/native/PipelineSentinel.cpp"
    "${IW_RENDER_ROOT}/tests/native/FixtureExternalBootstrap.cpp"
    "${IW_RENDER_ROOT}/tests/native/SteamOverlayFixture.cpp"
    "${IW_RENDER_ROOT}/tests/native/SteamOverlayFixtureTests.cpp"
    "${IW_RENDER_ROOT}/tests/native/ActiveTransitionChecks.cpp"
    "${IW_RENDER_ROOT}/tests/native/active-transition/RendererFailureChecks.cpp"
    "${IW_RENDER_ROOT}/tests/native/active-transition/SwapChainProxy.cpp"
    "${IW_RENDER_ROOT}/tests/native/OpenedTransitionChecks.cpp"
    "${IW_RENDER_ROOT}/tests/native/ClosedTransitionChecks.cpp")
target_include_directories(Dx11Fixture PRIVATE "${IW_RENDER_ROOT}/native")
target_link_libraries(Dx11Fixture PRIVATE iw_overlay_renderer d3d11 dxgi d3dcompiler shell32)
target_link_options(Dx11Fixture PRIVATE -municode -mwindows)
target_compile_options(Dx11Fixture PRIVATE -Wall -Wextra -Werror)

# The timing helper uses an explicit clock and has no Windows or graphics dependency
add_executable(iw_width_animation_tests
    "${IW_RENDER_ROOT}/tests/native-animation/WidthAnimationTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/WidthAnimation.cpp")
target_compile_options(iw_width_animation_tests PRIVATE -Wall -Wextra -Werror)

# Color sampling stays portable and never creates a graphics device
add_executable(iw_smooth_rainbow_tests
    "${IW_RENDER_ROOT}/tests/native-color/SmoothRainbowTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/SmoothRainbow.cpp")
target_compile_options(iw_smooth_rainbow_tests PRIVATE -Wall -Wextra -Werror)

# Pure cursor presentation never opens a window or a graphics device
add_executable(iw_scroll_presentation_tests
    "${IW_RENDER_ROOT}/tests/native-scroll-presentation/ScrollPresentationTests.cpp"
    "${IW_RENDER_ROOT}/tests/native-scroll-presentation/policy/OpeningPolicyScrollTests.cpp"
    "${IW_RENDER_ROOT}/tests/native-scroll-presentation/policy/LocalMenuEligibilityScrollTests.cpp"
    "${IW_RENDER_ROOT}/tests/native-scroll-presentation/callbacks/CallbackScrollTests.cpp"
    "${IW_RENDER_ROOT}/tests/native-scroll-presentation/paint/BooleanPaintScrollTests.cpp"
    "${IW_RENDER_ROOT}/tests/native-scroll-presentation/spacer/SpacerScrollTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ScrollPresentation.cpp" "${CMAKE_CURRENT_LIST_DIR}/spacer/SpacerScrollPresentation.cpp" "${IW_RENDER_ROOT}/native/runtime/scroll/SpacerScrollPlan.cpp" "${CMAKE_CURRENT_LIST_DIR}/opening/OpeningPresentationClock.cpp")
target_compile_options(iw_scroll_presentation_tests PRIVATE -Wall -Wextra -Werror)

# Credits timing and bounded local interpolation need no graphics or Windows calls
add_executable(iw_credits_timeline_tests
    "${IW_RENDER_ROOT}/tests/native-credits/CreditsTimelineTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/credits/CreditsCatalog.cpp" "${CMAKE_CURRENT_LIST_DIR}/credits/CreditsTimeline.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/PanelText.cpp")
target_compile_options(iw_credits_timeline_tests PRIVATE -Wall -Wextra -Werror)
add_executable(iw_credits_clock_tests
    "${IW_RENDER_ROOT}/tests/native-credits/CreditsPresentationClockTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/credits/CreditsPresentationClock.cpp")
target_compile_options(iw_credits_clock_tests PRIVATE -Wall -Wextra -Werror)

# Opening clocks and alpha takeover remain pure, with no platform or graphics calls
add_executable(iw_opening_clock_tests
    "${IW_RENDER_ROOT}/tests/native-opening-presentation/OpeningClockTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/opening/OpeningPresentationClock.cpp")
target_compile_options(iw_opening_clock_tests PRIVATE -Wall -Wextra -Werror)
add_executable(iw_opening_rows_tests
    "${IW_RENDER_ROOT}/tests/native-opening-presentation/OpeningRowsTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ScrollPresentation.cpp" "${CMAKE_CURRENT_LIST_DIR}/spacer/SpacerScrollPresentation.cpp" "${IW_RENDER_ROOT}/native/runtime/scroll/SpacerScrollPlan.cpp" "${CMAKE_CURRENT_LIST_DIR}/opening/OpeningPresentationClock.cpp")
target_compile_options(iw_opening_rows_tests PRIVATE -Wall -Wextra -Werror)

# Row reservations and complete-prefix fitting remain independent from graphics APIs
add_executable(iw_row_text_tests
    "${IW_RENDER_ROOT}/tests/native-row-text/RowTextTests.cpp"
    "${IW_RENDER_ROOT}/tests/native-row-text/RowTextLayoutTests.cpp"
    "${IW_RENDER_ROOT}/tests/native-row-text/RowLabelFitTests.cpp"
    "${IW_RENDER_ROOT}/tests/native-row-text/RowFontTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/rows/RowTextLayout.cpp" "${CMAKE_CURRENT_LIST_DIR}/PanelText.cpp")
target_compile_options(iw_row_text_tests PRIVATE -Wall -Wextra -Werror)

# Source color history has no graphics or game dependency
add_executable(iw_basic_scroller_history_tests
    "${IW_RENDER_ROOT}/tests/native-chrome/BasicScrollerHistoryTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/chrome/BasicScrollerHistory.cpp")
target_compile_features(iw_basic_scroller_history_tests PRIVATE cxx_std_20)
target_compile_options(iw_basic_scroller_history_tests PRIVATE -Wall -Wextra -Werror)
add_test(NAME BasicScrollerHistory COMMAND iw_basic_scroller_history_tests)
