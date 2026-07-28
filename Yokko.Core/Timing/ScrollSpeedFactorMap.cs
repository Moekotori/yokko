namespace Yokko.Core.Timing;

/// <summary>
/// Evaluates linearly interpolated scroll-speed-factor keyframes.
/// </summary>
public sealed class ScrollSpeedFactorMap
{
    private readonly YokkoScrollSpeedFactor[] factors;

    public ScrollSpeedFactorMap(
        IEnumerable<YokkoScrollSpeedFactor>? scrollSpeedFactors)
    {
        factors = scrollSpeedFactors?
                  .OrderBy(static factor => factor.TimeMilliseconds)
                  .GroupBy(static factor => factor.TimeMilliseconds)
                  .Select(static group => group.Last())
                  .ToArray()
                  ?? [];
        ScrollSpeedFactors = factors;
    }

    public IReadOnlyList<YokkoScrollSpeedFactor> ScrollSpeedFactors { get; }

    public double FactorAt(double timeMilliseconds)
    {
        if (!double.IsFinite(timeMilliseconds))
            throw new ArgumentOutOfRangeException(nameof(timeMilliseconds));

        int index = factorIndexAt(timeMilliseconds);

        if (index < 0)
            return 1;

        YokkoScrollSpeedFactor current = factors[index];

        if (index == factors.Length - 1)
            return current.Multiplier;

        YokkoScrollSpeedFactor next = factors[index + 1];
        double progress = (timeMilliseconds - current.TimeMilliseconds)
                          / (next.TimeMilliseconds - current.TimeMilliseconds);
        return current.Multiplier
               + (next.Multiplier - current.Multiplier) * progress;
    }

    private int factorIndexAt(double timeMilliseconds)
    {
        int low = 0;
        int high = factors.Length - 1;
        int result = -1;

        while (low <= high)
        {
            int middle = low + (high - low) / 2;

            if (factors[middle].TimeMilliseconds <= timeMilliseconds)
            {
                result = middle;
                low = middle + 1;
            }
            else
                high = middle - 1;
        }

        return result;
    }
}
