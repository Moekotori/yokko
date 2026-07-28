#pragma once

#include "yokko_audio.h"

#include <memory>

namespace yokko::audio
{
    class AudioEngine;

    class WasapiOutput
    {
    public:
        explicit WasapiOutput(AudioEngine& engine);
        ~WasapiOutput();

        WasapiOutput(const WasapiOutput&) = delete;
        WasapiOutput& operator=(const WasapiOutput&) = delete;

        yokko_audio_result open(
            const yokko_audio_output_config& config,
            yokko_audio_output_status& status) noexcept;
        void close() noexcept;

    private:
        class Impl;
        std::unique_ptr<Impl> implementation_;
    };
}
