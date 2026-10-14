# Ordinary current-level checks use the production router and owned packet facts
add_executable(iw_ordinary_source_level_input_tests
    "${CMAKE_CURRENT_LIST_DIR}/OrdinarySourceLevelMain.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/OrdinarySourceCurrentLevelTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/OrdinarySourceTimingTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/OrdinarySourceScrollTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/OrdinarySourceBackendWaitTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/OrdinarySourceRecoveryTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/OrdinarySourceGestureTests.cpp")
if(TARGET iw_overlay_runtime)
    target_link_libraries(iw_ordinary_source_level_input_tests PRIVATE iw_overlay_runtime)
else()
    target_link_libraries(iw_ordinary_source_level_input_tests PRIVATE iw_portable_input)
endif()
target_compile_options(iw_ordinary_source_level_input_tests PRIVATE -Wall -Wextra -Werror)
add_test(NAME OrdinarySourceLevelInput COMMAND iw_ordinary_source_level_input_tests)
