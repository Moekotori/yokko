#include "yokko_audio.h"
#include "spsc_pcm_ring_buffer.hpp"

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
            std::abs(status.playback_time_milliseconds - 0.125) < 0.000001,
            "provisional frame clock");
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
    test_pause_and_stop_are_deterministic();
    test_output_safety();
    test_hardware_clock_supersedes_callback_clock();
    test_callback_deadline_telemetry();
    test_ring_buffer_is_safe_for_one_producer_and_consumer();
    test_stop_can_race_with_output_callback();

    std::cout << "Yokko native audio core tests passed.\n";
    return 0;
}
