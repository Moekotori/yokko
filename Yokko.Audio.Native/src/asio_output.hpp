#pragma once

#include "yokko_audio.h"

#include <memory>

namespace yokko::audio
{
    class AudioEngine;

    class AsioOutput
    {
    public:
        explicit AsioOutput(AudioEngine& engine);
        ~AsioOutput();

        AsioOutput(const AsioOutput&) = delete;
        AsioOutput& operator=(const AsioOutput&) = delete;

        yokko_audio_result open(
            const yokko_audio_output_config& config,
            yokko_audio_output_status& status) noexcept;
        void close() noexcept;

    private:
        class Impl;
        std::unique_ptr<Impl> implementation_;
    };
}
