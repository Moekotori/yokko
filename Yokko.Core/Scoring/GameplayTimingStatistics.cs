using System;
using System.Collections.Generic;

namespace Yokko.Core.Scoring;

public sealed record GameplayTimingStatistics(
    int SampleCount,
    int EarlyCount,
    int OnTimeCount,
    int LateCount,
    double? EarlyAverageMilliseconds,
    double? LateAverageMilliseconds,
    double MeanMilliseconds,
    double UnstableRate)
{
    // Matches the live timing readout's early/on-time/late boundary.
    public const double OnTimeToleranceMilliseconds = 0.05;

    public static GameplayTimingStatistics? FromHitErrors(
        IReadOnlyList<double> hitErrors)
    {
        if (hitErrors == null || hitErrors.Count == 0)
            return null;

        int sampleCount = 0;
        int earlyCount = 0;
        int onTimeCount = 0;
        int lateCount = 0;
        double earlySum = 0;
        double lateSum = 0;
        double mean = 0;
        double squaredDeviation = 0;

        foreach (double error in hitErrors)
        {
            if (!double.IsFinite(error))
                continue;

            sampleCount++;
            double delta = error - mean;
            mean += delta / sampleCount;
            squaredDeviation += delta * (error - mean);

            if (error < -OnTimeToleranceMilliseconds)
            {
                earlyCount++;
                earlySum += error;
            }
            else if (error > OnTimeToleranceMilliseconds)
            {
                lateCount++;
                lateSum += error;
            }
            else
            {
                onTimeCount++;
            }
        }

        if (sampleCount == 0)
            return null;

        return new GameplayTimingStatistics(
            sampleCount,
            earlyCount,
            onTimeCount,
            lateCount,
            earlyCount == 0 ? null : earlySum / earlyCount,
            lateCount == 0 ? null : lateSum / lateCount,
            mean,
            Math.Sqrt(squaredDeviation / sampleCount) * 10);
    }

    public static bool TryGetRealInputError(
        JudgementEvent judgement,
        double speedMultiplier,
        out double hitErrorMilliseconds)
    {
        hitErrorMilliseconds = 0;
        if (judgement == null
            || judgement.HitTimeMilliseconds is null
            || judgement.IsMiss
            || judgement.Phase is not JudgementPhase.Tap
                and not JudgementPhase.Hold
                and not JudgementPhase.HoldHead
                and not JudgementPhase.HoldTail
            || !double.IsFinite(judgement.HitErrorMilliseconds)
            || !double.IsFinite(speedMultiplier)
            || speedMultiplier <= 0)
        {
            return false;
        }

        // JudgementEvent is expressed in rate-adjusted chart time. Scoring
        // uses the same division, yielding the physical millisecond offset.
        hitErrorMilliseconds =
            judgement.HitErrorMilliseconds / speedMultiplier;
        return true;
    }
}
