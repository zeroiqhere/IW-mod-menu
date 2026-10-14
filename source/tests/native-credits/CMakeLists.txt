cmake_minimum_required(VERSION 3.20)
project(ApparitionCreditsTests LANGUAGES CXX)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
enable_testing()
add_executable(CreditsTimelineTests CreditsTimelineTests.cpp
    ../../native/render/credits/CreditsCatalog.cpp ../../native/render/credits/CreditsTimeline.cpp
    ../../native/render/PanelText.cpp)
target_compile_options(CreditsTimelineTests PRIVATE -Wall -Wextra -Werror)
add_test(NAME CreditsTimelineTests COMMAND CreditsTimelineTests)
add_executable(CreditsPresentationClockTests CreditsPresentationClockTests.cpp
    ../../native/render/credits/CreditsPresentationClock.cpp)
target_compile_options(CreditsPresentationClockTests PRIVATE -Wall -Wextra -Werror)
add_test(NAME CreditsPresentationClockTests COMMAND CreditsPresentationClockTests)
