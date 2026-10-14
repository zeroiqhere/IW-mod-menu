cmake_minimum_required(VERSION 3.20)
project(ApparitionLayoutTests LANGUAGES CXX)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
enable_testing()
# This portable target links no graphics, Windows, hook, or feature dependency
add_executable(ApparitionLayoutTests instructions/InstructionHudLayoutTests.cpp ApparitionLayoutTests.cpp StealthLayoutTests.cpp DesignLayoutTests.cpp InitialOpenLayoutTests.cpp DesignFooterLayoutTests.cpp
    ../../native/render/designs/DesignGeometry.cpp ../../native/render/layout/ApparitionLayout.cpp ../../native/render/layout/MenuPositionEditorLayout.cpp ../../native/render/layout/MenuNoticePlacement.cpp ../../native/render/layout/NoticePlacement.cpp ../../native/render/PanelText.cpp)
target_compile_options(ApparitionLayoutTests PRIVATE -Wall -Wextra -Werror)
add_test(NAME ApparitionLayoutTests COMMAND ApparitionLayoutTests)

# Compile the same panel source with ImGui core, without any platform or graphics backend
set(IW_CPU_ROOT "${CMAKE_CURRENT_LIST_DIR}/../..")
add_library(iw_imgui_cpu STATIC
    "${IW_CPU_ROOT}/third_party/imgui/imgui.cpp" "${IW_CPU_ROOT}/third_party/imgui/imgui_draw.cpp"
    "${IW_CPU_ROOT}/third_party/imgui/imgui_tables.cpp" "${IW_CPU_ROOT}/third_party/imgui/imgui_widgets.cpp")
target_include_directories(iw_imgui_cpu PUBLIC "${IW_CPU_ROOT}/third_party/imgui")
add_executable(ApparitionPanelTests instructions/withdrawal/FooterWithdrawalPanelTests.cpp instructions/withdrawal/ActualFooterWithdrawalPanelTests.cpp
    "${IW_CPU_ROOT}/tests/native-runtime/instructions/withdrawal/fixtures/FooterWithdrawalFixtureTests.cpp" chrome/theme/ActualChromeThemePanelTests.cpp
    "${IW_CPU_ROOT}/tests/native-runtime/chrome/theme/fixtures/ChromeThemeFixtureTests.cpp" chrome/theme/ChromeThemePanel.cpp chrome/theme/ChromeThemePanelTests.cpp
    "${IW_CPU_ROOT}/native/render/chrome/theme/ChromeColorHistory.cpp" instructions/theme/ActualInstructionThemePanelTests.cpp
    "${IW_CPU_ROOT}/tests/native-runtime/instructions/theme/fixtures/InstructionThemeFixtureTests.cpp" instructions/theme/InstructionThemePanelTests.cpp instructions/color/ActualInstructionColorPanelTests.cpp
    "${IW_CPU_ROOT}/tests/native-runtime/instructions/color/fixtures/InstructionColorFixtureTests.cpp" instructions/color/InstructionColorPanelTests.cpp
    "${IW_CPU_ROOT}/native/render/instructions/color/InstructionHudColorHistory.cpp" "${IW_CPU_ROOT}/native/render/instructions/color/InstructionThemeTimeline.cpp" instructions/ActualInstructionHudPanelTests.cpp instructions/InstructionFixturePanel.cpp instructions/InstructionHudPanelTests.cpp palette/ActualPaletteTests.cpp palette/PalettePanel.cpp palette/LiteralPaletteTests.cpp chrome/BasicChromeFixturePanelTests.cpp chrome/BasicScrollerPanelTests.cpp
    "${IW_CPU_ROOT}/native/render/chrome/BasicScrollerHistory.cpp" instruction-options/InstructionOptionPanelTests.cpp local-options/LocalOptionPanelTests.cpp local-options/LocalOptionPaletteTests.cpp paint/BooleanPaintPanelTests.cpp callbacks/CallbackFrameTests.cpp quick/EligibilityFrameTests.cpp spacer/SpacerPanelTests.cpp open-controls/OpeningPanelTests.cpp rows/RowTextPanelTests.cpp ApparitionPanelTests.cpp StealthPanelTests.cpp ScrollPanelTests.cpp MenuPositionPanelTests.cpp CreditsPanelTests.cpp PreferenceRefreshPanelTests.cpp DesignPanelTests.cpp OpeningPanelTests.cpp InitialOpenPanelTests.cpp DesignFooterPanelTests.cpp CpuDrawlist.cpp BackendPreview.cpp
    "${IW_CPU_ROOT}/native/render/designs/DesignChrome.cpp" "${IW_CPU_ROOT}/native/render/designs/DesignText.cpp"
    "${IW_CPU_ROOT}/native/render/rows/RowTextLayout.cpp"
    "${IW_CPU_ROOT}/native/render/ScrollRows.cpp" "${IW_CPU_ROOT}/native/render/ScrollPresentation.cpp" "${IW_CPU_ROOT}/native/render/spacer/SpacerScrollPresentation.cpp" "${IW_CPU_ROOT}/native/runtime/scroll/SpacerScrollPlan.cpp" "${IW_CPU_ROOT}/native/runtime/scroll/SpacerScrollReceiptTracker.cpp" "${IW_CPU_ROOT}/native/render/opening/OpeningPresentationClock.cpp" "${IW_CPU_ROOT}/native/render/SmoothRainbow.cpp" "${IW_CPU_ROOT}/native/render/InstructionGeometry.cpp" "${IW_CPU_ROOT}/native/render/InstructionPanel.cpp" "${IW_CPU_ROOT}/native/render/instructions/ClosedInstructionPanel.cpp" "${IW_CPU_ROOT}/native/render/instructions/InstructionHudLifetime.cpp" "${IW_CPU_ROOT}/native/render/WidthAnimation.cpp" "${IW_CPU_ROOT}/native/render/MenuPositionEditorPanel.cpp" "${IW_CPU_ROOT}/native/render/WidthEditorPanel.cpp" "${IW_CPU_ROOT}/native/render/credits/CreditsCatalog.cpp" "${IW_CPU_ROOT}/native/render/credits/CreditsTimeline.cpp" "${IW_CPU_ROOT}/native/render/credits/CreditsPanel.cpp" "${IW_CPU_ROOT}/native/render/credits/CreditsPresentationClock.cpp" "${IW_CPU_ROOT}/native/render/ApparitionPanel.cpp" "${IW_CPU_ROOT}/native/render/PanelGeometry.cpp" "${IW_CPU_ROOT}/native/render/PanelText.cpp"
    "${IW_CPU_ROOT}/native/render/designs/DesignGeometry.cpp" "${IW_CPU_ROOT}/native/render/layout/ApparitionLayout.cpp" "${IW_CPU_ROOT}/native/render/layout/MenuPositionEditorLayout.cpp" "${IW_CPU_ROOT}/native/render/layout/MenuNoticePlacement.cpp" "${IW_CPU_ROOT}/native/render/layout/NoticePlacement.cpp"
    "${IW_CPU_ROOT}/native/runtime/ControlLabels.cpp" "${IW_CPU_ROOT}/native/runtime/NativeInputRouter.cpp"
 "${IW_CPU_ROOT}/native/runtime/monitor/SourceUseInput.cpp"
    "${IW_CPU_ROOT}/native/runtime/monitor/SourceMonitorInput.cpp"
    "${IW_CPU_ROOT}/native/runtime/quick/ClosedMenuOpeningArbitrator.cpp"
    "${IW_CPU_ROOT}/native/runtime/ChoiceRepeat.cpp"
    "${IW_CPU_ROOT}/native/runtime/QuickExitGesture.cpp"
    "${IW_CPU_ROOT}/native/runtime/WidthEditorInput.cpp"
    "${IW_CPU_ROOT}/native/runtime/InstructionPositionInput.cpp"
    "${IW_CPU_ROOT}/native/runtime/MenuPositionInput.cpp"
    "${IW_CPU_ROOT}/native/runtime/MenuCreditsInput.cpp"
    "${IW_CPU_ROOT}/native/runtime/BoundedSessionJson.cpp"
    "${IW_CPU_ROOT}/native/runtime/OpeningControlsSchema.cpp"
    "${IW_CPU_ROOT}/native/runtime/LocalMenuEligibilitySchema.cpp"
    "${IW_CPU_ROOT}/native/runtime/instructions/InstructionHudSchema.cpp"
    "${IW_CPU_ROOT}/native/runtime/instructions/InstructionThemeSchema.cpp"
    "${IW_CPU_ROOT}/native/runtime/chrome/ChromeThemeSchema.cpp"
    "${IW_CPU_ROOT}/native/runtime/MenuOptionCallbackSchema.cpp"
    "${IW_CPU_ROOT}/native/runtime/MenuBooleanPaintSchema.cpp")
target_link_libraries(ApparitionPanelTests PRIVATE iw_imgui_cpu)
target_include_directories(ApparitionPanelTests PRIVATE "${IW_CPU_ROOT}/third_party/json/include")
target_compile_options(ApparitionPanelTests PRIVATE -Wall -Wextra -Werror)
add_test(NAME ApparitionPanelTests COMMAND ApparitionPanelTests)

# Test-only paths point to unchanged copies of the sealed offline producer packets
target_compile_definitions(ApparitionPanelTests PRIVATE IW_ROW_TEXT_FIXTURE_DIRECTORY="${CMAKE_CURRENT_LIST_DIR}/rows/fixtures")

add_subdirectory(../native-row-text row-text)

# The actual local graph does not replace the established preview inventory
set(IW_LOCAL_OPTION_FIXTURE_DIRECTORY "" CACHE PATH "Separate actual-controller local option fixture directory")
if(IW_LOCAL_OPTION_FIXTURE_DIRECTORY)
    target_compile_definitions(ApparitionPanelTests PRIVATE IW_LOCAL_OPTION_FIXTURE_DIRECTORY="${IW_LOCAL_OPTION_FIXTURE_DIRECTORY}")
endif()

# The renderer's portable phase keeps logical observation independent of graphics preparation
include("${IW_CPU_ROOT}/tests/native-instruction-observation/InstructionObservationTargets.cmake")

# Real instruction packets remain independent from ordinary preview and palette inventories
set(IW_INSTRUCTION_OPTION_FIXTURE_DIRECTORY "" CACHE PATH "Separate actual-controller instruction option fixture directory")
if(IW_INSTRUCTION_OPTION_FIXTURE_DIRECTORY)
    target_compile_definitions(ApparitionPanelTests PRIVATE IW_INSTRUCTION_OPTION_FIXTURE_DIRECTORY="${IW_INSTRUCTION_OPTION_FIXTURE_DIRECTORY}")
endif()

# New chrome snapshots remain separate from all earlier producer inventories
set(IW_BASIC_CHROME_FIXTURE_DIRECTORY "" CACHE PATH "Separate actual Basic chrome fixture directory")
if(IW_BASIC_CHROME_FIXTURE_DIRECTORY)
    target_compile_definitions(ApparitionPanelTests PRIVATE IW_BASIC_CHROME_FIXTURE_DIRECTORY="${IW_BASIC_CHROME_FIXTURE_DIRECTORY}")
endif()

# Both readers inspect the separate real palette graph without a process or graphics backend
target_sources(ApparitionPanelTests PRIVATE "${IW_CPU_ROOT}/native/runtime/RuntimeSchema.cpp")
target_include_directories(ApparitionPanelTests PRIVATE "${IW_CPU_ROOT}/tests/native-runtime/portable")
set(IW_LITERAL_PALETTE_FIXTURE_DIRECTORY "" CACHE PATH "Separate real controller literal palette directory")
if(IW_LITERAL_PALETTE_FIXTURE_DIRECTORY)
    target_compile_definitions(ApparitionPanelTests PRIVATE IW_LITERAL_PALETTE_FIXTURE_DIRECTORY="${IW_LITERAL_PALETTE_FIXTURE_DIRECTORY}")
endif()

# Persistent footer packets have their own fixed actual-controller inventory
set(IW_INSTRUCTION_HUD_FIXTURE_DIRECTORY "" CACHE PATH "Separate actual instruction HUD fixture directory")
if(IW_INSTRUCTION_HUD_FIXTURE_DIRECTORY)
    target_compile_definitions(ApparitionPanelTests PRIVATE IW_INSTRUCTION_HUD_FIXTURE_DIRECTORY="${IW_INSTRUCTION_HUD_FIXTURE_DIRECTORY}")
endif()

# Actual creation-color chronology remains separate from all earlier graphs
set(IW_INSTRUCTION_COLOR_FIXTURE_DIRECTORY "" CACHE PATH "Actual instruction creation color graph directory")
if(IW_INSTRUCTION_COLOR_FIXTURE_DIRECTORY)
    target_compile_definitions(ApparitionPanelTests PRIVATE IW_INSTRUCTION_COLOR_FIXTURE_DIRECTORY="${IW_INSTRUCTION_COLOR_FIXTURE_DIRECTORY}")
endif()

set(IW_INSTRUCTION_THEME_FIXTURE_DIRECTORY "" CACHE PATH "Actual instruction theme journal directory")
if(IW_INSTRUCTION_THEME_FIXTURE_DIRECTORY)
    target_compile_definitions(ApparitionPanelTests PRIVATE IW_INSTRUCTION_THEME_FIXTURE_DIRECTORY="${IW_INSTRUCTION_THEME_FIXTURE_DIRECTORY}")
endif()

set(IW_CHROME_THEME_FIXTURE_DIRECTORY "" CACHE PATH "Actual owned chrome invocation graph directory")
if(IW_CHROME_THEME_FIXTURE_DIRECTORY)
    target_compile_definitions(ApparitionPanelTests PRIVATE IW_CHROME_THEME_FIXTURE_DIRECTORY="${IW_CHROME_THEME_FIXTURE_DIRECTORY}")
endif()

set(IW_FOOTER_WITHDRAWAL_FIXTURE_DIRECTORY "" CACHE PATH "Actual qualified footer withdrawal graph directory")
if(IW_FOOTER_WITHDRAWAL_FIXTURE_DIRECTORY)
    target_compile_definitions(ApparitionPanelTests PRIVATE IW_FOOTER_WITHDRAWAL_FIXTURE_DIRECTORY="${IW_FOOTER_WITHDRAWAL_FIXTURE_DIRECTORY}")
endif()
