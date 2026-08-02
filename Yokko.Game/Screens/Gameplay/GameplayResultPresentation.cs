using System;
using System.Collections.Generic;

namespace Yokko.Game.Screens.Gameplay;

internal sealed record GameplayTimingSummary(
    int EarlyCount,
    int OnTimeCount,
    int LateCount,
    double MeanMilliseconds,
    double UnstableRate)
{
    public static GameplayTimingSummary FromHitErrors(
        IReadOnlyList<double> hitErrors)
    {
        if (hitErrors == null || hitErrors.Count == 0)
            return null;

        int early = 0;
        int onTime = 0;
        int late = 0;
        double sum = 0;

        foreach (double error in hitErrors)
        {
            sum += error;
            if (error < -0.5)
                early++;
            else if (error > 0.5)
                late++;
            else
                onTime++;
        }

        double mean = sum / hitErrors.Count;
        double squaredDeviation = 0;
        foreach (double error in hitErrors)
        {
            double deviation = error - mean;
            squaredDeviation += deviation * deviation;
        }

        return new GameplayTimingSummary(
            early,
            onTime,
            late,
            mean,
            Math.Sqrt(squaredDeviation / hitErrors.Count) * 10);
    }
}

internal sealed record GameplayResultPresentation(
    string PlayerName,
    string PlayerId,
    DateTimeOffset? PlayedAt,
    long? PreviousBestScore = null,
    bool ReplaySaved = false,
    GameplayTimingSummary Timing = null)
{
    public static GameplayResultPresentation LocalFallback(
        DateTimeOffset? playedAt = null) =>
        new("LOCAL PLAYER", "LOCAL", playedAt);
}
