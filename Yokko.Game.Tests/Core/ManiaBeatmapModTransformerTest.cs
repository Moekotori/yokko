using System.Linq;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Mods;
using Yokko.Core.Timing;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class ManiaBeatmapModTransformerTest
{
    [Test]
    public void MirrorFlipsEveryObjectHorizontally()
    {
        YokkoBeatmap transformed = ManiaBeatmapModTransformer.Apply(
            createBeatmap(),
            new ManiaModSet([ManiaModId.Mirror]));

        Assert.That(
            transformed.HitObjects.Select(static hitObject => hitObject.Lane),
            Is.EqualTo(new[] { 3, 2, 1, 0, 2 }));
    }

    [Test]
    public void RandomUsesOneSeededGlobalLanePermutation()
    {
        YokkoBeatmap original = createBeatmap();
        var mods = new ManiaModSet([ManiaModId.Random], 741852);

        YokkoBeatmap first =
            ManiaBeatmapModTransformer.Apply(original, mods);
        YokkoBeatmap second =
            ManiaBeatmapModTransformer.Apply(original, mods);
        int[] mappedLanes = first.HitObjects
                                 .Take(4)
                                 .Select(static hitObject => hitObject.Lane)
                                 .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                second.HitObjects.Select(static hitObject => hitObject.Lane),
                Is.EqualTo(first.HitObjects.Select(static hitObject => hitObject.Lane)));
            Assert.That(
                mappedLanes.Order(),
                Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(
                first.HitObjects[1].Lane,
                Is.EqualTo(first.HitObjects[4].Lane),
                "Every object in one source lane must use the same mapping.");
            Assert.That(
                original.HitObjects.Select(static hitObject => hitObject.Lane),
                Is.EqualTo(new[] { 0, 1, 2, 3, 1 }),
                "The imported beatmap must remain unchanged.");
        });
    }

    [Test]
    public void HoldOffKeepsTheHeadAndRemovesTheTail()
    {
        YokkoBeatmap transformed = ManiaBeatmapModTransformer.Apply(
            createBeatmap(),
            new ManiaModSet([ManiaModId.HoldOff]));
        YokkoHitObject converted = transformed.HitObjects[4];

        Assert.Multiple(() =>
        {
            Assert.That(converted.Kind, Is.EqualTo(HitObjectKind.Tap));
            Assert.That(converted.StartTimeMilliseconds, Is.EqualTo(2000));
            Assert.That(converted.EndTimeMilliseconds, Is.Null);
            Assert.That(converted.SampleKey, Is.EqualTo("hold.wav"));
            Assert.That(converted.ScrollProfileId, Is.EqualTo("main"));
        });
    }

    private static YokkoBeatmap createBeatmap() => new(
        "Mods",
        "Yokko",
        "Yokko",
        "4K",
        KeyMode.FourKey,
        ChartSourceFormat.Yokko,
        [YokkoTimingPoint.Default],
        null,
        [
            new YokkoHitObject(0, 1000, null, HitObjectKind.Tap),
            new YokkoHitObject(1, 1100, null, HitObjectKind.Tap),
            new YokkoHitObject(2, 1200, null, HitObjectKind.Mine),
            new YokkoHitObject(3, 1300, null, HitObjectKind.Sample),
            new YokkoHitObject(
                1,
                2000,
                2500,
                HitObjectKind.Hold,
                "hold.wav",
                "main"),
        ]);
}
