using Yokko.Core.Beatmaps;

namespace Yokko.Core.Scoring;

/// <summary>
/// osu!lazer's default (non-classic) mania score processor.
/// Ported from ppy/osu
/// osu.Game.Rulesets.Mania/Scoring/ManiaScoreProcessor.cs and
/// osu.Game/Rulesets/Scoring/ScoreProcessor.cs
/// commit 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0 (MIT).
/// </summary>
public sealed class ManiaScoreProcessor
{
    private const double comboBase = 4;

    private readonly int maximumAccuracyJudgementCount;
    private readonly double maximumComboPortion;
    private readonly double scoreMultiplier;

    private double currentBaseScore;
    private double currentMaximumBaseScore;
    private int currentAccuracyJudgementCount;
    private double currentComboPortion;

    public ManiaScoreProcessor(
        YokkoBeatmap beatmap,
        double scoreMultiplier = 1)
    {
        if (!double.IsFinite(scoreMultiplier)
            || scoreMultiplier < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scoreMultiplier));
        }

        this.scoreMultiplier = scoreMultiplier;
        maximumAccuracyJudgementCount = beatmap.HitObjects.Sum(static hitObject => hitObject.Kind switch
        {
            HitObjectKind.Tap => 1,
            HitObjectKind.Hold => 2,
            _ => 0,
        });

        for (int combo = 1; combo <= maximumAccuracyJudgementCount; combo++)
            maximumComboPortion += comboScoreChange(JudgementRating.Perfect, combo);
    }

    public JudgementCounter Counts { get; } = new();

    public int Combo { get; private set; }

    public int MaxCombo { get; private set; }

    public double Accuracy => currentMaximumBaseScore > 0
        ? currentBaseScore / currentMaximumBaseScore
        : 1;

    public double MaximumAchievableAccuracy
    {
        get
        {
            if (maximumAccuracyJudgementCount == 0)
                return 1;

            int remainingJudgements = Math.Max(
                0,
                maximumAccuracyJudgementCount
                - currentAccuracyJudgementCount);
            double maximumFinalBaseScore =
                currentBaseScore
                + remainingJudgements
                * baseScoreFor(JudgementRating.Perfect);
            double absoluteMaximumBaseScore =
                maximumAccuracyJudgementCount
                * baseScoreFor(JudgementRating.Perfect);
            return maximumFinalBaseScore / absoluteMaximumBaseScore;
        }
    }

    public long TotalScore { get; private set; }

    public long TotalScoreWithoutMods { get; private set; }

    public ScoreRank Rank { get; private set; } = ScoreRank.X;

    public void Apply(JudgementRating rating)
    {
        if (rating == JudgementRating.None)
            throw new ArgumentOutOfRangeException(nameof(rating));

        Counts.Add(rating);

        if (rating.IncreasesCombo())
            Combo++;
        else if (rating.BreaksCombo())
            Combo = 0;

        MaxCombo = Math.Max(MaxCombo, Combo);

        if (maximumResultFor(rating).AffectsAccuracy())
        {
            currentMaximumBaseScore += baseScoreFor(maximumResultFor(rating));
            currentAccuracyJudgementCount++;
        }

        if (rating.AffectsAccuracy())
            currentBaseScore += baseScoreFor(rating);

        if (rating.IsScorable())
            currentComboPortion += comboScoreChange(rating, Combo);

        updateScore();
    }

    public static int BaseScoreFor(JudgementRating rating) => baseScoreFor(rating);

    /// <summary>
    /// Resolves a Mania rank from accuracy and result counts using the pinned
    /// osu!lazer ManiaScoreProcessor rules.
    /// </summary>
    public static ScoreRank RankFromScore(
        double accuracy,
        JudgementCounter counts)
    {
        ArgumentNullException.ThrowIfNull(counts);

        ScoreRank rank = accuracy switch
        {
            1 => ScoreRank.X,
            >= 0.95 => ScoreRank.S,
            >= 0.9 => ScoreRank.A,
            >= 0.8 => ScoreRank.B,
            >= 0.7 => ScoreRank.C,
            _ => ScoreRank.D,
        };

        if (rank != ScoreRank.S)
            return rank;

        bool anyImperfect =
            counts.Good > 0
            || counts.Ok > 0
            || counts.Meh > 0
            || counts.Miss > 0;

        return anyImperfect ? rank : ScoreRank.X;
    }

    private void updateScore()
    {
        double comboProgress = maximumComboPortion > 0
            ? currentComboPortion / maximumComboPortion
            : 1;
        double accuracyProgress = maximumAccuracyJudgementCount > 0
            ? (double)currentAccuracyJudgementCount / maximumAccuracyJudgementCount
            : 1;

        TotalScoreWithoutMods = (long)Math.Round(
            150_000 * comboProgress
            + 850_000 * Math.Pow(Accuracy, 2 + 2 * Accuracy) * accuracyProgress);
        TotalScore = (long)Math.Round(
            TotalScoreWithoutMods * scoreMultiplier);
        Rank = RankFromScore(Accuracy, Counts);
    }

    private static JudgementRating maximumResultFor(JudgementRating rating)
        => rating switch
        {
            JudgementRating.Miss
                or JudgementRating.Meh
                or JudgementRating.Ok
                or JudgementRating.Good
                or JudgementRating.Great
                or JudgementRating.Perfect => JudgementRating.Perfect,
            JudgementRating.IgnoreHit
                or JudgementRating.IgnoreMiss => JudgementRating.IgnoreHit,
            JudgementRating.ComboBreak => JudgementRating.IgnoreHit,
            _ => JudgementRating.None,
        };

    private static int baseScoreFor(JudgementRating rating) => rating switch
    {
        JudgementRating.Meh => 50,
        JudgementRating.Ok => 100,
        JudgementRating.Good => 200,
        JudgementRating.Great => 300,
        JudgementRating.Perfect => 305,
        _ => 0,
    };

    private static int comboBaseScoreFor(JudgementRating rating)
        => rating == JudgementRating.Perfect ? 300 : baseScoreFor(rating);

    private static double comboScoreChange(JudgementRating rating, int comboAfterJudgement)
    {
        int baseScore = comboBaseScoreFor(rating);
        if (baseScore == 0)
            return 0;

        double multiplier = Math.Min(
            Math.Max(0.5, Math.Log(comboAfterJudgement, comboBase)),
            Math.Log(400, comboBase));
        return baseScore * multiplier;
    }
}
