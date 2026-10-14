# The new receipt readers share production schema and the existing portable input library
function(iw_link_use_readers target)
target_sources(${target} PRIVATE "${IW_SCHEMA_ROOT}/tests/native-layout/BackendPreview.cpp")
target_compile_features(${target} PRIVATE cxx_std_20)
target_compile_options(${target} PRIVATE -Wall -Wextra -Werror)
if(TARGET iw_overlay_runtime)
    target_link_libraries(${target} PRIVATE iw_overlay_runtime)
else()
target_sources(${target} PRIVATE
    "${IW_SCHEMA_ROOT}/native/runtime/RuntimeSchema.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/ControlLabels.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/OpeningControlsSchema.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/LocalMenuEligibilitySchema.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/instructions/InstructionHudSchema.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/instructions/InstructionThemeSchema.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/chrome/ChromeThemeSchema.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/MenuOptionCallbackSchema.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/monitor/MenuMonitorWaitSchema.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/monitor/MenuUseContinuationSchema.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/monitor/MenuMonitorContinuationSchema.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/MenuBooleanPaintSchema.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/BoundedSessionJson.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/scroll/SpacerScrollPlan.cpp"
    "${IW_SCHEMA_ROOT}/native/runtime/scroll/SpacerScrollReceiptTracker.cpp")
target_link_libraries(${target} PRIVATE iw_portable_input)
target_include_directories(${target} PRIVATE
    "${IW_SCHEMA_ROOT}/tests/native-runtime/portable"
    "${IW_SCHEMA_ROOT}/third_party/json/include")
endif()
endfunction()
add_executable(iw_use_continuation_schema_tests "${CMAKE_CURRENT_LIST_DIR}/UseContinuationSchemaTests.cpp")
iw_link_use_readers(iw_use_continuation_schema_tests)
add_test(NAME UseContinuationSchema COMMAND iw_use_continuation_schema_tests)

# Controller exports are consumed directly without rewriting their callback or acknowledgment
add_executable(iw_use_continuation_graph_tests
    "${CMAKE_CURRENT_LIST_DIR}/UseContinuationGraphTests.cpp"
    "${CMAKE_CURRENT_LIST_DIR}/UseContinuationGraphInventory.cpp")
iw_link_use_readers(iw_use_continuation_graph_tests)
set(IW_USE_CONTINUATION_FIXTURE_DIRECTORY "" CACHE PATH "Actual controller Use callback graphs")
if(IW_USE_CONTINUATION_FIXTURE_DIRECTORY)
    add_test(NAME ActualUseContinuationGraph COMMAND iw_use_continuation_graph_tests "${IW_USE_CONTINUATION_FIXTURE_DIRECTORY}")
endif()
