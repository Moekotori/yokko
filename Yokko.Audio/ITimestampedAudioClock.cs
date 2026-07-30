namespace Yokko.Audio;

/// <summary>
/// Maps monotonic input timestamps onto the authoritative playback timeline.
/// </summary>
public interface ITimestampedAudioClock
{
    bool TryGetPlaybackTimeAtTimestamp(
        AudioEngineSnapshot snapshot,
        long timestamp,
        long timestampFrequency,
        out double playbackTimeMilliseconds);
}
