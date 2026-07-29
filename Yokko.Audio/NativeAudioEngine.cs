using Yokko.Audio.Decoding;
using Yokko.Audio.Native;

namespace Yokko.Audio;

public sealed class NativeAudioEngine : IAudioEngine, IAudioSamplePlayback, IAudioMixControl, IAudioRateControl
{
    private const int outputChannels = 2;
    private const int decodeBlockFrames = 4096;
    private const long maximumPreparedSampleBytes = 256L * 1024 * 1024;
    private readonly SemaphoreSlim lifecycle = new(1, 1);
    private readonly Dictionary<string, DecodedAudioSample> preparedSamples =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, uint> activeSampleIds =
        new(StringComparer.OrdinalIgnoreCase);

    private NativeAudioCore? core;
    private DecodedAudioSource? source;
    private CancellationTokenSource? feederCancellation;
    private Task? feederTask;
    private AudioEngineStartRequest? activeRequest;
    private NativeAudioOutputStatus outputStatus;
    private AudioEngineStatus status = stoppedStatus;
    private double playbackBaseMilliseconds;
    private uint metronomeSampleId;
    private double musicVolume = 1;
    private double hitSoundVolume = 1;
    private double metronomeVolume;
    private readonly object rateClockLock = new();
    private double currentPlaybackRate = 1;
    private double rateClockOutputAnchorMilliseconds;
    private double rateClockSourceAnchorMilliseconds;
    private bool disposed;

    public static bool IsAvailable => NativeAudioLibrary.IsAvailable;

    public static IReadOnlyList<AudioBackendCapabilities> SupportedBackends { get; } =
    [
        new(
            AudioBackendKind.WasapiExclusive,
            true,
            true,
            false,
            "Yokko native event-driven WASAPI exclusive output."),
        new(
            AudioBackendKind.SharedWasapi,
            false,
            false,
            false,
            "Yokko native WASAPI shared fallback."),
        new(
            AudioBackendKind.Asio,
            true,
            true,
            true,
            "Yokko native ASIO output is the next backend.",
            false),
    ];

    public AudioEngineStatus Status
        => Snapshot.Status;

    public AudioEngineSnapshot Snapshot
    {
        get
        {
            NativeAudioCore? current = core;
            if (current == null)
            {
                return new AudioEngineSnapshot(
                    status,
                    playbackBaseMilliseconds);
            }

            try
            {
                NativeAudioStatus native = current.GetStatus();
                return new AudioEngineSnapshot(
                    status with
                    {
                        IsRunning =
                            native.State == NativeAudioState.Running,
                        IsFaulted =
                            native.State == NativeAudioState.Faulted,
                        HasUnderrun = native.UnderrunCount > 0,
                        CallbackCount = native.CallbackCount,
                        CallbackDeadlineMissCount =
                            native.CallbackDeadlineMissCount,
                        CallbackBudgetMilliseconds =
                            native.CallbackBudgetMicroseconds / 1000.0,
                        MaxCallbackDurationMilliseconds =
                            native.CallbackMaxDurationMicroseconds / 1000.0,
                        CallbackCadenceMissCount =
                            native.CallbackCadenceMissCount,
                        MaxCallbackIntervalMilliseconds =
                            native.CallbackMaxIntervalMicroseconds / 1000.0,
                        BackendError = native.BackendError,
                        BackendErrorStage = native.BackendErrorStage,
                    },
                    scaledPlaybackTime(
                        native.PlaybackTimeMilliseconds));
            }
            catch (ObjectDisposedException)
            {
                return new AudioEngineSnapshot(
                    status,
                    playbackBaseMilliseconds);
            }
        }
    }

    public double PlaybackTimeMilliseconds =>
        Snapshot.PlaybackTimeMilliseconds;

    public double DurationMilliseconds =>
        source?.TotalTime.TotalMilliseconds ?? 0;

    public double MusicVolume => musicVolume;

    public double HitSoundVolume => hitSoundVolume;

    public double MetronomeVolume => metronomeVolume;

    public double PlaybackRate
    {
        get
        {
            lock (rateClockLock)
                return currentPlaybackRate;
        }
    }

    public IReadOnlyList<AudioBackendCapabilities> Backends
        => SupportedBackends;

    public async ValueTask<IReadOnlyList<AudioDeviceInfo>> GetOutputDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<NativeWasapiDevice> nativeDevices = await Task.Run(
            NativeWasapiDevices.Enumerate,
            cancellationToken).ConfigureAwait(false);
        return nativeDevices
               .SelectMany(device => new[]
               {
                   new AudioDeviceInfo(
                       device.Id,
                       device.Name,
                       AudioBackendKind.WasapiExclusive,
                       [],
                       [],
                       device.IsDefault),
                   new AudioDeviceInfo(
                       device.Id,
                       device.Name,
                       AudioBackendKind.SharedWasapi,
                       [],
                       [],
                       device.IsDefault),
               })
               .ToArray();
    }

    public async ValueTask StartAsync(
        AudioEngineStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsAvailable)
            throw new PlatformNotSupportedException(
                "Yokko native audio is available only on a built Windows desktop runtime.");
        if (request.PreferredBackend == AudioBackendKind.Asio)
            throw new NotSupportedException(
                "ASIO is not connected yet. Select WASAPI exclusive or shared.");
        if (!double.IsFinite(request.PlaybackRate)
            || request.PlaybackRate is < 0.25 or > 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Playback rate must be finite and between 0.25x and 4x.");
        }
        if (request.FixedFrequencyScale is double frequency
            && (!double.IsFinite(frequency)
                || frequency is < 0.25 or > 4))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Fixed frequency scale must be finite and between 0.25x and 4x.");
        }
        string? audioPath = string.IsNullOrWhiteSpace(request.AudioPath)
            ? null
            : Path.GetFullPath(request.AudioPath);
        if (audioPath != null && !File.Exists(audioPath))
            throw new FileNotFoundException("Audio file was not found.", audioPath);

        await lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            throwIfDisposed();
            await stopCurrentAsync().ConfigureAwait(false);

            source = audioPath == null
                ? null
                : await Task.Run(
                    () => DecodedAudioSource.Open(
                        audioPath,
                        request.PlaybackRate,
                        request.PitchMode,
                        request.DynamicPlaybackRate,
                        request.FixedFrequencyScale),
                    cancellationToken).ConfigureAwait(false);
            activeRequest = request with { AudioPath = audioPath ?? string.Empty };
            await startFromCurrentPositionAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            await stopCurrentAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            lifecycle.Release();
        }
    }

    public async ValueTask PrepareSamplesAsync(
        IReadOnlyCollection<string> samplePaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(samplePaths);
        await lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            throwIfDisposed();
            preparedSamples.Clear();
            activeSampleIds.Clear();
            long preparedBytes = 0;

            foreach (string path in samplePaths
                         .Where(static path => !string.IsNullOrWhiteSpace(path))
                         .Select(Path.GetFullPath)
                         .Where(File.Exists)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    DecodedAudioSample sample = await Task.Run(
                        () => DecodedAudioSample.Decode(path),
                        cancellationToken).ConfigureAwait(false);
                    long sampleBytes =
                        (long)sample.Samples.Length * sizeof(float);
                    if (preparedBytes > maximumPreparedSampleBytes - sampleBytes)
                        break;

                    preparedSamples[path] = sample;
                    preparedBytes += sampleBytes;
                }
                catch (Exception exception)
                    when (exception is not OperationCanceledException)
                {
                    // A missing, corrupt, or unsupported optional keysound must
                    // not prevent the backing track from starting.
                }
            }
        }
        finally
        {
            lifecycle.Release();
        }
    }

    public bool TriggerSample(string samplePath)
    {
        if (string.IsNullOrWhiteSpace(samplePath))
            return false;

        string path;
        try
        {
            path = Path.GetFullPath(samplePath);
        }
        catch
        {
            return false;
        }

        NativeAudioCore? current = core;
        if (current == null
            || !activeSampleIds.TryGetValue(path, out uint sampleId))
            return false;

        try
        {
            return current.TriggerSample(sampleId);
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (NativeAudioException)
        {
            return false;
        }
    }

    public void SetMixVolumes(
        double musicVolume,
        double hitSoundVolume,
        double metronomeVolume)
    {
        validateVolume(musicVolume, nameof(musicVolume));
        validateVolume(hitSoundVolume, nameof(hitSoundVolume));
        validateVolume(metronomeVolume, nameof(metronomeVolume));

        this.musicVolume = musicVolume;
        this.hitSoundVolume = hitSoundVolume;
        this.metronomeVolume = metronomeVolume;
        core?.SetMixVolumes(
            (float)musicVolume,
            (float)hitSoundVolume,
            (float)metronomeVolume);
    }

    public bool TriggerMetronome()
    {
        NativeAudioCore? current = core;
        if (current == null || metronomeSampleId == 0)
            return false;

        try
        {
            return current.TriggerSample(metronomeSampleId);
        }
        catch (Exception exception)
            when (exception is ObjectDisposedException or NativeAudioException)
        {
            return false;
        }
    }

    public void SetPlaybackRate(double playbackRate)
    {
        if (!double.IsFinite(playbackRate)
            || playbackRate is < 0.25 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(playbackRate));
        }
        if (activeRequest?.DynamicPlaybackRate != true)
        {
            throw new InvalidOperationException(
                "The active audio request does not allow dynamic rate changes.");
        }

        double outputTime =
            core?.GetStatus().PlaybackTimeMilliseconds ?? 0;
        lock (rateClockLock)
        {
            rateClockSourceAnchorMilliseconds +=
                (outputTime - rateClockOutputAnchorMilliseconds)
                * currentPlaybackRate;
            rateClockOutputAnchorMilliseconds = outputTime;
            currentPlaybackRate = playbackRate;
        }
        source?.SetPlaybackRate(playbackRate);
        core?.SetSamplePlaybackRate((float)playbackRate);
    }

    public async ValueTask PauseAsync(
        CancellationToken cancellationToken = default)
    {
        await lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (core == null)
                return;

            core.CloseOutput();
            core.Pause();
            status = status with { IsRunning = false };
        }
        finally
        {
            lifecycle.Release();
        }
    }

    public async ValueTask SeekAsync(
        double timeMilliseconds,
        CancellationToken cancellationToken = default)
    {
        await lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (activeRequest == null)
                return;

            await stopFeederAsync().ConfigureAwait(false);
            core?.CloseOutput();
            core?.Stop();
            core?.Dispose();
            core = null;
            if (source != null)
            {
                source.CurrentTime =
                    TimeSpan.FromMilliseconds(Math.Max(0, timeMilliseconds));
            }
            else
            {
                playbackBaseMilliseconds = Math.Max(0, timeMilliseconds);
            }
            await startFromCurrentPositionAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            lifecycle.Release();
        }
    }

    public async ValueTask StopAsync(
        CancellationToken cancellationToken = default)
    {
        await lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await stopCurrentAsync().ConfigureAwait(false);
        }
        finally
        {
            lifecycle.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

        await lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed)
                return;

            disposed = true;
            await stopCurrentAsync().ConfigureAwait(false);
        }
        finally
        {
            lifecycle.Release();
        }
    }

    private async Task startFromCurrentPositionAsync(
        CancellationToken cancellationToken)
    {
        AudioEngineStartRequest request =
            activeRequest ?? throw new InvalidOperationException("Audio request is missing.");
        DecodedAudioSource? currentSource = source;
        int sampleRate = currentSource?.SampleRate
                         ?? (request.PreferredSampleRate > 0
                             ? request.PreferredSampleRate
                             : 48000);
        if (currentSource != null)
        {
            playbackBaseMilliseconds =
                currentSource.CurrentTime.TotalMilliseconds;
        }
        lock (rateClockLock)
        {
            currentPlaybackRate = request.PlaybackRate;
            rateClockOutputAnchorMilliseconds = 0;
            rateClockSourceAnchorMilliseconds =
                playbackBaseMilliseconds;
        }

        uint preferredBufferFrames = (uint)Math.Clamp(
            request.PreferredBufferSize <= 0 ? 128 : request.PreferredBufferSize,
            64,
            2048);
        uint startupFrames = Math.Max(preferredBufferFrames * 2, 256);
        uint ringFrames = request.DynamicPlaybackRate
            ? Math.Max(preferredBufferFrames * 4, 1024)
            : Math.Max(preferredBufferFrames * 32, 8192);

        core = new NativeAudioCore(
            (uint)sampleRate,
            outputChannels,
            ringFrames,
            startupFrames);
        core.SetMixVolumes(
            (float)musicVolume,
            (float)hitSoundVolume,
            (float)metronomeVolume);
        core.SetSamplePlaybackRate(
            request.DynamicPlaybackRate
                ? (float)request.PlaybackRate
                : 1);
        metronomeSampleId = core.RegisterMetronomeSample(
            createMetronomeClick(sampleRate));
        activeSampleIds.Clear();
        foreach ((string path, DecodedAudioSample sample) in preparedSamples)
        {
            // osu! applies fixed rate Mods to gameplay samples as frequency
            // changes, so DT and NC keysounds both become shorter and higher.
            float[] pcm = sample.GetSamplesAt(
                sampleRate,
                request.DynamicPlaybackRate
                    ? 1
                    : request.PlaybackRate);
            activeSampleIds[path] = core.RegisterSample(pcm);
        }

        var primed = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        feederCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        feederTask = currentSource == null
            ? Task.Run(
                () => feedSilenceAsync(
                    core,
                    primed,
                    feederCancellation.Token),
                CancellationToken.None)
            : Task.Run(
                () => feedPcmAsync(
                    currentSource,
                    core,
                    primed,
                    feederCancellation.Token),
                CancellationToken.None);

        await primed.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        core.Start();

        NativeAudioBackendMode requestedMode =
            request.PreferredBackend == AudioBackendKind.SharedWasapi
                ? NativeAudioBackendMode.WasapiShared
                : NativeAudioBackendMode.WasapiExclusive;
        AudioBackendKind activeBackend;
        try
        {
            outputStatus = core.OpenWasapi(
                requestedMode,
                request.DeviceId,
                preferredBufferFrames);
            activeBackend =
                outputStatus.Backend == NativeAudioBackendMode.WasapiExclusive
                    ? AudioBackendKind.WasapiExclusive
                    : AudioBackendKind.SharedWasapi;
        }
        catch (NativeAudioException exclusiveFailure)
            when (requestedMode == NativeAudioBackendMode.WasapiExclusive)
        {
            try
            {
                outputStatus = core.OpenWasapi(
                    NativeAudioBackendMode.WasapiShared,
                    request.DeviceId,
                    preferredBufferFrames);
                activeBackend = AudioBackendKind.SharedWasapi;
            }
            catch (NativeAudioException sharedFailure)
            {
                throw new NativeAudioException(
                    "WASAPI Exclusive and Shared fallback both failed. "
                    + $"Exclusive: {exclusiveFailure.Message} "
                    + $"Shared: {sharedFailure.Message}",
                    sharedFailure);
            }
        }

        status = new AudioEngineStatus(
            activeBackend,
            string.IsNullOrWhiteSpace(request.DeviceId)
                ? "Default Windows output"
                : request.DeviceId,
            (int)outputStatus.SampleRate,
            (int)outputStatus.BufferFrames,
            outputStatus.SampleRate == 0
                ? 0
                : outputStatus.LatencyFrames * 1000.0
                  / outputStatus.SampleRate,
            activeBackend == AudioBackendKind.WasapiExclusive,
            true,
            false,
            false,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0);
    }

    private static async Task feedPcmAsync(
        DecodedAudioSource source,
        NativeAudioCore core,
        TaskCompletionSource primed,
        CancellationToken cancellationToken)
    {
        var samples = new float[decodeBlockFrames * outputChannels];
        int sampleCount = 0;
        int sampleOffset = 0;
        bool reachedEnd = false;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (sampleOffset >= sampleCount)
                {
                    sampleCount = source.Read(samples);
                    sampleOffset = 0;
                    if (sampleCount == 0)
                    {
                        reachedEnd = true;
                        if (core.GetStatus().State == NativeAudioState.Primed)
                            primed.TrySetResult();
                        else
                        {
                            Array.Clear(samples);
                            sampleCount = samples.Length;
                        }
                    }
                }

                uint acceptedFrames = core.Submit(
                    samples.AsSpan(sampleOffset, sampleCount - sampleOffset));
                sampleOffset += checked((int)acceptedFrames * outputChannels);

                if (core.GetStatus().State == NativeAudioState.Primed)
                    primed.TrySetResult();

                if (reachedEnd && sampleOffset >= sampleCount)
                    return;
                if (acceptedFrames == 0)
                    await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            primed.TrySetCanceled(cancellationToken);
        }
        catch (Exception exception)
        {
            primed.TrySetException(exception);
            throw;
        }
    }

    private static async Task feedSilenceAsync(
        NativeAudioCore core,
        TaskCompletionSource primed,
        CancellationToken cancellationToken)
    {
        var samples = new float[decodeBlockFrames * outputChannels];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                uint acceptedFrames = core.Submit(samples);
                if (core.GetStatus().State == NativeAudioState.Primed)
                    primed.TrySetResult();
                if (acceptedFrames == 0)
                    await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            primed.TrySetCanceled(cancellationToken);
        }
        catch (Exception exception)
        {
            primed.TrySetException(exception);
            throw;
        }
    }

    private async Task stopFeederAsync()
    {
        CancellationTokenSource? cancellation = feederCancellation;
        Task? task = feederTask;
        feederCancellation = null;
        feederTask = null;

        if (cancellation != null)
        {
            cancellation.Cancel();
            if (task != null)
            {
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
            cancellation.Dispose();
        }
    }

    private async Task stopCurrentAsync()
    {
        await stopFeederAsync().ConfigureAwait(false);
        if (core != null)
        {
            core.CloseOutput();
            core.Stop();
            core.Dispose();
            core = null;
        }

        source?.Dispose();
        source = null;
        activeRequest = null;
        activeSampleIds.Clear();
        metronomeSampleId = 0;
        outputStatus = default;
        playbackBaseMilliseconds = 0;
        status = stoppedStatus;
        lock (rateClockLock)
        {
            currentPlaybackRate = 1;
            rateClockOutputAnchorMilliseconds = 0;
            rateClockSourceAnchorMilliseconds = 0;
        }
    }

    private void throwIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private static float[] createMetronomeClick(int sampleRate)
    {
        int frameCount = Math.Max(1, sampleRate / 50);
        var samples = new float[frameCount * outputChannels];
        for (int frame = 0; frame < frameCount; frame++)
        {
            double progress = frame / (double)frameCount;
            float sample = (float)(
                Math.Sin(2 * Math.PI * 1760 * frame / sampleRate)
                * Math.Pow(1 - progress, 4)
                * 0.32);
            samples[frame * outputChannels] = sample;
            samples[frame * outputChannels + 1] = sample;
        }

        return samples;
    }

    private static void validateVolume(double volume, string name)
    {
        if (!double.IsFinite(volume) || volume is < 0 or > 1)
            throw new ArgumentOutOfRangeException(name);
    }

    internal static double ScalePlaybackTime(
        double baseTimeMilliseconds,
        double outputTimeMilliseconds,
        double playbackRate)
    {
        if (!double.IsFinite(baseTimeMilliseconds)
            || !double.IsFinite(outputTimeMilliseconds)
            || !double.IsFinite(playbackRate)
            || playbackRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(playbackRate));
        }

        return baseTimeMilliseconds
               + outputTimeMilliseconds * playbackRate;
    }

    private double scaledPlaybackTime(double outputTimeMilliseconds)
    {
        AudioEngineStartRequest? request = activeRequest;
        if (request?.DynamicPlaybackRate != true)
        {
            return ScalePlaybackTime(
                playbackBaseMilliseconds,
                outputTimeMilliseconds,
                request?.PlaybackRate ?? 1);
        }

        lock (rateClockLock)
        {
            return rateClockSourceAnchorMilliseconds
                   + (outputTimeMilliseconds
                      - rateClockOutputAnchorMilliseconds)
                   * currentPlaybackRate;
        }
    }

    private static readonly AudioEngineStatus stoppedStatus = new(
        AudioBackendKind.Fallback,
        null,
        0,
        0,
        0,
        false,
        false,
        false,
        false,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0);
}
