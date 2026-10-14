cmake_minimum_required(VERSION 3.20)
project(OpeningPresentationTests LANGUAGES CXX)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
enable_testing()
# These regressions use only owner reports and explicit local clocks
add_executable(OpeningClockTests OpeningClockTests.cpp ../../native/render/opening/OpeningPresentationClock.cpp)
target_compile_options(OpeningClockTests PRIVATE -Wall -Wextra -Werror)
add_test(NAME OpeningClockTests COMMAND OpeningClockTests)
# Row geometry and alpha use the production presentation helper without a renderer
add_executable(OpeningRowsTests OpeningRowsTests.cpp ../../native/render/ScrollPresentation.cpp ../../native/render/spacer/SpacerScrollPresentation.cpp ../../native/runtime/scroll/SpacerScrollPlan.cpp ../../native/render/opening/OpeningPresentationClock.cpp)
target_compile_options(OpeningRowsTests PRIVATE -Wall -Wextra -Werror)
add_test(NAME OpeningRowsTests COMMAND OpeningRowsTests)
