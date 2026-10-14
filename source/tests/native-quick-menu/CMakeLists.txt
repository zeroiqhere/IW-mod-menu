cmake_minimum_required(VERSION 3.20)
project(QuickMenuOpeningFoundation LANGUAGES CXX)
enable_testing()
set(IW_QUICK_ROOT "${CMAKE_CURRENT_LIST_DIR}/../..")
# Existing portable input sources supply the exact production saved-binding validator
add_library(iw_quick_opening_foundation STATIC
    "${IW_QUICK_ROOT}/native/runtime/quick/ClosedMenuOpeningArbitrator.cpp"
    "${IW_QUICK_ROOT}/native/runtime/NativeInputRouter.cpp"
    "${IW_QUICK_ROOT}/native/runtime/monitor/SourceUseInput.cpp"
    "${IW_QUICK_ROOT}/native/runtime/monitor/SourceMonitorInput.cpp"
    "${IW_QUICK_ROOT}/native/runtime/ChoiceRepeat.cpp"
    "${IW_QUICK_ROOT}/native/runtime/QuickExitGesture.cpp"
    "${IW_QUICK_ROOT}/native/runtime/WidthEditorInput.cpp"
    "${IW_QUICK_ROOT}/native/runtime/InstructionPositionInput.cpp"
    "${IW_QUICK_ROOT}/native/runtime/MenuPositionInput.cpp"
    "${IW_QUICK_ROOT}/native/runtime/MenuCreditsInput.cpp")
target_compile_features(iw_quick_opening_foundation PUBLIC cxx_std_20)
set_target_properties(iw_quick_opening_foundation PROPERTIES CXX_EXTENSIONS OFF)
target_compile_options(iw_quick_opening_foundation PRIVATE -Wall -Wextra -Werror)
add_executable(iw_quick_menu_foundation_tests main.cpp ClosedMenuOpeningArbitratorTests.cpp)
target_link_libraries(iw_quick_menu_foundation_tests PRIVATE iw_quick_opening_foundation)
set_target_properties(iw_quick_menu_foundation_tests PROPERTIES CXX_EXTENSIONS OFF)
target_compile_options(iw_quick_menu_foundation_tests PRIVATE -Wall -Wextra -Werror)
add_test(NAME ClosedMenuOpeningFoundation COMMAND iw_quick_menu_foundation_tests)
