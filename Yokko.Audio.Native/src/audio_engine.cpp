#include "audio_engine.hpp"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <limits>
#include <new>
#include <thread>

namespace
{
    constexpr uint32_t max_sample_rate = 768000;
    constexpr uint32_t max_channels = 32;
    constexpr uint32_t max_ring_capacity_frames = 1u << 24;

    bool valid_config(const yokko_audio_config& config) noexcept
    {
        return config.struct_size >= sizeof(yokko_audio_config)
               && config.sample_rate > 0
               && config.sample_rate <= max_sample_rate
               && config.channels > 0
               && config.channels <= max_channels
               && config.ring_capacity_frames > 0
               && config.ring_capacity_frames <= max_ring_capacity_frames
               && config.startup_threshold_frames <= config.ring_capacity_frames;
    }

    float sanitise_sample(const float sample) noexcept
    {
        if (!std::isfinite(sample))
            return 0.0f;

        return std::clamp(sample, -1.0f, 1.0f);
    }
}

namespace yokko::audio
{
    AudioEngine::AudioEngine(const yokko_audio_config& config)
        : sample_rate_(config.sample_rate),
          channels_(config.channels),
          startup_threshold_frames_(config.startup_threshold_frames),
          ring_(config.ring_capacity_frames, config.channels)
    {
        update_primed_state();
    }

    yokko_audio_result AudioEngine::start() noexcept
    {
        const yokko_audio_state current = state_.load(std::memory_order_acquire);
        if (current == YOKKO_AUDIO_STATE_RUNNING)
            return YOKKO_AUDIO_OK;

        if (current == YOKKO_AUDIO_STATE_PAUSED)
        {
            state_.store(YOKKO_AUDIO_STATE_RUNNING, std::memory_order_release);
            return YOKKO_AUDIO_OK;
        }

        if (current != YOKKO_AUDIO_STATE_PRIMED)
            return YOKKO_AUDIO_NOT_READY;

        state_.store(YOKKO_AUDIO_STATE_RUNNING, std::memory_order_release);
        return YOKKO_AUDIO_OK;
    }

    yokko_audio_result AudioEngine::pause() noexcept
    {
        const yokko_audio_state current = state_.load(std::memory_order_acquire);
        if (current == YOKKO_AUDIO_STATE_PAUSED)
            return YOKKO_AUDIO_OK;

        if (current != YOKKO_AUDIO_STATE_RUNNING)
            return YOKKO_AUDIO_INVALID_STATE;

        state_.store(YOKKO_AUDIO_STATE_PAUSED, std::memory_order_release);
        return YOKKO_AUDIO_OK;
    }

    yokko_audio_result AudioEngine::stop() noexcept
    {
        accepting_submissions_.store(false, std::memory_order_release);
        state_.store(YOKKO_AUDIO_STATE_IDLE, std::memory_order_release);

        /*
         * The control thread may wait for an in-flight producer/callback, but
         * neither real-time path ever waits for the control thread or takes a
         * lock. A callback entering after the state change observes Idle and
         * does not touch the ring.
         */
        while (active_submit_calls_.load(std::memory_order_acquire) != 0
               || active_render_callbacks_.load(std::memory_order_acquire) != 0)
            std::this_thread::yield();

        ring_.reset();
        submitted_frames_.store(0, std::memory_order_release);
        source_frames_rendered_.store(0, std::memory_order_release);
        device_frames_rendered_.store(0, std::memory_order_release);
        underrun_count_.store(0, std::memory_order_release);
        reported_device_frame_position_.store(0, std::memory_order_release);
        device_latency_frames_.store(0, std::memory_order_release);
        has_reported_device_position_.store(false, std::memory_order_release);
        accepting_submissions_.store(true, std::memory_order_release);
        update_primed_state();
        return YOKKO_AUDIO_OK;
    }

    yokko_audio_result AudioEngine::submit(
        const float* samples,
        const uint32_t frame_count,
        uint32_t& accepted_frames) noexcept
    {
        accepted_frames = 0;
        if (frame_count == 0)
            return YOKKO_AUDIO_OK;
        if (samples == nullptr)
            return YOKKO_AUDIO_INVALID_ARGUMENT;
        if (!accepting_submissions_.load(std::memory_order_acquire))
            return YOKKO_AUDIO_INVALID_STATE;

        active_submit_calls_.fetch_add(1, std::memory_order_acq_rel);
        if (!accepting_submissions_.load(std::memory_order_acquire))
        {
            active_submit_calls_.fetch_sub(1, std::memory_order_release);
            return YOKKO_AUDIO_INVALID_STATE;
        }

        const yokko_audio_state current = state_.load(std::memory_order_acquire);
        if (current == YOKKO_AUDIO_STATE_FAULTED)
        {
            active_submit_calls_.fetch_sub(1, std::memory_order_release);
            return YOKKO_AUDIO_INVALID_STATE;
        }

        accepted_frames = ring_.write(samples, frame_count);
        submitted_frames_.fetch_add(accepted_frames, std::memory_order_relaxed);
        update_primed_state();
        active_submit_calls_.fetch_sub(1, std::memory_order_release);
        return YOKKO_AUDIO_OK;
    }

    yokko_audio_result AudioEngine::render(
        float* output,
        const uint32_t frame_count,
        uint32_t& source_frames_rendered) noexcept
    {
        source_frames_rendered = 0;
        if (frame_count == 0)
            return YOKKO_AUDIO_OK;
        if (output == nullptr)
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        active_render_callbacks_.fetch_add(1, std::memory_order_acq_rel);
        const size_t sample_count = static_cast<size_t>(frame_count) * channels_;
        if (state_.load(std::memory_order_acquire) != YOKKO_AUDIO_STATE_RUNNING)
        {
            std::fill_n(output, sample_count, 0.0f);
            active_render_callbacks_.fetch_sub(1, std::memory_order_release);
            return YOKKO_AUDIO_OK;
        }

        source_frames_rendered = ring_.read(output, frame_count);
        const size_t rendered_sample_count =
            static_cast<size_t>(source_frames_rendered) * channels_;

        for (size_t index = 0; index < rendered_sample_count; ++index)
            output[index] = sanitise_sample(output[index]);

        if (source_frames_rendered < frame_count)
        {
            std::fill(
                output + rendered_sample_count,
                output + sample_count,
                0.0f);
            underrun_count_.fetch_add(1, std::memory_order_relaxed);
        }

        source_frames_rendered_.fetch_add(
            source_frames_rendered,
            std::memory_order_relaxed);
        device_frames_rendered_.fetch_add(frame_count, std::memory_order_relaxed);
        active_render_callbacks_.fetch_sub(1, std::memory_order_release);
        return YOKKO_AUDIO_OK;
    }

    yokko_audio_result AudioEngine::report_device_position(
        const uint64_t device_frame_position,
        const uint32_t device_latency_frames) noexcept
    {
        const bool already_reported =
            has_reported_device_position_.load(std::memory_order_acquire);
        const uint64_t previous =
            reported_device_frame_position_.load(std::memory_order_acquire);

        if (already_reported && device_frame_position < previous)
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        reported_device_frame_position_.store(
            device_frame_position,
            std::memory_order_release);
        device_latency_frames_.store(
            device_latency_frames,
            std::memory_order_release);
        has_reported_device_position_.store(true, std::memory_order_release);
        return YOKKO_AUDIO_OK;
    }

    void AudioEngine::get_status(yokko_audio_status& status) const noexcept
    {
        status = {};
        status.struct_size = sizeof(yokko_audio_status);
        status.abi_version = YOKKO_AUDIO_ABI_VERSION;
        status.state = state_.load(std::memory_order_acquire);
        status.sample_rate = sample_rate_;
        status.channels = channels_;
        status.ring_capacity_frames = ring_.capacity_frames();
        status.buffered_frames = ring_.available_frames();
        status.device_latency_frames =
            device_latency_frames_.load(std::memory_order_acquire);
        status.submitted_frames =
            submitted_frames_.load(std::memory_order_acquire);
        status.source_frames_rendered =
            source_frames_rendered_.load(std::memory_order_acquire);
        status.device_frames_rendered =
            device_frames_rendered_.load(std::memory_order_acquire);
        status.underrun_count =
            underrun_count_.load(std::memory_order_acquire);
        status.playback_time_milliseconds = playback_time_milliseconds();
    }

    double AudioEngine::playback_time_milliseconds() const noexcept
    {
        const uint64_t device_position =
            has_reported_device_position_.load(std::memory_order_acquire)
                ? reported_device_frame_position_.load(std::memory_order_acquire)
                : device_frames_rendered_.load(std::memory_order_acquire);
        const uint64_t latency =
            device_latency_frames_.load(std::memory_order_acquire);
        const uint64_t presented_position =
            device_position > latency ? device_position - latency : 0;
        return static_cast<double>(presented_position) * 1000.0
               / static_cast<double>(sample_rate_);
    }

    void AudioEngine::update_primed_state() noexcept
    {
        const yokko_audio_state current = state_.load(std::memory_order_acquire);
        if (current != YOKKO_AUDIO_STATE_IDLE
            && current != YOKKO_AUDIO_STATE_PRIMED)
            return;

        const bool primed =
            ring_.available_frames() >= startup_threshold_frames_;
        state_.store(
            primed ? YOKKO_AUDIO_STATE_PRIMED : YOKKO_AUDIO_STATE_IDLE,
            std::memory_order_release);
    }
}

extern "C"
{
    uint32_t YOKKO_AUDIO_CALL yokko_audio_get_abi_version(void)
    {
        return YOKKO_AUDIO_ABI_VERSION;
    }

    yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_create(
        const yokko_audio_config* config,
        yokko_audio_engine** engine)
    {
        if (config == nullptr || engine == nullptr || !valid_config(*config))
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        *engine = nullptr;
        try
        {
            *engine = new yokko_audio_engine(*config);
            return YOKKO_AUDIO_OK;
        }
        catch (const std::bad_alloc&)
        {
            return YOKKO_AUDIO_OUT_OF_MEMORY;
        }
        catch (...)
        {
            return YOKKO_AUDIO_INTERNAL_ERROR;
        }
    }

    void YOKKO_AUDIO_CALL yokko_audio_destroy(yokko_audio_engine* engine)
    {
        delete engine;
    }

    yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_start(
        yokko_audio_engine* engine)
    {
        return engine == nullptr
                   ? YOKKO_AUDIO_INVALID_ARGUMENT
                   : engine->implementation.start();
    }

    yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_pause(
        yokko_audio_engine* engine)
    {
        return engine == nullptr
                   ? YOKKO_AUDIO_INVALID_ARGUMENT
                   : engine->implementation.pause();
    }

    yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_stop(
        yokko_audio_engine* engine)
    {
        return engine == nullptr
                   ? YOKKO_AUDIO_INVALID_ARGUMENT
                   : engine->implementation.stop();
    }

    yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_submit_interleaved_f32(
        yokko_audio_engine* engine,
        const float* samples,
        const uint32_t frame_count,
        uint32_t* accepted_frames)
    {
        if (engine == nullptr || accepted_frames == nullptr)
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        return engine->implementation.submit(
            samples,
            frame_count,
            *accepted_frames);
    }

    yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_render_interleaved_f32(
        yokko_audio_engine* engine,
        float* output,
        const uint32_t frame_count,
        uint32_t* source_frames_rendered)
    {
        if (engine == nullptr || source_frames_rendered == nullptr)
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        return engine->implementation.render(
            output,
            frame_count,
            *source_frames_rendered);
    }

    yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_report_device_position(
        yokko_audio_engine* engine,
        const uint64_t device_frame_position,
        const uint32_t device_latency_frames)
    {
        return engine == nullptr
                   ? YOKKO_AUDIO_INVALID_ARGUMENT
                   : engine->implementation.report_device_position(
                       device_frame_position,
                       device_latency_frames);
    }

    yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_get_status(
        const yokko_audio_engine* engine,
        yokko_audio_status* status)
    {
        if (engine == nullptr || status == nullptr)
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        if (status->struct_size < sizeof(yokko_audio_status))
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        engine->implementation.get_status(*status);
        return YOKKO_AUDIO_OK;
    }
}
