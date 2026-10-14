if(NOT TARGET iw_instruction_theme_tests)
    set(IW_THEME_ROOT "${CMAKE_CURRENT_LIST_DIR}/../..")
    add_executable(iw_instruction_theme_tests "${CMAKE_CURRENT_LIST_DIR}/InstructionThemeTimelineTests.cpp"
        "${IW_THEME_ROOT}/native/render/instructions/color/InstructionThemeTimeline.cpp")
    target_compile_features(iw_instruction_theme_tests PRIVATE cxx_std_20)
    target_compile_options(iw_instruction_theme_tests PRIVATE -Wall -Wextra -Werror)
    add_test(NAME InstructionThemeTimeline COMMAND iw_instruction_theme_tests)
endif()
