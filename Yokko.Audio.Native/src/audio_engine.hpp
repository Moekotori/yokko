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
        static constexpr uint32_t sample_voice_capacity = 32;

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
        };

        [[nodiscard]] double playback_time_milliseconds() const noexcept;
        void update_primed_state() noexcept;
        void activate_pending_samples() noexcept;
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
        std::vector<RegisteredSample> sample_bank_;
        std::array<SampleTrigger, sample_trigger_capacity> sample_trigger_queue_{};
        std::atomic<uint32_t> sample_trigger_read_{0};
        std::atomic<uint32_t> sample_trigger_write_{0};
        std::array<SampleVoice, sample_voice_capacity> sample_voices_{};
        uint32_t next_sample_voice_{0};
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
