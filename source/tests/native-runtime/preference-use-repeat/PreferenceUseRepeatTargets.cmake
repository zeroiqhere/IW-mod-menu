# Both production readers consume the actual controller packets before input is exercised
set(IW_PREFERENCE_USE_REPEAT_FIXTURE_DIRECTORY "" CACHE PATH "Actual preference Use controller exports")
add_executable(iw_preference_use_repeat_graph_tests "${CMAKE_CURRENT_LIST_DIR}/PreferenceUseRepeatGraphTests.cpp")
iw_link_use_readers(iw_preference_use_repeat_graph_tests)
if(IW_PREFERENCE_USE_REPEAT_FIXTURE_DIRECTORY)
    add_test(NAME ActualPreferenceUseRepeat COMMAND iw_preference_use_repeat_graph_tests "${IW_PREFERENCE_USE_REPEAT_FIXTURE_DIRECTORY}")
endif()
