using System.Linq;
using System;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Core.Timing;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class ManiaHealthStateTest
{
    [Test]
    public void ManiaHealthUsesLazerDrainAndRecoveryValues()
    {
        YokkoBeatmap beatmap = createBeatmap(drainRate: 5);
        var health = new ManiaHealthState(beatmap);

        health.Apply(judgement(JudgementRating.Miss));
        Assert.That(health.Health, Is.EqualTo(0.955).Within(1e-12));

        health.Apply(judgement(JudgementRating.Perfect));
        Assert.Multiple(() =>
        {
            double expectedRecoveryMultiplier =
                Math.Pow(1.01, 191);
            Assert.That(
                health.RecoveryMultiplier,
                Is.EqualTo(expectedRecoveryMultiplier).Within(1e-12));
            Assert.That(
                health.Health,
                Is.EqualTo(
                    0.955
                    + expectedRecoveryMultiplier * 0.003)
                  .Within(1e-12));
        });
    }

    [Test]
    public void BreakPeriodsMatchLazerRecoverySimulation()
    {
        YokkoBeatmap withoutBreak = new(
            "Health gap test",
            "Yokko",
            "Tests",
            "4K",
            KeyMode.FourKey,
            ChartSourceFormat.OsuMania,
            [YokkoTimingPoint.Default],
            null,
            [
                new YokkoHitObject(
                    0,
                    1000,
                    null,
                    HitObjectKind.Tap),
                new YokkoHitObject(
                    1,
                    10_000,
                    null,
                    HitObjectKind.Tap),
            ],
            DrainRate: 5.5);
        YokkoBeatmap withBreak = withoutBreak with
        {
            BreakPeriods =
            [
                new YokkoBreakPeriod(2000, 9000),
            ],
        };

        var uninterruptedHealth =
            new ManiaHealthState(withoutBreak);
        var breakHealth = new ManiaHealthState(withBreak);

        Assert.That(
            breakHealth.RecoveryMultiplier,
            Is.LessThan(uninterruptedHealth.RecoveryMultiplier));
    }

    [Test]
    public void HoldHeadAndTailUseHalfMissPenalty()
    {
        YokkoBeatmap beatmap = createBeatmap(
            drainRate: 5,
            kind: HitObjectKind.Hold);
        var health = new ManiaHealthState(beatmap);

        health.Apply(judgement(
            JudgementRating.Miss,
            JudgementPhase.HoldHead));

        Assert.That(health.Health, Is.EqualTo(0.9775).Within(1e-12));
    }

    [Test]
    public void MineHitUsesStepManiaHealthPenalty()
    {
        YokkoBeatmap beatmap = createBeatmap(
            kind: HitObjectKind.Mine);
        var health = new ManiaHealthState(beatmap);

        health.Apply(judgement(
            JudgementRating.IgnoreMiss,
            JudgementPhase.Mine));

        Assert.That(health.Health, Is.EqualTo(0.84).Within(1e-12));
    }

    [Test]
    public void EtternaUsesDefaultLifeTable()
    {
        YokkoBeatmap beatmap = createBeatmap();
        var health = new ManiaHealthState(
            beatmap,
            judgementConfiguration:
                JudgementConfiguration.EtternaDefault);

        health.Apply(judgement(JudgementRating.Meh));
        health.Apply(judgement(JudgementRating.Good));
        health.Apply(judgement(JudgementRating.Ok));
        health.Apply(judgement(JudgementRating.Miss));
        health.Apply(judgement(
            JudgementRating.IgnoreMiss,
            JudgementPhase.Mine));

        Assert.That(
            health.Health,
            Is.EqualTo(0.724).Within(1e-12));
    }

    [Test]
    public void EtternaSuddenDeathUsesW3ComboThreshold()
    {
        YokkoBeatmap beatmap = createBeatmap();
        var etterna = new ManiaHealthState(
            beatmap,
            new ManiaModSet([ManiaModId.SuddenDeath]),
            JudgementConfiguration.EtternaDefault);
        var yokko = new ManiaHealthState(
            beatmap,
            new ManiaModSet([ManiaModId.SuddenDeath]));

        ManiaHealthUpdate w3 =
            etterna.Apply(judgement(JudgementRating.Good));
        ManiaHealthUpdate w4 =
            etterna.Apply(judgement(JudgementRating.Ok));
        ManiaHealthUpdate yokkoOkay =
            yokko.Apply(judgement(JudgementRating.Ok));

        Assert.Multiple(() =>
        {
            Assert.That(w3.Failed, Is.False);
            Assert.That(
                w4.FailReason,
                Is.EqualTo(ManiaFailReason.SuddenDeath));
            Assert.That(yokkoOkay.Failed, Is.False);
            Assert.That(
                yokkoOkay.FailReason,
                Is.EqualTo(ManiaFailReason.None));
        });
    }

    [Test]
    public void NoFailAllowsHealthToRemainAtZero()
    {
        YokkoBeatmap beatmap = createBeatmap(drainRate: 10);
        var health = new ManiaHealthState(
            beatmap,
            new ManiaModSet([ManiaModId.NoFail]));

        for (int i = 0; i < 20; i++)
            health.Apply(judgement(JudgementRating.Miss));

        Assert.Multiple(() =>
        {
            Assert.That(health.Health, Is.Zero);
            Assert.That(health.HasFailed, Is.False);
        });
    }

    [Test]
    public void SuddenDeathOnlyFailsOnComboBreakingResult()
    {
        YokkoBeatmap beatmap = createBeatmap();
        var health = new ManiaHealthState(
            beatmap,
            new ManiaModSet([ManiaModId.SuddenDeath]));

        ManiaHealthUpdate good =
            health.Apply(judgement(JudgementRating.Good));
        ManiaHealthUpdate ignored =
            health.Apply(judgement(JudgementRating.IgnoreHit));
        ManiaHealthUpdate comboBreak =
            health.Apply(judgement(JudgementRating.ComboBreak));

        Assert.Multiple(() =>
        {
            Assert.That(good.Failed, Is.False);
            Assert.That(ignored.Failed, Is.False);
            Assert.That(comboBreak.Failed, Is.True);
            Assert.That(
                comboBreak.FailReason,
                Is.EqualTo(ManiaFailReason.SuddenDeath));
        });
    }

    [Test]
    public void PerfectUsesLazerManiaDefaultGreatThreshold()
    {
        YokkoBeatmap beatmap = createBeatmap();
        var health = new ManiaHealthState(
            beatmap,
            new ManiaModSet([ManiaModId.Perfect]));

        ManiaHealthUpdate great =
            health.Apply(judgement(JudgementRating.Great));
        ManiaHealthUpdate ignored =
            health.Apply(judgement(JudgementRating.IgnoreHit));
        ManiaHealthUpdate good =
            health.Apply(judgement(JudgementRating.Good));

        Assert.Multiple(() =>
        {
            Assert.That(great.Failed, Is.False);
            Assert.That(ignored.Failed, Is.False);
            Assert.That(good.Failed, Is.True);
            Assert.That(
                good.FailReason,
                Is.EqualTo(ManiaFailReason.PerfectBroken));
        });
    }

    [Test]
    public void PerfectFailsOnHoldBodyComboBreak()
    {
        YokkoBeatmap beatmap = createBeatmap();
        var health = new ManiaHealthState(
            beatmap,
            new ManiaModSet([ManiaModId.Perfect]));

        ManiaHealthUpdate comboBreak = health.Apply(
            judgement(
                JudgementRating.ComboBreak,
                JudgementPhase.HoldBody));

        Assert.That(
            comboBreak.FailReason,
            Is.EqualTo(ManiaFailReason.PerfectBroken));
    }

    [Test]
    public void PerfectStrictModeFailsOnGreatLikeLazer()
    {
        YokkoBeatmap beatmap = createBeatmap();
        var defaultHealth = new ManiaHealthState(
            beatmap,
            ManiaModSet.Empty.WithPerfect(false));
        var strictHealth = new ManiaHealthState(
            beatmap,
            ManiaModSet.Empty.WithPerfect(true));

        ManiaHealthUpdate defaultGreat =
            defaultHealth.Apply(judgement(JudgementRating.Great));
        ManiaHealthUpdate strictGreat =
            strictHealth.Apply(judgement(JudgementRating.Great));

        Assert.Multiple(() =>
        {
            Assert.That(defaultGreat.Failed, Is.False);
            Assert.That(strictGreat.Failed, Is.True);
            Assert.That(
                strictGreat.FailReason,
                Is.EqualTo(ManiaFailReason.PerfectBroken));
        });
    }

    [Test]
    public void EasyHalvesDrainWidensWindowsAndProvidesTwoLives()
    {
        YokkoBeatmap beatmap = createBeatmap(drainRate: 8);
        var mods = new ManiaModSet(
        [
            ManiaModId.Easy,
            ManiaModId.Perfect,
        ]);
        var health = new ManiaHealthState(beatmap, mods);

        ManiaHealthUpdate first =
            health.Apply(judgement(JudgementRating.Good));
        ManiaHealthUpdate second =
            health.Apply(judgement(JudgementRating.Good));
        ManiaHealthUpdate third =
            health.Apply(judgement(JudgementRating.Good));

        Assert.Multiple(() =>
        {
            Assert.That(health.EffectiveDrainRate, Is.EqualTo(4));
            Assert.That(
                mods.HitWindowDifficultyMultiplier,
                Is.EqualTo(1 / 1.4).Within(1e-12));
            Assert.That(first.ExtraLifeConsumed, Is.True);
            Assert.That(second.ExtraLifeConsumed, Is.True);
            Assert.That(third.Failed, Is.True);
            Assert.That(health.RemainingExtraLives, Is.Zero);
        });
    }

    [Test]
    public void HardRockRaisesDrainAndTightensWindows()
    {
        YokkoBeatmap beatmap = createBeatmap(drainRate: 8);
        var mods = new ManiaModSet([ManiaModId.HardRock]);
        var health = new ManiaHealthState(beatmap, mods);

        Assert.Multiple(() =>
        {
            Assert.That(health.EffectiveDrainRate, Is.EqualTo(10));
            Assert.That(
                mods.HitWindowDifficultyMultiplier,
                Is.EqualTo(1.4));
        });
    }

    [TestCase(ManiaModId.IidxHardGauge, ManiaGaugeMode.IidxHard, 0.91)]
    [TestCase(ManiaModId.Lr2HardGauge, ManiaGaugeMode.Lr2Hard, 0.90)]
    [TestCase(ManiaModId.BeatorajaHardGauge, ManiaGaugeMode.BeatorajaHard, 0.90)]
    public void HardGaugeIsIndependentFromJudgementMode(
        ManiaModId gaugeMod,
        ManiaGaugeMode expectedMode,
        double expectedHealth)
    {
        YokkoBeatmap beatmap = createBeatmap();
        var health = new ManiaHealthState(
            beatmap,
            new ManiaModSet([gaugeMod]),
            JudgementConfiguration.QuaverDefault);

        health.Apply(judgement(JudgementRating.Miss));

        Assert.Multiple(() =>
        {
            Assert.That(health.GaugeMode, Is.EqualTo(expectedMode));
            Assert.That(health.Health, Is.EqualTo(expectedHealth).Within(1e-12));
        });
    }

    [Test]
    public void HardGaugesApplyTheirLowHealthScaling()
    {
        YokkoBeatmap beatmap = createBeatmap();
        var iidx = new ManiaHealthState(
            beatmap,
            new ManiaModSet([ManiaModId.IidxHardGauge]));
        var lr2 = new ManiaHealthState(
            beatmap,
            new ManiaModSet([ManiaModId.Lr2HardGauge]));

        for (int i = 0; i < 8; i++)
            iidx.Apply(judgement(JudgementRating.Miss));
        for (int i = 0; i < 8; i++)
            lr2.Apply(judgement(JudgementRating.Miss));

        Assert.Multiple(() =>
        {
            Assert.That(iidx.Health, Is.EqualTo(0.28).Within(1e-12));
            Assert.That(lr2.Health, Is.EqualTo(0.20).Within(1e-12));
        });

        iidx.Apply(judgement(JudgementRating.Miss));
        lr2.Apply(judgement(JudgementRating.Miss));

        Assert.Multiple(() =>
        {
            Assert.That(iidx.Health, Is.EqualTo(0.235).Within(1e-12));
            Assert.That(lr2.Health, Is.EqualTo(0.14).Within(1e-12));
        });
    }

    [Test]
    public void BeatorajaHardInterpolatesDamageBelowHalfHealth()
    {
        YokkoBeatmap beatmap = createBeatmap();
        var health = new ManiaHealthState(
            beatmap,
            new ManiaModSet([ManiaModId.BeatorajaHardGauge]));

        for (int i = 0; i < 5; i++)
            health.Apply(judgement(JudgementRating.Miss));
        health.Apply(judgement(JudgementRating.Miss));
        health.Apply(judgement(JudgementRating.Miss));

        Assert.That(health.Health, Is.EqualTo(0.32).Within(1e-12));
    }

    [Test]
    public void HardGaugeSelectionIsExclusiveAndDisablesNoFail()
    {
        ManiaModSet selected = ManiaModSet.Empty
            .With(ManiaModId.IidxHardGauge, true)
            .With(ManiaModId.Lr2HardGauge, true)
            .With(ManiaModId.NoFail, true);

        Assert.Multiple(() =>
        {
            Assert.That(selected.Contains(ManiaModId.NoFail), Is.True);
            Assert.That(selected.GaugeMode, Is.EqualTo(ManiaGaugeMode.Yokko));
            Assert.That(
                () => new ManiaModSet([
                    ManiaModId.IidxHardGauge,
                    ManiaModId.BeatorajaHardGauge,
                ]),
                Throws.ArgumentException);
        });
    }

    [Test]
    public void DifficultyAdjustOverridesDrainAndExtendedHitWindows()
    {
        YokkoBeatmap beatmap = createBeatmap(drainRate: 5);
        ManiaModSet mods =
            ManiaModSet.Empty.WithDifficultyAdjust(9.5, 12, true);
        var health = new ManiaHealthState(beatmap, mods);
        var windows = new JudgementWindows(
            mods.EffectiveOverallDifficulty(
                beatmap.OverallDifficulty));

        Assert.Multiple(() =>
        {
            Assert.That(
                health.EffectiveDrainRate,
                Is.EqualTo(9.5));
            Assert.That(windows.OverallDifficulty, Is.EqualTo(12));
            Assert.That(
                windows.PerfectMilliseconds,
                Is.LessThan(
                    new JudgementWindows(10)
                        .PerfectMilliseconds));
        });
    }

    [Test]
    public void AccuracyChallengeUsesConfiguredAccuracyMode()
    {
        YokkoBeatmap beatmap = createBeatmap();
        ManiaModSet maximumMods =
            ManiaModSet.Empty.WithAccuracyChallenge(
                0.9,
                ManiaAccuracyMode.MaximumAchievable);
        var maximumHealth =
            new ManiaHealthState(beatmap, maximumMods);

        ManiaHealthUpdate atThreshold = maximumHealth.Apply(
            judgement(JudgementRating.Miss),
            standardAccuracy: 0,
            maximumAchievableAccuracy: 0.9);
        ManiaHealthUpdate belowThreshold = maximumHealth.Apply(
            judgement(JudgementRating.Miss),
            standardAccuracy: 0,
            maximumAchievableAccuracy: 0.899);

        ManiaModSet standardMods =
            ManiaModSet.Empty.WithAccuracyChallenge(
                0.95,
                ManiaAccuracyMode.Standard);
        var standardHealth =
            new ManiaHealthState(beatmap, standardMods);
        ManiaHealthUpdate standardFailure = standardHealth.Apply(
            judgement(JudgementRating.Great),
            standardAccuracy: 0.949,
            maximumAchievableAccuracy: 1);

        Assert.Multiple(() =>
        {
            Assert.That(atThreshold.Failed, Is.False);
            Assert.That(
                belowThreshold.FailReason,
                Is.EqualTo(ManiaFailReason.AccuracyChallenge));
            Assert.That(
                standardFailure.FailReason,
                Is.EqualTo(ManiaFailReason.AccuracyChallenge));
        });
    }

    [Test]
    public void ScoreProcessorTracksMaximumAchievableAccuracy()
    {
        YokkoBeatmap beatmap = createBeatmap(noteCount: 2);
        var processor = new ManiaScoreProcessor(beatmap);

        processor.Apply(JudgementRating.Miss);

        Assert.Multiple(() =>
        {
            Assert.That(processor.Accuracy, Is.Zero);
            Assert.That(
                processor.MaximumAchievableAccuracy,
                Is.EqualTo(0.5));
        });
    }

    private static JudgementEvent judgement(
        JudgementRating rating,
        JudgementPhase phase = JudgementPhase.Tap) =>
        new(
            0,
            0,
            1000,
            1000,
            0,
            rating,
            phase);

    private static YokkoBeatmap createBeatmap(
        double drainRate = 5,
        HitObjectKind kind = HitObjectKind.Tap,
        int noteCount = 1) =>
        new(
            "Health test",
            "Yokko",
            "Tests",
            "4K",
            KeyMode.FourKey,
            ChartSourceFormat.Yokko,
            [YokkoTimingPoint.Default],
            null,
            Enumerable.Range(0, noteCount)
                      .Select(index => new YokkoHitObject(
                          index % 4,
                          1000 + index * 500,
                          kind == HitObjectKind.Hold
                              ? 1250 + index * 500
                              : null,
                          kind))
                      .ToArray(),
            DrainRate: drainRate);
}
