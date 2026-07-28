namespace Yokko.Core.Timing;

/// <summary>
/// A change to the rate at which chart time is mapped onto visual scroll
/// distance. Zero and negative multipliers are valid.
/// </summary>
public sealed record YokkoScrollVelocity
{
    public YokkoScrollVelocity(double timeMilliseconds, double multiplier)
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
