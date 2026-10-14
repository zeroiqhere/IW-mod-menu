# Compile the source-backed helper without wiring it into the live input router
set(IW_QUICK_RUNTIME_ROOT "${CMAKE_CURRENT_LIST_DIR}/../../..")
target_sources(iw_overlay_runtime PRIVATE "${CMAKE_CURRENT_LIST_DIR}/ClosedMenuOpeningArbitrator.cpp")

# The Windows counterpart exercises explicit owned facts and creates no window or process binding
add_executable(iw_quick_menu_foundation_tests
    "${IW_QUICK_RUNTIME_ROOT}/tests/native-quick-menu/main.cpp"
    "${IW_QUICK_RUNTIME_ROOT}/tests/native-quick-menu/ClosedMenuOpeningArbitratorTests.cpp")
target_link_libraries(iw_quick_menu_foundation_tests PRIVATE iw_overlay_runtime)
target_compile_options(iw_quick_menu_foundation_tests PRIVATE -Wall -Wextra -Werror)
