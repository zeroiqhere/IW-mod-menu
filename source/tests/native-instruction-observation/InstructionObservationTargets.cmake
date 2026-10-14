if(NOT TARGET iw_instruction_observation_tests)
    set(IW_OBSERVATION_ROOT "${CMAKE_CURRENT_LIST_DIR}/../..")
    add_executable(iw_instruction_observation_tests
        "${CMAKE_CURRENT_LIST_DIR}/InstructionRenderPhaseMain.cpp"
        "${CMAKE_CURRENT_LIST_DIR}/InstructionRenderPhaseTests.cpp"
        "${IW_OBSERVATION_ROOT}/native/render/instructions/InstructionHudLifetime.cpp"
        "${IW_OBSERVATION_ROOT}/native/render/instructions/color/InstructionHudColorHistory.cpp"
        "${IW_OBSERVATION_ROOT}/native/render/instructions/color/InstructionThemeTimeline.cpp")
    target_compile_features(iw_instruction_observation_tests PRIVATE cxx_std_20)
    target_compile_options(iw_instruction_observation_tests PRIVATE -Wall -Wextra -Werror)
    add_test(NAME InstructionRenderPhase COMMAND iw_instruction_observation_tests)
endif()
