using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;

namespace Yokko.Core.Scoring;

public readonly record struct OsuStableScoreV1ModMultipliers(
    double ScoreMultiplier,
    double BonusPunishmentDivider);

/// <summary>
/// osu!stable mania ScoreV1 ModMultiplier and ModDivider.
/// Sources: osu! wiki Gameplay/Score/ScoreV1/osu!mania and
/// Gameplay/Game_modifier/xK (CC BY-NC-SA 4.0).
/// </summary>
public static class OsuStableScoreV1Mods
{
    public static OsuStableScoreV1ModMultipliers Calculate(
        YokkoBeatmap beatmap,
        ManiaModSet mods)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        ArgumentNullException.ThrowIfNull(mods);

        double scoreMultiplier = 1;
        if (mods.Contains(ManiaModId.Easy))
            scoreMultiplier *= 0.5;
        if (mods.Contains(ManiaModId.NoFail))
            scoreMultiplier *= 0.5;
        if (mods.Contains(ManiaModId.HalfTime)
            || mods.Contains(ManiaModId.Daycore))
        {
            scoreMultiplier *= 0.5;
        }

        if (beatmap.ConversionSource is not null
            && mods.KeyConversionTarget is int targetKeys)
        {
            int defaultKeys = OsuStandardManiaConverter
                .DetermineDefaultColumnCount(beatmap.ConversionSource);
            scoreMultiplier *= keyConversionMultiplier(
                defaultKeys,
                targetKeys);
        }

        double divider = 1;
        if (mods.Contains(ManiaModId.HardRock))
            divider *= 1.08;
        if (mods.Contains(ManiaModId.DoubleTime)
            || mods.Contains(ManiaModId.Nightcore))
        {
            divider *= 1.1;
        }
        if (mods.Contains(ManiaModId.FadeIn)
            || mods.Contains(ManiaModId.Hidden)
            || mods.Contains(ManiaModId.Flashlight))
        {
            divider *= 1.06;
        }

        return new OsuStableScoreV1ModMultipliers(
            scoreMultiplier,
            divider);
    }

    private static double keyConversionMultiplier(
        int defaultKeys,
        int targetKeys)
    {
        if (defaultKeys == targetKeys)
            return 1;
        if (targetKeys > defaultKeys)
            return 0.9;

        // Stable's 4K-7K convert table drops 0.04 for every removed key from
        // the 0.90 baseline (one removed key is therefore 0.86).
        return Math.Clamp(
            0.9 - 0.04 * (defaultKeys - targetKeys),
            0.66,
            0.9);
    }
}
