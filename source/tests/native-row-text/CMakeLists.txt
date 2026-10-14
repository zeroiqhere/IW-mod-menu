cmake_minimum_required(VERSION 3.20)
project(RowTextTests LANGUAGES CXX)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
enable_testing()
# This helper target has no fonts, platform code or graphics dependency
add_executable(RowTextTests RowTextTests.cpp RowTextLayoutTests.cpp RowLabelFitTests.cpp RowFontTests.cpp
    ../../native/render/rows/RowTextLayout.cpp ../../native/render/PanelText.cpp)
target_compile_options(RowTextTests PRIVATE -Wall -Wextra -Werror)
add_test(NAME RowTextTests COMMAND RowTextTests)
