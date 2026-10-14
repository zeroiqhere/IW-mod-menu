# Actual Open Controls packets join both readers and the production input router
set(IW_OPEN_CONTROLS_USE_FIXTURE_DIRECTORY "" CACHE PATH "Actual Open Controls Use controller exports")
add_executable(iw_open_controls_use_graph_tests "${CMAKE_CURRENT_LIST_DIR}/OpeningControlsUseGraphTests.cpp")
iw_link_use_readers(iw_open_controls_use_graph_tests)
if(IW_OPEN_CONTROLS_USE_FIXTURE_DIRECTORY)
    add_test(NAME ActualOpeningControlsUse COMMAND iw_open_controls_use_graph_tests "${IW_OPEN_CONTROLS_USE_FIXTURE_DIRECTORY}")
endif()
