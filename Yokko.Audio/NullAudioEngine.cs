namespace Yokko.Audio;

public sealed class NullAudioEngine : IAudioEngine
{
    public AudioEngineStatus Status { get; } = new(
        AudioBackendKind.Fallback,
        null,
        0,
        0,
        0,
        false,
        false,
        false);

    public double PlaybackTimeMilliseconds => 0;

    public IReadOnlyList<AudioBackendCapabilities> Backends { get; } =
    [
        new(AudioBackendKind.SharedWasapi, false, false, false, "Default Windows shared playback path."),
        new(AudioBackendKind.WasapiExclusive, true, true, false, "Exclusive Windows endpoint access for lower latency."),
        new(AudioBackendKind.Asio, true, true, true, "Pro-audio driver path for devices with native ASIO support."),
    ];

    public ValueTask<IReadOnlyList<AudioDeviceInfo>> GetOutputDevicesAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyList<AudioDeviceInfo>>([]);

    public ValueTask StartAsync(AudioEngineStartRequest request, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask PauseAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask SeekAsync(double timeMilliseconds, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}
