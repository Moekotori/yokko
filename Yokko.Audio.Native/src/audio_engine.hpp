#pragma once

#include "spsc_pcm_ring_buffer.hpp"
#include "yokko_audio.h"

#include <atomic>
#include <array>
#include <cstdint>
#include <memory>
#include <vector>

namespace yokko::audio
{
    class AsioOutput;
    class WasapiOutput;

    class AudioEngine
    {
    public:
        explicit AudioEngine(const yokko_audio_config& config);
        ~AudioEngine();

        yokko_audio_result start() noexcept;
        yokko_audio_result pause() noexcept;
        yokko_audio_result stop() noexcept;
        yokko_audio_result submit(
            const float* samples,
            uint32_t frame_count,
            uint32_t& accepted_frames) noexcept;
        yokko_audio_result register_sample(
            const float* samples,
            uint32_t frame_count,
            uint32_t& sample_id,
            bool metronome = false) noexcept;
        yokko_audio_result set_mix_volumes(
            float music_volume,
            float hit_sound_volume,
            float metronome_volume) noexcept;
        yokko_audio_result set_sample_playback_rate(
            float playback_rate) noexcept;
        yokko_audio_result trigger_sample(
            uint32_t sample_id,
            float gain = 1.0f) noexcept;
        yokko_audio_result trigger_sample_traced(
            uint32_t sample_id,
            float gain,
            uint64_t capture_timestamp,
            uint64_t timestamp_frequency,
            uint64_t& trace_id) noexcept;
        yokko_audio_result try_dequeue_sample_telemetry(
            yokko_audio_sample_trigger_telemetry& telemetry) noexcept;
        void get_sample_telemetry_status(
            yokko_audio_sample_telemetry_status& status) const noexcept;
        yokko_audio_result start_looping_sample(
            uint32_t sample_id,
            float gain,
            uint32_t& loop_id) noexcept;
        yokko_audio_result stop_looping_sample(
            uint32_t loop_id) noexcept;
        yokko_audio_result render(
            float* output,
            uint32_t frame_count,
            uint32_t& source_frames_rendered) noexcept;
        yokko_audio_result report_presented_position(
            uint64_t presented_frame_position,
            uint32_t output_latency_frames,
            uint64_t observation_time_100ns) noexcept;
        void report_callback_timing(
            uint32_t duration_microseconds,
            uint32_t budget_microseconds,
            uint32_t interval_microseconds = 0) noexcept;
        void report_callback_overload() noexcept;
        void report_output_failure(
            int32_t backend_error,
            uint32_t backend_error_stage) noexcept;
        void get_status(yokko_audio_status& status) const noexcept;
        yokko_audio_result open_wasapi(
            const yokko_audio_output_config& config,
            yokko_audio_output_status& status) noexcept;
        yokko_audio_result open_asio(
            const yokko_audio_output_config& config,
            yokko_audio_output_status& status) noexcept;
        void close_output() noexcept;

        [[nodiscard]] uint32_t sample_rate() const noexcept
        {
            return sample_rate_;
        }

        [[nodiscard]] uint32_t channels() const noexcept
        {
            return channels_;
        }

    private:
        static constexpr uint32_t sample_trigger_capacity = 128;
        static constexpr uint32_t sample_voice_capacity =
            sample_trigger_capacity;
        static constexpr uint32_t sample_telemetry_capacity = 512;

        struct RegisteredSample
        {
            std::vector<float> pcm;
            uint32_t frame_count{0};
            bool metronome{false};
        };

        struct SampleVoice
        {
            uint32_t sample_id{0};
            double frame_position{0};
            float gain{1.0f};
            uint32_t loop_id{0};
            bool looping{false};
            uint64_t trace_id{0};
            uint64_t capture_time_100ns{0};
            uint64_t enqueue_time_100ns{0};
            uint64_t callback_time_100ns{0};
        };

        enum class SampleTriggerAction : uint8_t
        {
            play,
            start_loop,
            stop_loop,
        };

        struct SampleTrigger
        {
            SampleTriggerAction action{SampleTriggerAction::play};
            uint32_t sample_id{0};
            float gain{1.0f};
            uint32_t loop_id{0};
            uint64_t trace_id{0};
            uint64_t capture_time_100ns{0};
            uint64_t enqueue_time_100ns{0};
        };

        struct SampleTriggerCell
        {
            std::atomic<uint64_t> sequence{0};
            SampleTrigger trigger{};
        };

        [[nodiscard]] double playback_time_milliseconds(
            bool has_presented_position,
            uint64_t presented_frame_position,
            uint64_t observation_time_100ns) const noexcept;
        void update_primed_state() noexcept;
        yokko_audio_result enqueue_sample_trigger(
            const SampleTrigger& trigger) noexcept;
        bool try_dequeue_sample_trigger(SampleTrigger& trigger) noexcept;
        void activate_pending_samples() noexcept;
        void publish_sample_telemetry(
            const SampleTrigger& trigger,
            uint64_t callback_time_100ns,
            uint64_t first_output_frame_position) noexcept;
        void mix_active_samples(float* output, uint32_t frame_count) noexcept;
        void reset_sample_playback() noexcept;

        uint32_t sample_rate_;
        uint32_t channels_;
        uint32_t startup_threshold_frames_;
        SpscPcmRingBuffer ring_;

        std::atomic<yokko_audio_state> state_{YOKKO_AUDIO_STATE_IDLE};
        std::atomic<uint64_t> submitted_frames_{0};
        std::atomic<uint64_t> source_frames_rendered_{0};
        std::atomic<uint64_t> device_frames_rendered_{0};
        std::atomic<uint64_t> underrun_count_{0};
        std::atomic<uint32_t> reported_position_sequence_{0};
        std::atomic<uint64_t> reported_presented_frame_position_{0};
        std::atomic<uint64_t> reported_position_time_100ns_{0};
        std::atomic<uint32_t> device_latency_frames_{0};
        std::atomic<bool> has_reported_presented_position_{false};
        std::atomic<uint64_t> callback_count_{0};
        std::atomic<uint64_t> callback_deadline_miss_count_{0};
        std::atomic<uint32_t> callback_budget_microseconds_{0};
        std::atomic<uint32_t> callback_max_duration_microseconds_{0};
        std::atomic<uint64_t> callback_cadence_miss_count_{0};
        std::atomic<uint32_t> callback_max_interval_microseconds_{0};
        std::atomic<uint64_t> backend_overload_count_{0};
        std::atomic<int32_t> backend_error_{0};
        std::atomic<uint32_t> backend_error_stage_{0};
        mutable std::atomic<uint64_t> last_playback_frame_position_{0};
        std::atomic<bool> accepting_submissions_{true};
        std::atomic<uint32_t> active_submit_calls_{0};
        std::atomic<uint32_t> active_render_callbacks_{0};
        std::atomic<float> music_volume_{1.0f};
        std::atomic<float> hit_sound_volume_{1.0f};
        std::atomic<float> metronome_volume_{0.0f};
        std::atomic<float> sample_playback_rate_{1.0f};
        std::atomic<uint32_t> next_sample_loop_id_{1};
        std::atomic<bool> accepting_sample_triggers_{true};
        std::atomic<uint32_t> active_sample_trigger_calls_{0};
        std::atomic<uint64_t> next_sample_trace_id_{1};
        std::vector<RegisteredSample> sample_bank_;
        std::array<SampleTriggerCell, sample_trigger_capacity>
            sample_trigger_queue_{};
        std::atomic<uint64_t> sample_trigger_dequeue_{0};
        std::atomic<uint64_t> sample_trigger_enqueue_{0};
        std::array<SampleVoice, sample_voice_capacity> sample_voices_{};
        uint32_t next_sample_voice_{0};
        std::array<yokko_audio_sample_trigger_telemetry,
                   sample_telemetry_capacity>
            sample_telemetry_queue_{};
        std::atomic<uint64_t> sample_telemetry_read_{0};
        std::atomic<uint64_t> sample_telemetry_write_{0};
        std::atomic<uint64_t> sample_telemetry_dropped_{0};
        std::unique_ptr<WasapiOutput> output_;
        std::unique_ptr<AsioOutput> asio_output_;
    };
}

struct yokko_audio_engine
{
    explicit yokko_audio_engine(const yokko_audio_config& config)
        : implementation(config)
    {
    }

    yokko::audio::AudioEngine implementation;
};
