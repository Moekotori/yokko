using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Scoring;
using Yokko.Core.Timing;
using Yokko.Game.Screens.Gameplay;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class GameplayScrollVelocityTest
{
    [Test]
    public void ZeroVelocityFreezesDrawableAndNegativeVelocityReversesIt()
    {
        var hitObject = new YokkoHitObject(
            0,
            3500,
            null,
            HitObjectKind.Tap);
        var drawable = new DrawableNote(0, hitObject, 80);
        ScrollVelocityMap map = createScrollMap();

        drawable.UpdatePosition(
            1000,
            false,
            false,
            0,
            500,
            1800,
            map);
        float atZeroStart = drawable.Y;

        drawable.UpdatePosition(
            1750,
            false,
            false,
            0,
            500,
            1800,
            map);
        float duringZero = drawable.Y;

        drawable.UpdatePosition(
            2500,
            false,
            false,
            0,
            500,
            1800,
            map);
        float duringReverse = drawable.Y;

        Assert.Multiple(() =>
        {
            Assert.That(duringZero, Is.EqualTo(atZeroStart));
            Assert.That(duringReverse, Is.LessThan(duringZero));
        });
    }

    [Test]
    public void TapCanLeaveAndReenterTheVisibleWindowDuringReverseSv()
    {
        YokkoBeatmap beatmap = createBeatmap(
            new YokkoHitObject(
                0,
                3500,
                null,
                HitObjectKind.Tap));
        var state = new BeatmapJudgementState(beatmap);
        var playfield = new GameplayPlayfield(
            beatmap,
            KeyModeBindings.ForMode(KeyMode.FourKey));

        playfield.UpdateGameplayTime(500, state);
        int beforeReverse = playfield.VisibleNoteCount;
        playfield.UpdateGameplayTime(1500, state);
        int duringFreeze = playfield.VisibleNoteCount;
        playfield.UpdateGameplayTime(2500, state);
        int afterReentry = playfield.VisibleNoteCount;

        Assert.Multiple(() =>
        {
            Assert.That(beforeReverse, Is.EqualTo(1));
            Assert.That(duringFreeze, Is.Zero);
            Assert.That(afterReentry, Is.EqualTo(1));
        });
    }

    [Test]
    public void HoldTailUsesLastNonZeroDirectionDuringZeroSv()
    {
        var hold = new YokkoHitObject(
            0,
            750,
            1750,
            HitObjectKind.Hold);
        var drawable = new DrawableNote(0, hold, 80);
        var map = new ScrollVelocityMap(
        [
            new YokkoScrollVelocity(1000, -1),
            new YokkoScrollVelocity(1500, 0),
            new YokkoScrollVelocity(2000, 1),
        ]);

        drawable.UpdatePosition(
            1000,
            false,
            false,
            0,
            500,
            1800,
            map);

        Assert.That(
            drawable.ReverseHoldTailForScrollVelocity,
            Is.True);
    }

    [Test]
    public void JudgementTimingDoesNotDependOnVisualDirection()
    {
        YokkoBeatmap beatmap = createBeatmap(
            new YokkoHitObject(
                0,
                2500,
                null,
                HitObjectKind.Tap));
        var state = new BeatmapJudgementState(beatmap);

        JudgementEvent result =
            state.TryJudgeLanePress(0, 2500);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Rating, Is.EqualTo(
                JudgementRating.Perfect));
            Assert.That(result.HitErrorMilliseconds, Is.Zero);
        });
    }

    private static ScrollVelocityMap createScrollMap() =>
        new(
        [
            new YokkoScrollVelocity(1000, 0),
            new YokkoScrollVelocity(2000, -1),
            new YokkoScrollVelocity(3000, 1),
        ]);

    private static YokkoBeatmap createBeatmap(
        params YokkoHitObject[] hitObjects) =>
        new(
            "Reverse SV test",
            "Yokko",
            "Yokko",
            "4K",
            KeyMode.FourKey,
            ChartSourceFormat.Quaver,
            [YokkoTimingPoint.Default],
            null,
            hitObjects,
            ScrollVelocities:
            [
                new YokkoScrollVelocity(1000, 0),
                new YokkoScrollVelocity(2000, -1),
                new YokkoScrollVelocity(3000, 1),
            ]);
}
