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
        yokko_audio_enumerate_wasapi_devices(
            yokko_audio_wasapi_device_info* devices,
            const uint32_t device_capacity,
            uint32_t* written_device_count,
            uint32_t* active_device_count)
    {
        if (written_device_count == nullptr
            || active_device_count == nullptr
            || (devices == nullptr && device_capacity != 0))
        {
            return YOKKO_AUDIO_INVALID_ARGUMENT;
        }

        *written_device_count = 0;
        *active_device_count = 0;
        return YOKKO_AUDIO_BACKEND_UNAVAILABLE;
    }
}
