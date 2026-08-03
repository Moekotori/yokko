using System;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Timing;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class GameplayPracticePlanTest
{
    [Test]
    public void SliceKeepsOnlySelectedObjectsSamplesAndBreaks()
    {
        var beatmap = new YokkoBeatmap(
            "Practice",
            "Yokko",
            "Yokko",
            "4K",
            KeyMode.FourKey,
            ChartSourceFormat.Yokko,
            [YokkoTimingPoint.Default],
            null,
            [
                new YokkoHitObject(0, 500, null, HitObjectKind.Tap),
                new YokkoHitObject(1, 1500, null, HitObjectKind.Tap),
                new YokkoHitObject(3, 2000, null, HitObjectKind.Tap),
                new YokkoHitObject(2, 2500, null, HitObjectKind.Tap),
            ],
            BreakPeriods:
            [
                new YokkoBreakPeriod(1200, 1800),
                new YokkoBreakPeriod(4000, 5000),
            ],
            ScheduledSamples:
            [
                new YokkoScheduledSample(600, "outside.wav"),
                new YokkoScheduledSample(1600, "inside.wav"),
            ]);
        var plan = new GameplayPracticePlan(1000, 2000, 3);

        YokkoBeatmap sliced = plan.Slice(beatmap);

        Assert.Multiple(() =>
        {
            Assert.That(sliced.HitObjects, Has.Count.EqualTo(1));
            Assert.That(sliced.HitObjects[0].StartTimeMilliseconds, Is.EqualTo(1500));
            Assert.That(sliced.ScheduledSamples, Has.Count.EqualTo(1));
            Assert.That(sliced.BreakPeriods, Has.Count.EqualTo(1));
            Assert.That(sliced.DifficultyName, Does.Contain("PRACTICE"));
        });
    }

    [TestCase(0, 400, 1)]
    [TestCase(-1, 1000, 1)]
    [TestCase(0, 1000, 21)]
    public void RejectsInvalidRange(double start, double end, int repetitions)
    {
        Assert.That(
            () => new GameplayPracticePlan(start, end, repetitions),
            Throws.InstanceOf<ArgumentOutOfRangeException>());
    }
}
