using System.Linq;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Scoring;
using Yokko.Game.Gameplay;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class GameplayKeysoundSelectorTest
{
    [Test]
    public void LanePressUsesCurrentObjectAndAdvancesAfterJudgement()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
        {
            HitObjects =
            [
                new YokkoHitObject(
                    0,
                    1000,
                    null,
                    HitObjectKind.Tap,
                    "first.wav"),
                new YokkoHitObject(
                    0,
                    2000,
                    null,
                    HitObjectKind.Tap,
                    "second.wav"),
            ],
        };
        var judgementState = new BeatmapJudgementState(beatmap);
        var selector = new GameplayKeysoundSelector(
            beatmap,
            judgementState);

        Assert.That(selector.Select(0, 0), Is.Zero);
        Assert.That(selector.Select(0, 900), Is.Zero);

        JudgementEvent judgement =
            judgementState.JudgeLanePress(0, 1000).Single();
        Assert.That(judgement.Rating.IsHit(), Is.True);

        Assert.That(selector.Select(0, 1800), Is.EqualTo(1));
    }

    [Test]
    public void EmptyLaneDoesNotSelectSample()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
        {
            HitObjects =
            [
                new YokkoHitObject(
                    1,
                    1000,
                    null,
                    HitObjectKind.Tap,
                    "other-lane.wav"),
            ],
        };
        var judgementState = new BeatmapJudgementState(beatmap);
        var selector = new GameplayKeysoundSelector(
            beatmap,
            judgementState);

        Assert.That(selector.Select(0, 1000), Is.EqualTo(-1));
    }
}
