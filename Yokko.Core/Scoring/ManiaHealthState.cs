using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;

namespace Yokko.Core.Scoring;

/// <summary>
/// Deterministic osu!lazer-style Mania health and fail state.
/// Health deltas and Easy behaviour are adapted from ppy/osu
/// ManiaHealthProcessor, LegacyDrainingHealthProcessor, ManiaModEasy,
/// ManiaModPerfect and ManiaModSuddenDeath at
/// 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0 (MIT).
/// LegacyDrainingHealthProcessor is part of this pinned lazer source tree;
/// its name describes compatibility behaviour, not an osu!stable dependency.
/// Optional IIDX/LR2/beatoraja hard-gauge tables and low-health scaling are
/// adapted from SK-la/Ez2Lazer ManiaHealthProcessor and HealthModeHelper at
/// ecb85832fca6f7d3731079fa474df2f44bd34101 (MIT).
/// </summary>
public sealed class ManiaHealthState
{
    private const int defaultEasyExtraLives = 2;

    // StepMania fallback metric LifePercentChangeHitMine.
    // Source: stepmania/stepmania Themes/_fallback/metrics.ini
    // commit 21bb8dcd6c7e3782f23d5f4e01b6ee4c82cccc71
    // (StepMania permissive licence; see Docs/Licenses.txt).
    private const double mineHitHealthPenalty = 0.16;
    private readonly ManiaModSet mods;
    private readonly JudgementConfiguration judgementConfiguration;

    public ManiaHealthState(
        YokkoBeatmap beatmap,
        ManiaModSet? mods = null,
        JudgementConfiguration? judgementConfiguration = null)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        this.mods = mods ?? ManiaModSet.Empty;
        this.judgementConfiguration =
            judgementConfiguration
            ?? JudgementConfiguration.YokkoDefault;

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

    public ManiaGaugeMode GaugeMode => mods.GaugeMode;

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
            && judgement.Phase != JudgementPhase.BmsEmptyPress
            && (judgement.Rating.AffectsAccuracy()
                || judgement.Rating.AffectsCombo())
            && (mods.PerfectRequirePerfectHits
                ? judgement.Rating
                  != JudgementRating.Perfect
                : judgement.Rating is not (
                    JudgementRating.Great
                    or JudgementRating.Perfect)))
        {
            return ManiaFailReason.PerfectBroken;
        }

        if (mods.Contains(ManiaModId.SuddenDeath)
            && (this.judgementConfiguration.Mode
                    == JudgementMode.Etterna
                ? EtternaScoringRules.BreaksCombo(judgement.Rating)
                  || judgement.Phase == JudgementPhase.HoldBody
                  && judgement.Rating == JudgementRating.ComboBreak
                : judgement.Rating.AffectsCombo()
                  && !judgement.Rating.IsHit()))
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
        if (GaugeMode != ManiaGaugeMode.Yokko)
            return hardGaugeDelta(judgement);

        if (judgementConfiguration.Mode == JudgementMode.Etterna)
            return EtternaScoringRules.LifeDelta(judgement);

        if (judgement.Phase == JudgementPhase.Mine)
        {
            return judgement.Rating == JudgementRating.IgnoreMiss
                ? -mineHitHealthPenalty
                : 0;
        }

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

    private double hardGaugeDelta(JudgementEvent judgement)
    {
        if (judgement.Phase == JudgementPhase.Mine)
        {
            return judgement.Rating == JudgementRating.IgnoreMiss
                ? -mineHitHealthPenalty
                : 0;
        }

        double delta = (GaugeMode, judgement.Rating) switch
        {
            (ManiaGaugeMode.IidxHard, JudgementRating.Perfect) => 0.0016,
            (ManiaGaugeMode.IidxHard, JudgementRating.Great) => 0.0016,
            (ManiaGaugeMode.IidxHard, JudgementRating.Good) => 0,
            (ManiaGaugeMode.IidxHard, JudgementRating.Ok) => 0,
            (ManiaGaugeMode.IidxHard, JudgementRating.Meh) => -0.05,
            (ManiaGaugeMode.IidxHard, JudgementRating.Miss) => -0.09,
            (ManiaGaugeMode.IidxHard, JudgementRating.ComboBreak) => -0.05,

            (ManiaGaugeMode.Lr2Hard, JudgementRating.Perfect) => 0.001,
            (ManiaGaugeMode.Lr2Hard, JudgementRating.Great) => 0.001,
            (ManiaGaugeMode.Lr2Hard, JudgementRating.Good) => 0.0005,
            (ManiaGaugeMode.Lr2Hard, JudgementRating.Ok) => 0,
            (ManiaGaugeMode.Lr2Hard, JudgementRating.Meh) => -0.06,
            (ManiaGaugeMode.Lr2Hard, JudgementRating.Miss) => -0.10,
            (ManiaGaugeMode.Lr2Hard, JudgementRating.ComboBreak) => -0.02,

            (ManiaGaugeMode.BeatorajaHard, JudgementRating.Perfect) => 0.0015,
            (ManiaGaugeMode.BeatorajaHard, JudgementRating.Great) => 0.0012,
            (ManiaGaugeMode.BeatorajaHard, JudgementRating.Good) => 0.0003,
            (ManiaGaugeMode.BeatorajaHard, JudgementRating.Ok) => 0,
            (ManiaGaugeMode.BeatorajaHard, JudgementRating.Meh) => -0.05,
            (ManiaGaugeMode.BeatorajaHard, JudgementRating.Miss) => -0.10,
            (ManiaGaugeMode.BeatorajaHard, JudgementRating.ComboBreak) => -0.05,
            _ => 0,
        };

        if (delta >= 0)
            return delta;

        return GaugeMode switch
        {
            ManiaGaugeMode.IidxHard when Health <= 0.3 => delta * 0.5,
            ManiaGaugeMode.Lr2Hard when Health <= 0.3 => delta * 0.6,
            ManiaGaugeMode.BeatorajaHard when Health <= 0.3 => delta * 0.6,
            ManiaGaugeMode.BeatorajaHard when Health < 0.5 =>
                delta * (0.6 + (Health - 0.3) / 0.2 * 0.4),
            _ => delta,
        };
    }

    private static double computeRecoveryMultiplier(
        YokkoBeatmap beatmap,
        double drainRate)
    {
        YokkoHitObject[] hitObjects = beatmap.HitObjects
            .Where(static hitObject => hitObject.Kind is
                HitObjectKind.Tap or HitObjectKind.Hold)
            .ToArray();
        if (hitObjects.Length == 0)
            return 1;

        double basePerfectIncrease =
            0.0055 - drainRate * 0.0005;
        if (basePerfectIncrease <= 0)
            return 1;

        double lowestHealthEver = difficultyRange(
            drainRate,
            0.975,
            0.8,
            0.3);
        double lowestHealthEnd = difficultyRange(
            drainRate,
            0.99,
            0.9,
            0.4);
        double recoveryRequired = difficultyRange(
            drainRate,
            0.04,
            0.02,
            0);
        YokkoBreakPeriod[] breaks = beatmap.BreakPeriods
            .OrderBy(static period => period.StartTimeMilliseconds)
            .ThenBy(static period => period.EndTimeMilliseconds)
            .ToArray();

        double testDrop = 0.00025;
        double recoveryMultiplier = 1;
        double drainStartTime =
            hitObjects[0].StartTimeMilliseconds;

        while (true)
        {
            double currentHealth = 1;
            double uncappedHealth = 1;
            double lastTime = drainStartTime;
            int currentBreak = 0;
            bool failedIteration = false;

            foreach (YokkoHitObject hitObject in hitObjects)
            {
                while (currentBreak < breaks.Length
                       && breaks[currentBreak].EndTimeMilliseconds
                       <= hitObject.StartTimeMilliseconds)
                {
                    lastTime = hitObject.StartTimeMilliseconds;
                    currentBreak++;
                }

                reduceHealth(
                    testDrop
                    * (hitObject.StartTimeMilliseconds - lastTime));

                double endTime =
                    hitObject.EndTimeMilliseconds
                    ?? hitObject.StartTimeMilliseconds;
                lastTime = endTime;

                if (currentHealth <= lowestHealthEver)
                {
                    failedIteration = true;
                    testDrop *= 0.96;
                    break;
                }

                double healthReduction =
                    testDrop
                    * (endTime - hitObject.StartTimeMilliseconds);
                double healthOverkill = Math.Max(
                    0,
                    healthReduction - currentHealth);
                reduceHealth(healthReduction);

                int perfectJudgementCount =
                    hitObject.Kind == HitObjectKind.Hold ? 2 : 1;
                increaseHealth(
                    perfectJudgementCount
                    * recoveryMultiplier
                    * basePerfectIncrease);

                if (healthOverkill > 0
                    && currentHealth - healthOverkill
                    <= lowestHealthEver)
                {
                    failedIteration = true;
                    testDrop *= 0.96;
                    break;
                }
            }

            if (!failedIteration
                && currentHealth < lowestHealthEnd)
            {
                failedIteration = true;
                testDrop *= 0.94;
                recoveryMultiplier *= 1.01;
            }

            double recovery =
                (uncappedHealth - 1)
                / Math.Max(1, hitObjects.Length);
            if (!failedIteration
                && recovery < recoveryRequired)
            {
                failedIteration = true;
                testDrop *= 0.96;
                recoveryMultiplier *= 1.01;
            }

            if (!failedIteration
                && double.IsInfinity(recoveryMultiplier))
            {
                return 1;
            }

            if (!failedIteration)
                return recoveryMultiplier;

            void reduceHealth(double amount)
            {
                uncappedHealth = Math.Max(0, uncappedHealth - amount);
                currentHealth = Math.Max(0, currentHealth - amount);
            }

            void increaseHealth(double amount)
            {
                uncappedHealth += amount;
                currentHealth = Math.Clamp(
                    currentHealth + amount,
                    0,
                    1);
            }
        }
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
