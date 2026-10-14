cmake_minimum_required(VERSION 3.20)
project(IWApparitionInjected LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
add_compile_definitions(UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)

# Keep the injected payload independent of compiler runtime DLL search paths
if(MINGW)
    add_link_options(-static -static-libgcc -static-libstdc++)
endif()

add_library(iw_imgui STATIC
    third_party/imgui/imgui.cpp
    third_party/imgui/imgui_draw.cpp
    third_party/imgui/imgui_tables.cpp
    third_party/imgui/imgui_widgets.cpp
    third_party/imgui/backends/imgui_impl_dx11.cpp)
target_include_directories(iw_imgui PUBLIC third_party/imgui third_party/imgui/backends)
target_link_libraries(iw_imgui PUBLIC d3d11 dxgi d3dcompiler)
add_subdirectory(third_party/minhook)

# Components keep ownership of their sources and their isolated tests
enable_testing()
include(native/runtime/RuntimeTargets.cmake)
add_test(NAME QuickOpeningRouter COMMAND iw_quick_opening_router_tests)
include(native/render/RenderTargets.cmake)
add_library(iw_apparition_injected SHARED native/Payload.cpp)
target_include_directories(iw_apparition_injected PRIVATE native)
target_link_libraries(iw_apparition_injected PRIVATE iw_overlay_runtime iw_overlay_renderer)
set_target_properties(iw_apparition_injected PROPERTIES PREFIX "")
target_compile_options(iw_apparition_injected PRIVATE -Wall -Wextra -Werror)

add_executable(iw_overlay_bootstrap native/loader/BootstrapMain.cpp native/loader/PeExport.cpp
    native/loader/RemoteLibraryLoader.cpp native/loader/BootstrapPacket.cpp)
target_include_directories(iw_overlay_bootstrap PRIVATE native third_party/json/include)
target_compile_options(iw_overlay_bootstrap PRIVATE -Wall -Wextra -Werror)
target_link_libraries(iw_overlay_bootstrap PRIVATE psapi shell32)
target_link_options(iw_overlay_bootstrap PRIVATE -municode -mwindows)

add_executable(iw_pe_export_tests tests/loader/PeExportTests.cpp native/loader/PeExport.cpp)
target_include_directories(iw_pe_export_tests PRIVATE native)
target_compile_options(iw_pe_export_tests PRIVATE -Wall -Wextra -Werror)

include(native/loader/LoaderTargets.cmake)
include(native/diagnostics/DiagnosticTargets.cmake)

# The offline ABI fixture is optional and never links into the injected payload
option(IW_BUILD_STEAM_IDENTITY_FIXTURE "Build offline SteamUser016 ABI fixture" OFF)
if(IW_BUILD_STEAM_IDENTITY_FIXTURE)
    enable_testing()
    include(native/identity/SteamIdentityTargets.cmake)
    iw_add_steam_identity_fixture()
endif()
