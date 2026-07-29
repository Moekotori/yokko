using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;

namespace Yokko.Core.Scoring;

/// <summary>
/// Deterministic osu!lazer-style Mania health and fail state.
/// Health deltas and Easy behaviour are adapted from ppy/osu
/// ManiaHealthProcessor and ManiaModEasy at
/// 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0 (MIT).
/// </summary>
public sealed class ManiaHealthState
{
    private const int defaultEasyExtraLives = 2;
    private readonly ManiaModSet mods;

    public ManiaHealthState(
        YokkoBeatmap beatmap,
        ManiaModSet? mods = null)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        this.mods = mods ?? ManiaModSet.Empty;

        EffectiveDrainRate =
            this.mods.EffectiveDrainRate(beatmap.DrainRate);
        RecoveryMultiplier = computeRecoveryMultiplier(
            beatmap,
            EffectiveDrainRate);
        RemainingExtraLives = this.mods.Contains(ManiaModId.Easy)
            ? defaultEasyExtraLives
            : 0;
    }

    public double Health { get; private set; } = 1;

    public double EffectiveDrainRate { get; }

    public double RecoveryMultiplier { get; }

    public int RemainingExtraLives { get; private set; }

    public bool HasFailed { get; private set; }

    public ManiaFailReason FailureReason { get; private set; }

    public ManiaHealthUpdate Apply(
        JudgementEvent judgement,
        double standardAccuracy = 1,
        double maximumAchievableAccuracy = 1)
    {
        ArgumentNullException.ThrowIfNull(judgement);

        double previousHealth = Health;
        if (HasFailed)
        {
            return new ManiaHealthUpdate(
                previousHealth,
                Health,
                false,
                FailureReason);
        }

        Health = Math.Clamp(
            Health + healthDelta(judgement),
            0,
            1);

        ManiaFailReason reason = failReasonFor(
            judgement,
            standardAccuracy,
            maximumAchievableAccuracy);
        if (reason == ManiaFailReason.None)
        {
            return new ManiaHealthUpdate(
                previousHealth,
                Health,
                false,
                ManiaFailReason.None);
        }

        if (RemainingExtraLives > 0)
        {
            RemainingExtraLives--;
            Health = 1;
            return new ManiaHealthUpdate(
                previousHealth,
                Health,
                true,
                ManiaFailReason.None);
        }

        HasFailed = true;
        FailureReason = reason;
        return new ManiaHealthUpdate(
            previousHealth,
            Health,
            false,
            reason);
    }

    private ManiaFailReason failReasonFor(
        JudgementEvent judgement,
        double standardAccuracy,
        double maximumAchievableAccuracy)
    {
        if (mods.Contains(ManiaModId.Perfect)
            && (judgement.Rating.AffectsAccuracy()
                || judgement.Rating.AffectsCombo())
            && judgement.Rating != JudgementRating.Perfect)
        {
            return ManiaFailReason.PerfectBroken;
        }

        if (mods.Contains(ManiaModId.SuddenDeath)
            && judgement.Rating.AffectsCombo()
            && !judgement.Rating.IsHit())
        {
            return ManiaFailReason.SuddenDeath;
        }

        if (mods.Contains(ManiaModId.AccuracyChallenge))
        {
            double judgedAccuracy =
                mods.AccuracyChallengeMode
                == ManiaAccuracyMode.MaximumAchievable
                    ? maximumAchievableAccuracy
                    : standardAccuracy;
            if (judgedAccuracy < mods.AccuracyChallengeMinimum)
                return ManiaFailReason.AccuracyChallenge;
        }

        if (!mods.Contains(ManiaModId.NoFail)
            && !mods.Contains(ManiaModId.Cinema)
            && Health <= 0)
        {
            return ManiaFailReason.HealthDepleted;
        }

        return ManiaFailReason.None;
    }

    private double healthDelta(JudgementEvent judgement)
    {
        double drainFactor = EffectiveDrainRate + 1;

        return judgement.Rating switch
        {
            JudgementRating.Miss =>
                -drainFactor
                * (judgement.Phase is JudgementPhase.HoldHead
                    or JudgementPhase.HoldTail
                    ? 0.00375
                    : 0.0075),
            JudgementRating.Meh => -drainFactor * 0.0016,
            JudgementRating.Ok => 0,
            JudgementRating.Good =>
                RecoveryMultiplier
                * (0.004 - EffectiveDrainRate * 0.0004),
            JudgementRating.Great =>
                RecoveryMultiplier
                * (0.005 - EffectiveDrainRate * 0.0005),
            JudgementRating.Perfect =>
                RecoveryMultiplier
                * (0.0055 - EffectiveDrainRate * 0.0005),
            _ => 0,
        };
    }

    private static double computeRecoveryMultiplier(
        YokkoBeatmap beatmap,
        double drainRate)
    {
        int topLevelCount = beatmap.HitObjects.Count(
            static hitObject => hitObject.Kind is
                HitObjectKind.Tap or HitObjectKind.Hold);
        if (topLevelCount == 0)
            return 1;

        int healthJudgementCount = beatmap.HitObjects.Sum(
            static hitObject => hitObject.Kind switch
            {
                HitObjectKind.Tap => 1,
                HitObjectKind.Hold => 2,
                _ => 0,
            });
        if (healthJudgementCount == 0)
            return 1;

        double basePerfectIncrease =
            0.0055 - drainRate * 0.0005;
        if (basePerfectIncrease <= 0)
            return 1;

        double targetRecovery = difficultyRange(
            drainRate,
            0.04,
            0.02,
            0);
        return Math.Max(
            1,
            targetRecovery * topLevelCount
            / (basePerfectIncrease * healthJudgementCount));
    }

    private static double difficultyRange(
        double difficulty,
        double minimum,
        double average,
        double maximum)
    {
        return difficulty > 5
            ? average
              + (maximum - average) * (difficulty - 5) / 5
            : minimum
              + (average - minimum) * difficulty / 5;
    }
}
