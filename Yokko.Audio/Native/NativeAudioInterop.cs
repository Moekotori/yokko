using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Yokko.Audio.Native;

internal static partial class NativeAudioInterop
{
    internal const uint AbiVersion = 10;
    internal const string LibraryName = "yokko_audio_native";

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_get_abi_version")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial uint GetAbiVersion();

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_create")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeAudioResult Create(
        in NativeAudioConfig config,
        out nint engine);

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_destroy")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial void Destroy(nint engine);

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_start")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeAudioResult Start(NativeAudioSafeHandle engine);

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_pause")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeAudioResult Pause(NativeAudioSafeHandle engine);

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_stop")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeAudioResult Stop(NativeAudioSafeHandle engine);

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_submit_interleaved_f32")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static unsafe partial NativeAudioResult SubmitInterleavedFloat32(
        NativeAudioSafeHandle engine,
        float* samples,
        uint frameCount,
        out uint acceptedFrames);

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_register_sample_f32")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static unsafe partial NativeAudioResult RegisterSampleFloat32(
        NativeAudioSafeHandle engine,
        float* samples,
        uint frameCount,
        out uint sampleId);

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_register_metronome_sample_f32")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static unsafe partial NativeAudioResult RegisterMetronomeSampleFloat32(
        NativeAudioSafeHandle engine,
        float* samples,
        uint frameCount,
        out uint sampleId);

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_set_mix_volumes")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeAudioResult SetMixVolumes(
        NativeAudioSafeHandle engine,
        float musicVolume,
        float hitSoundVolume,
        float metronomeVolume);

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_set_sample_playback_rate")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeAudioResult SetSamplePlaybackRate(
        NativeAudioSafeHandle engine,
        float playbackRate);

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_trigger_sample")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeAudioResult TriggerSample(
        NativeAudioSafeHandle engine,
        uint sampleId);

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_trigger_sample_with_gain")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeAudioResult TriggerSampleWithGain(
        NativeAudioSafeHandle engine,
        uint sampleId,
        float gain);

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_start_looping_sample")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeAudioResult StartLoopingSample(
        NativeAudioSafeHandle engine,
        uint sampleId,
        float gain,
        out uint loopId);

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_stop_looping_sample")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeAudioResult StopLoopingSample(
        NativeAudioSafeHandle engine,
        uint loopId);

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_get_status")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeAudioResult GetStatus(
        NativeAudioSafeHandle engine,
        ref NativeAudioStatus status);

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_open_wasapi")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeAudioResult OpenWasapi(
        NativeAudioSafeHandle engine,
        in NativeAudioOutputConfig config,
        ref NativeAudioOutputStatus status);

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_open_asio")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeAudioResult OpenAsio(
        NativeAudioSafeHandle engine,
        in NativeAudioOutputConfig config,
        ref NativeAudioOutputStatus status);

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_close_output")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial void CloseOutput(NativeAudioSafeHandle engine);

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_get_wasapi_device_count")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeAudioResult GetWasapiDeviceCount(
        out uint deviceCount);

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_get_wasapi_device_info")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static unsafe partial NativeAudioResult GetWasapiDeviceInfo(
        uint deviceIndex,
        char* deviceId,
        uint deviceIdCapacity,
        char* deviceName,
        uint deviceNameCapacity,
        out uint isDefault);

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_get_asio_device_count")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeAudioResult GetAsioDeviceCount(
        out uint deviceCount);

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_get_asio_device_info")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static unsafe partial NativeAudioResult GetAsioDeviceInfo(
        uint deviceIndex,
        char* deviceId,
        uint deviceIdCapacity,
        char* deviceName,
        uint deviceNameCapacity,
        out uint isDefault);
}

internal sealed class NativeAudioSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal NativeAudioSafeHandle(nint handle)
        : base(true)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
        NativeAudioInterop.Destroy(handle);
        return true;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct NativeAudioConfig
{
    internal NativeAudioConfig(
        uint sampleRate,
        uint channels,
        uint ringCapacityFrames,
        uint startupThresholdFrames)
    {
        StructSize = (uint)Marshal.SizeOf<NativeAudioConfig>();
        SampleRate = sampleRate;
        Channels = channels;
        RingCapacityFrames = ringCapacityFrames;
        StartupThresholdFrames = startupThresholdFrames;
    }

    internal readonly uint StructSize;
    internal readonly uint SampleRate;
    internal readonly uint Channels;
    internal readonly uint RingCapacityFrames;
    internal readonly uint StartupThresholdFrames;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeAudioStatus
{
    internal uint StructSize;
    internal uint AbiVersion;
    internal NativeAudioState State;
    internal uint SampleRate;
    internal uint Channels;
    internal uint RingCapacityFrames;
    internal uint BufferedFrames;
    internal uint DeviceLatencyFrames;
    internal ulong SubmittedFrames;
    internal ulong SourceFramesRendered;
    internal ulong DeviceFramesRendered;
    internal ulong UnderrunCount;
    internal ulong CallbackCount;
    internal ulong CallbackDeadlineMissCount;
    internal uint CallbackBudgetMicroseconds;
    internal uint CallbackMaxDurationMicroseconds;
    internal int BackendError;
    internal uint BackendErrorStage;
    internal double PlaybackTimeMilliseconds;
    internal ulong CallbackCadenceMissCount;
    internal uint CallbackMaxIntervalMicroseconds;
    internal ulong BackendOverloadCount;

    internal static NativeAudioStatus Create()
        => new()
        {
            StructSize = (uint)Marshal.SizeOf<NativeAudioStatus>(),
        };
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct NativeAudioOutputConfig
{
    internal NativeAudioOutputConfig(
        NativeAudioBackendMode backend,
        nint deviceId,
        uint preferredBufferFrames)
    {
        StructSize = (uint)Marshal.SizeOf<NativeAudioOutputConfig>();
        Backend = backend;
        DeviceId = deviceId;
        PreferredBufferFrames = preferredBufferFrames;
    }

    internal readonly uint StructSize;
    internal readonly NativeAudioBackendMode Backend;
    internal readonly nint DeviceId;
    internal readonly uint PreferredBufferFrames;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeAudioOutputStatus
{
    internal uint StructSize;
    internal NativeAudioBackendMode Backend;
    internal NativeAudioSampleFormat SampleFormat;
    internal uint SampleRate;
    internal uint Channels;
    internal uint BufferFrames;
    internal uint LatencyFrames;
    internal uint IsActive;
    internal int BackendError;
    internal uint BackendErrorStage;
    internal uint PeriodFrames;
    internal uint SharedExplicitPeriod;
    internal int SharedExplicitPeriodError;

    internal static NativeAudioOutputStatus Create()
        => new()
        {
            StructSize = (uint)Marshal.SizeOf<NativeAudioOutputStatus>(),
        };
}

internal enum NativeAudioResult
{
    Ok = 0,
    InvalidArgument = 1,
    InvalidState = 2,
    NotReady = 3,
    OutOfMemory = 4,
    InternalError = 5,
    BackendUnavailable = 6,
    QueueFull = 7,
}

internal enum NativeAudioState
{
    Idle = 0,
    Primed = 1,
    Running = 2,
    Paused = 3,
    Faulted = 4,
}

internal enum NativeAudioBackendMode
{
    WasapiShared = 1,
    WasapiExclusive = 2,
    Asio = 3,
}

internal enum NativeAudioSampleFormat
{
    Float32 = 1,
    Pcm32 = 2,
    Pcm24In32 = 3,
    Pcm16 = 4,
}
