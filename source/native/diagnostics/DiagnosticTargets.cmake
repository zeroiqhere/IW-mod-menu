set(IW_DIAGNOSTIC_ROOT "${CMAKE_CURRENT_LIST_DIR}/../..")

# Neither probe links the runtime, renderer, input, loader, or a game feature transport
add_library(iw_bootstrap_probe SHARED "${CMAKE_CURRENT_LIST_DIR}/BootstrapProbe.cpp")
set_target_properties(iw_bootstrap_probe PROPERTIES PREFIX "" OUTPUT_NAME "iw_apparition_bootstrap_probe")
target_compile_options(iw_bootstrap_probe PRIVATE -Wall -Wextra -Werror -fno-exceptions -fno-rtti)
target_link_libraries(iw_bootstrap_probe PRIVATE kernel32 user32)

add_library(iw_bootstrap_probe_fixture SHARED "${CMAKE_CURRENT_LIST_DIR}/BootstrapProbe.cpp")
set_target_properties(iw_bootstrap_probe_fixture PROPERTIES PREFIX "")
target_compile_definitions(iw_bootstrap_probe_fixture PRIVATE IW_BOOTSTRAP_PROBE_FIXTURE)
target_compile_options(iw_bootstrap_probe_fixture PRIVATE -Wall -Wextra -Werror -fno-exceptions -fno-rtti)
target_link_libraries(iw_bootstrap_probe_fixture PRIVATE kernel32 user32)

# The invisible fixture creates only owned HWNDs and never starts a graphics device
add_executable(BootstrapProbeTests
    "${IW_DIAGNOSTIC_ROOT}/tests/diagnostics/BootstrapProbeTests.cpp"
    "${IW_DIAGNOSTIC_ROOT}/tests/diagnostics/OwnedForeignWindow.cpp")
target_link_libraries(BootstrapProbeTests PRIVATE shell32)
target_link_options(BootstrapProbeTests PRIVATE -municode -mwindows)
target_compile_options(BootstrapProbeTests PRIVATE -Wall -Wextra -Werror)
