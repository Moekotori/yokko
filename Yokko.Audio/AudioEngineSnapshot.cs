namespace Yokko.Audio;

/// <summary>
/// A coherent, allocation-free observation of audio state and playback time.
/// </summary>
public readonly record struct AudioEngineSnapshot(
    AudioEngineStatus Status,
    double PlaybackTimeMilliseconds,
    AudioClockCorrelation ClockCorrelation = default);
