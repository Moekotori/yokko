using System.Linq;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Mods;
using Yokko.Core.Timing;
using Yokko.Game.Gameplay;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class GameplayAutoGeneratorTest
{
    [Test]
    public void TapReleaseUsesDelayWithoutOverlappingNextNote()
    {
        GameplayReplay replay = GameplayAutoGenerator.Generate(beatmap(
            new YokkoHitObject(0, 100, null, HitObjectKind.Tap),
            new YokkoHitObject(0, 110, null, HitObjectKind.Tap)));

        Assert.That(
            replay.Inputs,
            Is.EqualTo(new[]
            {
                new GameplayReplayInput(0, true, 100),
                new GameplayReplayInput(0, false, 109),
                new GameplayReplayInput(0, true, 110),
                new GameplayReplayInput(0, false, 130),
            }));
    }

    [Test]
    public void HoldTailIsReleasedExactlyAndNonPlayableObjectsAreIgnored()
    {
        GameplayReplay replay = GameplayAutoGenerator.Generate(beatmap(
            new YokkoHitObject(1, 200, 500, HitObjectKind.Hold),
            new YokkoHitObject(2, 250, null, HitObjectKind.Sample)));

        Assert.That(
            replay.Inputs,
            Is.EqualTo(new[]
            {
                new GameplayReplayInput(1, true, 200),
                new GameplayReplayInput(1, false, 500),
            }));
    }

    [Test]
    public void GeneratedReplayPerfectlyCompletesBeatmap()
    {
        YokkoBeatmap chart = beatmap(
            new YokkoHitObject(0, 100, null, HitObjectKind.Tap),
            new YokkoHitObject(1, 150, 350, HitObjectKind.Hold),
            new YokkoHitObject(0, 400, null, HitObjectKind.Tap));
        GameplayReplay replay = GameplayAutoGenerator.Generate(chart);
        var state = new Yokko.Core.Scoring.BeatmapJudgementState(chart);

        foreach (GameplayReplayInput input in replay.Inputs)
        {
            if (input.IsPressed)
                state.JudgeLanePress(input.Lane, input.TimeMilliseconds);
            else
                state.JudgeLaneRelease(input.Lane, input.TimeMilliseconds);
        }

        Assert.Multiple(() =>
        {
            Assert.That(state.IsComplete, Is.True);
            Assert.That(state.Accuracy, Is.EqualTo(1));
            Assert.That(state.Counts.Miss, Is.Zero);
        });
    }

    [Test]
    public void GeneratedReplayCarriesExactModConfiguration()
    {
        ManiaModSet mods = ManiaModSet.Empty.WithFixedRate(
            ManiaModId.Nightcore,
            1.25);

        GameplayReplay replay = GameplayAutoGenerator.Generate(
            beatmap(new YokkoHitObject(
                0,
                100,
                null,
                HitObjectKind.Tap)),
            mods);

        Assert.That(replay.Mods, Is.EqualTo(mods));
    }

    private static YokkoBeatmap beatmap(
        params YokkoHitObject[] hitObjects) =>
        new(
            "Auto test",
            "Yokko",
            "Yokko",
            "4K",
            KeyMode.FourKey,
            ChartSourceFormat.Yokko,
            [YokkoTimingPoint.Default],
            null,
            hitObjects.OrderBy(static hitObject =>
                hitObject.StartTimeMilliseconds).ToArray());
}
