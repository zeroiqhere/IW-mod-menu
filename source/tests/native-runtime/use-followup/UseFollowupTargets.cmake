# Both build surfaces reuse the original strict readers and input implementation
add_executable(iw_use_followup_graph_tests
    "${CMAKE_CURRENT_LIST_DIR}/UseFollowupGraphInventory.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/UseFollowupGraphTests.cpp")
iw_link_use_readers(iw_use_followup_graph_tests)
set(IW_USE_FOLLOWUP_FIXTURE_DIRECTORY "" CACHE PATH "Actual controller lower branches after Select")
if(IW_USE_FOLLOWUP_FIXTURE_DIRECTORY)
    add_test(NAME ActualUseFollowupGraph COMMAND iw_use_followup_graph_tests "${IW_USE_FOLLOWUP_FIXTURE_DIRECTORY}")
endif()

# A distinct no-op branch never borrows the slider's 180 ms source pause
add_executable(iw_noop_choice_schema_tests "${CMAKE_CURRENT_LIST_DIR}/NoopChoiceSchemaTests.cpp")
iw_link_use_readers(iw_noop_choice_schema_tests)
add_test(NAME NoopChoiceSchema COMMAND iw_noop_choice_schema_tests)

# The source no-op trace has its own fixed inventory and does not replace slider graphs
add_executable(iw_noop_choice_graph_tests "${CMAKE_CURRENT_LIST_DIR}/NoopChoiceGraphTests.cpp")
iw_link_use_readers(iw_noop_choice_graph_tests)
set(IW_NOOP_CHOICE_FIXTURE_DIRECTORY "" CACHE PATH "Actual controller nonslider outer50ms packets")
if(IW_NOOP_CHOICE_FIXTURE_DIRECTORY)
    add_test(NAME ActualNoopChoiceGraph COMMAND iw_noop_choice_graph_tests "${IW_NOOP_CHOICE_FIXTURE_DIRECTORY}")
endif()
