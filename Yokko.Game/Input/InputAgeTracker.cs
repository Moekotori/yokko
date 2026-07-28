using System;

namespace Yokko.Game.Input;

internal readonly record struct InputAgeStatistics(
    int Count,
    int RawInputCount,
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds);

internal sealed class InputAgeTracker
{
    private const int capacity = 256;

    private readonly double[] samples = new double[capacity];
    private readonly KeyInputTimestampKind[] kinds =
        new KeyInputTimestampKind[capacity];
    private int count;
    private int nextIndex;

    public void Record(double ageMilliseconds, KeyInputTimestampKind kind)
    {
        if (!double.IsFinite(ageMilliseconds)
            || ageMilliseconds < 0
            || kind == KeyInputTimestampKind.None)
            return;

        samples[nextIndex] = ageMilliseconds;
        kinds[nextIndex] = kind;
        nextIndex = (nextIndex + 1) % capacity;
        count = Math.Min(count + 1, capacity);
    }

    public InputAgeStatistics Snapshot()
    {
        if (count == 0)
            return default;

        var sorted = new double[count];
        int rawInputCount = 0;

        for (int index = 0; index < count; index++)
        {
            sorted[index] = samples[index];
            if (kinds[index] == KeyInputTimestampKind.RawInput)
                rawInputCount++;
        }

        Array.Sort(sorted);
        return new InputAgeStatistics(
            count,
            rawInputCount,
            percentile(sorted, 0.50),
            percentile(sorted, 0.95),
            percentile(sorted, 0.99));
    }

    private static double percentile(double[] sorted, double percentile)
    {
        int index = Math.Clamp(
            (int)Math.Ceiling(sorted.Length * percentile) - 1,
            0,
            sorted.Length - 1);
        return sorted[index];
    }
}
