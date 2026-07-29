#ifdef _WIN32

#include "asio_output.hpp"

#include "audio_engine.hpp"

#ifndef NOMINMAX
#define NOMINMAX
#endif

#include <windows.h>

#include "asiosys.h"
#include "asio.h"
#include "iasiodrv.h"

#include <algorithm>
#include <array>
#include <atomic>
#include <cmath>
#include <condition_variable>
#include <cstdint>
#include <cstring>
#include <limits>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

namespace
{
    constexpr wchar_t asio_registry_path[] = L"SOFTWARE\\ASIO";
    constexpr wchar_t asio_device_prefix[] = L"asio:";
    constexpr uint32_t max_asio_devices = 128;
    constexpr long max_output_channels = 2;

    struct AsioDevice
    {
        std::wstring id;
        std::wstring name;
        CLSID clsid{};
    };

    bool copy_string(
        const std::wstring& source,
        wchar_t* destination,
        const uint32_t capacity) noexcept
    {
        if (destination == nullptr || capacity == 0)
            return false;

        const size_t copied =
            std::min(source.size(), static_cast<size_t>(capacity - 1));
        std::copy_n(source.data(), copied, destination);
        destination[copied] = L'\0';
        return copied == source.size();
    }

    bool read_registry_string(
        HKEY key,
        const wchar_t* value_name,
        std::wstring& value)
    {
        DWORD type = 0;
        DWORD size = 0;
        LONG result = RegGetValueW(
            key,
            nullptr,
            value_name,
            RRF_RT_REG_SZ,
            &type,
            nullptr,
            &size);
        if (result != ERROR_SUCCESS || size < sizeof(wchar_t))
            return false;

        std::vector<wchar_t> buffer(
            static_cast<size_t>(size / sizeof(wchar_t)) + 1,
            L'\0');
        result = RegGetValueW(
            key,
            nullptr,
            value_name,
            RRF_RT_REG_SZ,
            &type,
            buffer.data(),
            &size);
        if (result != ERROR_SUCCESS)
            return false;

        value.assign(buffer.data());
        return !value.empty();
    }

    std::vector<AsioDevice> enumerate_asio_devices()
    {
        std::vector<AsioDevice> devices;
        HKEY root = nullptr;
        const LONG open_result = RegOpenKeyExW(
            HKEY_LOCAL_MACHINE,
            asio_registry_path,
            0,
            KEY_READ | KEY_WOW64_64KEY,
            &root);
        if (open_result != ERROR_SUCCESS)
            return devices;

        for (DWORD index = 0; index < max_asio_devices; ++index)
        {
            std::array<wchar_t, 512> subkey_name{};
            DWORD subkey_length =
                static_cast<DWORD>(subkey_name.size());
            const LONG enumerate_result = RegEnumKeyExW(
                root,
                index,
                subkey_name.data(),
                &subkey_length,
                nullptr,
                nullptr,
                nullptr,
                nullptr);
            if (enumerate_result == ERROR_NO_MORE_ITEMS)
                break;
            if (enumerate_result != ERROR_SUCCESS)
                continue;

            HKEY subkey = nullptr;
            if (RegOpenKeyExW(
                    root,
                    subkey_name.data(),
                    0,
                    KEY_READ | KEY_WOW64_64KEY,
                    &subkey)
                != ERROR_SUCCESS)
            {
                continue;
            }

            std::wstring clsid_text;
            std::wstring description;
            const bool has_clsid =
                read_registry_string(subkey, L"CLSID", clsid_text);
            read_registry_string(subkey, L"Description", description);
            RegCloseKey(subkey);
            if (!has_clsid)
                continue;

            CLSID clsid{};
            if (FAILED(CLSIDFromString(clsid_text.c_str(), &clsid)))
                continue;

            std::array<wchar_t, 64> canonical_clsid{};
            if (StringFromGUID2(
                    clsid,
                    canonical_clsid.data(),
                    static_cast<int>(canonical_clsid.size()))
                <= 0)
            {
                continue;
            }

            AsioDevice device;
            device.id = asio_device_prefix;
            device.id += canonical_clsid.data();
            device.name = description.empty()
                              ? std::wstring(
                                    subkey_name.data(),
                                    subkey_length)
                              : description;
            device.clsid = clsid;

            const auto duplicate =
                std::find_if(
                    devices.begin(),
                    devices.end(),
                    [&](const AsioDevice& candidate)
                    {
                        return candidate.id == device.id;
                    });
            if (duplicate == devices.end())
                devices.push_back(std::move(device));
        }

        RegCloseKey(root);
        return devices;
    }

    bool asio_succeeded(const ASIOError error) noexcept
    {
        return error == ASE_OK;
    }

    float clamp_sample(const float sample) noexcept
    {
        if (!std::isfinite(sample))
            return 0;
        return std::clamp(sample, -1.0f, 1.0f);
    }

    int32_t scaled_integer_sample(
        const float sample,
        const int valid_bits) noexcept
    {
        const double peak =
            std::ldexp(1.0, valid_bits - 1) - 1.0;
        return static_cast<int32_t>(
            std::llround(
                static_cast<double>(clamp_sample(sample)) * peak));
    }

    int32_t aligned_integer_sample(
        const float sample,
        const int valid_bits) noexcept
    {
        return static_cast<int32_t>(
            static_cast<int64_t>(
                scaled_integer_sample(sample, valid_bits))
            * (int64_t{1} << (32 - valid_bits)));
    }

    void write_u16_be(
        unsigned char* destination,
        const uint16_t value) noexcept
    {
        destination[0] =
            static_cast<unsigned char>((value >> 8) & 0xff);
        destination[1] =
            static_cast<unsigned char>(value & 0xff);
    }

    void write_u24(
        unsigned char* destination,
        const int32_t value,
        const bool big_endian) noexcept
    {
        if (big_endian)
        {
            destination[0] =
                static_cast<unsigned char>((value >> 16) & 0xff);
            destination[1] =
                static_cast<unsigned char>((value >> 8) & 0xff);
            destination[2] =
                static_cast<unsigned char>(value & 0xff);
        }
        else
        {
            destination[0] =
                static_cast<unsigned char>(value & 0xff);
            destination[1] =
                static_cast<unsigned char>((value >> 8) & 0xff);
            destination[2] =
                static_cast<unsigned char>((value >> 16) & 0xff);
        }
    }

    void write_u32_be(
        unsigned char* destination,
        const uint32_t value) noexcept
    {
        destination[0] =
            static_cast<unsigned char>((value >> 24) & 0xff);
        destination[1] =
            static_cast<unsigned char>((value >> 16) & 0xff);
        destination[2] =
            static_cast<unsigned char>((value >> 8) & 0xff);
        destination[3] =
            static_cast<unsigned char>(value & 0xff);
    }

    void write_u64_be(
        unsigned char* destination,
        const uint64_t value) noexcept
    {
        for (int byte = 0; byte < 8; ++byte)
        {
            destination[byte] = static_cast<unsigned char>(
                (value >> ((7 - byte) * 8)) & 0xff);
        }
    }

    bool is_supported_sample_type(
        const ASIOSampleType type) noexcept
    {
        switch (type)
        {
            case ASIOSTInt16MSB:
            case ASIOSTInt24MSB:
            case ASIOSTInt32MSB:
            case ASIOSTFloat32MSB:
            case ASIOSTFloat64MSB:
            case ASIOSTInt32MSB16:
            case ASIOSTInt32MSB18:
            case ASIOSTInt32MSB20:
            case ASIOSTInt32MSB24:
            case ASIOSTInt16LSB:
            case ASIOSTInt24LSB:
            case ASIOSTInt32LSB:
            case ASIOSTFloat32LSB:
            case ASIOSTFloat64LSB:
            case ASIOSTInt32LSB16:
            case ASIOSTInt32LSB18:
            case ASIOSTInt32LSB20:
            case ASIOSTInt32LSB24:
                return true;
            default:
                return false;
        }
    }

    void write_sample(
        void* buffer,
        const ASIOSampleType type,
        const long frame,
        const float sample) noexcept
    {
        auto* bytes = static_cast<unsigned char*>(buffer);
        switch (type)
        {
            case ASIOSTInt16LSB:
                reinterpret_cast<int16_t*>(buffer)[frame] =
                    static_cast<int16_t>(
                        scaled_integer_sample(sample, 16));
                break;

            case ASIOSTInt16MSB:
                write_u16_be(
                    bytes + frame * 2,
                    static_cast<uint16_t>(
                        static_cast<int16_t>(
                            scaled_integer_sample(sample, 16))));
                break;

            case ASIOSTInt24LSB:
                write_u24(
                    bytes + frame * 3,
                    scaled_integer_sample(sample, 24),
                    false);
                break;

            case ASIOSTInt24MSB:
                write_u24(
                    bytes + frame * 3,
                    scaled_integer_sample(sample, 24),
                    true);
                break;

            case ASIOSTInt32LSB:
                reinterpret_cast<int32_t*>(buffer)[frame] =
                    scaled_integer_sample(sample, 32);
                break;

            case ASIOSTInt32MSB:
                write_u32_be(
                    bytes + frame * 4,
                    static_cast<uint32_t>(
                        scaled_integer_sample(sample, 32)));
                break;

            case ASIOSTInt32LSB16:
                reinterpret_cast<int32_t*>(buffer)[frame] =
                    aligned_integer_sample(sample, 16);
                break;

            case ASIOSTInt32LSB18:
                reinterpret_cast<int32_t*>(buffer)[frame] =
                    aligned_integer_sample(sample, 18);
                break;

            case ASIOSTInt32LSB20:
                reinterpret_cast<int32_t*>(buffer)[frame] =
                    aligned_integer_sample(sample, 20);
                break;

            case ASIOSTInt32LSB24:
                reinterpret_cast<int32_t*>(buffer)[frame] =
                    aligned_integer_sample(sample, 24);
                break;

            case ASIOSTInt32MSB16:
                write_u32_be(
                    bytes + frame * 4,
                    static_cast<uint32_t>(
                        aligned_integer_sample(sample, 16)));
                break;

            case ASIOSTInt32MSB18:
                write_u32_be(
                    bytes + frame * 4,
                    static_cast<uint32_t>(
                        aligned_integer_sample(sample, 18)));
                break;

            case ASIOSTInt32MSB20:
                write_u32_be(
                    bytes + frame * 4,
                    static_cast<uint32_t>(
                        aligned_integer_sample(sample, 20)));
                break;

            case ASIOSTInt32MSB24:
                write_u32_be(
                    bytes + frame * 4,
                    static_cast<uint32_t>(
                        aligned_integer_sample(sample, 24)));
                break;

            case ASIOSTFloat32LSB:
                reinterpret_cast<float*>(buffer)[frame] =
                    clamp_sample(sample);
                break;

            case ASIOSTFloat32MSB:
            {
                uint32_t bits = 0;
                const float value = clamp_sample(sample);
                std::memcpy(&bits, &value, sizeof(bits));
                write_u32_be(bytes + frame * 4, bits);
                break;
            }

            case ASIOSTFloat64LSB:
                reinterpret_cast<double*>(buffer)[frame] =
                    static_cast<double>(clamp_sample(sample));
                break;

            case ASIOSTFloat64MSB:
            {
                uint64_t bits = 0;
                const double value =
                    static_cast<double>(clamp_sample(sample));
                std::memcpy(&bits, &value, sizeof(bits));
                write_u64_be(bytes + frame * 8, bits);
                break;
            }

            default:
                break;
        }
    }

    yokko_audio_sample_format to_output_format(
        const ASIOSampleType type) noexcept
    {
        switch (type)
        {
            case ASIOSTFloat32MSB:
            case ASIOSTFloat32LSB:
            case ASIOSTFloat64MSB:
            case ASIOSTFloat64LSB:
                return YOKKO_AUDIO_SAMPLE_FLOAT32;

            case ASIOSTInt16MSB:
            case ASIOSTInt16LSB:
            case ASIOSTInt32MSB16:
            case ASIOSTInt32LSB16:
                return YOKKO_AUDIO_SAMPLE_PCM16;

            case ASIOSTInt24MSB:
            case ASIOSTInt24LSB:
            case ASIOSTInt32MSB18:
            case ASIOSTInt32MSB20:
            case ASIOSTInt32MSB24:
            case ASIOSTInt32LSB18:
            case ASIOSTInt32LSB20:
            case ASIOSTInt32LSB24:
                return YOKKO_AUDIO_SAMPLE_PCM24_IN_32;

            default:
                return YOKKO_AUDIO_SAMPLE_PCM32;
        }
    }

    void add_candidate(
        std::vector<long>& candidates,
        const long candidate,
        const long minimum,
        const long maximum)
    {
        if (candidate < minimum || candidate > maximum)
            return;
        if (std::find(
                candidates.begin(),
                candidates.end(),
                candidate)
            == candidates.end())
        {
            candidates.push_back(candidate);
        }
    }

    void add_nearest_legal_candidates(
        std::vector<long>& candidates,
        const long requested,
        const long minimum,
        const long maximum,
        const long granularity)
    {
        if (requested <= 0)
            return;

        const long clamped =
            std::clamp(requested, minimum, maximum);
        if (granularity == -1)
        {
            long lower = 1;
            while (lower <= clamped / 2)
                lower *= 2;
            long upper = lower;
            while (upper < clamped && upper <= maximum / 2)
                upper *= 2;
            add_candidate(candidates, lower, minimum, maximum);
            add_candidate(candidates, upper, minimum, maximum);
            return;
        }

        if (granularity > 0)
        {
            const long offset = clamped - minimum;
            const long lower =
                minimum + (offset / granularity) * granularity;
            add_candidate(candidates, lower, minimum, maximum);
            add_candidate(
                candidates,
                lower + granularity,
                minimum,
                maximum);
            return;
        }

        add_candidate(candidates, clamped, minimum, maximum);
    }

    std::vector<long> build_buffer_candidates(
        long minimum,
        long maximum,
        long preferred,
        const long granularity,
        const uint32_t requested)
    {
        minimum = std::max(1L, minimum);
        maximum = std::max(minimum, maximum);
        preferred = std::clamp(preferred, minimum, maximum);

        std::vector<long> candidates;
        add_nearest_legal_candidates(
            candidates,
            static_cast<long>(requested),
            minimum,
            maximum,
            granularity);
        add_candidate(candidates, preferred, minimum, maximum);
        for (const long common : {64L, 128L, 256L, 512L, 1024L})
        {
            add_nearest_legal_candidates(
                candidates,
                common,
                minimum,
                maximum,
                granularity);
        }
        add_candidate(candidates, minimum, minimum, maximum);
        return candidates;
    }

    uint64_t asio_samples_to_uint64(
        const ASIOSamples& samples) noexcept
    {
#if NATIVE_INT64
        return samples < 0 ? 0 : static_cast<uint64_t>(samples);
#else
        return (
                   static_cast<uint64_t>(
                       static_cast<uint32_t>(samples.hi))
                   << 32)
               | static_cast<uint32_t>(samples.lo);
#endif
    }

    uint64_t qpc_to_100ns(
        const LARGE_INTEGER counter,
        const LARGE_INTEGER frequency) noexcept
    {
        if (frequency.QuadPart <= 0 || counter.QuadPart <= 0)
            return 0;

        const long double ticks =
            static_cast<long double>(counter.QuadPart)
            * 10'000'000.0L
            / static_cast<long double>(frequency.QuadPart);
        return ticks <= 0
                   ? 0
                   : static_cast<uint64_t>(ticks);
    }

    uint32_t elapsed_microseconds(
        const LARGE_INTEGER start,
        const LARGE_INTEGER finish,
        const LARGE_INTEGER frequency) noexcept
    {
        if (frequency.QuadPart <= 0
            || finish.QuadPart <= start.QuadPart)
        {
            return 0;
        }

        const long double microseconds =
            static_cast<long double>(
                finish.QuadPart - start.QuadPart)
            * 1'000'000.0L
            / static_cast<long double>(frequency.QuadPart);
        return static_cast<uint32_t>(
            std::min<long double>(
                microseconds,
                std::numeric_limits<uint32_t>::max()));
    }
}

namespace yokko::audio
{
    class AsioOutput::Impl
    {
    public:
        explicit Impl(AudioEngine& engine)
            : engine_(engine)
        {
            QueryPerformanceFrequency(&performance_frequency_);
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
            status = {};
            status.struct_size = sizeof(status);
            status.backend = YOKKO_AUDIO_BACKEND_ASIO;
            device_id_ =
                config.device_id == nullptr ? L"" : config.device_id;
            preferred_buffer_frames_ =
                config.preferred_buffer_frames == 0
                    ? 128
                    : config.preferred_buffer_frames;

            stop_event_ =
                CreateEventW(nullptr, TRUE, FALSE, nullptr);
            if (stop_event_ == nullptr)
                return YOKKO_AUDIO_INTERNAL_ERROR;

            {
                std::lock_guard lock(initialization_mutex_);
                initialization_complete_ = false;
                initialization_result_ = YOKKO_AUDIO_INTERNAL_ERROR;
                initialized_status_ = status;
            }

            try
            {
                thread_ = std::thread([this] { run(); });
            }
            catch (...)
            {
                close_stop_event();
                return YOKKO_AUDIO_INTERNAL_ERROR;
            }

            std::unique_lock lock(initialization_mutex_);
            initialization_changed_.wait(
                lock,
                [this] { return initialization_complete_; });
            const yokko_audio_result result =
                initialization_result_;
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
            close_stop_event();
        }

    private:
        static std::atomic<Impl*> active_output_;
        static std::atomic<uint32_t> active_callbacks_;

        class CallbackGuard
        {
        public:
            CallbackGuard()
            {
                active_callbacks_.fetch_add(
                    1,
                    std::memory_order_acq_rel);
            }

            ~CallbackGuard()
            {
                active_callbacks_.fetch_sub(
                    1,
                    std::memory_order_release);
            }
        };

        void run() noexcept
        {
            const HRESULT com_result =
                CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
            const bool com_initialized = SUCCEEDED(com_result);
            if (!com_initialized)
            {
                finish_initialization(
                    YOKKO_AUDIO_BACKEND_UNAVAILABLE,
                    failure_status(
                        static_cast<int32_t>(com_result),
                        1));
                return;
            }

            Impl* expected = nullptr;
            if (!active_output_.compare_exchange_strong(
                    expected,
                    this,
                    std::memory_order_acq_rel,
                    std::memory_order_acquire))
            {
                finish_initialization(
                    YOKKO_AUDIO_INVALID_STATE,
                    failure_status(
                        static_cast<int32_t>(ASE_InvalidMode),
                        2));
                CoUninitialize();
                return;
            }

            yokko_audio_output_status opened_status{};
            const yokko_audio_result open_result =
                initialize_driver(opened_status);
            if (open_result != YOKKO_AUDIO_OK)
            {
                finish_initialization(open_result, opened_status);
                teardown_driver();
                CoUninitialize();
                return;
            }

            finish_initialization(
                YOKKO_AUDIO_OK,
                opened_status);

            while (true)
            {
                const DWORD wait_result =
                    MsgWaitForMultipleObjectsEx(
                        1,
                        &stop_event_,
                        10,
                        QS_ALLINPUT,
                        MWMO_INPUTAVAILABLE);
                if (wait_result == WAIT_OBJECT_0)
                    break;

                MSG message{};
                while (PeekMessageW(
                    &message,
                    nullptr,
                    0,
                    0,
                    PM_REMOVE))
                {
                    TranslateMessage(&message);
                    DispatchMessageW(&message);
                }

                if (latencies_changed_.exchange(
                        false,
                        std::memory_order_acq_rel))
                {
                    refresh_latencies();
                }

                if (restart_requested_.load(
                        std::memory_order_acquire))
                {
                    engine_.report_output_failure(
                        static_cast<int32_t>(ASE_SPNotAdvancing),
                        13);
                    break;
                }
            }

            teardown_driver();
            CoUninitialize();
        }

        yokko_audio_result initialize_driver(
            yokko_audio_output_status& status) noexcept
        {
            status = failure_status(0, 3);
            const std::vector<AsioDevice> devices =
                enumerate_asio_devices();
            if (devices.empty())
                return YOKKO_AUDIO_BACKEND_UNAVAILABLE;

            const AsioDevice* selected = nullptr;
            if (!device_id_.empty())
            {
                const auto found = std::find_if(
                    devices.begin(),
                    devices.end(),
                    [&](const AsioDevice& device)
                    {
                        return device.id == device_id_;
                    });
                if (found == devices.end())
                    return YOKKO_AUDIO_BACKEND_UNAVAILABLE;
                selected = &*found;
            }
            else
            {
                selected = &devices.front();
            }

            host_window_ = CreateWindowExW(
                0,
                L"STATIC",
                L"Yokko ASIO Host",
                WS_POPUP,
                0,
                0,
                1,
                1,
                nullptr,
                nullptr,
                GetModuleHandleW(nullptr),
                nullptr);
            if (host_window_ == nullptr)
            {
                status = failure_status(
                    static_cast<int32_t>(GetLastError()),
                    4);
                return YOKKO_AUDIO_BACKEND_UNAVAILABLE;
            }

            const HRESULT create_result = CoCreateInstance(
                selected->clsid,
                nullptr,
                CLSCTX_INPROC_SERVER,
                selected->clsid,
                reinterpret_cast<void**>(&driver_));
            if (FAILED(create_result) || driver_ == nullptr)
            {
                status = failure_status(
                    static_cast<int32_t>(create_result),
                    5);
                return YOKKO_AUDIO_BACKEND_UNAVAILABLE;
            }

            if (driver_->init(host_window_) != ASIOTrue)
            {
                status = failure_status(
                    static_cast<int32_t>(ASE_NotPresent),
                    6);
                return YOKKO_AUDIO_BACKEND_UNAVAILABLE;
            }
            initialized_ = true;

            long input_channels = 0;
            long output_channels = 0;
            ASIOError error =
                driver_->getChannels(
                    &input_channels,
                    &output_channels);
            if (!asio_succeeded(error) || output_channels <= 0)
            {
                status = failure_status(
                    static_cast<int32_t>(error),
                    7);
                return YOKKO_AUDIO_BACKEND_UNAVAILABLE;
            }
            output_channel_count_ =
                std::min(
                    output_channels,
                    std::min<long>(
                        max_output_channels,
                        engine_.channels()));

            error = negotiate_sample_rate();
            if (!asio_succeeded(error))
            {
                status = failure_status(
                    static_cast<int32_t>(error),
                    8);
                return YOKKO_AUDIO_BACKEND_UNAVAILABLE;
            }

            error = driver_->getBufferSize(
                &minimum_buffer_frames_,
                &maximum_buffer_frames_,
                &driver_preferred_buffer_frames_,
                &buffer_granularity_);
            if (!asio_succeeded(error)
                || minimum_buffer_frames_ <= 0
                || maximum_buffer_frames_
                       < minimum_buffer_frames_)
            {
                status = failure_status(
                    static_cast<int32_t>(error),
                    9);
                return YOKKO_AUDIO_BACKEND_UNAVAILABLE;
            }

            callbacks_.bufferSwitch = buffer_switch;
            callbacks_.sampleRateDidChange =
                sample_rate_changed;
            callbacks_.asioMessage = asio_message;
            callbacks_.bufferSwitchTimeInfo =
                buffer_switch_time_info;

            const std::vector<long> candidates =
                build_buffer_candidates(
                    minimum_buffer_frames_,
                    maximum_buffer_frames_,
                    driver_preferred_buffer_frames_,
                    buffer_granularity_,
                    preferred_buffer_frames_);
            error = ASE_InvalidParameter;
            for (const long candidate : candidates)
            {
                buffer_frames_ = candidate;
                scratch_.assign(
                    static_cast<size_t>(buffer_frames_)
                        * engine_.channels(),
                    0);

                for (long channel = 0;
                     channel < output_channel_count_;
                     ++channel)
                {
                    buffer_infos_[
                        static_cast<size_t>(channel)] = {};
                    buffer_infos_[
                        static_cast<size_t>(channel)].isInput =
                        ASIOFalse;
                    buffer_infos_[
                        static_cast<size_t>(channel)].channelNum =
                        channel;
                }

                error = driver_->createBuffers(
                    buffer_infos_.data(),
                    output_channel_count_,
                    buffer_frames_,
                    &callbacks_);
                if (asio_succeeded(error))
                {
                    buffers_created_ = true;
                    break;
                }

                // Some drivers leave a partial allocation behind even when
                // createBuffers rejects the requested size.
                driver_->disposeBuffers();
            }
            if (!buffers_created_)
            {
                status = failure_status(
                    static_cast<int32_t>(error),
                    10);
                return YOKKO_AUDIO_BACKEND_UNAVAILABLE;
            }

            for (long channel = 0;
                 channel < output_channel_count_;
                 ++channel)
            {
                ASIOChannelInfo& channel_info =
                    channel_infos_[
                        static_cast<size_t>(channel)];
                channel_info = {};
                channel_info.channel = channel;
                channel_info.isInput = ASIOFalse;
                error = driver_->getChannelInfo(
                    &channel_info);
                if (!asio_succeeded(error)
                    || !is_supported_sample_type(
                        channel_info.type))
                {
                    status = failure_status(
                        static_cast<int32_t>(error),
                        11);
                    return YOKKO_AUDIO_BACKEND_UNAVAILABLE;
                }
            }

            clear_device_buffers();
            output_ready_supported_ =
                asio_succeeded(driver_->outputReady());
            refresh_latencies();

            error = driver_->start();
            if (!asio_succeeded(error))
            {
                status = failure_status(
                    static_cast<int32_t>(error),
                    12);
                return YOKKO_AUDIO_BACKEND_UNAVAILABLE;
            }
            started_ = true;

            status = {};
            status.struct_size = sizeof(status);
            status.backend = YOKKO_AUDIO_BACKEND_ASIO;
            status.sample_format =
                to_output_format(channel_infos_[0].type);
            status.sample_rate = engine_.sample_rate();
            status.channels =
                static_cast<uint32_t>(output_channel_count_);
            status.buffer_frames =
                static_cast<uint32_t>(buffer_frames_);
            status.latency_frames =
                output_latency_frames_.load(
                    std::memory_order_acquire);
            status.is_active = 1;
            return YOKKO_AUDIO_OK;
        }

        ASIOError negotiate_sample_rate() noexcept
        {
            const ASIOSampleRate requested =
                static_cast<ASIOSampleRate>(
                    engine_.sample_rate());
            ASIOSampleRate current = 0;
            ASIOError error =
                driver_->getSampleRate(&current);
            if (asio_succeeded(error)
                && std::llround(current)
                       == static_cast<long long>(
                           engine_.sample_rate()))
            {
                return ASE_OK;
            }

            error = driver_->canSampleRate(requested);
            if (!asio_succeeded(error))
                return error;

            error = driver_->setSampleRate(requested);
            if (!asio_succeeded(error))
                return error;

            for (int attempt = 0; attempt < 50; ++attempt)
            {
                current = 0;
                error = driver_->getSampleRate(&current);
                if (asio_succeeded(error)
                    && std::llround(current)
                           == static_cast<long long>(
                               engine_.sample_rate()))
                {
                    return ASE_OK;
                }
                Sleep(10);
            }
            return ASE_NoClock;
        }

        void refresh_latencies() noexcept
        {
            if (driver_ == nullptr)
                return;

            long input_latency = 0;
            long output_latency = 0;
            if (asio_succeeded(driver_->getLatencies(
                    &input_latency,
                    &output_latency))
                && output_latency >= 0)
            {
                output_latency_frames_.store(
                    static_cast<uint32_t>(output_latency),
                    std::memory_order_release);
                return;
            }

            output_latency_frames_.store(
                static_cast<uint32_t>(
                    std::max(1L, buffer_frames_)),
                std::memory_order_release);
        }

        void clear_device_buffers() noexcept
        {
            for (long channel = 0;
                 channel < output_channel_count_;
                 ++channel)
            {
                for (long buffer_index = 0;
                     buffer_index < 2;
                     ++buffer_index)
                {
                    void* buffer =
                        buffer_infos_[
                            static_cast<size_t>(channel)]
                            .buffers[buffer_index];
                    if (buffer == nullptr)
                        continue;

                    for (long frame = 0;
                         frame < buffer_frames_;
                         ++frame)
                    {
                        write_sample(
                            buffer,
                            channel_infos_[
                                static_cast<size_t>(channel)]
                                .type,
                            frame,
                            0);
                    }
                }
            }
        }

        void render(
            const long double_buffer_index,
            const ASIOTime* time_info) noexcept
        {
            if (double_buffer_index < 0
                || double_buffer_index > 1
                || buffer_frames_ <= 0)
            {
                return;
            }

            if (rendering_.test_and_set(
                    std::memory_order_acquire))
            {
                clear_device_buffer(double_buffer_index);
                restart_requested_.store(
                    true,
                    std::memory_order_release);
                return;
            }

            struct RenderingGuard
            {
                std::atomic_flag& flag;

                ~RenderingGuard()
                {
                    flag.clear(std::memory_order_release);
                }
            } rendering_guard{rendering_};

            LARGE_INTEGER callback_start{};
            LARGE_INTEGER callback_finish{};
            QueryPerformanceCounter(&callback_start);
            const uint32_t interval_microseconds =
                previous_callback_start_.QuadPart == 0
                    ? 0
                    : elapsed_microseconds(
                          previous_callback_start_,
                          callback_start,
                          performance_frequency_);
            previous_callback_start_ = callback_start;

            uint32_t rendered_frames = 0;
            engine_.render(
                scratch_.data(),
                static_cast<uint32_t>(buffer_frames_),
                rendered_frames);

            for (long channel = 0;
                 channel < output_channel_count_;
                 ++channel)
            {
                void* output =
                    buffer_infos_[
                        static_cast<size_t>(channel)]
                        .buffers[double_buffer_index];
                if (output == nullptr)
                    continue;

                const ASIOSampleType sample_type =
                    channel_infos_[
                        static_cast<size_t>(channel)]
                        .type;
                for (long frame = 0;
                     frame < buffer_frames_;
                     ++frame)
                {
                    const size_t source_index =
                        static_cast<size_t>(frame)
                            * engine_.channels()
                        + static_cast<size_t>(channel);
                    write_sample(
                        output,
                        sample_type,
                        frame,
                        scratch_[source_index]);
                }
            }

            if (output_ready_supported_)
                driver_->outputReady();

            ASIOSamples sample_position{};
            ASIOTimeStamp timestamp{};
            bool has_position = false;
            if (time_info != nullptr
                && (time_info->timeInfo.flags
                    & kSamplePositionValid)
                       != 0)
            {
                sample_position =
                    time_info->timeInfo.samplePosition;
                has_position = true;
            }
            else if (driver_ != nullptr
                     && asio_succeeded(
                         driver_->getSamplePosition(
                             &sample_position,
                             &timestamp)))
            {
                has_position = true;
            }

            if (has_position)
            {
                const uint64_t buffer_position =
                    asio_samples_to_uint64(sample_position);
                const uint32_t output_latency =
                    output_latency_frames_.load(
                        std::memory_order_acquire);
                const uint64_t presented_position =
                    buffer_position > output_latency
                        ? buffer_position - output_latency
                        : 0;
                engine_.report_presented_position(
                    presented_position,
                    output_latency,
                    qpc_to_100ns(
                        callback_start,
                        performance_frequency_));
            }

            QueryPerformanceCounter(&callback_finish);
            const uint32_t budget_microseconds =
                static_cast<uint32_t>(
                    std::max<long double>(
                        1,
                        static_cast<long double>(
                            buffer_frames_)
                            * 1'000'000.0L
                            / engine_.sample_rate()));
            engine_.report_callback_timing(
                elapsed_microseconds(
                    callback_start,
                    callback_finish,
                    performance_frequency_),
                budget_microseconds,
                interval_microseconds);
        }

        void clear_device_buffer(
            const long double_buffer_index) noexcept
        {
            if (double_buffer_index < 0
                || double_buffer_index > 1)
                return;

            for (long channel = 0;
                 channel < output_channel_count_;
                 ++channel)
            {
                void* output =
                    buffer_infos_[
                        static_cast<size_t>(channel)]
                        .buffers[double_buffer_index];
                if (output == nullptr)
                    continue;

                for (long frame = 0;
                     frame < buffer_frames_;
                     ++frame)
                {
                    write_sample(
                        output,
                        channel_infos_[
                            static_cast<size_t>(channel)]
                            .type,
                        frame,
                        0);
                }
            }
        }

        void teardown_driver() noexcept
        {
            Impl* expected = this;
            active_output_.compare_exchange_strong(
                expected,
                nullptr,
                std::memory_order_acq_rel,
                std::memory_order_acquire);
            wait_for_callbacks();

            if (driver_ != nullptr && started_)
            {
                driver_->stop();
                started_ = false;
            }
            wait_for_callbacks();

            if (driver_ != nullptr && buffers_created_)
            {
                driver_->disposeBuffers();
                buffers_created_ = false;
            }
            if (driver_ != nullptr)
            {
                driver_->Release();
                driver_ = nullptr;
            }
            initialized_ = false;

            if (host_window_ != nullptr)
            {
                DestroyWindow(host_window_);
                host_window_ = nullptr;
            }

            scratch_.clear();
            previous_callback_start_ = {};
            restart_requested_.store(
                false,
                std::memory_order_release);
            latencies_changed_.store(
                false,
                std::memory_order_release);
        }

        static void wait_for_callbacks() noexcept
        {
            for (int attempt = 0; attempt < 1000; ++attempt)
            {
                if (active_callbacks_.load(
                        std::memory_order_acquire)
                    == 0)
                {
                    return;
                }
                Sleep(1);
            }
        }

        yokko_audio_output_status failure_status(
            const int32_t error,
            const uint32_t stage) const noexcept
        {
            yokko_audio_output_status status{};
            status.struct_size = sizeof(status);
            status.backend = YOKKO_AUDIO_BACKEND_ASIO;
            status.sample_rate = engine_.sample_rate();
            status.channels = engine_.channels();
            status.backend_error = error;
            status.backend_error_stage = stage;
            return status;
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

        void close_stop_event() noexcept
        {
            if (stop_event_ != nullptr)
            {
                CloseHandle(stop_event_);
                stop_event_ = nullptr;
            }
        }

        static void buffer_switch(
            const long double_buffer_index,
            ASIOBool) noexcept
        {
            CallbackGuard guard;
            if (Impl* output = active_output_.load(
                    std::memory_order_acquire))
            {
                output->render(
                    double_buffer_index,
                    nullptr);
            }
        }

        static ASIOTime* buffer_switch_time_info(
            ASIOTime* time_info,
            const long double_buffer_index,
            ASIOBool) noexcept
        {
            CallbackGuard guard;
            if (Impl* output = active_output_.load(
                    std::memory_order_acquire))
            {
                output->render(
                    double_buffer_index,
                    time_info);
            }
            return time_info;
        }

        static void sample_rate_changed(
            ASIOSampleRate) noexcept
        {
            CallbackGuard guard;
            if (Impl* output = active_output_.load(
                    std::memory_order_acquire))
            {
                output->restart_requested_.store(
                    true,
                    std::memory_order_release);
            }
        }

        static long asio_message(
            const long selector,
            const long value,
            void*,
            double*) noexcept
        {
            CallbackGuard guard;
            Impl* output = active_output_.load(
                std::memory_order_acquire);

            switch (selector)
            {
                case kAsioSelectorSupported:
                    switch (value)
                    {
                        case kAsioResetRequest:
                        case kAsioResyncRequest:
                        case kAsioLatenciesChanged:
                        case kAsioSupportsTimeInfo:
                        case kAsioOverload:
                            return 1;
                        default:
                            return 0;
                    }

                case kAsioEngineVersion:
                    return 2;

                case kAsioSupportsTimeInfo:
                    return 1;

                case kAsioSupportsTimeCode:
                    return 0;

                case kAsioResetRequest:
                case kAsioResyncRequest:
                    if (output != nullptr)
                    {
                        output->restart_requested_.store(
                            true,
                            std::memory_order_release);
                    }
                    return 1;

                case kAsioLatenciesChanged:
                    if (output != nullptr)
                    {
                        output->latencies_changed_.store(
                            true,
                            std::memory_order_release);
                    }
                    return 1;

                case kAsioOverload:
                    if (output != nullptr)
                        output->engine_.report_callback_overload();
                    return 1;

                default:
                    return 0;
            }
        }

        AudioEngine& engine_;
        std::wstring device_id_;
        uint32_t preferred_buffer_frames_ = 128;
        HANDLE stop_event_ = nullptr;
        std::thread thread_;
        std::mutex initialization_mutex_;
        std::condition_variable initialization_changed_;
        bool initialization_complete_ = false;
        yokko_audio_result initialization_result_ =
            YOKKO_AUDIO_INTERNAL_ERROR;
        yokko_audio_output_status initialized_status_{};

        IASIO* driver_ = nullptr;
        HWND host_window_ = nullptr;
        ASIOCallbacks callbacks_{};
        std::array<ASIOBufferInfo, max_output_channels>
            buffer_infos_{};
        std::array<ASIOChannelInfo, max_output_channels>
            channel_infos_{};
        long output_channel_count_ = 0;
        long minimum_buffer_frames_ = 0;
        long maximum_buffer_frames_ = 0;
        long driver_preferred_buffer_frames_ = 0;
        long buffer_granularity_ = 0;
        long buffer_frames_ = 0;
        std::vector<float> scratch_;
        std::atomic<uint32_t> output_latency_frames_{0};
        std::atomic<bool> restart_requested_{false};
        std::atomic<bool> latencies_changed_{false};
        std::atomic_flag rendering_ = ATOMIC_FLAG_INIT;
        bool initialized_ = false;
        bool buffers_created_ = false;
        bool started_ = false;
        bool output_ready_supported_ = false;
        LARGE_INTEGER performance_frequency_{};
        LARGE_INTEGER previous_callback_start_{};
    };

    std::atomic<AsioOutput::Impl*>
        AsioOutput::Impl::active_output_{nullptr};
    std::atomic<uint32_t>
        AsioOutput::Impl::active_callbacks_{0};

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

        const std::vector<AsioDevice> devices =
            enumerate_asio_devices();
        *device_count =
            static_cast<uint32_t>(devices.size());
        return YOKKO_AUDIO_OK;
    }

    yokko_audio_result YOKKO_AUDIO_CALL
        yokko_audio_get_asio_device_info(
            const uint32_t device_index,
            wchar_t* device_id,
            const uint32_t device_id_capacity,
            wchar_t* device_name,
            const uint32_t device_name_capacity,
            uint32_t* is_default)
    {
        if (device_id == nullptr
            || device_id_capacity == 0
            || device_name == nullptr
            || device_name_capacity == 0
            || is_default == nullptr)
        {
            return YOKKO_AUDIO_INVALID_ARGUMENT;
        }

        const std::vector<AsioDevice> devices =
            enumerate_asio_devices();
        if (device_index >= devices.size())
            return YOKKO_AUDIO_INVALID_ARGUMENT;

        const AsioDevice& device = devices[device_index];
        if (!copy_string(
                device.id,
                device_id,
                device_id_capacity)
            || !copy_string(
                device.name,
                device_name,
                device_name_capacity))
        {
            return YOKKO_AUDIO_INVALID_ARGUMENT;
        }

        *is_default = device_index == 0 ? 1u : 0u;
        return YOKKO_AUDIO_OK;
    }
}

#endif
