#pragma once

#include <algorithm>
#include <atomic>
#include <cstddef>
#include <cstdint>
#include <vector>

namespace yokko::audio
{
    class SpscPcmRingBuffer
    {
    public:
        SpscPcmRingBuffer(const uint32_t capacity_frames, const uint32_t channels)
            : capacity_frames_(capacity_frames),
              channels_(channels),
              samples_(static_cast<size_t>(capacity_frames) * channels)
        {
        }

        [[nodiscard]] uint32_t capacity_frames() const noexcept
        {
            return capacity_frames_;
        }

        [[nodiscard]] uint32_t available_frames() const noexcept
        {
            const uint64_t write = write_frame_.load(std::memory_order_acquire);
            const uint64_t read = read_frame_.load(std::memory_order_acquire);
            return static_cast<uint32_t>(write - read);
        }

        uint32_t write(const float* input, const uint32_t frame_count) noexcept
        {
            const uint64_t write = write_frame_.load(std::memory_order_relaxed);
            const uint64_t read = read_frame_.load(std::memory_order_acquire);
            const uint32_t free_frames =
                capacity_frames_ - static_cast<uint32_t>(write - read);
            const uint32_t accepted = std::min(frame_count, free_frames);

            copy_into_ring(input, write, accepted);
            write_frame_.store(write + accepted, std::memory_order_release);
            return accepted;
        }

        uint32_t read(float* output, const uint32_t frame_count) noexcept
        {
            const uint64_t read = read_frame_.load(std::memory_order_relaxed);
            const uint64_t write = write_frame_.load(std::memory_order_acquire);
            const uint32_t available = static_cast<uint32_t>(write - read);
            const uint32_t consumed = std::min(frame_count, available);

            copy_from_ring(output, read, consumed);
            read_frame_.store(read + consumed, std::memory_order_release);
            return consumed;
        }

        void reset() noexcept
        {
            read_frame_.store(0, std::memory_order_release);
            write_frame_.store(0, std::memory_order_release);
        }

    private:
        void copy_into_ring(
            const float* input,
            const uint64_t start_frame,
            const uint32_t frame_count) noexcept
        {
            const uint32_t first_frame =
                static_cast<uint32_t>(start_frame % capacity_frames_);
            const uint32_t first_frame_count =
                std::min(frame_count, capacity_frames_ - first_frame);
            const size_t first_sample_count =
                static_cast<size_t>(first_frame_count) * channels_;

            std::copy_n(
                input,
                first_sample_count,
                samples_.data() + static_cast<size_t>(first_frame) * channels_);

            const uint32_t remaining_frames = frame_count - first_frame_count;
            if (remaining_frames > 0)
            {
                std::copy_n(
                    input + first_sample_count,
                    static_cast<size_t>(remaining_frames) * channels_,
                    samples_.data());
            }
        }

        void copy_from_ring(
            float* output,
            const uint64_t start_frame,
            const uint32_t frame_count) const noexcept
        {
            const uint32_t first_frame =
                static_cast<uint32_t>(start_frame % capacity_frames_);
            const uint32_t first_frame_count =
                std::min(frame_count, capacity_frames_ - first_frame);
            const size_t first_sample_count =
                static_cast<size_t>(first_frame_count) * channels_;

            std::copy_n(
                samples_.data() + static_cast<size_t>(first_frame) * channels_,
                first_sample_count,
                output);

            const uint32_t remaining_frames = frame_count - first_frame_count;
            if (remaining_frames > 0)
            {
                std::copy_n(
                    samples_.data(),
                    static_cast<size_t>(remaining_frames) * channels_,
                    output + first_sample_count);
            }
        }

        uint32_t capacity_frames_;
        uint32_t channels_;
        std::vector<float> samples_;
        alignas(64) std::atomic<uint64_t> read_frame_{0};
        alignas(64) std::atomic<uint64_t> write_frame_{0};
    };
}
