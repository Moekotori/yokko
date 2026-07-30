cmake_minimum_required(VERSION 3.25)

project(YokkoAudioNative VERSION 0.1.0 LANGUAGES CXX)

include(CTest)
find_package(Threads REQUIRED)

add_library(yokko_audio_native SHARED
    include/yokko_audio.h
    src/audio_engine.cpp
    src/audio_engine.hpp
    src/asio_output.hpp
    src/spsc_pcm_ring_buffer.hpp
    src/wasapi_output.hpp
)

if(WIN32)
    target_sources(yokko_audio_native PRIVATE src/wasapi_output.cpp)
    target_link_libraries(yokko_audio_native PRIVATE
        advapi32
        avrt
        ole32
        user32
        uuid
    )

    set(
        YOKKO_ASIO_SDK_DIR
        "$ENV{YOKKO_ASIO_SDK_DIR}"
        CACHE PATH
        "Path to a separately licensed Steinberg ASIO SDK root"
    )
    if(
        EXISTS "${YOKKO_ASIO_SDK_DIR}/common/asio.h"
        AND EXISTS "${YOKKO_ASIO_SDK_DIR}/common/iasiodrv.h"
    )
        target_sources(yokko_audio_native PRIVATE src/asio_output.cpp)
        target_include_directories(yokko_audio_native PRIVATE
            "${YOKKO_ASIO_SDK_DIR}/common"
        )
        target_compile_definitions(
            yokko_audio_native
            PRIVATE YOKKO_ENABLE_ASIO=1
        )
        message(STATUS "Yokko ASIO backend enabled")
    else()
        target_sources(
            yokko_audio_native
            PRIVATE src/asio_output_stub.cpp
        )
        target_compile_definitions(
            yokko_audio_native
            PRIVATE YOKKO_ENABLE_ASIO=0
        )
        message(STATUS
            "Yokko ASIO backend disabled; set YOKKO_ASIO_SDK_DIR "
            "to an externally licensed ASIO SDK"
        )
    endif()
else()
    target_sources(yokko_audio_native PRIVATE
        src/asio_output_stub.cpp
        src/wasapi_output_stub.cpp
    )
    target_compile_definitions(
        yokko_audio_native
        PRIVATE YOKKO_ENABLE_ASIO=0
    )
endif()

target_compile_features(yokko_audio_native PRIVATE cxx_std_20)
target_compile_definitions(yokko_audio_native PRIVATE YOKKO_AUDIO_NATIVE_BUILD)
target_include_directories(yokko_audio_native
    PUBLIC
        "${CMAKE_CURRENT_SOURCE_DIR}/include"
)

if(MSVC)
    # C4324 is expected: atomics are intentionally placed on separate cache lines.
    target_compile_options(yokko_audio_native PRIVATE /W4 /permissive- /EHsc /wd4324)
else()
    target_compile_options(yokko_audio_native PRIVATE -Wall -Wextra -Wpedantic)
endif()

set_target_properties(yokko_audio_native PROPERTIES
    OUTPUT_NAME "yokko_audio_native"
)

if(BUILD_TESTING)
    add_executable(yokko_audio_native_tests
        tests/audio_core_tests.cpp
    )

    target_compile_features(yokko_audio_native_tests PRIVATE cxx_std_20)
    target_include_directories(yokko_audio_native_tests PRIVATE
        "${CMAKE_CURRENT_SOURCE_DIR}/src"
    )
    target_link_libraries(yokko_audio_native_tests PRIVATE
        yokko_audio_native
        Threads::Threads
    )

    if(MSVC)
        target_compile_options(yokko_audio_native_tests PRIVATE /W4 /permissive- /EHsc /wd4324)
    else()
        target_compile_options(yokko_audio_native_tests PRIVATE -Wall -Wextra -Wpedantic)
    endif()

    add_test(NAME yokko_audio_native_tests COMMAND yokko_audio_native_tests)
    set_tests_properties(yokko_audio_native_tests PROPERTIES TIMEOUT 30)
endif()
