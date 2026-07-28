#pragma once

#include <stdint.h>

#if defined(_WIN32)
#if defined(YOKKO_AUDIO_NATIVE_BUILD)
#define YOKKO_AUDIO_API __declspec(dllexport)
#else
#define YOKKO_AUDIO_API __declspec(dllimport)
#endif
#define YOKKO_AUDIO_CALL __cdecl
#else
#define YOKKO_AUDIO_API __attribute__((visibility("default")))
#define YOKKO_AUDIO_CALL
#endif

#ifdef __cplusplus
extern "C"
{
#endif

#define YOKKO_AUDIO_ABI_VERSION 1u

    typedef struct yokko_audio_engine yokko_audio_engine;

    typedef enum yokko_audio_result
    {
        YOKKO_AUDIO_OK = 0,
        YOKKO_AUDIO_INVALID_ARGUMENT = 1,
        YOKKO_AUDIO_INVALID_STATE = 2,
        YOKKO_AUDIO_NOT_READY = 3,
        YOKKO_AUDIO_OUT_OF_MEMORY = 4,
        YOKKO_AUDIO_INTERNAL_ERROR = 5,
    } yokko_audio_result;

    typedef enum yokko_audio_state
    {
        YOKKO_AUDIO_STATE_IDLE = 0,
        YOKKO_AUDIO_STATE_PRIMED = 1,
        YOKKO_AUDIO_STATE_RUNNING = 2,
        YOKKO_AUDIO_STATE_PAUSED = 3,
        YOKKO_AUDIO_STATE_FAULTED = 4,
    } yokko_audio_state;

    typedef struct yokko_audio_config
    {
        uint32_t struct_size;
        uint32_t sample_rate;
        uint32_t channels;
        uint32_t ring_capacity_frames;
        uint32_t startup_threshold_frames;
    } yokko_audio_config;

    typedef struct yokko_audio_status
    {
        uint32_t struct_size;
        uint32_t abi_version;
        yokko_audio_state state;
        uint32_t sample_rate;
        uint32_t channels;
        uint32_t ring_capacity_frames;
        uint32_t buffered_frames;
        uint32_t device_latency_frames;
        uint64_t submitted_frames;
        uint64_t source_frames_rendered;
        uint64_t device_frames_rendered;
        uint64_t underrun_count;
        double playback_time_milliseconds;
    } yokko_audio_status;

    YOKKO_AUDIO_API uint32_t YOKKO_AUDIO_CALL yokko_audio_get_abi_version(void);

    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_create(
        const yokko_audio_config* config,
        yokko_audio_engine** engine);

    YOKKO_AUDIO_API void YOKKO_AUDIO_CALL yokko_audio_destroy(yokko_audio_engine* engine);

    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_start(yokko_audio_engine* engine);

    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_pause(yokko_audio_engine* engine);

    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_stop(yokko_audio_engine* engine);

    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_submit_interleaved_f32(
        yokko_audio_engine* engine,
        const float* samples,
        uint32_t frame_count,
        uint32_t* accepted_frames);

    /*
     * Real output backends call this function from their audio callback.
     * It never allocates, blocks, logs, or calls managed code.
     * Missing source frames are replaced with silence and counted as one underrun.
     */
    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_render_interleaved_f32(
        yokko_audio_engine* engine,
        float* output,
        uint32_t frame_count,
        uint32_t* source_frames_rendered);

    /*
     * Backends report the device-observed playback position and current output
     * latency. Until a backend reports a position, the render callback frame
     * count is used as the provisional monotonic clock.
     */
    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_report_device_position(
        yokko_audio_engine* engine,
        uint64_t device_frame_position,
        uint32_t device_latency_frames);

    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_get_status(
        const yokko_audio_engine* engine,
        yokko_audio_status* status);

#ifdef __cplusplus
}
#endif
