# Actual producer graphs remain separate from all earlier fixed inventories
add_executable(iw_mechanical_monitor_graph_tests
    "${CMAKE_CURRENT_LIST_DIR}/MechanicalGraphTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MechanicalGraphInventory.cpp")
iw_link_use_readers(iw_mechanical_monitor_graph_tests)
set(IW_MECHANICAL_MONITOR_FIXTURE_DIRECTORY "" CACHE PATH "Actual controller mechanical continuations")
if(IW_MECHANICAL_MONITOR_FIXTURE_DIRECTORY)
    add_test(NAME ActualMechanicalMonitorGraph COMMAND iw_mechanical_monitor_graph_tests "${IW_MECHANICAL_MONITOR_FIXTURE_DIRECTORY}")
endif()
