using System.Collections.Generic;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Core.Timing;

namespace Yokko.Game.Tests.Core;

/// <summary>
/// Mirrors the current-score cases in osu!lazer's
/// ManiaScoreMultiplierTest at
/// 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0 (MIT).
/// Historical score-version compatibility cases do not apply to new Yokko
/// scores.
/// </summary>
[TestFixture]
public sealed class ManiaScoreMultiplierParityTest
{
    private static readonly double[] slow_rates =
    [
        0.50,
        0.55,
        0.60,
        0.65,
        0.70,
        0.75,
        0.80,
        0.85,
        0.90,
        0.95,
        0.99,
    ];

    private static readonly double[] fast_rates =
    [
        1.01,
        1.05,
        1.10,
        1.15,
        1.20,
        1.25,
        1.30,
        1.35,
        1.40,
        1.45,
        1.50,
        1.55,
        1.60,
        1.65,
        1.70,
        1.75,
        1.80,
        1.85,
        1.90,
        1.95,
        2.00,
    ];

    [TestCase(ManiaModId.Easy, 0.5)]
    [TestCase(ManiaModId.NoFail, 0.5)]
    [TestCase(ManiaModId.HalfTime, 0.3)]
    [TestCase(ManiaModId.Daycore, 0.3)]
    [TestCase(ManiaModId.NoRelease, 0.9)]
    [TestCase(ManiaModId.HardRock, 1)]
    [TestCase(ManiaModId.SuddenDeath, 1)]
    [TestCase(ManiaModId.Perfect, 1)]
    [TestCase(ManiaModId.DoubleTime, 1)]
    [TestCase(ManiaModId.Nightcore, 1)]
    [TestCase(ManiaModId.FadeIn, 1)]
    [TestCase(ManiaModId.Hidden, 1)]
    [TestCase(ManiaModId.Cover, 1)]
    [TestCase(ManiaModId.Flashlight, 1)]
    [TestCase(ManiaModId.AccuracyChallenge, 1)]
    [TestCase(ManiaModId.Random, 1)]
    [TestCase(ManiaModId.DualStages, 1)]
    [TestCase(ManiaModId.Mirror, 1)]
    [TestCase(ManiaModId.DifficultyAdjust, 0.5)]
    [TestCase(ManiaModId.Classic, 1)]
    [TestCase(ManiaModId.Invert, 1)]
    [TestCase(ManiaModId.ConstantSpeed, 0.9)]
    [TestCase(ManiaModId.HoldOff, 0.9)]
    [TestCase(ManiaModId.Key1, 0.9)]
    [TestCase(ManiaModId.Key2, 0.9)]
    [TestCase(ManiaModId.Key3, 0.9)]
    [TestCase(ManiaModId.Key4, 0.9)]
    [TestCase(ManiaModId.Key5, 0.9)]
    [TestCase(ManiaModId.Key6, 0.9)]
    [TestCase(ManiaModId.Key7, 0.9)]
    [TestCase(ManiaModId.Key8, 0.9)]
    [TestCase(ManiaModId.Key9, 0.9)]
    [TestCase(ManiaModId.Key10, 0.9)]
    [TestCase(ManiaModId.Autoplay, 1)]
    [TestCase(ManiaModId.Cinema, 1)]
    [TestCase(ManiaModId.WindUp, 0.5)]
    [TestCase(ManiaModId.WindDown, 0.5)]
    [TestCase(ManiaModId.Muted, 1)]
    [TestCase(ManiaModId.AdaptiveSpeed, 0.5)]
    [TestCase(ManiaModId.ScoreV2, 1)]
    public void SingleModMatchesLazerCurrentScoreMultiplier(
        ManiaModId mod,
        double expected)
    {
        var mods = new ManiaModSet([mod]);

        Assert.That(
            mods.ScoreMultiplier,
            Is.EqualTo(expected).Within(1e-12));
    }

    [Test]
    public void CombinationMultipliesLikeLazer()
    {
        var mods = new ManiaModSet(
        [
            ManiaModId.Easy,
            ManiaModId.Key4,
        ]);

        Assert.That(
            mods.ScoreMultiplier,
            Is.EqualTo(0.5 * 0.9).Within(1e-12));
    }

    [TestCaseSource(nameof(slowRateCases))]
    public void ConfiguredSlowRateMatchesCompleteLazerMatrix(
        ManiaModId mod,
        double rate,
        double expected)
    {
        ManiaModSet mods = ManiaModSet.Empty.WithFixedRate(mod, rate);

        Assert.That(
            mods.ScoreMultiplier,
            Is.EqualTo(expected).Within(1e-12));
    }

    [TestCaseSource(nameof(fastRateCases))]
    public void ConfiguredFastRateMatchesCompleteLazerMatrix(
        ManiaModId mod,
        double rate)
    {
        ManiaModSet mods = ManiaModSet.Empty.WithFixedRate(mod, rate);

        Assert.That(mods.ScoreMultiplier, Is.EqualTo(1).Within(1e-12));
    }

    [Test]
    public void ScoreProcessorRoundsBeforeApplyingMultiplierLikeLazer()
    {
        YokkoBeatmap beatmap = createTapBeatmap();
        var processor = new ManiaScoreProcessor(
            beatmap,
            new ManiaModSet([ManiaModId.Easy])
                .ScoreMultiplier);

        processor.Apply(JudgementRating.Perfect);

        Assert.Multiple(() =>
        {
            Assert.That(
                processor.TotalScoreWithoutMods,
                Is.EqualTo(1_000_000));
            Assert.That(
                processor.TotalScore,
                Is.EqualTo(500_000));
        });
    }

    private static YokkoBeatmap createTapBeatmap() =>
        new(
            "Score multiplier parity",
            "Yokko",
            "Tests",
            "4K",
            KeyMode.FourKey,
            ChartSourceFormat.Yokko,
            [YokkoTimingPoint.Default],
            null,
            [
                new YokkoHitObject(
                    0,
                    1000,
                    null,
                    HitObjectKind.Tap),
            ]);

    private static IEnumerable<TestCaseData> slowRateCases()
    {
        foreach (ManiaModId mod in new[]
                 {
                     ManiaModId.HalfTime,
                     ManiaModId.Daycore,
                 })
        {
            foreach (double rate in slow_rates)
            {
                double expected = rate switch
                {
                    < 0.60 => 0.1,
                    < 0.70 => 0.2,
                    < 0.80 => 0.3,
                    < 0.90 => 0.4,
                    _ => 0.5,
                };
                yield return new TestCaseData(mod, rate, expected)
                    .SetName($"{mod}_{rate:0.00}_matches_lazer_multiplier");
            }
        }
    }

    private static IEnumerable<TestCaseData> fastRateCases()
    {
        foreach (ManiaModId mod in new[]
                 {
                     ManiaModId.DoubleTime,
                     ManiaModId.Nightcore,
                 })
        {
            foreach (double rate in fast_rates)
            {
                yield return new TestCaseData(mod, rate)
                    .SetName($"{mod}_{rate:0.00}_matches_lazer_multiplier");
            }
        }
    }
}
