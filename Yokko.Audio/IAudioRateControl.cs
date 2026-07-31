namespace Yokko.Audio;

public interface IAudioRateControl
{
    double PlaybackRate { get; }

    /// <summary>
    /// Changes the streamed song rate. One-shot gameplay samples keep their
    /// original playback rate.
    /// </summary>
    void SetPlaybackRate(double playbackRate);
}
