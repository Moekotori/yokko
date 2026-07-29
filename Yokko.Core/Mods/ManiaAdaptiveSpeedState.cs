using Yokko.Core.Beatmaps;
using Yokko.Core.Scoring;

namespace Yokko.Core.Mods;

/// <summary>
/// Runtime feedback controller used by Adaptive Speed. This follows lazer's
/// eight-result moving target and 50 ms continuous damping.
/// Ported from ppy/osu ModAdaptiveSpeed.cs at
/// 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0 (MIT).
/// </summary>
public sealed class ManiaAdaptiveSpeedState
{
    public const double MinimumRate = 0.4;
    public const double MaximumRate = 2.5;
    private const double minimumRelativeRate = 0.9;
    private const double maximumRelativeRate = 1.11;
    private const double missMultiplier = 0.95;
    private const int recentResultCount = 8;
    private const double dampingHalfTimeMilliseconds = 50;

    private readonly double[] judgementPointTimes;
    private readonly Queue<double> recentRates =
        new(recentResultCount);

    public ManiaAdaptiveSpeedState(
        YokkoBeatmap beatmap,
        double initialRate = 1)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        if (!double.IsFinite(initialRate)
            || initialRate is < 0.5 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(initialRate));
        }

        CurrentRate = initialRate;
        TargetRate = initialRate;
        for (int i = 0; i < recentResultCount; i++)
            recentRates.Enqueue(initialRate);

        judgementPointTimes = beatmap.HitObjects
            .SelectMany(static hitObject =>
                hitObject.EndTimeMilliseconds is double end
                    ? new[]
                    {
                        hitObject.StartTimeMilliseconds,
                        end,
                    }
                    : [hitObject.StartTimeMilliseconds])
            .Distinct()
            .Order()
            .ToArray();
    }

    public double CurrentRate { get; private set; }

    public double TargetRate { get; private set; }

    public IReadOnlyList<double> RecentRates => recentRates.ToArray();

    public bool Apply(JudgementEvent judgement)
    {
        ArgumentNullException.ThrowIfNull(judgement);
        if (!judgement.Rating.AffectsAccuracy())
            return false;

        int index = Array.BinarySearch(
            judgementPointTimes,
            judgement.ObjectTimeMilliseconds);
        int precedingIndex = index >= 0 ? index - 1 : ~index - 1;
        if (precedingIndex < 0)
            return false;

        double relativeRate;
        if (judgement.IsMiss)
        {
            relativeRate = missMultiplier;
        }
        else if (judgement.HitTimeMilliseconds is double hitTime)
        {
            double precedingTime = judgementPointTimes[precedingIndex];
            double denominator = hitTime - precedingTime;
            if (denominator <= 0 || !double.IsFinite(denominator))
                return false;

            relativeRate = Math.Clamp(
                (judgement.ObjectTimeMilliseconds - precedingTime)
                / denominator,
                minimumRelativeRate,
                maximumRelativeRate);
        }
        else
        {
            return false;
        }

        double newRate = Math.Clamp(
            relativeRate * CurrentRate,
            MinimumRate,
            MaximumRate);
        recentRates.Dequeue();
        recentRates.Enqueue(newRate);

        double[] rates = recentRates.ToArray();
        int consistency = 0;
        for (int i = 1; i < rates.Length; i++)
            consistency += Math.Sign(rates[i] - rates[i - 1]);

        double average = rates.Average();
        double amount =
            Math.Abs(consistency) / (double)(recentResultCount - 1);
        TargetRate += (average - TargetRate) * amount;
        return true;
    }

    public void Update(double elapsedMilliseconds)
    {
        if (!double.IsFinite(elapsedMilliseconds)
            || elapsedMilliseconds <= 0)
        {
            return;
        }

        double exponent =
            elapsedMilliseconds / dampingHalfTimeMilliseconds;
        CurrentRate = TargetRate
                      + (CurrentRate - TargetRate)
                      * Math.Pow(0.5, exponent);
    }
}
