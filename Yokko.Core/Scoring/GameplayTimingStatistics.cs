using System;
using System.Collections.Generic;
using System.Linq;

namespace Yokko.Core.Scoring;

public readonly record struct GameplayTimingSample(
    int Lane,
    double ErrorMilliseconds,
    double? TimeMilliseconds = null,
    JudgementRating? Rating = null);

public sealed record GameplayLaneTimingStatistics(
    int Lane,
    int SampleCount,
    int EarlyCount,
    int OnTimeCount,
    int LateCount,
    double? EarlyAverageMilliseconds,
    double? LateAverageMilliseconds,
    double MeanMilliseconds,
    double UnstableRate);

public sealed record GameplayTimingStatistics(
    int SampleCount,
    int EarlyCount,
    int OnTimeCount,
    int LateCount,
    double? EarlyAverageMilliseconds,
    double? LateAverageMilliseconds,
    double MeanMilliseconds,
    double UnstableRate,
    IReadOnlyList<GameplayLaneTimingStatistics>? Lanes = null,
    IReadOnlyList<GameplayTimingSample>? Samples = null)
{
    // Matches the live timing readout's early/on-time/late boundary.
    public const double OnTimeToleranceMilliseconds = 0.05;

    public static GameplayTimingStatistics? FromHitErrors(
        IReadOnlyList<double> hitErrors)
    {
        if (hitErrors == null || hitErrors.Count == 0)
            return null;

        TimingAggregate? aggregate = calculate(hitErrors);
        return aggregate?.ToSummary();
    }

    public static GameplayTimingStatistics? FromSamples(
        IReadOnlyList<GameplayTimingSample> samples)
    {
        if (samples == null || samples.Count == 0)
            return null;

        GameplayTimingSample[] valid = samples
            .Where(static sample =>
                sample.Lane >= 0
                && double.IsFinite(sample.ErrorMilliseconds))
            .ToArray();
        if (valid.Length == 0)
            return null;

        TimingAggregate aggregate = calculate(
            valid.Select(static sample => sample.ErrorMilliseconds).ToArray())!;
        GameplayLaneTimingStatistics[] lanes = valid
            .GroupBy(static sample => sample.Lane)
            .OrderBy(static group => group.Key)
            .Select(group => calculate(
                    group.Select(static sample => sample.ErrorMilliseconds)
                         .ToArray())!
                .ToLane(group.Key))
            .ToArray();

        return aggregate.ToSummary(lanes, valid);
    }

    private static TimingAggregate? calculate(
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

        return new TimingAggregate(
            sampleCount,
            earlyCount,
            onTimeCount,
            lateCount,
            earlyCount == 0 ? null : earlySum / earlyCount,
            lateCount == 0 ? null : lateSum / lateCount,
            mean,
            Math.Sqrt(squaredDeviation / sampleCount) * 10);
    }

    private sealed record TimingAggregate(
        int SampleCount,
        int EarlyCount,
        int OnTimeCount,
        int LateCount,
        double? EarlyAverageMilliseconds,
        double? LateAverageMilliseconds,
        double MeanMilliseconds,
        double UnstableRate)
    {
        public GameplayTimingStatistics ToSummary(
            IReadOnlyList<GameplayLaneTimingStatistics>? lanes = null,
            IReadOnlyList<GameplayTimingSample>? samples = null) => new(
            SampleCount,
            EarlyCount,
            OnTimeCount,
            LateCount,
            EarlyAverageMilliseconds,
            LateAverageMilliseconds,
            MeanMilliseconds,
            UnstableRate,
            lanes,
            samples);

        public GameplayLaneTimingStatistics ToLane(int lane) => new(
            lane,
            SampleCount,
            EarlyCount,
            OnTimeCount,
            LateCount,
            EarlyAverageMilliseconds,
            LateAverageMilliseconds,
            MeanMilliseconds,
            UnstableRate);
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
