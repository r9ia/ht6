cmake_minimum_required(VERSION 3.22.1)
project(FearDetector CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Point this at wherever you extracted the SmartSpectra Windows SDK zip, e.g.
#   set SMARTSPECTRA_SDK_PATH=C:\SmartSpectra
set(SMARTSPECTRA_SDK_DIR "$ENV{SMARTSPECTRA_SDK_PATH}")
if (SMARTSPECTRA_SDK_DIR STREQUAL "")
    message(FATAL_ERROR "SMARTSPECTRA_SDK_PATH is not set. Run: set SMARTSPECTRA_SDK_PATH=C:\\SmartSpectra")
endif ()
list(APPEND CMAKE_PREFIX_PATH "${SMARTSPECTRA_SDK_DIR}")

find_package(SmartSpectra CONFIG REQUIRED)

# IMPORTANT: the Windows SDK zip only ships OpenCV's *runtime DLL*
# (opencv_world*.dll, used internally by smartspectra.dll) -- it does NOT
# include OpenCV headers, import libs, or an OpenCVConfig.cmake, so it can't
# satisfy find_package(OpenCV) on its own. You need a separate OpenCV dev
# install for *our* code (the HUD/imshow calls) to build against. Easiest
# path is vcpkg -- see README.md for the one-time setup. Once vcpkg is
# installed and you pass -DCMAKE_TOOLCHAIN_FILE=... on the configure line,
# this just works.
find_package(OpenCV REQUIRED COMPONENTS core imgproc highgui)

add_executable(fear_detector fear_detector.cpp)

set_target_properties(fear_detector PROPERTIES
    VS_DEBUGGER_COMMAND "$<TARGET_FILE:fear_detector>"
    VS_DEBUGGER_WORKING_DIRECTORY "$<TARGET_FILE_DIR:fear_detector>")

target_link_libraries(fear_detector PRIVATE
    SmartSpectra::SDK
    ${OpenCV_LIBS}
)

if (MSVC)
    set_property(DIRECTORY PROPERTY VS_STARTUP_PROJECT fear_detector)
endif ()