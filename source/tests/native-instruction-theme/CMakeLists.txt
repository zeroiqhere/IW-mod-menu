cmake_minimum_required(VERSION 3.20)
project(IWInstructionThemeTests LANGUAGES CXX)
enable_testing()
add_executable(iw_instruction_theme_tests InstructionThemeTimelineTests.cpp
    ../../native/render/instructions/color/InstructionThemeTimeline.cpp)
target_compile_features(iw_instruction_theme_tests PRIVATE cxx_std_20)
target_compile_options(iw_instruction_theme_tests PRIVATE -Wall -Wextra -Werror)
add_test(NAME InstructionThemeTimeline COMMAND iw_instruction_theme_tests)
