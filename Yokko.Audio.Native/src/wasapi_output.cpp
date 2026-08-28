#include "wasapi_output.hpp"

#include "audio_engine.hpp"

#define NOMINMAX
#include <Windows.h>
#include <audioclient.h>
#include <avrt.h>
#include <propkey.h>
#include <functiondiscoverykeys_devpkey.h>
#include <ksmedia.h>
#include <mmdeviceapi.h>
#include <wrl/client.h>

#include <algorithm>
#include <atomic>
#include <cmath>
#include <condition_variable>
#include <cstdint>
#include <limits>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

using Microsoft::WRL::ComPtr;

namespace
{
    constexpr REFERENCE_TIME reference_times_per_second = 10'000'000;
    constexpr DWORD shared_low_period_stream_flags =
        AUDCLNT_STREAMFLAGS_EVENTCALLBACK;
    constexpr DWORD shared_legacy_stream_flags =
        AUDCLNT_STREAMFLAGS_EVENTCALLBACK
        | AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM
        | AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY;

    static_assert(
        (shared_low_period_stream_flags
         & (AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM
            | AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY))
        == 0);

    struct OutputInitializationDetails
    {
        UINT32 period_frames = 0;
        uint32_t shared_explicit_period = 0;
        HRESULT shared_explicit_period_error = S_OK;
    };

    WAVEFORMATEXTENSIBLE make_wave_format(
        const uint32_t sample_rate,
        const uint32_t channels,
        const yokko_audio_sample_format sample_format) noexcept
    {
        WAVEFORMATEXTENSIBLE format{};
        format.Format.wFormatTag = WAVE_FORMAT_EXTENSIBLE;
        format.Format.nChannels = static_cast<WORD>(channels);
        format.Format.nSamplesPerSec = sample_rate;
        format.Format.cbSize = sizeof(WAVEFORMATEXTENSIBLE) - sizeof(WAVEFORMATEX);
        format.dwChannelMask =
            channels == 1 ? SPEAKER_FRONT_CENTER : SPEAKER_FRONT_LEFT | SPEAKER_FRONT_RIGHT;

        switch (sample_format)
        {
            case YOKKO_AUDIO_SAMPLE_FLOAT32:
                format.Format.wBitsPerSample = 32;
                format.Samples.wValidBitsPerSample = 32;
                format.SubFormat = KSDATAFORMAT_SUBTYPE_IEEE_FLOAT;
                break;

            case YOKKO_AUDIO_SAMPLE_PCM32:
                format.Format.wBitsPerSample = 32;
                format.Samples.wValidBitsPerSample = 32;
                format.SubFormat = KSDATAFORMAT_SUBTYPE_PCM;
                break;

            case YOKKO_AUDIO_SAMPLE_PCM24_IN_32:
                format.Format.wBitsPerSample = 32;
                format.Samples.wValidBitsPerSample = 24;
                format.SubFormat = KSDATAFORMAT_SUBTYPE_PCM;
                break;

            case YOKKO_AUDIO_SAMPLE_PCM16:
                format.Format.wBitsPerSample = 16;
                format.Samples.wValidBitsPerSample = 16;
                format.SubFormat = KSDATAFORMAT_SUBTYPE_PCM;
                break;
        }

        format.Format.nBlockAlign = static_cast<WORD>(
            channels * format.Format.wBitsPerSample / 8);
        format.Format.nAvgBytesPerSec =
            sample_rate * format.Format.nBlockAlign;
        return format;
    }

    uint32_t reference_time_to_frames(
        const REFERENCE_TIME time,
        const uint32_t sample_rate) noexcept
    {
        if (time <= 0)
            return 0;

        const auto frames = static_cast<uint64_t>(
            (static_cast<long double>(time) * sample_rate
             + reference_times_per_second - 1)
            / reference_times_per_second);
        return static_cast<uint32_t>(std::min<uint64_t>(
            frames,
            std::numeric_limits<uint32_t>::max()));
    }

    REFERENCE_TIME frames_to_reference_time(
        const uint32_t frames,
        const uint32_t sample_rate) noexcept
    {
        return static_cast<REFERENCE_TIME>(
            (static_cast<uint64_t>(frames) * reference_times_per_second
             + sample_rate - 1)
            / sample_rate);
    }

    HRESULT set_shared_client_properties(
        const ComPtr<IAudioClient>& audio_client) noexcept
    {
        ComPtr<IAudioClient2> audio_client2;
        const HRESULT query_result = audio_client.As(&audio_client2);
        if (FAILED(query_result))
            return query_result;

        AudioClientProperties properties{};
        properties.cbSize = sizeof(properties);
        properties.bIsOffload = FALSE;
        properties.eCategory = AudioCategory_GameMedia;
        properties.Options = AUDCLNT_STREAMOPTIONS_NONE;
        return audio_client2->SetClientProperties(&properties);
    }

    UINT32 get_shared_period_frames(
        const ComPtr<IAudioClient>& audio_client,
        const uint32_t sample_rate,
        const UINT32 fallback_frames) noexcept
    {
        ComPtr<IAudioClient3> audio_client3;
        if (SUCCEEDED(audio_client.As(&audio_client3)))
        {
            WAVEFORMATEX* current_format = nullptr;
            UINT32 current_period = 0;
            const HRESULT current_result =
                audio_client3->GetCurrentSharedModeEnginePeriod(
                    &current_format,
                    &current_period);
            CoTaskMemFree(current_format);
            if (SUCCEEDED(current_result) && current_period > 0)
                return current_period;
        }

        REFERENCE_TIME default_period = 0;
        if (SUCCEEDED(audio_client->GetDevicePeriod(
                &default_period,
                nullptr)))
        {
            const UINT32 default_period_frames =
                reference_time_to_frames(default_period, sample_rate);
            if (default_period_frames > 0)
                return default_period_frames;
        }

        return fallback_frames;
    }

    uint32_t callback_duration_microseconds(
        const LARGE_INTEGER start,
        const LARGE_INTEGER finish,
        const LARGE_INTEGER frequency) noexcept
    {
        if (frequency.QuadPart <= 0 || finish.QuadPart <= start.QuadPart)
            return 0;

        const long double microseconds =
            static_cast<long double>(finish.QuadPart - start.QuadPart)
            * 1'000'000.0L
            / static_cast<long double>(frequency.QuadPart);
        return static_cast<uint32_t>(std::min<long double>(
            std::ceil(microseconds),
            std::numeric_limits<uint32_t>::max()));
    }

    template <typename T>
    T clamp_integer(const long double value) noexcept
    {
        return static_cast<T>(std::clamp(
            value,
            static_cast<long double>(std::numeric_limits<T>::min()),
            static_cast<long double>(std::numeric_limits<T>::max())));
    }

    bool begin_com(bool& must_uninitialize) noexcept
    {
        const HRESULT result =
            CoInitializeEx(nullptr, COINIT_MULTITHREADED);
        must_uninitialize = SUCCEEDED(result);
        return SUCCEEDED(result) || result == RPC_E_CHANGED_MODE;
    }

    yokko_audio_result enumerate_devices(
        yokko_audio_wasapi_device_info* const devices,
        const uint32_t device_capacity,
        uint32_t* const written_device_count,
        uint32_t* const active_device_count) noexcept
    {
        *written_device_count = 0;
        *active_device_count = 0;

        bool must_uninitialize = false;
        if (!begin_com(must_uninitialize))
            return YOKKO_AUDIO_BACKEND_UNAVAILABLE;

        HRESULT result = S_OK;
        {
            ComPtr<IMMDeviceEnumerator> enumerator;
            ComPtr<IMMDeviceCollection> collection;
            result = CoCreateInstance(
                __uuidof(MMDeviceEnumerator),
                nullptr,
                CLSCTX_ALL,
                IID_PPV_ARGS(&enumerator));
            if (SUCCEEDED(result))
            {
                result = enumerator->EnumAudioEndpoints(
                    eRender,
                    DEVICE_STATE_ACTIVE,
                    &collection);
            }

            UINT count = 0;
            if (SUCCEEDED(result))
                result = collection->GetCount(&count);

            if (SUCCEEDED(result))
            {
                *active_device_count = count;

                // The default endpoint id is resolved once for the whole
                // batch. A missing default (for example no active devices)
                // is not an enumeration failure.
                wchar_t default_id[YOKKO_AUDIO_WASAPI_DEVICE_ID_CAPACITY]{};
                bool has_default = false;
                {
                    ComPtr<IMMDevice> default_device;
                    LPWSTR raw_default_id = nullptr;
                    if (SUCCEEDED(enumerator->GetDefaultAudioEndpoint(
                            eRender,
                            eConsole,
                            &default_device))
                        && SUCCEEDED(default_device->GetId(&raw_default_id))
                        && raw_default_id != nullptr)
                    {
                        wcsncpy_s(default_id, raw_default_id, _TRUNCATE);
                        has_default = true;
                    }
                    CoTaskMemFree(raw_default_id);
                }

                const UINT readable_count =
                    std::min<UINT>(count, device_capacity);
                uint32_t written = 0;
                for (UINT index = 0; index < readable_count; ++index)
                {
                    ComPtr<IMMDevice> device;
                    ComPtr<IPropertyStore> properties;
                    LPWSTR raw_id = nullptr;
                    PROPVARIANT friendly_name;
                    PropVariantInit(&friendly_name);

                    // Endpoints that disappear mid-enumeration are skipped
                    // instead of failing the batch.
                    const bool read_ok =
                        SUCCEEDED(collection->Item(index, &device))
                        && SUCCEEDED(device->GetId(&raw_id))
                        && raw_id != nullptr
                        && SUCCEEDED(device->OpenPropertyStore(
                            STGM_READ,
                            &properties))
                        && SUCCEEDED(properties->GetValue(
                            PKEY_Device_FriendlyName,
                            &friendly_name))
                        && friendly_name.vt == VT_LPWSTR
                        && friendly_name.pwszVal != nullptr;

                    if (read_ok)
                    {
                        yokko_audio_wasapi_device_info& entry =
                            devices[written];
                        wcsncpy_s(
                            entry.id,
                            YOKKO_AUDIO_WASAPI_DEVICE_ID_CAPACITY,
                            raw_id,
                            _TRUNCATE);
                        wcsncpy_s(
                            entry.name,
                            YOKKO_AUDIO_WASAPI_DEVICE_NAME_CAPACITY,
                            friendly_name.pwszVal,
                            _TRUNCATE);
                        entry.is_default =
                            has_default && wcscmp(raw_id, default_id) == 0
                                ? 1u
                                : 0u;
                        ++written;
                    }

                    PropVariantClear(&friendly_name);
                    CoTaskMemFree(raw_id);
                }

                *written_device_count = written;
            }
        }

        if (must_uninitialize)
            CoUninitialize();
        return SUCCEEDED(result)
                   ? YOKKO_AUDIO_OK
                   : YOKKO_AUDIO_BACKEND_UNAVAILABLE;
    }
}

namespace yokko::audio
{
    class WasapiOutput::Impl
    {
    public:
        explicit Impl(AudioEngine& engine)
            : engine_(engine)
        {
        }

        ~Impl()
        {
            close();
        }

        yokko_audio_result open(
            const yokko_audio_output_config& config,
            yokko_audio_output_status& status) noexcept
        {
            close();
            backend_ = config.backend;
            preferred_buffer_frames_ =
                config.preferred_buffer_frames == 0
                    ? 128
                    : config.preferred_buffer_frames;
            device_id_ = config.device_id == nullptr ? L"" : config.device_id;

            stop_event_ = CreateEventW(nullptr, TRUE, FALSE, nullptr);
            audio_event_ = CreateEventW(nullptr, FALSE, FALSE, nullptr);
            if (stop_event_ == nullptr || audio_event_ == nullptr)
            {
                close_handles();
                return YOKKO_AUDIO_INTERNAL_ERROR;
            }

            {
                std::lock_guard lock(initialization_mutex_);
                initialization_complete_ = false;
                initialization_result_ = YOKKO_AUDIO_INTERNAL_ERROR;
                initialized_status_ = {};
            }

            try
            {
                thread_ = std::thread([this] { run(); });
            }
            catch (...)
            {
                close_handles();
                return YOKKO_AUDIO_INTERNAL_ERROR;
            }

            std::unique_lock lock(initialization_mutex_);
            initialization_changed_.wait(
                lock,
                [this] { return initialization_complete_; });
            const yokko_audio_result result = initialization_result_;
            status = initialized_status_;
            lock.unlock();

            if (result != YOKKO_AUDIO_OK)
                close();
            return result;
        }

        void close() noexcept
        {
            if (stop_event_ != nullptr)
                SetEvent(stop_event_);
            if (thread_.joinable())
                thread_.join();
            close_handles();
        }

    private:
        void run() noexcept
        {
            const HRESULT com_result =
                CoInitializeEx(nullptr, COINIT_MULTITHREADED);
            const bool com_initialized = SUCCEEDED(com_result);
            if (!com_initialized)
            {
                finish_initialization(YOKKO_AUDIO_BACKEND_UNAVAILABLE, {});
                return;
            }

            ComPtr<IMMDeviceEnumerator> enumerator;
            ComPtr<IMMDevice> device;
            ComPtr<IAudioClient> audio_client;
            ComPtr<IAudioRenderClient> render_client;
            ComPtr<IAudioClock> audio_clock;
            uint32_t error_stage = 1;

            HRESULT result = CoCreateInstance(
                __uuidof(MMDeviceEnumerator),
                nullptr,
                CLSCTX_ALL,
                IID_PPV_ARGS(&enumerator));
            if (SUCCEEDED(result))
            {
                error_stage = 2;
                result = device_id_.empty()
                             ? enumerator->GetDefaultAudioEndpoint(
                                   eRender,
                                   eConsole,
                                   &device)
                             : enumerator->GetDevice(device_id_.c_str(), &device);
            }
            if (SUCCEEDED(result))
            {
                error_stage = 3;
                result = device->Activate(
                    __uuidof(IAudioClient),
                    CLSCTX_ALL,
                    nullptr,
                    reinterpret_cast<void**>(audio_client.GetAddressOf()));
            }

            WAVEFORMATEXTENSIBLE selected_format{};
            yokko_audio_sample_format selected_sample_format =
                YOKKO_AUDIO_SAMPLE_FLOAT32;
            if (SUCCEEDED(result))
            {
                error_stage = 4;
                result = select_format(
                    *audio_client.Get(),
                    selected_format,
                    selected_sample_format);
            }

            UINT32 buffer_frames = 0;
            OutputInitializationDetails initialization_details;
            REFERENCE_TIME stream_latency = 0;
            if (SUCCEEDED(result))
            {
                error_stage = 5;
                result = initialize_client(
                    *device.Get(),
                    audio_client,
                    selected_format,
                    buffer_frames,
                    initialization_details);
            }
            if (SUCCEEDED(result))
            {
                error_stage = 6;
                result = audio_client->SetEventHandle(audio_event_);
            }
            if (SUCCEEDED(result))
            {
                error_stage = 7;
                result = audio_client->GetStreamLatency(&stream_latency);
            }
            if (SUCCEEDED(result))
            {
                error_stage = 8;
                result = audio_client->GetService(
                    __uuidof(IAudioRenderClient),
                    reinterpret_cast<void**>(render_client.GetAddressOf()));
            }
            if (SUCCEEDED(result))
            {
                error_stage = 9;
                result = audio_client->GetService(
                    __uuidof(IAudioClock),
                    reinterpret_cast<void**>(audio_clock.GetAddressOf()));
            }

            UINT64 clock_frequency = 0;
            if (SUCCEEDED(result))
            {
                error_stage = 10;
                result = audio_clock->GetFrequency(&clock_frequency);
            }

            DWORD task_index = 0;
            HANDLE mmcss_task = nullptr;
            if (SUCCEEDED(result))
            {
                error_stage = 11;
                mmcss_task =
                    AvSetMmThreadCharacteristicsW(L"Pro Audio", &task_index);
                if (mmcss_task == nullptr)
                    result = HRESULT_FROM_WIN32(GetLastError());
            }

            const uint32_t latency_frames = std::max(
                reference_time_to_frames(
                    stream_latency,
                    engine_.sample_rate()),
                buffer_frames);
            const uint32_t callback_period_frames =
                initialization_details.period_frames > 0
                    ? initialization_details.period_frames
                    : buffer_frames;
            const uint32_t callback_budget_microseconds =
                static_cast<uint32_t>(
                    (static_cast<uint64_t>(callback_period_frames) * 1'000'000
                     + engine_.sample_rate() - 1)
                    / engine_.sample_rate());
            LARGE_INTEGER performance_frequency{};
            QueryPerformanceFrequency(&performance_frequency);
            std::vector<float> conversion_buffer;
            if (SUCCEEDED(result)
                && selected_sample_format != YOKKO_AUDIO_SAMPLE_FLOAT32)
            {
                try
                {
                    conversion_buffer.resize(
                        static_cast<size_t>(buffer_frames) * engine_.channels());
                }
                catch (...)
                {
                    result = E_OUTOFMEMORY;
                }
            }

            if (SUCCEEDED(result))
            {
                error_stage = 12;
                result = fill_available_buffer(
                    *audio_client.Get(),
                    *render_client.Get(),
                    buffer_frames,
                    selected_sample_format,
                    conversion_buffer);
            }
            if (SUCCEEDED(result))
            {
                error_stage = 13;
                result = audio_client->Start();
            }

            yokko_audio_output_status output_status{};
            output_status.struct_size = sizeof(output_status);
            output_status.backend = backend_;
            output_status.sample_format = selected_sample_format;
            output_status.sample_rate = engine_.sample_rate();
            output_status.channels = engine_.channels();
            output_status.buffer_frames = buffer_frames;
            output_status.latency_frames = latency_frames;
            output_status.is_active = SUCCEEDED(result) ? 1u : 0u;
            output_status.backend_error = result;
            output_status.backend_error_stage =
                SUCCEEDED(result) ? 0u : error_stage;
            output_status.period_frames =
                initialization_details.period_frames;
            output_status.shared_explicit_period =
                initialization_details.shared_explicit_period;
            output_status.shared_explicit_period_error =
                initialization_details.shared_explicit_period_error;

            finish_initialization(
                SUCCEEDED(result)
                    ? YOKKO_AUDIO_OK
                    : YOKKO_AUDIO_BACKEND_UNAVAILABLE,
                output_status);

            if (SUCCEEDED(result))
            {
                HANDLE events[]{stop_event_, audio_event_};
                LARGE_INTEGER previous_callback_start{};
                while (true)
                {
                    const DWORD wait_result =
                        WaitForMultipleObjects(2, events, FALSE, 2000);
                    if (wait_result == WAIT_OBJECT_0)
                        break;
                    if (wait_result == WAIT_TIMEOUT)
                        continue;
                    if (wait_result != WAIT_OBJECT_0 + 1)
                    {
                        engine_.report_output_failure(
                            HRESULT_FROM_WIN32(GetLastError()),
                            14);
                        break;
                    }

                    LARGE_INTEGER callback_start{};
                    LARGE_INTEGER callback_finish{};
                    QueryPerformanceCounter(&callback_start);
                    const uint32_t callback_interval_microseconds =
                        previous_callback_start.QuadPart == 0
                            ? 0
                            : callback_duration_microseconds(
                                previous_callback_start,
                                callback_start,
                                performance_frequency);
                    previous_callback_start = callback_start;
                    const HRESULT fill_result = fill_available_buffer(
                        *audio_client.Get(),
                        *render_client.Get(),
                        buffer_frames,
                        selected_sample_format,
                        conversion_buffer);

                    UINT64 device_position = 0;
                    UINT64 observation_time_100ns = 0;
                    HRESULT clock_result = audio_clock->GetPosition(
                        &device_position,
                        &observation_time_100ns);
                    if (clock_result == S_FALSE)
                    {
                        clock_result = audio_clock->GetPosition(
                            &device_position,
                            &observation_time_100ns);
                    }
                    if (SUCCEEDED(clock_result) && clock_frequency > 0)
                    {
                        const uint64_t presented_frames =
                            static_cast<uint64_t>(
                                static_cast<long double>(device_position)
                                * engine_.sample_rate()
                                / clock_frequency);
                        engine_.report_presented_position(
                            presented_frames,
                            latency_frames,
                            observation_time_100ns);
                    }

                    QueryPerformanceCounter(&callback_finish);
                    engine_.report_callback_timing(
                        callback_duration_microseconds(
                            callback_start,
                            callback_finish,
                            performance_frequency),
                        callback_budget_microseconds,
                        callback_interval_microseconds);

                    if (FAILED(fill_result))
                    {
                        engine_.report_output_failure(fill_result, 15);
                        break;
                    }
                }
            }

            if (audio_client != nullptr)
                audio_client->Stop();
            if (mmcss_task != nullptr)
                AvRevertMmThreadCharacteristics(mmcss_task);
            CoUninitialize();
        }

        HRESULT select_format(
            IAudioClient& audio_client,
            WAVEFORMATEXTENSIBLE& selected_format,
            yokko_audio_sample_format& selected_sample_format) const noexcept
        {
            const AUDCLNT_SHAREMODE share_mode =
                backend_ == YOKKO_AUDIO_BACKEND_WASAPI_EXCLUSIVE
                    ? AUDCLNT_SHAREMODE_EXCLUSIVE
                    : AUDCLNT_SHAREMODE_SHARED;

            constexpr yokko_audio_sample_format candidates[]{
                YOKKO_AUDIO_SAMPLE_FLOAT32,
                YOKKO_AUDIO_SAMPLE_PCM32,
                YOKKO_AUDIO_SAMPLE_PCM24_IN_32,
                YOKKO_AUDIO_SAMPLE_PCM16,
            };

            for (const yokko_audio_sample_format candidate : candidates)
            {
                WAVEFORMATEXTENSIBLE format = make_wave_format(
                    engine_.sample_rate(),
                    engine_.channels(),
                    candidate);
                WAVEFORMATEX* closest = nullptr;
                const HRESULT result = audio_client.IsFormatSupported(
                    share_mode,
                    &format.Format,
                    share_mode == AUDCLNT_SHAREMODE_SHARED ? &closest : nullptr);
                if (closest != nullptr)
                    CoTaskMemFree(closest);
                if (result == S_OK
                    || (share_mode == AUDCLNT_SHAREMODE_SHARED
                        && candidate == YOKKO_AUDIO_SAMPLE_FLOAT32))
                {
                    selected_format = format;
                    selected_sample_format = candidate;
                    return S_OK;
                }
            }

            return AUDCLNT_E_UNSUPPORTED_FORMAT;
        }

        HRESULT initialize_client(
            IMMDevice& device,
            ComPtr<IAudioClient>& audio_client,
            const WAVEFORMATEXTENSIBLE& format,
            UINT32& buffer_frames,
            OutputInitializationDetails& details) const noexcept
        {
            details = {};
            const AUDCLNT_SHAREMODE share_mode =
                backend_ == YOKKO_AUDIO_BACKEND_WASAPI_EXCLUSIVE
                    ? AUDCLNT_SHAREMODE_EXCLUSIVE
                    : AUDCLNT_SHAREMODE_SHARED;
            DWORD stream_flags = AUDCLNT_STREAMFLAGS_EVENTCALLBACK;
            REFERENCE_TIME buffer_duration = 0;
            REFERENCE_TIME periodicity = 0;

            if (share_mode == AUDCLNT_SHAREMODE_EXCLUSIVE)
            {
                REFERENCE_TIME minimum_period = 0;
                const HRESULT period_result =
                    audio_client->GetDevicePeriod(nullptr, &minimum_period);
                if (FAILED(period_result))
                    return period_result;

                buffer_duration = std::max(
                    frames_to_reference_time(
                        preferred_buffer_frames_,
                        engine_.sample_rate()),
                    minimum_period);
                periodicity = buffer_duration;
            }
            else
            {
                stream_flags = shared_legacy_stream_flags;

                ComPtr<IAudioClient3> audio_client3;
                const HRESULT properties_result =
                    set_shared_client_properties(audio_client);
                const HRESULT client3_result =
                    audio_client.As(&audio_client3);
                if (FAILED(properties_result))
                {
                    details.shared_explicit_period_error =
                        properties_result;
                }
                else if (FAILED(client3_result))
                {
                    details.shared_explicit_period_error = client3_result;
                }
                else
                {
                    UINT32 default_period = 0;
                    UINT32 fundamental_period = 0;
                    UINT32 minimum_period = 0;
                    UINT32 maximum_period = 0;
                    HRESULT period_result =
                        audio_client3->GetSharedModeEnginePeriod(
                            &format.Format,
                            &default_period,
                            &fundamental_period,
                            &minimum_period,
                            &maximum_period);

                    if (SUCCEEDED(period_result)
                        && fundamental_period > 0
                        && minimum_period > 0
                        && maximum_period >= minimum_period)
                    {
                        UINT32 desired_period = std::clamp(
                            preferred_buffer_frames_,
                            minimum_period,
                            maximum_period);
                        desired_period = static_cast<UINT32>(
                            (static_cast<uint64_t>(desired_period)
                             + fundamental_period / 2)
                            / fundamental_period
                            * fundamental_period);
                        desired_period = std::clamp(
                            desired_period,
                            minimum_period,
                            maximum_period);

                        const HRESULT low_latency_result =
                            audio_client3->InitializeSharedAudioStream(
                                shared_low_period_stream_flags,
                                desired_period,
                                &format.Format,
                                nullptr);
                        if (SUCCEEDED(low_latency_result))
                        {
                            const HRESULT buffer_result =
                                audio_client->GetBufferSize(&buffer_frames);
                            if (SUCCEEDED(buffer_result))
                            {
                                details.period_frames =
                                    get_shared_period_frames(
                                        audio_client,
                                        engine_.sample_rate(),
                                        desired_period);
                                details.shared_explicit_period = 1;
                            }
                            return buffer_result;
                        }

                        details.shared_explicit_period_error =
                            low_latency_result;

                        // A shared engine can already be locked to another
                        // period. Re-activate the client before using the
                        // compatible legacy shared-mode initialization below.
                        audio_client.Reset();
                        const HRESULT reactivate_result = device.Activate(
                            __uuidof(IAudioClient),
                            CLSCTX_ALL,
                            nullptr,
                            reinterpret_cast<void**>(
                                audio_client.GetAddressOf()));
                        if (FAILED(reactivate_result))
                            return reactivate_result;

                        set_shared_client_properties(audio_client);
                    }
                    else
                    {
                        details.shared_explicit_period_error =
                            FAILED(period_result)
                                ? period_result
                                : E_UNEXPECTED;
                    }
                }
            }

            HRESULT result = audio_client->Initialize(
                share_mode,
                stream_flags,
                buffer_duration,
                periodicity,
                &format.Format,
                nullptr);

            if (result == AUDCLNT_E_BUFFER_SIZE_NOT_ALIGNED)
            {
                UINT32 aligned_frames = 0;
                result = audio_client->GetBufferSize(&aligned_frames);
                if (FAILED(result))
                    return result;

                audio_client.Reset();
                result = device.Activate(
                    __uuidof(IAudioClient),
                    CLSCTX_ALL,
                    nullptr,
                    reinterpret_cast<void**>(audio_client.GetAddressOf()));
                if (FAILED(result))
                    return result;

                if (share_mode == AUDCLNT_SHAREMODE_SHARED)
                    set_shared_client_properties(audio_client);

                buffer_duration = frames_to_reference_time(
                    aligned_frames,
                    engine_.sample_rate());
                periodicity =
                    share_mode == AUDCLNT_SHAREMODE_EXCLUSIVE
                        ? buffer_duration
                        : 0;
                result = audio_client->Initialize(
                    share_mode,
                    stream_flags,
                    buffer_duration,
                    periodicity,
                    &format.Format,
                    nullptr);
            }

            if (FAILED(result))
                return result;
            result = audio_client->GetBufferSize(&buffer_frames);
            if (FAILED(result))
                return result;

            details.period_frames =
                share_mode == AUDCLNT_SHAREMODE_SHARED
                    ? get_shared_period_frames(
                          audio_client,
                          engine_.sample_rate(),
                          buffer_frames)
                    : buffer_frames;
            return S_OK;
        }

        HRESULT fill_available_buffer(
            IAudioClient& audio_client,
            IAudioRenderClient& render_client,
            const UINT32 buffer_frames,
            const yokko_audio_sample_format sample_format,
            std::vector<float>& conversion_buffer) noexcept
        {
            UINT32 available_frames = buffer_frames;
            if (backend_ == YOKKO_AUDIO_BACKEND_WASAPI_SHARED)
            {
                UINT32 padding = 0;
                const HRESULT padding_result =
                    audio_client.GetCurrentPadding(&padding);
                if (FAILED(padding_result))
                    return padding_result;
                if (padding >= buffer_frames)
                    return S_OK;

                available_frames = buffer_frames - padding;
            }

            BYTE* device_buffer = nullptr;
            HRESULT result =
                render_client.GetBuffer(available_frames, &device_buffer);
            if (FAILED(result))
                return result;

            uint32_t rendered_frames = 0;
            if (sample_format == YOKKO_AUDIO_SAMPLE_FLOAT32)
            {
                engine_.render(
                    reinterpret_cast<float*>(device_buffer),
                    available_frames,
                    rendered_frames);
            }
            else
            {
                engine_.render(
                    conversion_buffer.data(),
                    available_frames,
                    rendered_frames);
                convert_samples(
                    conversion_buffer.data(),
                    device_buffer,
                    static_cast<size_t>(available_frames) * engine_.channels(),
                    sample_format);
            }

            return render_client.ReleaseBuffer(available_frames, 0);
        }

        static void convert_samples(
            const float* input,
            BYTE* output,
            const size_t sample_count,
            const yokko_audio_sample_format format) noexcept
        {
            if (format == YOKKO_AUDIO_SAMPLE_PCM16)
            {
                auto* samples = reinterpret_cast<int16_t*>(output);
                for (size_t index = 0; index < sample_count; ++index)
                {
                    samples[index] = clamp_integer<int16_t>(
                        std::round(static_cast<long double>(input[index]) * 32767.0L));
                }
                return;
            }

            auto* samples = reinterpret_cast<int32_t*>(output);
            for (size_t index = 0; index < sample_count; ++index)
            {
                if (format == YOKKO_AUDIO_SAMPLE_PCM24_IN_32)
                {
                    const int32_t value = clamp_integer<int32_t>(
                        std::round(
                            static_cast<long double>(input[index])
                            * 8'388'607.0L));
                    samples[index] = static_cast<int32_t>(
                        static_cast<int64_t>(value) * 256);
                }
                else
                {
                    samples[index] = clamp_integer<int32_t>(
                        std::round(
                            static_cast<long double>(input[index])
                            * 2'147'483'647.0L));
                }
            }
        }

        void finish_initialization(
            const yokko_audio_result result,
            const yokko_audio_output_status& status) noexcept
        {
            {
                std::lock_guard lock(initialization_mutex_);
                initialization_result_ = result;
                initialized_status_ = status;
                initialization_complete_ = true;
            }
            initialization_changed_.notify_all();
        }

        void close_handles() noexcept
        {
            if (audio_event_ != nullptr)
            {
                CloseHandle(audio_event_);
                audio_event_ = nullptr;
            }
            if (stop_event_ != nullptr)
            {
                CloseHandle(stop_event_);
                stop_event_ = nullptr;
            }
        }

        AudioEngine& engine_;
        yokko_audio_backend_mode backend_ = YOKKO_AUDIO_BACKEND_WASAPI_SHARED;
        uint32_t preferred_buffer_frames_ = 128;
        std::wstring device_id_;
        HANDLE stop_event_ = nullptr;
        HANDLE audio_event_ = nullptr;
        std::thread thread_;
        std::mutex initialization_mutex_;
        std::condition_variable initialization_changed_;
        bool initialization_complete_ = false;
        yokko_audio_result initialization_result_ = YOKKO_AUDIO_INTERNAL_ERROR;
        yokko_audio_output_status initialized_status_{};
    };

    WasapiOutput::WasapiOutput(AudioEngine& engine)
        : implementation_(std::make_unique<Impl>(engine))
    {
    }

    WasapiOutput::~WasapiOutput() = default;

    yokko_audio_result WasapiOutput::open(
        const yokko_audio_output_config& config,
        yokko_audio_output_status& status) noexcept
    {
        return implementation_->open(config, status);
    }

    void WasapiOutput::close() noexcept
    {
        implementation_->close();
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

        return enumerate_devices(
            devices,
            device_capacity,
            written_device_count,
            active_device_count);
    }
}
