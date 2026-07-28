using Yokko.Audio.Decoding;
using Yokko.Audio.Native;

namespace Yokko.Audio;

public sealed class NativeAudioEngine : IAudioEngine
{
    private const int outputChannels = 2;
    private const int decodeBlockFrames = 4096;
    private readonly SemaphoreSlim lifecycle = new(1, 1);

    private NativeAudioCore? core;
    private DecodedAudioSource? source;
    private CancellationTokenSource? feederCancellation;
    private Task? feederTask;
    private AudioEngineStartRequest? activeRequest;
    private NativeAudioOutputStatus outputStatus;
    private AudioEngineStatus status = stoppedStatus;
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
    {
        get
        {
            NativeAudioCore? current = core;
            if (current == null)
                return status;

            try
            {
                NativeAudioStatus native = current.GetStatus();
                return status with
                {
                    IsRunning = native.State == NativeAudioState.Running,
                    HasUnderrun = native.UnderrunCount > 0,
                    CallbackCount = native.CallbackCount,
                    CallbackDeadlineMissCount =
                        native.CallbackDeadlineMissCount,
                    CallbackBudgetMilliseconds =
                        native.CallbackBudgetMicroseconds / 1000.0,
                    MaxCallbackDurationMilliseconds =
                        native.CallbackMaxDurationMicroseconds / 1000.0,
                    BackendError = native.BackendError,
                    BackendErrorStage = native.BackendErrorStage,
                };
            }
            catch (ObjectDisposedException)
            {
                return status;
            }
        }
    }

    public double PlaybackTimeMilliseconds
    {
        get
        {
            try
            {
                return core?.GetStatus().PlaybackTimeMilliseconds ?? 0;
            }
            catch (ObjectDisposedException)
            {
                return 0;
            }
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
        if (string.IsNullOrWhiteSpace(request.AudioPath))
            throw new ArgumentException(
                "An audio file path is required.",
                nameof(request));

        string audioPath = Path.GetFullPath(request.AudioPath);
        if (!File.Exists(audioPath))
            throw new FileNotFoundException(
                "Audio file was not found.",
                audioPath);

        await lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            throwIfDisposed();
            await stopCurrentAsync().ConfigureAwait(false);

            source = await Task.Run(
                () => DecodedAudioSource.Open(audioPath),
                cancellationToken).ConfigureAwait(false);
            activeRequest = request with { AudioPath = audioPath };
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
            if (source == null || activeRequest == null)
                return;

            await stopFeederAsync().ConfigureAwait(false);
            core?.CloseOutput();
            core?.Stop();
            core?.Dispose();
            core = null;
            source.CurrentTime =
                TimeSpan.FromMilliseconds(Math.Max(0, timeMilliseconds));
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
        DecodedAudioSource currentSource =
            source ?? throw new InvalidOperationException("Audio source is not open.");
        AudioEngineStartRequest request =
            activeRequest ?? throw new InvalidOperationException("Audio request is missing.");

        uint preferredBufferFrames = (uint)Math.Clamp(
            request.PreferredBufferSize <= 0 ? 128 : request.PreferredBufferSize,
            64,
            2048);
        uint startupFrames = Math.Max(preferredBufferFrames * 2, 256);
        uint ringFrames = Math.Max(preferredBufferFrames * 32, 8192);

        core = new NativeAudioCore(
            (uint)currentSource.SampleRate,
            outputChannels,
            ringFrames,
            startupFrames);

        var primed = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        feederCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        feederTask = Task.Run(
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
        catch (NativeAudioException)
            when (requestedMode == NativeAudioBackendMode.WasapiExclusive)
        {
            outputStatus = core.OpenWasapi(
                NativeAudioBackendMode.WasapiShared,
                request.DeviceId,
                preferredBufferFrames);
            activeBackend = AudioBackendKind.SharedWasapi;
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
        outputStatus = default;
        status = stoppedStatus;
    }

    private void throwIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
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
        0,
        0,
        0,
        0,
        0,
        0);
}
