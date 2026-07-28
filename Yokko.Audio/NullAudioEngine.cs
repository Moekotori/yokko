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
        false,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0);

    public double PlaybackTimeMilliseconds => 0;

    public IReadOnlyList<AudioBackendCapabilities> Backends { get; } =
    [
        new(AudioBackendKind.Fallback, false, false, false, "No native audio backend is available.", false),
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
