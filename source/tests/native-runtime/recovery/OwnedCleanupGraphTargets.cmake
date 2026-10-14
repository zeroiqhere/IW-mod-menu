# The cleanup graph consumes actual session exports without changing older fixture inventories
add_executable(iw_owned_cleanup_graph_tests "${CMAKE_CURRENT_LIST_DIR}/OwnedCleanupGraphTests.cpp")
iw_link_use_readers(iw_owned_cleanup_graph_tests)
set(IW_OWNED_CLEANUP_FIXTURE_DIRECTORY "" CACHE PATH "Actual original-owner cleanup controller packets")
if(IW_OWNED_CLEANUP_FIXTURE_DIRECTORY)
    add_test(NAME ActualOwnedCleanupGraph COMMAND iw_owned_cleanup_graph_tests "${IW_OWNED_CLEANUP_FIXTURE_DIRECTORY}")
endif()
