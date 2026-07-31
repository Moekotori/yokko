cmake_minimum_required(VERSION 3.21)

project(yokko_minacalc_native LANGUAGES CXX)

add_library(yokko_minacalc_native SHARED
    minacalc_adapter.cpp
    vendor/etterna/src/Etterna/MinaCalc/MinaCalc.cpp)

target_compile_features(yokko_minacalc_native PRIVATE cxx_std_20)
target_compile_definitions(yokko_minacalc_native PRIVATE
    STANDALONE_CALC
    YOKKO_MINACALC_BUILD)
target_include_directories(yokko_minacalc_native PRIVATE
    "${CMAKE_CURRENT_SOURCE_DIR}/vendor/etterna/src/Etterna/MinaCalc"
    "${CMAKE_CURRENT_SOURCE_DIR}/vendor/etterna/src/Etterna/Models/NoteData")

if(MSVC)
    target_compile_options(yokko_minacalc_native PRIVATE
        /W4
        /permissive-
        /wd4100
        /wd4189
        /wd4244
        /wd4267
        /wd4456
        /wd4458
        /wd4459)
else()
    target_compile_options(yokko_minacalc_native PRIVATE
        -Wall
        -Wextra
        -Wpedantic)
    if(MINGW)
        target_link_options(yokko_minacalc_native PRIVATE
            -static-libgcc
            -static-libstdc++)
    endif()
endif()

set_target_properties(yokko_minacalc_native PROPERTIES
    OUTPUT_NAME "yokko_minacalc_native")
