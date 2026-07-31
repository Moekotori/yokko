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
    public void BmsScratchLaneStaysFixedUnderRandomAndMirror()
    {
        var original = new YokkoBeatmap(
            "Scratch",
            "Yokko",
            "Yokko",
            "7K + SCR",
            KeyMode.EightKey,
            ChartSourceFormat.Bms,
            [YokkoTimingPoint.Default],
            null,
            Enumerable.Range(0, 8)
                      .Select(lane => new YokkoHitObject(
                          lane,
                          1000 + lane * 100,
                          null,
                          HitObjectKind.Tap))
                      .ToArray(),
            ScratchLane: 0);

        YokkoBeatmap mirrored = ManiaBeatmapModTransformer.Apply(
            original,
            new ManiaModSet([ManiaModId.Mirror]));
        YokkoBeatmap random = ManiaBeatmapModTransformer.Apply(
            original,
            new ManiaModSet([ManiaModId.Random], 741852));

        Assert.Multiple(() =>
        {
            Assert.That(mirrored.ScratchLane, Is.Zero);
            Assert.That(mirrored.HitObjects[0].Lane, Is.Zero);
            Assert.That(
                mirrored.HitObjects.Skip(1).Select(note => note.Lane),
                Is.EqualTo(Enumerable.Range(1, 7).Reverse()));
            Assert.That(random.ScratchLane, Is.Zero);
            Assert.That(random.HitObjects[0].Lane, Is.Zero);
            Assert.That(
                random.HitObjects.Skip(1)
                      .Select(note => note.Lane)
                      .Order(),
                Is.EqualTo(Enumerable.Range(1, 7)));
        });
    }

    [Test]
    public void HoldOffKeepsTheHeadAndRemovesTheTail()
    {
        YokkoBeatmap original = createBeatmap();
        YokkoBeatmap transformed = ManiaBeatmapModTransformer.Apply(
            original,
            new ManiaModSet([ManiaModId.HoldOff]));
        YokkoHitObject converted = transformed.HitObjects[4];

        Assert.Multiple(() =>
        {
            Assert.That(converted.Kind, Is.EqualTo(HitObjectKind.Tap));
            Assert.That(converted.StartTimeMilliseconds, Is.EqualTo(2000));
            Assert.That(converted.EndTimeMilliseconds, Is.Null);
            Assert.That(converted.SampleKey, Is.EqualTo("hold.wav"));
            Assert.That(converted.ScrollProfileId, Is.EqualTo("main"));
            Assert.That(
                converted.SamplePayload,
                Is.SameAs(original.HitObjects[4].SamplePayload));
        });
    }

    [Test]
    public void DifficultyAdjustCopiesDifficultyWithoutMutatingSource()
    {
        YokkoBeatmap original = createBeatmap();
        YokkoBeatmap transformed =
            ManiaBeatmapModTransformer.Apply(
                original,
                ManiaModSet.Empty.WithDifficultyAdjust(
                    8.5,
                    9.2,
                    false));

        Assert.Multiple(() =>
        {
            Assert.That(transformed, Is.Not.SameAs(original));
            Assert.That(transformed.DrainRate, Is.EqualTo(8.5));
            Assert.That(
                transformed.OverallDifficulty,
                Is.EqualTo(9.2));
            Assert.That(original.DrainRate, Is.EqualTo(5));
            Assert.That(original.OverallDifficulty, Is.EqualTo(5));
        });
    }

    [Test]
    public void InvertCreatesHoldsBetweenConsecutiveLaneLocations()
    {
        YokkoBeatmap original = createBeatmap() with
        {
            BreakPeriods =
            [
                new YokkoBreakPeriod(1400, 1800),
            ],
        };
        YokkoBeatmap transformed =
            ManiaBeatmapModTransformer.Apply(
                original,
                new ManiaModSet([ManiaModId.Invert]));

        YokkoHitObject laneOne =
            transformed.HitObjects.Single();
        Assert.Multiple(() =>
        {
            Assert.That(laneOne.Lane, Is.EqualTo(1));
            Assert.That(
                laneOne.StartTimeMilliseconds,
                Is.EqualTo(1100));
            Assert.That(
                laneOne.EndTimeMilliseconds,
                Is.EqualTo(1875));
            Assert.That(laneOne.Kind, Is.EqualTo(HitObjectKind.Hold));
            Assert.That(
                transformed.BreakPeriods,
                Is.Empty,
                "lazer ManiaModInvert explicitly removes all breaks");
            Assert.That(original.HitObjects.Count, Is.EqualTo(5));
            Assert.That(original.BreakPeriods, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void KeyModsRegenerateOnlyNonManiaConversionSources()
    {
        var source = new ManiaConversionSource(
            4,
            8,
            9,
            6,
            [
                new ManiaConversionHitObject(
                    0,
                    1000,
                    1000,
                    ManiaConversionObjectKind.Circle),
                new ManiaConversionHitObject(
                    256,
                    1100,
                    1100,
                    ManiaConversionObjectKind.Circle),
                new ManiaConversionHitObject(
                    511,
                    1200,
                    1200,
                    ManiaConversionObjectKind.Circle),
            ]);
        YokkoBeatmap convertedSource = createBeatmap() with
        {
            SourceFormat = ChartSourceFormat.OsuStandard,
            ConversionSource = source,
        };

        YokkoBeatmap fourKey = ManiaBeatmapModTransformer.Apply(
            convertedSource,
            ManiaModSet.Empty.With(ManiaModId.Key4, true));
        YokkoBeatmap nativeSource = createBeatmap();
        YokkoBeatmap nativeMania = ManiaBeatmapModTransformer.Apply(
            nativeSource,
            ManiaModSet.Empty.With(ManiaModId.Key7, true));

        Assert.Multiple(() =>
        {
            Assert.That(fourKey.KeyMode, Is.EqualTo(KeyMode.FourKey));
            Assert.That(
                fourKey.HitObjects.Select(hitObject => hitObject.Lane),
                Is.EqualTo(new[] { 0, 2, 3 }));
            Assert.That(
                nativeMania.KeyMode,
                Is.EqualTo(KeyMode.FourKey),
                "lazer does not apply key Mods to Mania-specific charts");
            Assert.That(
                nativeMania.HitObjects,
                Is.EqualTo(nativeSource.HitObjects));
        });
    }

    [Test]
    public void DualStagesRegeneratesTwoLazerStages()
    {
        var source = new ManiaConversionSource(
            4,
            8,
            9,
            6,
            [
                new ManiaConversionHitObject(
                    32,
                    1000,
                    1000,
                    ManiaConversionObjectKind.Circle),
                new ManiaConversionHitObject(
                    480,
                    1100,
                    1100,
                    ManiaConversionObjectKind.Circle),
            ]);
        YokkoBeatmap original = createBeatmap() with
        {
            SourceFormat = ChartSourceFormat.OsuStandard,
            ConversionSource = source,
        };
        ManiaModSet mods = ManiaModSet.Empty
            .With(ManiaModId.Key7, true)
            .With(ManiaModId.DualStages, true);

        YokkoBeatmap transformed =
            ManiaBeatmapModTransformer.Apply(original, mods);

        Assert.Multiple(() =>
        {
            Assert.That(
                transformed.KeyMode,
                Is.EqualTo(KeyMode.FourteenKey));
            Assert.That(transformed.StageCount, Is.EqualTo(2));
            Assert.That(transformed.KeysPerStage, Is.EqualTo(7));
            Assert.That(
                transformed.HitObjects.All(hitObject =>
                    hitObject.Lane is >= 0 and < 14),
                Is.True);
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
                "main",
                new YokkoHitSamplePayload(
                    [
                        new YokkoHitSample(
                            YokkoHitSample.HitNormal,
                            Volume: 60),
                    ],
                    PlaySlidingSamples: true)),
        ]);
}
