#pragma once

#include "spsc_pcm_ring_buffer.hpp"
#include "yokko_audio.h"

#include <atomic>
#include <cstdint>
#include <memory>

namespace yokko::audio
{
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
            uint32_t budget_microseconds) noexcept;
        void report_output_failure(
            int32_t backend_error,
            uint32_t backend_error_stage) noexcept;
        void get_status(yokko_audio_status& status) const noexcept;
        yokko_audio_result open_wasapi(
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
        [[nodiscard]] double playback_time_milliseconds() const noexcept;
        void update_primed_state() noexcept;

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
        std::atomic<int32_t> backend_error_{0};
        std::atomic<uint32_t> backend_error_stage_{0};
        mutable std::atomic<uint64_t> last_playback_frame_position_{0};
        std::atomic<bool> accepting_submissions_{true};
        std::atomic<uint32_t> active_submit_calls_{0};
        std::atomic<uint32_t> active_render_callbacks_{0};
        std::unique_ptr<WasapiOutput> output_;
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
