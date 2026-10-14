set(IW_LOADER_SOURCES
    native/loader/BootstrapMain.cpp native/loader/PeExport.cpp native/loader/RemoteLibraryLoader.cpp
    native/loader/BootstrapPacket.cpp)

# Fixture builds accept only the dedicated test executable and never the retail game
add_executable(iw_overlay_bootstrap_fixture ${IW_LOADER_SOURCES})
target_compile_definitions(iw_overlay_bootstrap_fixture PRIVATE IW_BOOTSTRAP_FIXTURE=1)
target_include_directories(iw_overlay_bootstrap_fixture PRIVATE native third_party/json/include)
target_compile_options(iw_overlay_bootstrap_fixture PRIVATE -Wall -Wextra -Werror)
target_link_libraries(iw_overlay_bootstrap_fixture PRIVATE psapi shell32)
target_link_options(iw_overlay_bootstrap_fixture PRIVATE -municode -mwindows)

add_executable(iw_remote_loader_tests tests/loader/RemoteLibraryLoaderTests.cpp native/loader/RemoteLibraryLoader.cpp)
target_include_directories(iw_remote_loader_tests PRIVATE native)
target_compile_options(iw_remote_loader_tests PRIVATE -Wall -Wextra -Werror)
target_link_libraries(iw_remote_loader_tests PRIVATE psapi)

add_executable(iw_bootstrap_packet_tests tests/loader/BootstrapPacketTests.cpp native/loader/BootstrapPacket.cpp)
target_include_directories(iw_bootstrap_packet_tests PRIVATE native)
target_compile_options(iw_bootstrap_packet_tests PRIVATE -Wall -Wextra -Werror)
