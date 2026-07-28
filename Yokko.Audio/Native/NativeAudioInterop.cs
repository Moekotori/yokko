using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Yokko.Audio.Native;

internal static partial class NativeAudioInterop
{
    internal const uint AbiVersion = 1;
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

    [LibraryImport(LibraryName, EntryPoint = "yokko_audio_get_status")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial NativeAudioResult GetStatus(
        NativeAudioSafeHandle engine,
        ref NativeAudioStatus status);
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
    internal double PlaybackTimeMilliseconds;

    internal static NativeAudioStatus Create()
        => new()
        {
            StructSize = (uint)Marshal.SizeOf<NativeAudioStatus>(),
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
}

internal enum NativeAudioState
{
    Idle = 0,
    Primed = 1,
    Running = 2,
    Paused = 3,
    Faulted = 4,
}
