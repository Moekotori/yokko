namespace Yokko.Core.Timing;

/// <summary>
/// A Quaver-style keyframe that scales the whole scroll field at the current
/// playback time. Values interpolate linearly until the next keyframe.
/// </summary>
public sealed record YokkoScrollSpeedFactor
{
    public YokkoScrollSpeedFactor(double timeMilliseconds, double multiplier)
    {
        if (!double.IsFinite(timeMilliseconds))
            throw new ArgumentOutOfRangeException(nameof(timeMilliseconds));

        if (!double.IsFinite(multiplier))
            throw new ArgumentOutOfRangeException(nameof(multiplier));

        TimeMilliseconds = timeMilliseconds;
        Multiplier = multiplier;
    }

    public double TimeMilliseconds { get; }

    public double Multiplier { get; }
}
