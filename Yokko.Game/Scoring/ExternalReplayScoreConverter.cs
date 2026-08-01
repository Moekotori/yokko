using System;
using System.IO;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Import.Malody;
using Yokko.Import.Osu;

namespace Yokko.Game.Scoring;

internal static class ExternalReplayScoreConverter
{
    public static ManiaScoreResult FromOsu(OsuReplay replay)
    {
        ArgumentNullException.ThrowIfNull(replay);
        OsuReplayScore score = replay.Score
                               ?? throw new InvalidDataException(
                                   "The osu! replay does not contain score metadata.");
        int total = score.Perfect
                    + score.Great
                    + score.Good
                    + score.Ok
                    + score.Meh
                    + score.Miss;
        double accuracy = total == 0
            ? 0
            : (300d * (score.Perfect + score.Great)
               + 200d * score.Good
               + 100d * score.Ok
               + 50d * score.Meh)
              / (300d * total);
        ScoreRank rank = rankFromAccuracy(accuracy);
        ManiaModSet mods = OsuLegacyManiaModConverter.Convert(replay.Mods);
        rank = mods.AdjustRank(rank);

        return new ManiaScoreResult(
            score.Score,
            accuracy,
            score.MaxCombo,
            rank,
            score.Perfect,
            score.Great,
            score.Good,
            score.Ok,
            score.Meh,
            score.Miss);
    }

    public static ManiaScoreResult FromMalody(MalodyReplay replay)
    {
        ArgumentNullException.ThrowIfNull(replay);
        int total = replay.Best + replay.Cool + replay.Good + replay.Miss;
        double accuracy = total == 0
            ? 0
            : (replay.Best + 0.75 * replay.Cool + 0.4 * replay.Good)
              / total;

        return new ManiaScoreResult(
            replay.Score,
            accuracy,
            replay.MaxCombo,
            rankFromAccuracy(accuracy),
            replay.Best,
            replay.Cool,
            replay.Good,
            0,
            0,
            replay.Miss,
            replay.HoldBreaks);
    }

    private static ScoreRank rankFromAccuracy(double accuracy) =>
        accuracy switch
        {
            >= 1 => ScoreRank.X,
            >= 0.95 => ScoreRank.S,
            >= 0.9 => ScoreRank.A,
            >= 0.8 => ScoreRank.B,
            >= 0.7 => ScoreRank.C,
            _ => ScoreRank.D,
        };
}
