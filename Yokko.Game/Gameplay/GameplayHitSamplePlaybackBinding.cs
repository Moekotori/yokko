using Yokko.Audio;

namespace Yokko.Game.Gameplay;

internal readonly record struct GameplayHitSamplePlaybackBinding(
    string Path,
    double Gain,
    PreparedAudioSampleHandle PreparedHandle,
    bool HasPreparedHandle)
{
    internal GameplayHitSamplePlaybackBinding(
        ResolvedGameplayHitSample sample)
        : this(sample.Path, sample.Gain, default, false)
    {
    }

    internal GameplayHitSamplePlaybackBinding WithPreparedHandle(
        PreparedAudioSampleHandle handle) =>
        this with
        {
            PreparedHandle = handle,
            HasPreparedHandle = true,
        };
}
