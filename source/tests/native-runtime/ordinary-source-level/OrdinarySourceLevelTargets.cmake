# Current held actions consume the same immutable producer packets as closed opening proofs
add_executable(iw_ordinary_opening_hold_tests "${CMAKE_CURRENT_LIST_DIR}/OrdinaryOpeningHoldTests.cpp")
iw_link_use_readers(iw_ordinary_opening_hold_tests)
if(IW_CLOSED_LEVEL_OPENING_FIXTURE_DIRECTORY)
    add_test(NAME ActualOrdinaryOpeningHolds COMMAND iw_ordinary_opening_hold_tests "${IW_CLOSED_LEVEL_OPENING_FIXTURE_DIRECTORY}")
endif()

# The independent current-level graph joins a real callback with its session result and paint
set(IW_ORDINARY_SOURCE_LEVEL_FIXTURE_DIRECTORY "" CACHE PATH "Actual ordinary source-level controller exports")
add_executable(iw_ordinary_source_use_graph_tests "${CMAKE_CURRENT_LIST_DIR}/OrdinaryUseGraphTests.cpp")
iw_link_use_readers(iw_ordinary_source_use_graph_tests)
if(IW_ORDINARY_SOURCE_LEVEL_FIXTURE_DIRECTORY)
    add_test(NAME ActualOrdinarySourceUseGraph COMMAND iw_ordinary_source_use_graph_tests "${IW_ORDINARY_SOURCE_LEVEL_FIXTURE_DIRECTORY}")
endif()
