using System.Runtime.InteropServices;

namespace Yokko.Audio.Native;

internal sealed class NativeAudioCore : IDisposable
{
    private readonly uint channels;
    private NativeAudioSafeHandle? handle;

    internal NativeAudioCore(
        uint sampleRate,
        uint channels,
        uint ringCapacityFrames,
        uint startupThresholdFrames)
    {
        NativeAudioLibrary.EnsureLoaded();
        if (NativeAudioInterop.GetAbiVersion() != NativeAudioInterop.AbiVersion)
            throw new NativeAudioException("The native audio ABI version is incompatible.");

        var config = new NativeAudioConfig(
            sampleRate,
            channels,
            ringCapacityFrames,
            startupThresholdFrames);
        throwForResult(
            NativeAudioInterop.Create(config, out nint nativeHandle),
            "create");

        this.channels = channels;
        handle = new NativeAudioSafeHandle(nativeHandle);
    }

    internal void Start()
        => throwForResult(NativeAudioInterop.Start(getHandle()), "start");

    internal void Pause()
        => throwForResult(NativeAudioInterop.Pause(getHandle()), "pause");

    internal void Stop()
        => throwForResult(NativeAudioInterop.Stop(getHandle()), "stop");

    internal unsafe uint Submit(ReadOnlySpan<float> interleavedSamples)
    {
        if (interleavedSamples.Length == 0)
            return 0;

        if (channels == 0 || interleavedSamples.Length % channels != 0)
            throw new ArgumentException(
                "PCM samples must contain complete interleaved frames.",
                nameof(interleavedSamples));

        fixed (float* samples = interleavedSamples)
        {
            throwForResult(
                NativeAudioInterop.SubmitInterleavedFloat32(
                    getHandle(),
                    samples,
                    (uint)(interleavedSamples.Length / channels),
                    out uint acceptedFrames),
                "submit PCM");
            return acceptedFrames;
        }
    }

    internal unsafe uint RegisterSample(ReadOnlySpan<float> interleavedSamples)
    {
        if (interleavedSamples.Length == 0
            || channels == 0
            || interleavedSamples.Length % channels != 0)
        {
            throw new ArgumentException(
                "Sample PCM must contain complete interleaved frames.",
                nameof(interleavedSamples));
        }

        fixed (float* samples = interleavedSamples)
        {
            throwForResult(
                NativeAudioInterop.RegisterSampleFloat32(
                    getHandle(),
                    samples,
                    (uint)(interleavedSamples.Length / channels),
                    out uint sampleId),
                "register sample");
            return sampleId;
        }
    }

    internal unsafe uint RegisterMetronomeSample(ReadOnlySpan<float> interleavedSamples)
    {
        if (interleavedSamples.Length == 0
            || channels == 0
            || interleavedSamples.Length % channels != 0)
        {
            throw new ArgumentException(
                "Metronome PCM must contain complete interleaved frames.",
                nameof(interleavedSamples));
        }

        fixed (float* samples = interleavedSamples)
        {
            throwForResult(
                NativeAudioInterop.RegisterMetronomeSampleFloat32(
                    getHandle(),
                    samples,
                    (uint)(interleavedSamples.Length / channels),
                    out uint sampleId),
                "register metronome sample");
            return sampleId;
        }
    }

    internal void SetMixVolumes(float music, float hitSounds, float metronome)
        => throwForResult(
            NativeAudioInterop.SetMixVolumes(
                getHandle(),
                music,
                hitSounds,
                metronome),
            "set mix volumes");

    internal void SetSamplePlaybackRate(float playbackRate)
        => throwForResult(
            NativeAudioInterop.SetSamplePlaybackRate(
                getHandle(),
                playbackRate),
            "set sample playback rate");

    internal bool TriggerSample(uint sampleId, float gain = 1)
    {
        NativeAudioResult result =
            NativeAudioInterop.TriggerSampleWithGain(
                getHandle(),
                sampleId,
                gain);
        if (result is NativeAudioResult.NotReady
            or NativeAudioResult.QueueFull)
            return false;

        throwForResult(result, "trigger sample");
        return true;
    }

    internal uint StartLoopingSample(uint sampleId, float gain)
    {
        NativeAudioResult result =
            NativeAudioInterop.StartLoopingSample(
                getHandle(),
                sampleId,
                gain,
                out uint loopId);
        if (result is NativeAudioResult.NotReady
            or NativeAudioResult.QueueFull)
            return 0;

        throwForResult(result, "start looping sample");
        return loopId;
    }

    internal bool StopLoopingSample(uint loopId)
    {
        NativeAudioResult result =
            NativeAudioInterop.StopLoopingSample(
                getHandle(),
                loopId);
        if (result is NativeAudioResult.NotReady
            or NativeAudioResult.QueueFull)
            return false;

        throwForResult(result, "stop looping sample");
        return true;
    }

    internal void ReportPresentedPosition(
        ulong presentedFramePosition,
        uint outputLatencyFrames,
        ulong observationTime100ns) =>
        throwForResult(
            NativeAudioInterop.ReportPresentedPosition(
                getHandle(),
                presentedFramePosition,
                outputLatencyFrames,
                observationTime100ns),
            "report presented position");

    internal NativeAudioStatus GetStatus()
    {
        NativeAudioStatus status = NativeAudioStatus.Create();
        throwForResult(
            NativeAudioInterop.GetStatus(getHandle(), ref status),
            "get status");
        return status;
    }

    internal NativeAudioOutputStatus OpenWasapi(
        NativeAudioBackendMode backend,
        string? deviceId,
        uint preferredBufferFrames)
    {
        nint nativeDeviceId = 0;
        try
        {
            if (!string.IsNullOrWhiteSpace(deviceId))
                nativeDeviceId = Marshal.StringToCoTaskMemUni(deviceId);

            var config = new NativeAudioOutputConfig(
                backend,
                nativeDeviceId,
                preferredBufferFrames);
            NativeAudioOutputStatus status = NativeAudioOutputStatus.Create();
            NativeAudioResult result = NativeAudioInterop.OpenWasapi(
                getHandle(),
                config,
                ref status);
            if (result != NativeAudioResult.Ok)
            {
                throw new NativeAudioException(
                    $"Native audio operation 'open {backend}' failed with {result} "
                    + $"(HRESULT 0x{status.BackendError:X8}, stage {status.BackendErrorStage}).");
            }
            return status;
        }
        finally
        {
            if (nativeDeviceId != 0)
                Marshal.FreeCoTaskMem(nativeDeviceId);
        }
    }

    internal NativeAudioOutputStatus OpenAsio(
        string? deviceId,
        uint preferredBufferFrames)
    {
        nint nativeDeviceId = 0;
        try
        {
            if (!string.IsNullOrWhiteSpace(deviceId))
                nativeDeviceId = Marshal.StringToCoTaskMemUni(deviceId);

            var config = new NativeAudioOutputConfig(
                NativeAudioBackendMode.Asio,
                nativeDeviceId,
                preferredBufferFrames);
            NativeAudioOutputStatus status =
                NativeAudioOutputStatus.Create();
            NativeAudioResult result = NativeAudioInterop.OpenAsio(
                getHandle(),
                config,
                ref status);
            if (result != NativeAudioResult.Ok)
            {
                throw new NativeAudioException(
                    $"Native audio operation 'open ASIO' failed with {result} "
                    + $"(driver error 0x{status.BackendError:X8}, "
                    + $"stage {status.BackendErrorStage}).");
            }
            return status;
        }
        finally
        {
            if (nativeDeviceId != 0)
                Marshal.FreeCoTaskMem(nativeDeviceId);
        }
    }

    internal void CloseOutput()
        => NativeAudioInterop.CloseOutput(getHandle());

    public void Dispose()
    {
        NativeAudioSafeHandle? current = Interlocked.Exchange(ref handle, null);
        current?.Dispose();
    }

    private NativeAudioSafeHandle getHandle()
        => handle is { IsClosed: false, IsInvalid: false } current
            ? current
            : throw new ObjectDisposedException(nameof(NativeAudioCore));

    private static void throwForResult(
        NativeAudioResult result,
        string operation)
    {
        if (result != NativeAudioResult.Ok)
            throw new NativeAudioException(
                $"Native audio operation '{operation}' failed with {result}.");
    }
}

internal sealed class NativeAudioException : Exception
{
    internal NativeAudioException(string message)
        : base(message)
    {
    }

    internal NativeAudioException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
