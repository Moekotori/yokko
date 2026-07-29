#pragma once

#include <stdint.h>
#include <wchar.h>

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

#define YOKKO_AUDIO_ABI_VERSION 9u

    typedef struct yokko_audio_engine yokko_audio_engine;

    typedef enum yokko_audio_result
    {
        YOKKO_AUDIO_OK = 0,
        YOKKO_AUDIO_INVALID_ARGUMENT = 1,
        YOKKO_AUDIO_INVALID_STATE = 2,
        YOKKO_AUDIO_NOT_READY = 3,
        YOKKO_AUDIO_OUT_OF_MEMORY = 4,
        YOKKO_AUDIO_INTERNAL_ERROR = 5,
        YOKKO_AUDIO_BACKEND_UNAVAILABLE = 6,
        YOKKO_AUDIO_QUEUE_FULL = 7,
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
        uint64_t callback_count;
        uint64_t callback_deadline_miss_count;
        uint32_t callback_budget_microseconds;
        uint32_t callback_max_duration_microseconds;
        int32_t backend_error;
        uint32_t backend_error_stage;
        double playback_time_milliseconds;
        uint64_t callback_cadence_miss_count;
        uint32_t callback_max_interval_microseconds;
    } yokko_audio_status;

    typedef enum yokko_audio_backend_mode
    {
        YOKKO_AUDIO_BACKEND_WASAPI_SHARED = 1,
        YOKKO_AUDIO_BACKEND_WASAPI_EXCLUSIVE = 2,
        YOKKO_AUDIO_BACKEND_ASIO = 3,
    } yokko_audio_backend_mode;

    typedef enum yokko_audio_sample_format
    {
        YOKKO_AUDIO_SAMPLE_FLOAT32 = 1,
        YOKKO_AUDIO_SAMPLE_PCM32 = 2,
        YOKKO_AUDIO_SAMPLE_PCM24_IN_32 = 3,
        YOKKO_AUDIO_SAMPLE_PCM16 = 4,
    } yokko_audio_sample_format;

    typedef struct yokko_audio_output_config
    {
        uint32_t struct_size;
        yokko_audio_backend_mode backend;
        const wchar_t* device_id;
        uint32_t preferred_buffer_frames;
    } yokko_audio_output_config;

    typedef struct yokko_audio_output_status
    {
        uint32_t struct_size;
        yokko_audio_backend_mode backend;
        yokko_audio_sample_format sample_format;
        uint32_t sample_rate;
        uint32_t channels;
        uint32_t buffer_frames;
        uint32_t latency_frames;
        uint32_t is_active;
        int32_t backend_error;
        uint32_t backend_error_stage;
    } yokko_audio_output_status;

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
     * Registers immutable, engine-rate interleaved PCM for callback-side
     * keysound mixing. Registration is a control-thread operation and must
     * complete before the engine starts.
     */
    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_register_sample_f32(
        yokko_audio_engine* engine,
        const float* samples,
        uint32_t frame_count,
        uint32_t* sample_id);

    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_register_metronome_sample_f32(
        yokko_audio_engine* engine,
        const float* samples,
        uint32_t frame_count,
        uint32_t* sample_id);

    /*
     * Updates callback-side bus gains without blocking. Values must be finite
     * and within 0..1.
     */
    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_set_mix_volumes(
        yokko_audio_engine* engine,
        float music_volume,
        float hit_sound_volume,
        float metronome_volume);

    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_set_sample_playback_rate(
        yokko_audio_engine* engine,
        float playback_rate);

    /*
     * Enqueues a registered sample without allocating or blocking. The next
     * output callback mixes it directly, bypassing the prefetched music ring.
     */
    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_trigger_sample(
        yokko_audio_engine* engine,
        uint32_t sample_id);

    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_trigger_sample_with_gain(
        yokko_audio_engine* engine,
        uint32_t sample_id,
        float gain);

    /*
     * Starts or stops a callback-owned looping sample voice. A successful
     * start returns a non-zero handle which remains valid until stopped or the
     * engine is reset.
     */
    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_start_looping_sample(
        yokko_audio_engine* engine,
        uint32_t sample_id,
        float gain,
        uint32_t* loop_id);

    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_stop_looping_sample(
        yokko_audio_engine* engine,
        uint32_t loop_id);

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
     * Backends report the frame currently presented by the endpoint together
     * with the correlated monotonic timestamp. WASAPI obtains both from
     * IAudioClock::GetPosition. Output latency is telemetry only and must not
     * be subtracted from the already-presented position.
     *
     * Until a backend reports a position, the render callback frame count is
     * used as the provisional monotonic clock. Pass zero for the timestamp
     * when a backend cannot provide a correlated monotonic observation.
     */
    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_report_presented_position(
        yokko_audio_engine* engine,
        uint64_t presented_frame_position,
        uint32_t output_latency_frames,
        uint64_t observation_time_100ns);

    /*
     * Backends report callback work duration against the accepted device
     * period. This is lock-free telemetry and is safe on the real-time thread.
     */
    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_report_callback_timing(
        yokko_audio_engine* engine,
        uint32_t duration_microseconds,
        uint32_t budget_microseconds,
        uint32_t interval_microseconds);

    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_get_status(
        const yokko_audio_engine* engine,
        yokko_audio_status* status);

    /*
     * Opens an event-driven WASAPI stream owned entirely by the native engine.
     * The engine must already be primed and running. The device callback reads
     * directly from the native PCM ring and never enters managed code.
     */
    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_open_wasapi(
        yokko_audio_engine* engine,
        const yokko_audio_output_config* config,
        yokko_audio_output_status* status);

    /*
     * Opens the selected ASIO driver without a WASAPI fallback. The device id
     * must be one returned by yokko_audio_get_asio_device_info, or null to use
     * the first registered 64-bit driver.
     */
    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_open_asio(
        yokko_audio_engine* engine,
        const yokko_audio_output_config* config,
        yokko_audio_output_status* status);

    YOKKO_AUDIO_API void YOKKO_AUDIO_CALL yokko_audio_close_output(
        yokko_audio_engine* engine);

    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL
        yokko_audio_get_wasapi_device_count(uint32_t* device_count);

    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL
        yokko_audio_get_wasapi_device_info(
            uint32_t device_index,
            wchar_t* device_id,
            uint32_t device_id_capacity,
            wchar_t* device_name,
            uint32_t device_name_capacity,
            uint32_t* is_default);

    /*
     * ASIO discovery is passive: it reads registered 64-bit drivers without
     * loading vendor DLLs, reserving hardware, or showing a control panel.
     * Builds without an externally supplied ASIO SDK return
     * YOKKO_AUDIO_BACKEND_UNAVAILABLE.
     */
    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL
        yokko_audio_get_asio_device_count(uint32_t* device_count);

    YOKKO_AUDIO_API yokko_audio_result YOKKO_AUDIO_CALL
        yokko_audio_get_asio_device_info(
            uint32_t device_index,
            wchar_t* device_id,
            uint32_t device_id_capacity,
            wchar_t* device_name,
            uint32_t device_name_capacity,
            uint32_t* is_default);

#ifdef __cplusplus
}
#endif
