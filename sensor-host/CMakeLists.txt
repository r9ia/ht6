cmake_minimum_required(VERSION 3.22.1)
project(DreadDirectorSensorHost LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

set(SMARTSPECTRA_SDK_PATH "$ENV{SMARTSPECTRA_SDK_PATH}" CACHE PATH
    "Root of the extracted SmartSpectra SDK (include/lib/bin/share)")
if(SMARTSPECTRA_SDK_PATH STREQUAL "")
    message(FATAL_ERROR "SMARTSPECTRA_SDK_PATH is not set")
endif()

list(PREPEND CMAKE_PREFIX_PATH "${SMARTSPECTRA_SDK_PATH}")
find_package(SmartSpectra CONFIG REQUIRED)

add_executable(dread-sensor-host src/main.cpp)
target_link_libraries(dread-sensor-host PRIVATE SmartSpectra::SDK)

if(WIN32)
    target_link_libraries(dread-sensor-host PRIVATE ws2_32)
    target_compile_definitions(dread-sensor-host PRIVATE WIN32_LEAN_AND_MEAN NOMINMAX)
endif()

if(MSVC)
    target_compile_options(dread-sensor-host PRIVATE /W4 /permissive-)
else()
    target_compile_options(dread-sensor-host PRIVATE -Wall -Wextra -Wpedantic)
endif()
