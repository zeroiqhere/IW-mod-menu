if(NOT TARGET iw_chrome_theme_tests)
    set(IW_CHROME_ROOT "${CMAKE_CURRENT_LIST_DIR}/../..")
    add_executable(iw_chrome_theme_tests "${CMAKE_CURRENT_LIST_DIR}/ChromeColorHistoryTests.cpp"
        "${IW_CHROME_ROOT}/native/render/chrome/theme/ChromeColorHistory.cpp")
    target_compile_features(iw_chrome_theme_tests PRIVATE cxx_std_20)
    target_compile_options(iw_chrome_theme_tests PRIVATE -Wall -Wextra -Werror)
    add_test(NAME ChromeColorHistory COMMAND iw_chrome_theme_tests)
endif()
