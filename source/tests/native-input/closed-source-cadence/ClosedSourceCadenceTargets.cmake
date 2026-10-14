# The cadence runner reuses the actual router and the existing canonical saved-key test support
add_executable(iw_closed_source_cadence_tests
    "${CMAKE_CURRENT_LIST_DIR}/ClosedSourceCadenceMain.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ClosedSourceCadenceTimingTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ClosedSourceCadencePauseTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ClosedSourceCadenceRecoveryTests.cpp")
if(TARGET iw_overlay_runtime)
    target_link_libraries(iw_closed_source_cadence_tests PRIVATE iw_overlay_runtime)
else()
    target_link_libraries(iw_closed_source_cadence_tests PRIVATE iw_portable_input)
endif()
target_compile_options(iw_closed_source_cadence_tests PRIVATE -Wall -Wextra -Werror)
add_test(NAME ClosedSourceCadence COMMAND iw_closed_source_cadence_tests)
