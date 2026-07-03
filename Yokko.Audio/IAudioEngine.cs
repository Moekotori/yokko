namespace Yokko.Audio;

public interface IAudioEngine : IAsyncDisposable
{
    AudioEngineStatus Status { get; }

    double PlaybackTimeMilliseconds { get; }

    IReadOnlyList<AudioBackendCapabilities> Backends { get; }

    ValueTask<IReadOnlyList<AudioDeviceInfo>> GetOutputDevicesAsync(CancellationToken cancellationToken = default);

    ValueTask StartAsync(AudioEngineStartRequest request, CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
