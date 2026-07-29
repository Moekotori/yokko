#include "asio_output.hpp"

#include "audio_engine.hpp"

namespace yokko::audio
{
    class AsioOutput::Impl
    {
    public:
        explicit Impl(AudioEngine&)
        {
        }

        yokko_audio_result open(
            const yokko_audio_output_config&,
            yokko_audio_output_status& status) noexcept
        {
            status = {};
            status.struct_size = sizeof(status);
            status.backend = YOKKO_AUDIO_BACKEND_ASIO;
            return YOKKO_AUDIO_BACKEND_UNAVAILABLE;
        }

        void close() noexcept
        {
        }
    };

    AsioOutput::AsioOutput(AudioEngine& engine)
        : implementation_(std::make_unique<Impl>(engine))
    {
    }

    AsioOutput::~AsioOutput() = default;

    yokko_audio_result AsioOutput::open(
        const yokko_audio_output_config& config,
        yokko_audio_output_status& status) noexcept
    {
        return implementation_->open(config, status);
    }

    void AsioOutput::close() noexcept
    {
        implementation_->close();
    }
}

extern "C"
{
    yokko_audio_result YOKKO_AUDIO_CALL
        yokko_audio_get_asio_device_count(uint32_t* device_count)
    {
        if (device_count == nullptr)
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        *device_count = 0;
        return YOKKO_AUDIO_BACKEND_UNAVAILABLE;
    }

    yokko_audio_result YOKKO_AUDIO_CALL
        yokko_audio_get_asio_device_info(
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
