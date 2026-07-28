#include "wasapi_output.hpp"

namespace yokko::audio
{
    class WasapiOutput::Impl
    {
    };

    WasapiOutput::WasapiOutput(AudioEngine&)
        : implementation_(std::make_unique<Impl>())
    {
    }

    WasapiOutput::~WasapiOutput() = default;

    yokko_audio_result WasapiOutput::open(
        const yokko_audio_output_config&,
        yokko_audio_output_status&) noexcept
    {
        return YOKKO_AUDIO_BACKEND_UNAVAILABLE;
    }

    void WasapiOutput::close() noexcept
    {
    }
}
