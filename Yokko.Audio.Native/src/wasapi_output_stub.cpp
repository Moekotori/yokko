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

extern "C"
{
    yokko_audio_result YOKKO_AUDIO_CALL
        yokko_audio_get_wasapi_device_count(uint32_t* device_count)
    {
        if (device_count != nullptr)
            *device_count = 0;
        return YOKKO_AUDIO_BACKEND_UNAVAILABLE;
    }

    yokko_audio_result YOKKO_AUDIO_CALL
        yokko_audio_get_wasapi_device_info(
            uint32_t,
            wchar_t*,
            uint32_t,
            wchar_t*,
            uint32_t,
            uint32_t*)
    {
        return YOKKO_AUDIO_BACKEND_UNAVAILABLE;
    }
}
