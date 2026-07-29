namespace Yokko.Audio;

public interface IAudioRateControl
{
    double PlaybackRate { get; }

    void SetPlaybackRate(double playbackRate);
}
