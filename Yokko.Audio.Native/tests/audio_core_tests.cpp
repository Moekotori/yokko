#include "yokko_audio.h"
#include "spsc_pcm_ring_buffer.hpp"

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <iostream>
#include <limits>
#include <string_view>
#include <thread>
#include <vector>

namespace
{
    class EngineHandle
    {
    public:
        explicit EngineHandle(
            const uint32_t capacity_frames = 8,
            const uint32_t startup_frames = 4)
        {
            const yokko_audio_config config{
                sizeof(yokko_audio_config),
                48000,
                2,
                capacity_frames,
                startup_frames,
            };

            require(
                yokko_audio_create(&config, &value_) == YOKKO_AUDIO_OK,
                "engine creation");
        }

        ~EngineHandle()
        {
            yokko_audio_destroy(value_);
        }

        EngineHandle(const EngineHandle&) = delete;
        EngineHandle& operator=(const EngineHandle&) = delete;

        operator yokko_audio_engine*() const
        {
            return value_;
        }

    private:
        static void require(const bool condition, const std::string_view message)
        {
            if (!condition)
            {
                std::cerr << "FAILED: " << message << '\n';
                std::exit(1);
            }
        }

        yokko_audio_engine* value_{};
    };

    void require(const bool condition, const std::string_view message)
    {
        if (!condition)
        {
            std::cerr << "FAILED: " << message << '\n';
            std::exit(1);
        }
    }

    yokko_audio_status status_of(yokko_audio_engine* engine)
    {
        yokko_audio_status status{};
        status.struct_size = sizeof(status);
        require(
            yokko_audio_get_status(engine, &status) == YOKKO_AUDIO_OK,
            "status query");
        return status;
    }

    void test_abi_and_validation()
    {
        require(
            yokko_audio_get_abi_version() == YOKKO_AUDIO_ABI_VERSION,
            "ABI version");

        yokko_audio_engine* engine = nullptr;
        yokko_audio_config invalid{};
        invalid.struct_size = sizeof(invalid);
        require(
            yokko_audio_create(&invalid, &engine)
                == YOKKO_AUDIO_INVALID_ARGUMENT,
            "invalid config rejected");
        require(engine == nullptr, "invalid config leaves null handle");
    }

    void test_start_requires_priming()
    {
        EngineHandle engine;
        require(
            status_of(engine).state == YOKKO_AUDIO_STATE_IDLE,
            "new engine is idle");
        require(
            yokko_audio_start(engine) == YOKKO_AUDIO_NOT_READY,
            "unprimed start rejected");

        const std::vector<float> samples(8, 0.25f);
        uint32_t accepted = 0;
        require(
            yokko_audio_submit_interleaved_f32(
                engine,
                samples.data(),
                4,
                &accepted)
                == YOKKO_AUDIO_OK,
            "prime submit");
        require(accepted == 4, "all prime frames accepted");
        require(
            status_of(engine).state == YOKKO_AUDIO_STATE_PRIMED,
            "threshold primes engine");
        require(
            yokko_audio_start(engine) == YOKKO_AUDIO_OK,
            "primed engine starts");
    }

    void test_render_and_underrun()
    {
        EngineHandle engine;
        const std::vector<float> samples{
            0.1f,
            -0.1f,
            0.2f,
            -0.2f,
            0.3f,
            -0.3f,
            0.4f,
            -0.4f,
        };

        uint32_t accepted = 0;
        require(
            yokko_audio_submit_interleaved_f32(
                engine,
                samples.data(),
                4,
                &accepted)
                == YOKKO_AUDIO_OK,
            "render submit");
        require(yokko_audio_start(engine) == YOKKO_AUDIO_OK, "render start");

        std::vector<float> output(12, 99.0f);
        uint32_t rendered = 0;
        require(
            yokko_audio_render_interleaved_f32(
                engine,
                output.data(),
                6,
                &rendered)
                == YOKKO_AUDIO_OK,
            "render callback");
        require(rendered == 4, "render reports source frames");

        for (size_t index = 0; index < samples.size(); ++index)
            require(
                std::abs(output[index] - samples[index]) < 0.00001f,
                "render preserves samples");
        for (size_t index = samples.size(); index < output.size(); ++index)
            require(output[index] == 0, "underrun fills silence");

        const yokko_audio_status status = status_of(engine);
        require(status.source_frames_rendered == 4, "source frame counter");
        require(status.device_frames_rendered == 6, "device frame counter");
        require(status.underrun_count == 1, "underrun counter");
        require(
            status.playback_time_milliseconds == 0,
            "submitted endpoint frames are not treated as presented");
    }

    void test_keysounds_mix_on_the_next_callback()
    {
        EngineHandle engine;
        const std::vector<float> music(8, 0.25f);
        const std::vector<float> keysound{
            0.5f,
            -0.5f,
            0.75f,
            -0.75f,
        };

        uint32_t sample_id = 0;
        require(
            yokko_audio_register_sample_f32(
                engine,
                keysound.data(),
                2,
                &sample_id)
                == YOKKO_AUDIO_OK,
            "keysound registration");
        require(sample_id != 0, "keysound id assigned");
        require(
            yokko_audio_trigger_sample(engine, sample_id)
                == YOKKO_AUDIO_NOT_READY,
            "keysound cannot queue before playback");

        uint32_t accepted = 0;
        require(
            yokko_audio_submit_interleaved_f32(
                engine,
                music.data(),
                4,
                &accepted)
                == YOKKO_AUDIO_OK,
            "keysound music submit");
        require(yokko_audio_start(engine) == YOKKO_AUDIO_OK, "keysound start");
        require(
            yokko_audio_trigger_sample(engine, sample_id) == YOKKO_AUDIO_OK,
            "keysound trigger");

        std::vector<float> output(8);
        uint32_t rendered = 0;
        require(
            yokko_audio_render_interleaved_f32(
                engine,
                output.data(),
                4,
                &rendered)
                == YOKKO_AUDIO_OK,
            "keysound render");

        require(rendered == 4, "keysound does not alter music clock");
        require(std::abs(output[0] - 0.75f) < 0.00001f, "keysound left mixed");
        require(std::abs(output[1] + 0.25f) < 0.00001f, "keysound right mixed");
        require(output[2] == 1.0f, "keysound mix clamps positive peak");
        require(std::abs(output[3] + 0.5f) < 0.00001f, "keysound second frame mixed");
        require(output[4] == 0.25f && output[7] == 0.25f, "keysound ends cleanly");
    }

    void test_traced_keysound_reports_callback_and_output_frame()
    {
        EngineHandle engine;
        const std::vector<float> sample{0.25f, -0.25f};
        uint32_t sample_id = 0;
        require(
            yokko_audio_register_sample_f32(
                engine,
                sample.data(),
                1,
                &sample_id) == YOKKO_AUDIO_OK,
            "traced sample registration");

        const std::vector<float> music(8, 0.0f);
        uint32_t accepted = 0;
        require(
            yokko_audio_submit_interleaved_f32(
                engine,
                music.data(),
                4,
                &accepted) == YOKKO_AUDIO_OK,
            "traced sample prime");
        require(yokko_audio_start(engine) == YOKKO_AUDIO_OK, "traced start");
        require(
            yokko_audio_report_presented_position(
                engine,
                0,
                96,
                0) == YOKKO_AUDIO_OK,
            "traced output latency");

        uint64_t trace_id = 0;
        require(
            yokko_audio_trigger_sample_traced(
                engine,
                sample_id,
                1.0f,
                1,
                1,
                &trace_id) == YOKKO_AUDIO_OK,
            "traced trigger");
        require(trace_id != 0, "traced trigger id");

        float output[2]{};
        uint32_t rendered = 0;
        require(
            yokko_audio_render_interleaved_f32(
                engine,
                output,
                1,
                &rendered) == YOKKO_AUDIO_OK,
            "traced render");

        yokko_audio_sample_trigger_telemetry telemetry{};
        telemetry.struct_size = sizeof(telemetry);
        require(
            yokko_audio_try_dequeue_sample_telemetry(
                engine,
                &telemetry) == YOKKO_AUDIO_OK,
            "traced telemetry dequeue");
        require(telemetry.trace_id == trace_id, "traced telemetry id");
        require(telemetry.sample_id == sample_id, "traced telemetry sample");
        require(telemetry.sample_rate == 48000, "traced sample rate");
        require(
            telemetry.estimated_output_latency_frames == 96,
            "traced output latency frames");
        require(
            telemetry.enqueue_time_100ns >= telemetry.capture_time_100ns,
            "traced enqueue follows capture");
        require(
            telemetry.callback_time_100ns >= telemetry.enqueue_time_100ns,
            "traced callback follows enqueue");
        require(
            telemetry.first_output_frame_position == 0,
            "traced first output frame");
    }

    void test_sample_trigger_queue_accepts_multiple_producers()
    {
        constexpr uint32_t producer_count = 4;
        constexpr uint32_t triggers_per_producer = 100;
        constexpr uint32_t trigger_count =
            producer_count * triggers_per_producer;

        EngineHandle engine(512, 1);
        const std::vector<float> sample{0.1f, -0.1f};
        uint32_t sample_id = 0;
        require(
            yokko_audio_register_sample_f32(
                engine,
                sample.data(),
                1,
                &sample_id) == YOKKO_AUDIO_OK,
            "MPSC sample registration");

        const std::vector<float> music(1024, 0.0f);
        uint32_t accepted = 0;
        require(
            yokko_audio_submit_interleaved_f32(
                engine,
                music.data(),
                512,
                &accepted) == YOKKO_AUDIO_OK,
            "MPSC sample prime");
        require(yokko_audio_start(engine) == YOKKO_AUDIO_OK, "MPSC start");

        std::atomic<bool> keep_rendering{true};
        std::thread renderer([&]
        {
            float output[2]{};
            while (keep_rendering.load(std::memory_order_acquire))
            {
                uint32_t rendered = 0;
                if (yokko_audio_render_interleaved_f32(
                        engine,
                        output,
                        1,
                        &rendered) != YOKKO_AUDIO_OK)
                    std::abort();
            }
        });

        std::vector<uint64_t> trace_ids(trigger_count);
        std::vector<std::thread> producers;
        producers.reserve(producer_count);
        for (uint32_t producer = 0; producer < producer_count; ++producer)
        {
            producers.emplace_back([&, producer]
            {
                for (uint32_t index = 0;
                     index < triggers_per_producer;
                     ++index)
                {
                    uint64_t trace_id = 0;
                    yokko_audio_result result{};
                    uint32_t attempts = 0;
                    do
                    {
                        result = yokko_audio_trigger_sample_traced(
                            engine,
                            sample_id,
                            1.0f,
                            1,
                            1,
                            &trace_id);
                        if (result == YOKKO_AUDIO_QUEUE_FULL)
                            std::this_thread::yield();
                        attempts++;
                    }
                    while (result == YOKKO_AUDIO_QUEUE_FULL
                           && attempts < 1'000'000);
                    if (result != YOKKO_AUDIO_OK || trace_id == 0)
                        std::abort();
                    trace_ids[
                        producer * triggers_per_producer + index] = trace_id;
                }
            });
        }

        for (std::thread& producer : producers)
            producer.join();

        yokko_audio_sample_telemetry_status telemetry_status{};
        telemetry_status.struct_size = sizeof(telemetry_status);
        uint32_t status_attempts = 0;
        do
        {
            require(
                yokko_audio_get_sample_telemetry_status(
                    engine,
                    &telemetry_status) == YOKKO_AUDIO_OK,
                "MPSC telemetry status");
            if (telemetry_status.pending_count < trigger_count)
                std::this_thread::yield();
            status_attempts++;
        }
        while (telemetry_status.pending_count < trigger_count
               && status_attempts < 1'000'000);

        keep_rendering.store(false, std::memory_order_release);
        renderer.join();
        require(
            telemetry_status.pending_count == trigger_count,
            "MPSC telemetry completes");
        require(telemetry_status.dropped_count == 0, "MPSC telemetry retained");

        for (uint32_t index = 0; index < trigger_count; ++index)
        {
            yokko_audio_sample_trigger_telemetry telemetry{};
            telemetry.struct_size = sizeof(telemetry);
            require(
                yokko_audio_try_dequeue_sample_telemetry(
                    engine,
                    &telemetry) == YOKKO_AUDIO_OK,
                "MPSC telemetry dequeue");
            require(telemetry.trace_id != 0, "MPSC telemetry trace id");
        }

        std::sort(trace_ids.begin(), trace_ids.end());
        require(
            std::adjacent_find(trace_ids.begin(), trace_ids.end())
                == trace_ids.end(),
            "MPSC trace ids are unique");
    }

    void test_mix_buses_apply_independent_gains()
    {
        EngineHandle engine;
        const std::vector<float> music(8, 0.4f);
        const std::vector<float> sound(4, 0.4f);
        uint32_t keysound_id = 0;
        uint32_t metronome_id = 0;
        require(
            yokko_audio_register_sample_f32(
                engine, sound.data(), 2, &keysound_id) == YOKKO_AUDIO_OK,
            "mix bus keysound registration");
        require(
            yokko_audio_register_metronome_sample_f32(
                engine, sound.data(), 2, &metronome_id) == YOKKO_AUDIO_OK,
            "mix bus metronome registration");
        require(
            yokko_audio_set_mix_volumes(engine, 0.5f, 0.25f, 0.75f)
                == YOKKO_AUDIO_OK,
            "mix bus gains");

        uint32_t accepted = 0;
        require(
            yokko_audio_submit_interleaved_f32(
                engine, music.data(), 4, &accepted) == YOKKO_AUDIO_OK,
            "mix bus submit");
        require(yokko_audio_start(engine) == YOKKO_AUDIO_OK, "mix bus start");
        require(
            yokko_audio_trigger_sample_with_gain(
                engine,
                keysound_id,
                0.5f) == YOKKO_AUDIO_OK,
            "mix bus gained keysound trigger");
        require(
            yokko_audio_trigger_sample(engine, metronome_id) == YOKKO_AUDIO_OK,
            "mix bus metronome trigger");

        std::vector<float> output(8);
        uint32_t rendered = 0;
        require(
            yokko_audio_render_interleaved_f32(
                engine, output.data(), 4, &rendered) == YOKKO_AUDIO_OK,
            "mix bus render");
        require(
            std::abs(output[0] - 0.55f) < 0.00001f,
            "music, per-trigger, keysound and metronome gains are independent");
        require(
            std::abs(output[4] - 0.2f) < 0.00001f,
            "music gain continues after samples end");
        require(
            yokko_audio_set_mix_volumes(engine, -0.1f, 1.0f, 1.0f)
                == YOKKO_AUDIO_INVALID_ARGUMENT,
            "invalid mix gain rejected");
        require(
            yokko_audio_trigger_sample_with_gain(
                engine,
                keysound_id,
                1.1f) == YOKKO_AUDIO_INVALID_ARGUMENT,
            "invalid per-trigger gain rejected");
    }

    void test_keysound_playback_rate_is_callback_side()
    {
        EngineHandle engine;
        const std::vector<float> music(8, 0.0f);
        const std::vector<float> keysound{
            0.1f, 0.1f,
            0.2f, 0.2f,
            0.3f, 0.3f,
            0.4f, 0.4f,
        };
        uint32_t sample_id = 0;
        require(
            yokko_audio_register_sample_f32(
                engine, keysound.data(), 4, &sample_id) == YOKKO_AUDIO_OK,
            "rated keysound registration");
        require(
            yokko_audio_set_sample_playback_rate(engine, 2.0f)
                == YOKKO_AUDIO_OK,
            "rated keysound speed");

        uint32_t accepted = 0;
        require(
            yokko_audio_submit_interleaved_f32(
                engine, music.data(), 4, &accepted) == YOKKO_AUDIO_OK,
            "rated keysound submit");
        require(yokko_audio_start(engine) == YOKKO_AUDIO_OK, "rated keysound start");
        require(
            yokko_audio_trigger_sample(engine, sample_id) == YOKKO_AUDIO_OK,
            "rated keysound trigger");

        std::vector<float> output(8);
        uint32_t rendered = 0;
        require(
            yokko_audio_render_interleaved_f32(
                engine, output.data(), 4, &rendered) == YOKKO_AUDIO_OK,
            "rated keysound render");
        require(
            std::abs(output[0] - 0.1f) < 0.00001f
            && std::abs(output[2] - 0.3f) < 0.00001f,
            "rated keysound advances source frames");
        require(
            output[4] == 0 && output[6] == 0,
            "rated keysound ends in half the output frames");
        require(
            yokko_audio_set_sample_playback_rate(engine, 4.1f)
                == YOKKO_AUDIO_INVALID_ARGUMENT,
            "invalid keysound rate rejected");
    }

    void test_looping_keysound_starts_wraps_and_stops()
    {
        EngineHandle engine(16, 4);
        const std::vector<float> music(16, 0.0f);
        const std::vector<float> keysound{
            0.2f, 0.2f,
            0.4f, 0.4f,
        };
        uint32_t sample_id = 0;
        require(
            yokko_audio_register_sample_f32(
                engine, keysound.data(), 2, &sample_id) == YOKKO_AUDIO_OK,
            "looping keysound registration");

        uint32_t accepted = 0;
        require(
            yokko_audio_submit_interleaved_f32(
                engine, music.data(), 8, &accepted) == YOKKO_AUDIO_OK,
            "looping keysound prime");
        require(yokko_audio_start(engine) == YOKKO_AUDIO_OK, "looping start");

        uint32_t loop_id = 0;
        require(
            yokko_audio_start_looping_sample(
                engine, sample_id, 0.5f, &loop_id) == YOKKO_AUDIO_OK,
            "looping keysound trigger");
        require(loop_id != 0, "looping keysound handle");

        std::vector<float> output(8);
        uint32_t rendered = 0;
        require(
            yokko_audio_render_interleaved_f32(
                engine, output.data(), 4, &rendered) == YOKKO_AUDIO_OK,
            "first looping render");
        require(
            std::abs(output[0] - 0.1f) < 0.00001f
                && std::abs(output[2] - 0.2f) < 0.00001f
                && std::abs(output[4] - 0.1f) < 0.00001f,
            "looping sample wraps inside a callback");

        std::fill(output.begin(), output.end(), 0.0f);
        require(
            yokko_audio_render_interleaved_f32(
                engine, output.data(), 4, &rendered) == YOKKO_AUDIO_OK,
            "second looping render");
        require(
            std::abs(output[0] - 0.1f) < 0.00001f,
            "looping sample remains active across callbacks");

        require(
            yokko_audio_stop_looping_sample(engine, loop_id) == YOKKO_AUDIO_OK,
            "looping keysound stop");
        std::fill(output.begin(), output.end(), 0.0f);
        require(
            yokko_audio_render_interleaved_f32(
                engine, output.data(), 4, &rendered) == YOKKO_AUDIO_OK,
            "post-stop looping render");
        require(
            std::all_of(
                output.begin(),
                output.end(),
                [](const float sample)
                {
                    return std::abs(sample) < 0.00001f;
                }),
            "stopped looping sample is silent");

        require(
            yokko_audio_start_looping_sample(
                engine, sample_id, 1.1f, &loop_id)
                == YOKKO_AUDIO_INVALID_ARGUMENT,
            "invalid looping gain rejected");
        require(
            yokko_audio_stop_looping_sample(engine, 0)
                == YOKKO_AUDIO_INVALID_ARGUMENT,
            "zero looping handle rejected");
    }

    void test_pause_and_stop_are_deterministic()
    {
        EngineHandle engine;
        const std::vector<float> samples(12, 0.5f);
        uint32_t accepted = 0;
        require(
            yokko_audio_submit_interleaved_f32(
                engine,
                samples.data(),
                6,
                &accepted)
                == YOKKO_AUDIO_OK,
            "pause submit");
        require(yokko_audio_start(engine) == YOKKO_AUDIO_OK, "pause start");
        require(yokko_audio_pause(engine) == YOKKO_AUDIO_OK, "pause");

        std::vector<float> output(4, 99.0f);
        uint32_t rendered = 99;
        require(
            yokko_audio_render_interleaved_f32(
                engine,
                output.data(),
                2,
                &rendered)
                == YOKKO_AUDIO_OK,
            "paused render");
        require(rendered == 0, "paused render consumes nothing");
        require(output[0] == 0 && output[3] == 0, "paused render is silent");
        require(
            status_of(engine).device_frames_rendered == 0,
            "paused clock does not move");

        require(yokko_audio_start(engine) == YOKKO_AUDIO_OK, "resume");
        require(yokko_audio_stop(engine) == YOKKO_AUDIO_OK, "stop");
        require(yokko_audio_stop(engine) == YOKKO_AUDIO_OK, "idempotent stop");

        const yokko_audio_status status = status_of(engine);
        require(status.state == YOKKO_AUDIO_STATE_IDLE, "stop returns idle");
        require(status.buffered_frames == 0, "stop clears ring");
        require(status.submitted_frames == 0, "stop clears counters");
        require(status.playback_time_milliseconds == 0, "stop clears clock");
    }

    void test_output_safety()
    {
        EngineHandle engine(8, 2);
        const std::vector<float> samples{
            2.0f,
            -2.0f,
            std::numeric_limits<float>::quiet_NaN(),
            std::numeric_limits<float>::infinity(),
        };

        uint32_t accepted = 0;
        require(
            yokko_audio_submit_interleaved_f32(
                engine,
                samples.data(),
                2,
                &accepted)
                == YOKKO_AUDIO_OK,
            "safety submit");
        require(yokko_audio_start(engine) == YOKKO_AUDIO_OK, "safety start");

        std::vector<float> output(4);
        uint32_t rendered = 0;
        require(
            yokko_audio_render_interleaved_f32(
                engine,
                output.data(),
                2,
                &rendered)
                == YOKKO_AUDIO_OK,
            "safety render");
        require(output[0] == 1.0f, "positive sample clamped");
        require(output[1] == -1.0f, "negative sample clamped");
        require(output[2] == 0.0f, "NaN sample cleared");
        require(output[3] == 0.0f, "infinite sample cleared");
    }

    void test_hardware_clock_supersedes_callback_clock()
    {
        EngineHandle engine(8, 2);
        const std::vector<float> samples(4, 0.25f);
        uint32_t accepted = 0;
        require(
            yokko_audio_submit_interleaved_f32(
                engine,
                samples.data(),
                2,
                &accepted)
                == YOKKO_AUDIO_OK,
            "clock submit");
        require(yokko_audio_start(engine) == YOKKO_AUDIO_OK, "clock start");

        require(
            yokko_audio_report_presented_position(engine, 4800, 480, 0)
                == YOKKO_AUDIO_OK,
            "presented clock report");
        const yokko_audio_status status = status_of(engine);
        require(status.device_latency_frames == 480, "latency report");
        require(status.has_presented_position == 1, "presented position flag");
        require(
            status.presented_frame_position == 4800,
            "presented position correlation");
        require(
            status.position_observation_time_100ns == 0,
            "presented timestamp correlation");
        require(
            std::abs(status.playback_time_milliseconds - 100.0) < 0.000001,
            "presented hardware clock is not latency-adjusted twice");
        require(
            yokko_audio_report_presented_position(engine, 4799, 480, 0)
                == YOKKO_AUDIO_INVALID_ARGUMENT,
            "regressing presented clock rejected");
    }

    void test_callback_deadline_telemetry()
    {
        EngineHandle engine;
        require(
            yokko_audio_report_callback_timing(engine, 80, 100, 100)
                == YOKKO_AUDIO_OK,
            "callback timing report");
        require(
            yokko_audio_report_callback_timing(engine, 125, 100, 160)
                == YOKKO_AUDIO_OK,
            "callback deadline miss report");

        yokko_audio_status status = status_of(engine);
        require(status.callback_count == 2, "callback count");
        require(
            status.callback_deadline_miss_count == 1,
            "callback deadline miss count");
        require(
            status.callback_budget_microseconds == 100,
            "callback budget");
        require(
            status.callback_max_duration_microseconds == 125,
            "callback maximum duration");
        require(
            status.callback_cadence_miss_count == 1,
            "callback cadence miss count");
        require(
            status.backend_overload_count == 0,
            "callback cadence does not imply backend overload");
        require(
            status.callback_max_interval_microseconds == 160,
            "callback maximum interval");

        require(yokko_audio_stop(engine) == YOKKO_AUDIO_OK, "telemetry stop");
        status = status_of(engine);
        require(status.callback_count == 0, "stop clears callback count");
        require(
            status.callback_deadline_miss_count == 0,
            "stop clears callback deadline misses");
        require(
            status.callback_cadence_miss_count == 0,
            "stop clears callback cadence misses");
        require(
            status.backend_overload_count == 0,
            "stop clears backend overload count");
        require(
            status.callback_max_interval_microseconds == 0,
            "stop clears callback maximum interval");
    }

    void test_ring_buffer_is_safe_for_one_producer_and_consumer()
    {
        constexpr uint32_t frame_count = 100000;
        yokko::audio::SpscPcmRingBuffer ring(257, 2);
        std::atomic<bool> producer_finished{false};
        std::atomic<bool> failed{false};

        std::thread producer([&]
        {
            uint32_t next_frame = 0;
            while (next_frame < frame_count)
            {
                const float samples[]{
                    static_cast<float>(next_frame),
                    -static_cast<float>(next_frame),
                };

                if (ring.write(samples, 1) == 1)
                    ++next_frame;
                else
                    std::this_thread::yield();
            }

            producer_finished.store(true, std::memory_order_release);
        });

        uint32_t expected_frame = 0;
        while (expected_frame < frame_count)
        {
            float samples[2]{};
            if (ring.read(samples, 1) == 0)
            {
                if (producer_finished.load(std::memory_order_acquire)
                    && ring.available_frames() == 0)
                    break;

                std::this_thread::yield();
                continue;
            }

            if (samples[0] != static_cast<float>(expected_frame)
                || samples[1] != -static_cast<float>(expected_frame))
                failed.store(true, std::memory_order_release);
            ++expected_frame;
        }

        producer.join();
        require(!failed.load(std::memory_order_acquire), "SPSC frame order");
        require(expected_frame == frame_count, "SPSC transfers all frames");
        require(ring.available_frames() == 0, "SPSC drains fully");
    }

    void test_stop_can_race_with_output_callback()
    {
        EngineHandle engine(512, 1);
        const std::vector<float> samples(1024, 0.25f);
        uint32_t accepted = 0;
        require(
            yokko_audio_submit_interleaved_f32(
                engine,
                samples.data(),
                512,
                &accepted)
                == YOKKO_AUDIO_OK,
            "stop-race submit");
        require(
            yokko_audio_start(engine) == YOKKO_AUDIO_OK,
            "stop-race start");

        std::atomic<bool> keep_rendering{true};
        std::thread renderer([&]
        {
            float output[2]{};
            while (keep_rendering.load(std::memory_order_acquire))
            {
                uint32_t rendered = 0;
                if (yokko_audio_render_interleaved_f32(
                        engine,
                        output,
                        1,
                        &rendered)
                    != YOKKO_AUDIO_OK)
                    std::abort();
            }
        });

        require(yokko_audio_stop(engine) == YOKKO_AUDIO_OK, "racing stop");
        keep_rendering.store(false, std::memory_order_release);
        renderer.join();

        const yokko_audio_status status = status_of(engine);
        require(status.state == YOKKO_AUDIO_STATE_IDLE, "racing stop is idle");
        require(status.buffered_frames == 0, "racing stop clears ring");
        require(
            status.device_frames_rendered == 0,
            "racing stop clears callback clock");
    }
}

int main()
{
    test_abi_and_validation();
    test_start_requires_priming();
    test_render_and_underrun();
    test_keysounds_mix_on_the_next_callback();
    test_traced_keysound_reports_callback_and_output_frame();
    test_sample_trigger_queue_accepts_multiple_producers();
    test_mix_buses_apply_independent_gains();
    test_keysound_playback_rate_is_callback_side();
    test_looping_keysound_starts_wraps_and_stops();
    test_pause_and_stop_are_deterministic();
    test_output_safety();
    test_hardware_clock_supersedes_callback_clock();
    test_callback_deadline_telemetry();
    test_ring_buffer_is_safe_for_one_producer_and_consumer();
    test_stop_can_race_with_output_callback();

    std::cout << "Yokko native audio core tests passed.\n";
    return 0;
}
