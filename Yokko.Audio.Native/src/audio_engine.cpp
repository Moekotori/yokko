#include "audio_engine.hpp"
#include "asio_output.hpp"
#include "wasapi_output.hpp"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <limits>
#include <new>
#include <thread>

#if defined(_WIN32)
#define NOMINMAX
#include <windows.h>
#endif

namespace
{
    constexpr uint32_t max_sample_rate = 768000;
    constexpr uint32_t max_channels = 32;
    constexpr uint32_t max_ring_capacity_frames = 1u << 24;
    constexpr uint32_t max_registered_sample_seconds = 60;

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

    uint64_t monotonic_time_100ns() noexcept
    {
#if defined(_WIN32)
        LARGE_INTEGER counter{};
        LARGE_INTEGER frequency{};
        if (!QueryPerformanceCounter(&counter)
            || !QueryPerformanceFrequency(&frequency)
            || frequency.QuadPart <= 0)
            return 0;

        return static_cast<uint64_t>(
            static_cast<long double>(counter.QuadPart) * 10'000'000.0L
            / static_cast<long double>(frequency.QuadPart));
#else
        return 0;
#endif
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
        reset_sample_playback();
        update_primed_state();
    }

    AudioEngine::~AudioEngine()
    {
        close_output();
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
        close_output();
        accepting_submissions_.store(false, std::memory_order_release);
        accepting_sample_triggers_.store(false, std::memory_order_release);
        state_.store(YOKKO_AUDIO_STATE_IDLE, std::memory_order_release);

        /*
         * The control thread may wait for an in-flight producer/callback, but
         * neither real-time path ever waits for the control thread or takes a
         * lock. A callback entering after the state change observes Idle and
         * does not touch the ring.
         */
        while (active_submit_calls_.load(std::memory_order_acquire) != 0
               || active_render_callbacks_.load(std::memory_order_acquire) != 0
               || active_sample_trigger_calls_.load(
                      std::memory_order_acquire) != 0)
            std::this_thread::yield();

        ring_.reset();
        reset_sample_playback();
        submitted_frames_.store(0, std::memory_order_release);
        source_frames_rendered_.store(0, std::memory_order_release);
        device_frames_rendered_.store(0, std::memory_order_release);
        underrun_count_.store(0, std::memory_order_release);
        reported_position_sequence_.store(0, std::memory_order_release);
        reported_presented_frame_position_.store(0, std::memory_order_release);
        reported_position_time_100ns_.store(0, std::memory_order_release);
        device_latency_frames_.store(0, std::memory_order_release);
        has_reported_presented_position_.store(false, std::memory_order_release);
        callback_count_.store(0, std::memory_order_release);
        callback_deadline_miss_count_.store(0, std::memory_order_release);
        callback_budget_microseconds_.store(0, std::memory_order_release);
        callback_max_duration_microseconds_.store(0, std::memory_order_release);
        callback_cadence_miss_count_.store(0, std::memory_order_release);
        callback_max_interval_microseconds_.store(0, std::memory_order_release);
        backend_overload_count_.store(0, std::memory_order_release);
        backend_error_.store(0, std::memory_order_release);
        backend_error_stage_.store(0, std::memory_order_release);
        last_playback_frame_position_.store(0, std::memory_order_release);
        accepting_submissions_.store(true, std::memory_order_release);
        accepting_sample_triggers_.store(true, std::memory_order_release);
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

    yokko_audio_result AudioEngine::register_sample(
        const float* samples,
        const uint32_t frame_count,
        uint32_t& sample_id,
        const bool metronome) noexcept
    {
        sample_id = 0;
        if (samples == nullptr || frame_count == 0
            || frame_count > sample_rate_ * max_registered_sample_seconds)
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        const yokko_audio_state current = state_.load(std::memory_order_acquire);
        if (current == YOKKO_AUDIO_STATE_RUNNING
            || current == YOKKO_AUDIO_STATE_PAUSED
            || current == YOKKO_AUDIO_STATE_FAULTED)
            return YOKKO_AUDIO_INVALID_STATE;

        try
        {
            RegisteredSample registered{};
            registered.pcm.assign(
                samples,
                samples + static_cast<size_t>(frame_count) * channels_);
            for (float& sample : registered.pcm)
                sample = sanitise_sample(sample);
            registered.frame_count = frame_count;
            registered.metronome = metronome;
            sample_bank_.push_back(std::move(registered));
            sample_id = static_cast<uint32_t>(sample_bank_.size());
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

    yokko_audio_result AudioEngine::set_mix_volumes(
        const float music_volume,
        const float hit_sound_volume,
        const float metronome_volume) noexcept
    {
        if (!std::isfinite(music_volume)
            || !std::isfinite(hit_sound_volume)
            || !std::isfinite(metronome_volume)
            || music_volume < 0.0f || music_volume > 1.0f
            || hit_sound_volume < 0.0f || hit_sound_volume > 1.0f
            || metronome_volume < 0.0f || metronome_volume > 1.0f)
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        music_volume_.store(music_volume, std::memory_order_release);
        hit_sound_volume_.store(hit_sound_volume, std::memory_order_release);
        metronome_volume_.store(metronome_volume, std::memory_order_release);
        return YOKKO_AUDIO_OK;
    }

    yokko_audio_result AudioEngine::set_sample_playback_rate(
        const float playback_rate) noexcept
    {
        if (!std::isfinite(playback_rate)
            || playback_rate < 0.25f
            || playback_rate > 4.0f)
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        sample_playback_rate_.store(
            playback_rate,
            std::memory_order_release);
        return YOKKO_AUDIO_OK;
    }

    yokko_audio_result AudioEngine::set_music_sample_playback_rate(
        const float playback_rate) noexcept
    {
        if (!std::isfinite(playback_rate)
            || playback_rate < 0.25f
            || playback_rate > 4.0f)
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        music_sample_playback_rate_.store(
            playback_rate,
            std::memory_order_release);
        return YOKKO_AUDIO_OK;
    }

    yokko_audio_result AudioEngine::trigger_sample(
        const uint32_t sample_id,
        const float gain) noexcept
    {
        if (sample_id == 0 || sample_id > sample_bank_.size())
            return YOKKO_AUDIO_INVALID_ARGUMENT;
        if (!std::isfinite(gain) || gain < 0.0f || gain > 1.0f)
            return YOKKO_AUDIO_INVALID_ARGUMENT;
        return enqueue_sample_trigger(
            {
                SampleTriggerAction::play,
                sample_id,
                gain,
                0,
                0,
                0,
                0,
            });
    }

    yokko_audio_result AudioEngine::trigger_music_sample(
        const uint32_t sample_id,
        const float gain) noexcept
    {
        if (sample_id == 0 || sample_id > sample_bank_.size())
            return YOKKO_AUDIO_INVALID_ARGUMENT;
        if (!std::isfinite(gain) || gain < 0.0f || gain > 1.0f)
            return YOKKO_AUDIO_INVALID_ARGUMENT;
        return enqueue_sample_trigger(
            {
                SampleTriggerAction::play,
                sample_id,
                gain,
                0,
                0,
                0,
                0,
                true,
            });
    }

    yokko_audio_result AudioEngine::trigger_sample_traced(
        const uint32_t sample_id,
        const float gain,
        const uint64_t capture_timestamp,
        const uint64_t timestamp_frequency,
        uint64_t& trace_id) noexcept
    {
        trace_id = 0;
        if (sample_id == 0 || sample_id > sample_bank_.size())
            return YOKKO_AUDIO_INVALID_ARGUMENT;
        if (!std::isfinite(gain) || gain < 0.0f || gain > 1.0f
            || capture_timestamp == 0 || timestamp_frequency == 0)
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        const long double capture_100ns =
            static_cast<long double>(capture_timestamp) * 10'000'000.0L
            / static_cast<long double>(timestamp_frequency);
        if (capture_100ns <= 0
            || capture_100ns
                   > static_cast<long double>(
                       std::numeric_limits<uint64_t>::max()))
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        uint64_t next_trace = next_sample_trace_id_.fetch_add(
            1,
            std::memory_order_relaxed);
        if (next_trace == 0)
        {
            next_trace = next_sample_trace_id_.fetch_add(
                1,
                std::memory_order_relaxed);
        }

        const yokko_audio_result result = enqueue_sample_trigger(
            {
                SampleTriggerAction::play,
                sample_id,
                gain,
                0,
                next_trace,
                static_cast<uint64_t>(capture_100ns),
                monotonic_time_100ns(),
            });
        if (result == YOKKO_AUDIO_OK)
            trace_id = next_trace;
        return result;
    }

    yokko_audio_result AudioEngine::start_looping_sample(
        const uint32_t sample_id,
        const float gain,
        uint32_t& loop_id) noexcept
    {
        loop_id = 0;
        if (sample_id == 0 || sample_id > sample_bank_.size())
            return YOKKO_AUDIO_INVALID_ARGUMENT;
        if (!std::isfinite(gain) || gain < 0.0f || gain > 1.0f)
            return YOKKO_AUDIO_INVALID_ARGUMENT;
        loop_id = next_sample_loop_id_.fetch_add(
            1,
            std::memory_order_relaxed);
        if (loop_id == 0)
        {
            loop_id = next_sample_loop_id_.fetch_add(
                1,
                std::memory_order_relaxed);
        }
        const yokko_audio_result result = enqueue_sample_trigger(
            {
                SampleTriggerAction::start_loop,
                sample_id,
                gain,
                loop_id,
                0,
                0,
                0,
            });
        if (result != YOKKO_AUDIO_OK)
            loop_id = 0;
        return result;
    }

    yokko_audio_result AudioEngine::stop_looping_sample(
        const uint32_t loop_id) noexcept
    {
        if (loop_id == 0)
            return YOKKO_AUDIO_INVALID_ARGUMENT;
        return enqueue_sample_trigger(
            {
                SampleTriggerAction::stop_loop,
                0,
                1.0f,
                loop_id,
                0,
                0,
                0,
            });
    }

    yokko_audio_result AudioEngine::enqueue_sample_trigger(
        const SampleTrigger& trigger) noexcept
    {
        if (!accepting_sample_triggers_.load(std::memory_order_acquire))
            return YOKKO_AUDIO_NOT_READY;

        active_sample_trigger_calls_.fetch_add(1, std::memory_order_acq_rel);
        if (!accepting_sample_triggers_.load(std::memory_order_acquire)
            || state_.load(std::memory_order_acquire)
                   != YOKKO_AUDIO_STATE_RUNNING)
        {
            active_sample_trigger_calls_.fetch_sub(
                1,
                std::memory_order_release);
            return YOKKO_AUDIO_NOT_READY;
        }

        uint64_t position =
            sample_trigger_enqueue_.load(std::memory_order_relaxed);
        for (;;)
        {
            SampleTriggerCell& cell =
                sample_trigger_queue_[position % sample_trigger_capacity];
            const uint64_t sequence =
                cell.sequence.load(std::memory_order_acquire);
            const int64_t difference =
                static_cast<int64_t>(sequence - position);
            if (difference == 0)
            {
                if (sample_trigger_enqueue_.compare_exchange_weak(
                        position,
                        position + 1,
                        std::memory_order_relaxed,
                        std::memory_order_relaxed))
                {
                    cell.trigger = trigger;
                    cell.sequence.store(
                        position + 1,
                        std::memory_order_release);
                    active_sample_trigger_calls_.fetch_sub(
                        1,
                        std::memory_order_release);
                    return YOKKO_AUDIO_OK;
                }
                continue;
            }

            if (difference < 0)
            {
                active_sample_trigger_calls_.fetch_sub(
                    1,
                    std::memory_order_release);
                return YOKKO_AUDIO_QUEUE_FULL;
            }

            position =
                sample_trigger_enqueue_.load(std::memory_order_relaxed);
        }
    }

    bool AudioEngine::try_dequeue_sample_trigger(
        SampleTrigger& trigger) noexcept
    {
        const uint64_t position =
            sample_trigger_dequeue_.load(std::memory_order_relaxed);
        SampleTriggerCell& cell =
            sample_trigger_queue_[position % sample_trigger_capacity];
        const uint64_t sequence =
            cell.sequence.load(std::memory_order_acquire);
        if (static_cast<int64_t>(sequence - (position + 1)) != 0)
            return false;

        trigger = cell.trigger;
        cell.sequence.store(
            position + sample_trigger_capacity,
            std::memory_order_release);
        sample_trigger_dequeue_.store(
            position + 1,
            std::memory_order_relaxed);
        return true;
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

        if (source_frames_rendered < frame_count)
        {
            std::fill(
                output + rendered_sample_count,
                output + sample_count,
                0.0f);
            underrun_count_.fetch_add(1, std::memory_order_relaxed);
        }

        const float music_volume =
            music_volume_.load(std::memory_order_acquire);
        for (size_t index = 0; index < rendered_sample_count; ++index)
            output[index] *= music_volume;

        activate_pending_samples();
        mix_active_samples(output, frame_count);
        for (size_t index = 0; index < sample_count; ++index)
            output[index] = sanitise_sample(output[index]);

        source_frames_rendered_.fetch_add(
            source_frames_rendered,
            std::memory_order_relaxed);
        device_frames_rendered_.fetch_add(frame_count, std::memory_order_relaxed);
        active_render_callbacks_.fetch_sub(1, std::memory_order_release);
        return YOKKO_AUDIO_OK;
    }

    void AudioEngine::activate_pending_samples() noexcept
    {
        SampleTrigger trigger{};
        uint64_t callback_time_100ns = 0;
        uint32_t activated_count = 0;
        while (activated_count < sample_trigger_capacity
               && try_dequeue_sample_trigger(trigger))
        {
            activated_count++;
            if (trigger.action == SampleTriggerAction::stop_loop)
            {
                for (SampleVoice& voice : sample_voices_)
                {
                    if (voice.looping
                        && voice.loop_id == trigger.loop_id)
                        voice = {};
                }
                continue;
            }

            SampleVoice& voice =
                sample_voices_[next_sample_voice_ % sample_voice_capacity];
            if (voice.trace_id != 0)
            {
                sample_telemetry_dropped_.fetch_add(
                    1,
                    std::memory_order_relaxed);
            }
            voice.sample_id = trigger.sample_id;
            voice.frame_position = 0;
            voice.gain = trigger.gain;
            voice.looping =
                trigger.action == SampleTriggerAction::start_loop;
            voice.loop_id = trigger.loop_id;
            voice.music = trigger.music;
            next_sample_voice_++;

            if (trigger.trace_id != 0)
            {
                if (callback_time_100ns == 0)
                    callback_time_100ns = monotonic_time_100ns();
                voice.trace_id = trigger.trace_id;
                voice.capture_time_100ns = trigger.capture_time_100ns;
                voice.enqueue_time_100ns = trigger.enqueue_time_100ns;
                voice.callback_time_100ns = callback_time_100ns;
            }
        }
    }

    void AudioEngine::publish_sample_telemetry(
        const SampleTrigger& trigger,
        const uint64_t callback_time_100ns,
        const uint64_t first_output_frame_position) noexcept
    {
        const uint64_t write =
            sample_telemetry_write_.load(std::memory_order_relaxed);
        const uint64_t read =
            sample_telemetry_read_.load(std::memory_order_acquire);
        if (write - read >= sample_telemetry_capacity)
        {
            sample_telemetry_dropped_.fetch_add(
                1,
                std::memory_order_relaxed);
            return;
        }

        sample_telemetry_queue_[write % sample_telemetry_capacity] =
            {
                sizeof(yokko_audio_sample_trigger_telemetry),
                YOKKO_AUDIO_SAMPLE_TELEMETRY_ABI_VERSION,
                trigger.trace_id,
                trigger.sample_id,
                device_latency_frames_.load(std::memory_order_relaxed),
                sample_rate_,
                0,
                trigger.capture_time_100ns,
                trigger.enqueue_time_100ns,
                callback_time_100ns,
                first_output_frame_position,
            };
        sample_telemetry_write_.store(write + 1, std::memory_order_release);
    }

    yokko_audio_result AudioEngine::try_dequeue_sample_telemetry(
        yokko_audio_sample_trigger_telemetry& telemetry) noexcept
    {
        if (telemetry.struct_size
            < sizeof(yokko_audio_sample_trigger_telemetry))
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        const uint64_t read =
            sample_telemetry_read_.load(std::memory_order_relaxed);
        const uint64_t write =
            sample_telemetry_write_.load(std::memory_order_acquire);
        if (read == write)
            return YOKKO_AUDIO_NOT_READY;

        telemetry =
            sample_telemetry_queue_[read % sample_telemetry_capacity];
        sample_telemetry_read_.store(read + 1, std::memory_order_release);
        return YOKKO_AUDIO_OK;
    }

    void AudioEngine::get_sample_telemetry_status(
        yokko_audio_sample_telemetry_status& status) const noexcept
    {
        const uint64_t read =
            sample_telemetry_read_.load(std::memory_order_acquire);
        const uint64_t write =
            sample_telemetry_write_.load(std::memory_order_acquire);
        status =
            {
                sizeof(yokko_audio_sample_telemetry_status),
                YOKKO_AUDIO_SAMPLE_TELEMETRY_ABI_VERSION,
                sample_telemetry_capacity,
                static_cast<uint32_t>(
                    std::min<uint64_t>(
                        write - read,
                        sample_telemetry_capacity)),
                sample_telemetry_dropped_.load(std::memory_order_acquire),
            };
    }

    void AudioEngine::mix_active_samples(
        float* output,
        const uint32_t frame_count) noexcept
    {
        for (SampleVoice& voice : sample_voices_)
        {
            if (voice.sample_id == 0 || voice.sample_id > sample_bank_.size())
                continue;

            if (voice.trace_id != 0)
            {
                publish_sample_telemetry(
                    {
                        SampleTriggerAction::play,
                        voice.sample_id,
                        voice.gain,
                        voice.loop_id,
                        voice.trace_id,
                        voice.capture_time_100ns,
                        voice.enqueue_time_100ns,
                    },
                    voice.callback_time_100ns,
                    device_frames_rendered_.load(
                        std::memory_order_relaxed));
                voice.trace_id = 0;
                voice.capture_time_100ns = 0;
                voice.enqueue_time_100ns = 0;
                voice.callback_time_100ns = 0;
            }

            const RegisteredSample& sample =
                sample_bank_[voice.sample_id - 1];
            const float volume = sample.metronome
                ? metronome_volume_.load(std::memory_order_acquire)
                : (voice.music
                       ? music_volume_.load(std::memory_order_acquire)
                       : hit_sound_volume_.load(std::memory_order_acquire))
                  * voice.gain;
            const double playback_rate = sample.metronome
                ? 1.0
                : (voice.music
                       ? music_sample_playback_rate_.load(
                           std::memory_order_acquire)
                       : sample_playback_rate_.load(
                           std::memory_order_acquire));

            for (uint32_t output_frame = 0;
                 output_frame < frame_count;
                 ++output_frame)
            {
                if (voice.frame_position >= sample.frame_count)
                {
                    if (!voice.looping || sample.frame_count == 0)
                        break;
                    voice.frame_position = std::fmod(
                        voice.frame_position,
                        static_cast<double>(sample.frame_count));
                }

                const uint32_t source_frame =
                    static_cast<uint32_t>(voice.frame_position);
                const uint32_t next_frame = std::min(
                    source_frame + 1,
                    sample.frame_count - 1);
                const float fraction = static_cast<float>(
                    voice.frame_position - source_frame);
                for (uint32_t channel = 0; channel < channels_; ++channel)
                {
                    const float first =
                        sample.pcm[
                            static_cast<size_t>(source_frame) * channels_
                            + channel];
                    const float second =
                        sample.pcm[
                            static_cast<size_t>(next_frame) * channels_
                            + channel];
                    output[
                        static_cast<size_t>(output_frame) * channels_
                        + channel] +=
                        (first + (second - first) * fraction) * volume;
                }
                voice.frame_position += playback_rate;
            }

            if (voice.frame_position >= sample.frame_count
                && !voice.looping)
                voice = {};
        }
    }

    void AudioEngine::reset_sample_playback() noexcept
    {
        sample_trigger_dequeue_.store(0, std::memory_order_relaxed);
        sample_trigger_enqueue_.store(0, std::memory_order_relaxed);
        for (uint64_t index = 0; index < sample_trigger_capacity; ++index)
        {
            sample_trigger_queue_[index].trigger = {};
            sample_trigger_queue_[index].sequence.store(
                index,
                std::memory_order_relaxed);
        }
        for (SampleVoice& voice : sample_voices_)
            voice = {};
        next_sample_voice_ = 0;

    }

    yokko_audio_result AudioEngine::report_presented_position(
        const uint64_t presented_frame_position,
        const uint32_t output_latency_frames,
        const uint64_t observation_time_100ns) noexcept
    {
        const bool already_reported =
            has_reported_presented_position_.load(std::memory_order_acquire);
        const uint64_t previous =
            reported_presented_frame_position_.load(std::memory_order_acquire);

        if (already_reported && presented_frame_position < previous)
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        reported_position_sequence_.fetch_add(1, std::memory_order_acq_rel);
        reported_position_time_100ns_.store(
            observation_time_100ns,
            std::memory_order_relaxed);
        reported_presented_frame_position_.store(
            presented_frame_position,
            std::memory_order_relaxed);
        reported_position_sequence_.fetch_add(1, std::memory_order_release);
        device_latency_frames_.store(
            output_latency_frames,
            std::memory_order_release);
        has_reported_presented_position_.store(true, std::memory_order_release);
        return YOKKO_AUDIO_OK;
    }

    void AudioEngine::report_callback_timing(
        const uint32_t duration_microseconds,
        const uint32_t budget_microseconds,
        const uint32_t interval_microseconds) noexcept
    {
        callback_count_.fetch_add(1, std::memory_order_relaxed);
        callback_budget_microseconds_.store(
            budget_microseconds,
            std::memory_order_relaxed);
        if (budget_microseconds > 0
            && duration_microseconds >= budget_microseconds)
            callback_deadline_miss_count_.fetch_add(
                1,
                std::memory_order_relaxed);

        uint32_t previous =
            callback_max_duration_microseconds_.load(std::memory_order_relaxed);
        while (duration_microseconds > previous
               && !callback_max_duration_microseconds_.compare_exchange_weak(
                   previous,
                   duration_microseconds,
                   std::memory_order_relaxed,
                   std::memory_order_relaxed))
        {
        }

        if (interval_microseconds == 0)
            return;

        const uint64_t cadence_limit =
            static_cast<uint64_t>(budget_microseconds) * 3 / 2;
        if (budget_microseconds > 0
            && interval_microseconds >= cadence_limit)
        {
            callback_cadence_miss_count_.fetch_add(
                1,
                std::memory_order_relaxed);
        }

        previous =
            callback_max_interval_microseconds_.load(std::memory_order_relaxed);
        while (interval_microseconds > previous
               && !callback_max_interval_microseconds_.compare_exchange_weak(
                   previous,
                   interval_microseconds,
                   std::memory_order_relaxed,
                   std::memory_order_relaxed))
        {
        }
    }

    void AudioEngine::report_callback_overload() noexcept
    {
        backend_overload_count_.fetch_add(
            1,
            std::memory_order_relaxed);
    }

    void AudioEngine::report_output_failure(
        const int32_t backend_error,
        const uint32_t backend_error_stage) noexcept
    {
        backend_error_.store(backend_error, std::memory_order_release);
        backend_error_stage_.store(
            backend_error_stage,
            std::memory_order_release);
        state_.store(YOKKO_AUDIO_STATE_FAULTED, std::memory_order_release);
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
        status.callback_count =
            callback_count_.load(std::memory_order_acquire);
        status.callback_deadline_miss_count =
            callback_deadline_miss_count_.load(std::memory_order_acquire);
        status.callback_budget_microseconds =
            callback_budget_microseconds_.load(std::memory_order_acquire);
        status.callback_max_duration_microseconds =
            callback_max_duration_microseconds_.load(std::memory_order_acquire);
        status.backend_error =
            backend_error_.load(std::memory_order_acquire);
        status.backend_error_stage =
            backend_error_stage_.load(std::memory_order_acquire);
        status.callback_cadence_miss_count =
            callback_cadence_miss_count_.load(std::memory_order_acquire);
        status.callback_max_interval_microseconds =
            callback_max_interval_microseconds_.load(std::memory_order_acquire);
        status.backend_overload_count =
            backend_overload_count_.load(std::memory_order_acquire);
        status.has_presented_position =
            has_reported_presented_position_.load(std::memory_order_acquire)
                ? 1u
                : 0u;
        if (status.has_presented_position != 0)
        {
            uint32_t sequence_before = 0;
            uint32_t sequence_after = 0;
            do
            {
                sequence_before =
                    reported_position_sequence_.load(
                        std::memory_order_acquire);
                if ((sequence_before & 1u) != 0)
                    continue;

                status.presented_frame_position =
                    reported_presented_frame_position_.load(
                        std::memory_order_relaxed);
                status.position_observation_time_100ns =
                    reported_position_time_100ns_.load(
                        std::memory_order_relaxed);
                sequence_after =
                    reported_position_sequence_.load(
                        std::memory_order_acquire);
            }
            while (sequence_before != sequence_after
                   || (sequence_before & 1u) != 0);
        }
        status.playback_time_milliseconds = playback_time_milliseconds(
            status.has_presented_position != 0,
            status.presented_frame_position,
            status.position_observation_time_100ns);
    }

    yokko_audio_result AudioEngine::open_wasapi(
        const yokko_audio_output_config& config,
        yokko_audio_output_status& status) noexcept
    {
        if (state_.load(std::memory_order_acquire) != YOKKO_AUDIO_STATE_RUNNING)
            return YOKKO_AUDIO_NOT_READY;

        close_output();
        try
        {
            output_ = std::make_unique<WasapiOutput>(*this);
            const yokko_audio_result result = output_->open(config, status);
            if (result != YOKKO_AUDIO_OK)
                output_.reset();
            return result;
        }
        catch (const std::bad_alloc&)
        {
            output_.reset();
            return YOKKO_AUDIO_OUT_OF_MEMORY;
        }
        catch (...)
        {
            output_.reset();
            return YOKKO_AUDIO_INTERNAL_ERROR;
        }
    }

    yokko_audio_result AudioEngine::open_asio(
        const yokko_audio_output_config& config,
        yokko_audio_output_status& status) noexcept
    {
        if (state_.load(std::memory_order_acquire) != YOKKO_AUDIO_STATE_RUNNING)
            return YOKKO_AUDIO_NOT_READY;

        close_output();
        try
        {
            asio_output_ = std::make_unique<AsioOutput>(*this);
            const yokko_audio_result result =
                asio_output_->open(config, status);
            if (result != YOKKO_AUDIO_OK)
                asio_output_.reset();
            return result;
        }
        catch (const std::bad_alloc&)
        {
            asio_output_.reset();
            return YOKKO_AUDIO_OUT_OF_MEMORY;
        }
        catch (...)
        {
            asio_output_.reset();
            return YOKKO_AUDIO_INTERNAL_ERROR;
        }
    }

    void AudioEngine::close_output() noexcept
    {
        if (asio_output_ != nullptr)
        {
            asio_output_->close();
            asio_output_.reset();
        }
        if (output_ != nullptr)
        {
            output_->close();
            output_.reset();
        }
    }

    double AudioEngine::playback_time_milliseconds(
        const bool has_presented_position,
        const uint64_t reported_presented_position,
        const uint64_t observation_time) const noexcept
    {
        // Frames submitted to the endpoint are not necessarily audible yet.
        // Keep the public clock at the stream origin until IAudioClock reports
        // the first device-presented position.
        uint64_t presented_position = 0;

        if (has_presented_position)
        {
            presented_position = reported_presented_position;

            const uint64_t now = monotonic_time_100ns();

            if (state_.load(std::memory_order_acquire)
                    == YOKKO_AUDIO_STATE_RUNNING
                && observation_time > 0
                && now > observation_time)
            {
                const uint64_t elapsed_100ns = now - observation_time;
                const uint64_t interpolated =
                    presented_position
                    + static_cast<uint64_t>(
                        static_cast<long double>(elapsed_100ns) * sample_rate_
                        / 10'000'000.0L);
                const uint64_t submitted_to_device =
                    device_frames_rendered_.load(std::memory_order_acquire);
                presented_position =
                    submitted_to_device >= presented_position
                        ? std::min(interpolated, submitted_to_device)
                        : interpolated;
            }
        }

        uint64_t previous =
            last_playback_frame_position_.load(std::memory_order_relaxed);
        while (presented_position > previous
               && !last_playback_frame_position_.compare_exchange_weak(
                   previous,
                   presented_position,
                   std::memory_order_relaxed,
                   std::memory_order_relaxed))
        {
        }
        presented_position = std::max(presented_position, previous);

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

    uint32_t YOKKO_AUDIO_CALL
        yokko_audio_get_sample_telemetry_abi_version(void)
    {
        return YOKKO_AUDIO_SAMPLE_TELEMETRY_ABI_VERSION;
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

    yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_register_sample_f32(
        yokko_audio_engine* engine,
        const float* samples,
        const uint32_t frame_count,
        uint32_t* sample_id)
    {
        if (engine == nullptr || sample_id == nullptr)
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        return engine->implementation.register_sample(
            samples,
            frame_count,
            *sample_id);
    }

    yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_register_metronome_sample_f32(
        yokko_audio_engine* engine,
        const float* samples,
        const uint32_t frame_count,
        uint32_t* sample_id)
    {
        if (engine == nullptr || sample_id == nullptr)
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        return engine->implementation.register_sample(
            samples,
            frame_count,
            *sample_id,
            true);
    }

    yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_set_mix_volumes(
        yokko_audio_engine* engine,
        const float music_volume,
        const float hit_sound_volume,
        const float metronome_volume)
    {
        return engine == nullptr
                   ? YOKKO_AUDIO_INVALID_ARGUMENT
                   : engine->implementation.set_mix_volumes(
                       music_volume,
                       hit_sound_volume,
                       metronome_volume);
    }

    yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_set_sample_playback_rate(
        yokko_audio_engine* engine,
        const float playback_rate)
    {
        return engine == nullptr
                   ? YOKKO_AUDIO_INVALID_ARGUMENT
                   : engine->implementation.set_sample_playback_rate(
                       playback_rate);
    }

    yokko_audio_result YOKKO_AUDIO_CALL
        yokko_audio_set_music_sample_playback_rate(
            yokko_audio_engine* engine,
            const float playback_rate)
    {
        return engine == nullptr
                   ? YOKKO_AUDIO_INVALID_ARGUMENT
                   : engine->implementation.set_music_sample_playback_rate(
                       playback_rate);
    }

    yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_trigger_sample(
        yokko_audio_engine* engine,
        const uint32_t sample_id)
    {
        return engine == nullptr
                   ? YOKKO_AUDIO_INVALID_ARGUMENT
                   : engine->implementation.trigger_sample(sample_id);
    }

    yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_trigger_sample_with_gain(
        yokko_audio_engine* engine,
        const uint32_t sample_id,
        const float gain)
    {
        return engine == nullptr
                   ? YOKKO_AUDIO_INVALID_ARGUMENT
                   : engine->implementation.trigger_sample(sample_id, gain);
    }

    yokko_audio_result YOKKO_AUDIO_CALL
        yokko_audio_trigger_music_sample_with_gain(
            yokko_audio_engine* engine,
            const uint32_t sample_id,
            const float gain)
    {
        return engine == nullptr
                   ? YOKKO_AUDIO_INVALID_ARGUMENT
                   : engine->implementation.trigger_music_sample(
                       sample_id,
                       gain);
    }

    yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_trigger_sample_traced(
        yokko_audio_engine* engine,
        const uint32_t sample_id,
        const float gain,
        const uint64_t capture_timestamp,
        const uint64_t timestamp_frequency,
        uint64_t* trace_id)
    {
        if (engine == nullptr || trace_id == nullptr)
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        return engine->implementation.trigger_sample_traced(
            sample_id,
            gain,
            capture_timestamp,
            timestamp_frequency,
            *trace_id);
    }

    yokko_audio_result YOKKO_AUDIO_CALL
        yokko_audio_try_dequeue_sample_telemetry(
            yokko_audio_engine* engine,
            yokko_audio_sample_trigger_telemetry* telemetry)
    {
        if (engine == nullptr || telemetry == nullptr)
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        return engine->implementation.try_dequeue_sample_telemetry(
            *telemetry);
    }

    yokko_audio_result YOKKO_AUDIO_CALL
        yokko_audio_get_sample_telemetry_status(
            const yokko_audio_engine* engine,
            yokko_audio_sample_telemetry_status* status)
    {
        if (engine == nullptr || status == nullptr
            || status->struct_size
                   < sizeof(yokko_audio_sample_telemetry_status))
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        engine->implementation.get_sample_telemetry_status(*status);
        return YOKKO_AUDIO_OK;
    }

    yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_start_looping_sample(
        yokko_audio_engine* engine,
        const uint32_t sample_id,
        const float gain,
        uint32_t* loop_id)
    {
        if (engine == nullptr || loop_id == nullptr)
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        return engine->implementation.start_looping_sample(
            sample_id,
            gain,
            *loop_id);
    }

    yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_stop_looping_sample(
        yokko_audio_engine* engine,
        const uint32_t loop_id)
    {
        return engine == nullptr
                   ? YOKKO_AUDIO_INVALID_ARGUMENT
                   : engine->implementation.stop_looping_sample(loop_id);
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

    yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_report_presented_position(
        yokko_audio_engine* engine,
        const uint64_t presented_frame_position,
        const uint32_t output_latency_frames,
        const uint64_t observation_time_100ns)
    {
        return engine == nullptr
                   ? YOKKO_AUDIO_INVALID_ARGUMENT
                   : engine->implementation.report_presented_position(
                       presented_frame_position,
                       output_latency_frames,
                       observation_time_100ns);
    }

    yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_report_callback_timing(
        yokko_audio_engine* engine,
        const uint32_t duration_microseconds,
        const uint32_t budget_microseconds,
        const uint32_t interval_microseconds)
    {
        if (engine == nullptr)
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        engine->implementation.report_callback_timing(
            duration_microseconds,
            budget_microseconds,
            interval_microseconds);
        return YOKKO_AUDIO_OK;
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

    yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_open_wasapi(
        yokko_audio_engine* engine,
        const yokko_audio_output_config* config,
        yokko_audio_output_status* status)
    {
        if (engine == nullptr || config == nullptr || status == nullptr)
            return YOKKO_AUDIO_INVALID_ARGUMENT;
        if (config->struct_size < sizeof(yokko_audio_output_config)
            || status->struct_size < sizeof(yokko_audio_output_status))
            return YOKKO_AUDIO_INVALID_ARGUMENT;
        if (config->backend != YOKKO_AUDIO_BACKEND_WASAPI_SHARED
            && config->backend != YOKKO_AUDIO_BACKEND_WASAPI_EXCLUSIVE)
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        return engine->implementation.open_wasapi(*config, *status);
    }

    yokko_audio_result YOKKO_AUDIO_CALL yokko_audio_open_asio(
        yokko_audio_engine* engine,
        const yokko_audio_output_config* config,
        yokko_audio_output_status* status)
    {
        if (engine == nullptr || config == nullptr || status == nullptr)
            return YOKKO_AUDIO_INVALID_ARGUMENT;
        if (config->struct_size < sizeof(yokko_audio_output_config)
            || status->struct_size < sizeof(yokko_audio_output_status))
            return YOKKO_AUDIO_INVALID_ARGUMENT;
        if (config->backend != YOKKO_AUDIO_BACKEND_ASIO)
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        return engine->implementation.open_asio(*config, *status);
    }

    void YOKKO_AUDIO_CALL yokko_audio_close_output(yokko_audio_engine* engine)
    {
        if (engine != nullptr)
            engine->implementation.close_output();
    }
}
