using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;

namespace Yokko.Core.Difficulty;

[Flags]
public enum ManiaStarRatingLimitations
{
    None = 0,
    MinesExcluded = 1 << 0,
    NoReleaseNotModelled = 1 << 1,
    DynamicRateApproximation = 1 << 2,
}

/// <summary>
/// Describes the gameplay rules which affect Star Rating Rebirth's input
/// difficulty. The upstream OD input is derived from the effective real-time
/// Great window because that is the hit-leniency window used by the algorithm.
/// </summary>
public sealed record ManiaStarRatingContext
{
    public ManiaStarRatingContext(
        double greatWindowMilliseconds,
        bool releaseJudgementsRequired = true,
        bool minesEnabled = true,
        ManiaStarRatingLimitations additionalLimitations =
            ManiaStarRatingLimitations.None,
        bool invertApplied = false)
    {
        if (!double.IsFinite(greatWindowMilliseconds)
            || greatWindowMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(greatWindowMilliseconds));
        }

        GreatWindowMilliseconds = greatWindowMilliseconds;
        ReleaseJudgementsRequired = releaseJudgementsRequired;
        MinesEnabled = minesEnabled;
        AdditionalLimitations = additionalLimitations;
        InvertApplied = invertApplied;
    }

    public double GreatWindowMilliseconds { get; }

    public bool ReleaseJudgementsRequired { get; }

    public bool MinesEnabled { get; }

    public ManiaStarRatingLimitations AdditionalLimitations { get; }

    public bool InvertApplied { get; }

    public static ManiaStarRatingContext ForBeatmap(
        YokkoBeatmap beatmap)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        return ForGameplay(
            beatmap,
            ManiaModSet.Empty,
            beatmap.SourceFormat == ChartSourceFormat.Quaver
                ? JudgementConfiguration.QuaverDefault
                : JudgementConfiguration.YokkoDefault,
            minesEnabled: true,
            timelineRate: 1);
    }

    /// <summary>
    /// Creates a context from the same judgement rules used by gameplay.
    /// <paramref name="timelineRate"/> converts chart-time windows to the
    /// real-time timeline supplied to Star Rating Rebirth.
    /// </summary>
    public static ManiaStarRatingContext ForGameplay(
        YokkoBeatmap beatmap,
        ManiaModSet mods,
        JudgementConfiguration judgementConfiguration,
        bool minesEnabled,
        double timelineRate,
        bool dynamicRatePretransformed = false)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        ArgumentNullException.ThrowIfNull(mods);
        if (!double.IsFinite(timelineRate) || timelineRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(timelineRate));

        var windows = new JudgementWindows(
            mods.EffectiveOverallDifficulty(
                beatmap.OverallDifficulty),
            mods.HitWindowSpeedMultiplier,
            mods.HitWindowDifficultyMultiplier,
            mods.Contains(ManiaModId.Classic),
            mods.Contains(ManiaModId.ScoreV2),
            beatmap.ConversionSource is not null,
            judgementConfiguration,
            beatmap.BmsJudgement?.WindowMultiplier
            ?? BmsJudgementMetadata.Default.WindowMultiplier,
            beatmap.BmsJudgement?.RegularKeysPerStage
            ?? (beatmap.RegularLaneCount / beatmap.StageCount == 5 ? 5 : 7));

        return new ManiaStarRatingContext(
            windows.GreatMilliseconds / timelineRate,
            !mods.Contains(ManiaModId.NoRelease),
            minesEnabled,
            mods.HasDynamicRate && !dynamicRatePretransformed
                ? ManiaStarRatingLimitations.DynamicRateApproximation
                : ManiaStarRatingLimitations.None,
            mods.Contains(ManiaModId.Invert));
    }
}
