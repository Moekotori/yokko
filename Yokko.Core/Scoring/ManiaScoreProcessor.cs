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
    private readonly double osuStableBonusPunishmentDivider;
    private readonly bool useOsuStableScoring;
    private readonly bool useEtternaScoring;
    private readonly bool useQuaverScoring;
    private readonly bool useBmsJudgement;
    private readonly int quaverMaximumScoreCount;

    private double currentBaseScore;
    private double currentMaximumBaseScore;
    private int currentAccuracyJudgementCount;
    private double currentComboPortion;
    private double osuStableAccuracyTotal;
    private double osuStableBaseScore;
    private double osuStableBonusScore;
    private double osuStableBonus = 100;
    private int quaverMultiplierCount;
    private int quaverScoreCount;
    private double quaverAccuracyWeightTotal;
    private double etternaCurrentWifePoints;
    private double etternaMaximumAppliedWifePoints;
    private readonly double etternaMaximumWifePoints;
    private double? etternaBrokenRowTimeMilliseconds;
    private int etternaMissCombo;
    private int etternaMaxMissCombo;

    public ManiaScoreProcessor(
        YokkoBeatmap beatmap,
        double scoreMultiplier = 1,
        JudgementConfiguration? judgementConfiguration = null,
        double osuStableBonusPunishmentDivider = 1)
    {
        if (!double.IsFinite(scoreMultiplier)
            || scoreMultiplier < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scoreMultiplier));
        }
        if (!double.IsFinite(osuStableBonusPunishmentDivider)
            || osuStableBonusPunishmentDivider <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(osuStableBonusPunishmentDivider));
        }

        this.scoreMultiplier = scoreMultiplier;
        this.osuStableBonusPunishmentDivider =
            osuStableBonusPunishmentDivider;
        JudgementConfiguration configuration =
            judgementConfiguration
            ?? (beatmap.SourceFormat == ChartSourceFormat.Quaver
                ? JudgementConfiguration.QuaverDefault
                : JudgementConfiguration.YokkoDefault);
        Configuration = configuration;
        useOsuStableScoring =
            configuration.Mode == JudgementMode.OsuStable;
        useEtternaScoring =
            configuration.Mode == JudgementMode.Etterna;
        useQuaverScoring =
            configuration.Mode == JudgementMode.Quaver;
        useBmsJudgement =
            configuration.Mode == JudgementMode.BmsBeatoraja;
        maximumAccuracyJudgementCount = beatmap.HitObjects.Sum(hitObject =>
            hitObject.Kind switch
            {
                HitObjectKind.Tap => 1,
                HitObjectKind.Hold => useOsuStableScoring || useBmsJudgement
                    ? 1
                    : 2,
                _ => 0,
            });
        etternaMaximumWifePoints =
            beatmap.HitObjects.Count(static hitObject =>
                hitObject.Kind is HitObjectKind.Tap
                    or HitObjectKind.Hold)
            * EtternaScoringRules.Wife3MaximumPoints;
        quaverMaximumScoreCount =
            calculateQuaverMaximumScoreCount(
                maximumAccuracyJudgementCount);

        for (int combo = 1; combo <= maximumAccuracyJudgementCount; combo++)
            maximumComboPortion += comboScoreChange(JudgementRating.Perfect, combo);
    }

    public JudgementCounter Counts { get; } = new();

    public JudgementConfiguration Configuration { get; }

    public int Combo { get; private set; }

    public int MaxCombo { get; private set; }

    public int ComboBreaks => useEtternaScoring
        ? Counts.Ok + Counts.Meh + Counts.Miss
        : Counts.ComboBreak;

    public int MissCombo =>
        useEtternaScoring ? etternaMissCombo : 0;

    public int MaxMissCombo =>
        useEtternaScoring ? etternaMaxMissCombo : 0;

    public double Accuracy => useEtternaScoring
        ? etternaMaximumAppliedWifePoints > 0
            ? etternaCurrentWifePoints
              / etternaMaximumAppliedWifePoints
            : etternaCurrentWifePoints < 0
                ? 0
                : 1
        : useQuaverScoring
        ? maximumAccuracyJudgementCount > 0
            ? Math.Max(
                quaverAccuracyWeightTotal
                / maximumAccuracyJudgementCount
                / 100,
                0)
            : 1
        : useOsuStableScoring
        ? currentAccuracyJudgementCount > 0
            ? osuStableAccuracyTotal
              / (currentAccuracyJudgementCount * 300)
            : 1
        : currentMaximumBaseScore > 0
        ? currentBaseScore / currentMaximumBaseScore
        : 1;

    public double MaximumAchievableAccuracy
    {
        get
        {
            if (useEtternaScoring)
            {
                if (etternaMaximumWifePoints <= 0)
                    return 1;

                double remaining =
                    etternaMaximumWifePoints
                    - etternaMaximumAppliedWifePoints;
                return (etternaCurrentWifePoints + remaining)
                       / etternaMaximumWifePoints;
            }

            if (maximumAccuracyJudgementCount == 0)
                return 1;

            int remainingJudgements = Math.Max(
                0,
                maximumAccuracyJudgementCount
                - currentAccuracyJudgementCount);
            if (useQuaverScoring)
            {
                return Math.Max(
                    (quaverAccuracyWeightTotal + remainingJudgements * 100)
                    / maximumAccuracyJudgementCount
                    / 100,
                    0);
            }

            if (useOsuStableScoring)
            {
                return (osuStableAccuracyTotal
                        + remainingJudgements * 300)
                       / (maximumAccuracyJudgementCount * 300);
            }

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

    public void Apply(JudgementRating rating) =>
        Apply(rating, 0, JudgementPhase.Tap, double.NaN);

    public void Apply(
        JudgementRating rating,
        double realHitErrorMilliseconds,
        JudgementPhase phase,
        double objectTimeMilliseconds)
    {
        if (rating == JudgementRating.None)
            throw new ArgumentOutOfRangeException(nameof(rating));

        if (useEtternaScoring)
        {
            applyEtterna(
                rating,
                realHitErrorMilliseconds,
                phase,
                objectTimeMilliseconds);
            return;
        }

        if (useQuaverScoring)
        {
            applyQuaver(rating, isMine: false);
            return;
        }

        if (useOsuStableScoring)
        {
            applyOsuStable(rating, phase);
            return;
        }

        if (useBmsJudgement)
        {
            applyBms(rating);
            return;
        }

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

    internal void ApplyBmsEmptyPress(bool breaksCombo)
    {
        if (!useBmsJudgement)
            return;

        Counts.Add(JudgementRating.Meh);
        if (breaksCombo)
            Combo = 0;
    }

    private void applyBms(JudgementRating rating)
    {
        if (!rating.AffectsAccuracy())
            return;

        Counts.Add(rating);
        if (rating is JudgementRating.Perfect
            or JudgementRating.Great
            or JudgementRating.Good)
        {
            Combo++;
        }
        else
        {
            Combo = 0;
        }

        MaxCombo = Math.Max(MaxCombo, Combo);
        currentMaximumBaseScore += baseScoreFor(JudgementRating.Perfect);
        currentAccuracyJudgementCount++;
        currentBaseScore += baseScoreFor(rating);
        currentComboPortion += comboScoreChange(rating, Combo);
        updateScore();
    }

    public void ApplyMine(bool wasHit)
    {
        if (useEtternaScoring)
        {
            if (wasHit)
            {
                etternaCurrentWifePoints +=
                    EtternaScoringRules.Wife3MineHitWeight;
                updateEtternaScore();
            }

            return;
        }

        if (!useQuaverScoring || !wasHit)
            return;

        applyQuaver(JudgementRating.Miss, isMine: true);
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

    /// <summary>
    /// osu!stable mania ScoreV1. Each tap or hold contributes exactly one
    /// result. MAX and 300 are both worth full accuracy, while the base and
    /// floating bonus portions each contribute half of the 1,000,000 score.
    /// Source: osu! wiki Gameplay/Score/ScoreV1/osu!mania (CC BY-NC-SA 4.0).
    /// </summary>
    internal void ApplyOsuStableHoldHead()
    {
        if (!useOsuStableScoring)
            return;

        Combo++;
        MaxCombo = Math.Max(MaxCombo, Combo);
    }

    internal void ApplyOsuStableHoldBreak()
    {
        if (!useOsuStableScoring)
            return;

        Counts.Add(JudgementRating.ComboBreak);
        Combo = 0;
    }

    private void applyOsuStable(
        JudgementRating rating,
        JudgementPhase phase)
    {
        if (!rating.AffectsAccuracy())
            return;

        Counts.Add(rating);
        currentAccuracyJudgementCount++;

        if (rating == JudgementRating.Miss)
            Combo = 0;
        else if (phase != JudgementPhase.Hold)
            Combo++;

        MaxCombo = Math.Max(MaxCombo, Combo);

        (double hitValue, double hitBonusValue, double hitBonus,
            double hitPunishment) = rating switch
        {
            JudgementRating.Perfect => (320, 32, 2, 0),
            JudgementRating.Great => (300, 32, 1, 0),
            JudgementRating.Good => (200, 16, 0, 8),
            JudgementRating.Ok => (100, 8, 0, 24),
            JudgementRating.Meh => (50, 4, 0, 44),
            JudgementRating.Miss => (0, 0, 0, double.PositiveInfinity),
            _ => throw new ArgumentOutOfRangeException(nameof(rating)),
        };

        osuStableBonus = double.IsPositiveInfinity(hitPunishment)
            ? 0
            : Math.Clamp(
                osuStableBonus
                + hitBonus
                - hitPunishment / osuStableBonusPunishmentDivider,
                0,
                100);

        double perObjectHalf = maximumAccuracyJudgementCount > 0
            ? 500_000d / maximumAccuracyJudgementCount
            : 0;
        osuStableBaseScore += perObjectHalf * hitValue / 320;
        osuStableBonusScore += perObjectHalf
                               * hitBonusValue
                               * Math.Sqrt(osuStableBonus)
                               / 320;
        osuStableAccuracyTotal += rating switch
        {
            JudgementRating.Perfect or JudgementRating.Great => 300,
            JudgementRating.Good => 200,
            JudgementRating.Ok => 100,
            JudgementRating.Meh => 50,
            _ => 0,
        };

        TotalScoreWithoutMods = (long)Math.Round(
            osuStableBaseScore + osuStableBonusScore);
        TotalScore = (long)Math.Round(
            (osuStableBaseScore + osuStableBonusScore)
            * scoreMultiplier);
        Rank = osuStableRankFromAccuracy(Accuracy);
    }

    private static ScoreRank osuStableRankFromAccuracy(double accuracy) =>
        accuracy switch
        {
            1 => ScoreRank.X,
            > 0.95 => ScoreRank.S,
            > 0.90 => ScoreRank.A,
            > 0.80 => ScoreRank.B,
            > 0.70 => ScoreRank.C,
            _ => ScoreRank.D,
        };

    private void applyEtterna(
        JudgementRating rating,
        double realHitErrorMilliseconds,
        JudgementPhase phase,
        double objectTimeMilliseconds)
    {
        if (phase == JudgementPhase.HoldBody)
        {
            if (rating is JudgementRating.ComboBreak
                or JudgementRating.IgnoreMiss)
            {
                etternaCurrentWifePoints +=
                    EtternaScoringRules.Wife3HoldDropWeight;
                updateEtternaScore();
            }

            return;
        }

        if (phase is not (
            JudgementPhase.Tap
            or JudgementPhase.HoldHead))
        {
            return;
        }

        if (!rating.AffectsAccuracy())
            return;

        Counts.Add(rating);
        if (EtternaScoringRules.BreaksCombo(rating))
        {
            Combo = 0;
            etternaMissCombo++;
            etternaMaxMissCombo = Math.Max(
                etternaMaxMissCombo,
                etternaMissCombo);
            etternaBrokenRowTimeMilliseconds =
                objectTimeMilliseconds;
        }
        else if (EtternaScoringRules.ContinuesCombo(rating)
                 && (etternaBrokenRowTimeMilliseconds is not double brokenTime
                     || brokenTime != objectTimeMilliseconds))
        {
            Combo++;
            etternaMissCombo = 0;
        }

        MaxCombo = Math.Max(MaxCombo, Combo);
        currentAccuracyJudgementCount++;
        etternaMaximumAppliedWifePoints +=
            EtternaScoringRules.Wife3MaximumPoints;
        etternaCurrentWifePoints += rating == JudgementRating.Miss
            ? EtternaScoringRules.Wife3MissWeight
            : EtternaScoringRules.Wife3(
                realHitErrorMilliseconds,
                Configuration.EtternaTimingScale);
        updateEtternaScore();
    }

    private void updateEtternaScore()
    {
        double scoreAccuracy = Math.Clamp(Accuracy, 0, 1);
        TotalScoreWithoutMods = (long)Math.Round(
            1_000_000 * scoreAccuracy);
        TotalScore = (long)Math.Round(
            TotalScoreWithoutMods * scoreMultiplier);
        Rank = EtternaScoringRules.ApproximateScoreRank(Accuracy);
    }

    private void applyQuaver(
        JudgementRating rating,
        bool isMine)
    {
        if (!rating.IsScorable())
            return;

        if (rating == JudgementRating.ComboBreak)
        {
            Counts.Add(rating);
            quaverMultiplierCount = Math.Max(
                0,
                quaverMultiplierCount - 20);
            Combo = 0;
            updateQuaverScore();
            return;
        }

        JudgementRating effectiveRating =
            rating;
        Counts.Add(effectiveRating);

        if (effectiveRating != JudgementRating.Miss)
        {
            quaverMultiplierCount += effectiveRating
                == JudgementRating.Ok
                ? -10
                : 1;
            if (!isMine)
                Combo++;
        }
        else
        {
            quaverMultiplierCount -= 20;
            Combo = 0;
        }

        quaverMultiplierCount = Math.Clamp(
            quaverMultiplierCount,
            0,
            150);
        MaxCombo = Math.Max(MaxCombo, Combo);

        int multiplierIndex = quaverMultiplierCount / 10;
        quaverScoreCount +=
            quaverScoreWeight(effectiveRating)
            + multiplierIndex * 10;
        if (!isMine)
        {
            currentAccuracyJudgementCount++;
            quaverAccuracyWeightTotal +=
                quaverAccuracyWeight(effectiveRating);
        }

        updateQuaverScore();
    }

    private void updateQuaverScore()
    {
        TotalScoreWithoutMods = quaverMaximumScoreCount > 0
            ? (long)(1_000_000d
                     * quaverScoreCount
                     / quaverMaximumScoreCount)
            : 0;
        TotalScore = (long)Math.Round(
            TotalScoreWithoutMods * scoreMultiplier);
        Rank = RankFromScore(Accuracy, Counts);
    }

    private static int calculateQuaverMaximumScoreCount(
        int judgementCount)
    {
        int result = 0;
        for (int index = 1;
             index <= judgementCount && index < 150;
             index++)
        {
            result += 100 + 10 * (index / 10);
        }

        if (judgementCount >= 150)
            result += (judgementCount - 149) * 250;

        return result;
    }

    private static int quaverScoreWeight(
        JudgementRating rating) => rating switch
        {
            JudgementRating.Perfect => 100,
            JudgementRating.Great => 50,
            JudgementRating.Good => 25,
            JudgementRating.Ok => 10,
            JudgementRating.Meh => 5,
            _ => 0,
        };

    private static double quaverAccuracyWeight(
        JudgementRating rating) => rating switch
        {
            JudgementRating.Perfect => 100,
            JudgementRating.Great => 98.25,
            JudgementRating.Good => 65,
            JudgementRating.Ok => 25,
            JudgementRating.Meh => -100,
            JudgementRating.Miss => -50,
            _ => 0,
        };

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
