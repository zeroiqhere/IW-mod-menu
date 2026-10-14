# Both readers and scoped command serialization use the actual production schema
add_executable(iw_mechanical_monitor_schema_tests
    "${CMAKE_CURRENT_LIST_DIR}/MechanicalSchemaMain.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MechanicalSchemaTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/MechanicalCommandTests.cpp")
iw_link_use_readers(iw_mechanical_monitor_schema_tests)
add_test(NAME MechanicalMonitorSchema COMMAND iw_mechanical_monitor_schema_tests)
