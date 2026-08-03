using System;
using System.Linq;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;

namespace Yokko.Game.Gameplay;

internal static class GameplayReplayRejudge
{
    public static ManiaScoreResult Preview(
        YokkoBeatmap beatmap,
        GameplayReplay replay,
        ManiaModSet mods,
        JudgementWindows windows,
        JudgementConfiguration judgementConfiguration,
        bool minesEnabled,
        double completionTimeMilliseconds,
        double offsetMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        ArgumentNullException.ThrowIfNull(replay);
        if (!double.IsFinite(offsetMilliseconds)
            || Math.Abs(offsetMilliseconds) > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(offsetMilliseconds));
        }

        var shiftedReplay = new GameplayReplay(
            replay.Inputs.Select(input => input with
            {
                TimeMilliseconds = input.TimeMilliseconds
                                   + offsetMilliseconds,
            }),
            mods,
            judgementConfiguration);
        var simulator = new GameplayReplaySimulator(
            beatmap,
            shiftedReplay,
            mods,
            windows,
            judgementConfiguration,
            minesEnabled);
        simulator.AdvanceTo(
            completionTimeMilliseconds
            + 2000
            + Math.Abs(offsetMilliseconds));
        ManiaScoreResult raw = simulator.JudgementState.CreateResult();
        return raw with { Rank = mods.AdjustRank(raw.Rank) };
    }
}
