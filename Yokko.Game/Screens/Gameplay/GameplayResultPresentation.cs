using System;
using Yokko.Core.Scoring;

namespace Yokko.Game.Screens.Gameplay;

internal sealed record GameplayResultPresentation(
    string PlayerName,
    string PlayerId,
    DateTimeOffset? PlayedAt,
    long? PreviousBestScore = null,
    bool ReplaySaved = false,
    GameplayTimingStatistics Timing = null)
{
    public static GameplayResultPresentation LocalFallback(
        DateTimeOffset? playedAt = null) =>
        new("LOCAL PLAYER", "LOCAL", playedAt);
}
