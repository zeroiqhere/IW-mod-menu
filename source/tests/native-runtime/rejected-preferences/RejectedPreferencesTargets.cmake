# Rejected source preferences join actual controller packets to both readers and the production router
set(IW_REJECTED_PREFERENCES_FIXTURE_DIRECTORY "" CACHE PATH "Actual rejected preference controller exports")
add_executable(iw_rejected_preferences_graph_tests "${CMAKE_CURRENT_LIST_DIR}/RejectedPreferencesGraphTests.cpp")
iw_link_use_readers(iw_rejected_preferences_graph_tests)
if(IW_REJECTED_PREFERENCES_FIXTURE_DIRECTORY)
    add_test(NAME ActualRejectedPreferences COMMAND iw_rejected_preferences_graph_tests "${IW_REJECTED_PREFERENCES_FIXTURE_DIRECTORY}")
endif()
