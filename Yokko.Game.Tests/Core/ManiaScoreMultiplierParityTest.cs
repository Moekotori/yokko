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
}
