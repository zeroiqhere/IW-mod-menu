# Both build surfaces exercise the same saved-key arbitration and production router
add_executable(iw_closed_level_opening_tests
    "${CMAKE_CURRENT_LIST_DIR}/ClosedLevelOpeningMain.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ClosedLevelArbitrationTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ClosedLevelRouterTests.cpp")
if(TARGET iw_overlay_runtime)
    target_link_libraries(iw_closed_level_opening_tests PRIVATE iw_overlay_runtime)
else()
    target_link_libraries(iw_closed_level_opening_tests PRIVATE iw_portable_input)
endif()
target_compile_options(iw_closed_level_opening_tests PRIVATE -Wall -Wextra -Werror)
add_test(NAME ClosedLevelOpening COMMAND iw_closed_level_opening_tests)
