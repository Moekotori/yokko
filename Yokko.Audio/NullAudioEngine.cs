namespace Yokko.Audio;

public sealed class NullAudioEngine : IAudioEngine, IAudioMixControl, IAudioRateControl
{
    public double PlaybackRate { get; private set; } = 1;

    public double MusicVolume { get; private set; } = 1;

    public double HitSoundVolume { get; private set; } = 1;

    public double MetronomeVolume { get; private set; }

    public int MetronomeTriggerCount { get; private set; }

    public AudioEngineStatus Status { get; } = new(
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

    public double PlaybackTimeMilliseconds => 0;

    public double DurationMilliseconds => 0;

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

    public void SetMixVolumes(
        double musicVolume,
        double hitSoundVolume,
        double metronomeVolume)
    {
        validateVolume(musicVolume, nameof(musicVolume));
        validateVolume(hitSoundVolume, nameof(hitSoundVolume));
        validateVolume(metronomeVolume, nameof(metronomeVolume));
        MusicVolume = musicVolume;
        HitSoundVolume = hitSoundVolume;
        MetronomeVolume = metronomeVolume;
    }

    public bool TriggerMetronome()
    {
        MetronomeTriggerCount++;
        return true;
    }

    public void SetPlaybackRate(double playbackRate)
    {
        if (!double.IsFinite(playbackRate)
            || playbackRate is < 0.25 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(playbackRate));
        }
        PlaybackRate = playbackRate;
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    private static void validateVolume(double volume, string name)
    {
        if (!double.IsFinite(volume) || volume is < 0 or > 1)
            throw new ArgumentOutOfRangeException(name);
    }
}
