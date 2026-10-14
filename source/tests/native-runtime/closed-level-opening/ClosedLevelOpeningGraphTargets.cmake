# Actual opening packets remain separate from earlier callback and mechanical inventories
add_executable(iw_closed_level_opening_graph_tests
    "${CMAKE_CURRENT_LIST_DIR}/ClosedLevelOpeningGraphTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ClosedLevelOpeningGraphInventory.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ClosedLevelOpeningTimingTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/ClosedLevelOpeningAuthorityTests.cpp")
iw_link_use_readers(iw_closed_level_opening_graph_tests)
set(IW_CLOSED_LEVEL_OPENING_FIXTURE_DIRECTORY "" CACHE PATH "Actual controller closed-level opening graphs")
if(IW_CLOSED_LEVEL_OPENING_FIXTURE_DIRECTORY)
    add_test(NAME ActualClosedLevelOpeningGraph COMMAND iw_closed_level_opening_graph_tests "${IW_CLOSED_LEVEL_OPENING_FIXTURE_DIRECTORY}")
endif()
