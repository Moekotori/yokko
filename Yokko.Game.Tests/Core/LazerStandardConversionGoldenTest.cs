using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Import.Osu;

namespace Yokko.Game.Tests.Core;

/// <summary>
/// Object-shape golden cases from osu!lazer's
/// ManiaBeatmapSampleConversionTest resources at
/// 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0 (MIT).
/// lazer's original sample test permits two milliseconds of legacy conversion
/// lenience; these assertions use the same tolerance.
/// </summary>
[TestFixture]
public sealed class LazerStandardConversionGoldenTest
{
    [Test]
    public void BasicComplexSliderCorpusMatchesLazerObjectShape()
    {
        YokkoBeatmap beatmap =
            OsuManiaBeatmapIO.ReadBeatmap(basicConversion);

        (int lane, double start, double end)[] expected =
        [
            (0, 500, 2500),
            (1, 1500, 2500),
            (2, 3000, 4000),
            (4, 4500, 5500),
            (2, 6000, 6500),
            (2, 7000, 8000),
            (0, 8500, 11000),
            (1, 11500, 12000),
            (4, 12500, 16500),
            (2, 17000, 18000),
            (0, 18500, 19450),
            (0, 19875, 23875),
            (1, 19875, 23875),
        ];

        Assert.That(beatmap.KeyMode, Is.EqualTo(KeyMode.FiveKey));
        Assert.That(beatmap.HitObjects, Has.Count.EqualTo(expected.Length));
        for (int index = 0; index < expected.Length; index++)
        {
            assertObject(
                beatmap.HitObjects[index],
                expected[index].lane,
                expected[index].start,
                expected[index].end,
                HitObjectKind.Hold);
        }
    }

    [Test]
    public void ZeroLengthSliderMatchesLazerTapShape()
    {
        YokkoBeatmap beatmap =
            OsuManiaBeatmapIO.ReadBeatmap(zeroLengthSlider);

        Assert.Multiple(() =>
        {
            Assert.That(beatmap.KeyMode, Is.EqualTo(KeyMode.FourKey));
            Assert.That(beatmap.HitObjects, Has.Count.EqualTo(1));
            assertObject(
                beatmap.HitObjects[0],
                0,
                4836,
                null,
                HitObjectKind.Tap);
        });
    }

    [Test]
    public void RepeatedSliderMatchesLazerObjectShape()
    {
        YokkoBeatmap beatmap =
            OsuManiaBeatmapIO.ReadBeatmap(convertSamples);

        Assert.Multiple(() =>
        {
            Assert.That(beatmap.KeyMode, Is.EqualTo(KeyMode.SixKey));
            Assert.That(beatmap.HitObjects, Has.Count.EqualTo(3));
            assertObject(
                beatmap.HitObjects[0],
                1,
                1000,
                2750,
                HitObjectKind.Hold);
            assertObject(
                beatmap.HitObjects[1],
                0,
                1875,
                2750,
                HitObjectKind.Hold);
            assertObject(
                beatmap.HitObjects[2],
                3,
                3750,
                null,
                HitObjectKind.Tap);
            Assert.That(
                sampleNames(beatmap.HitObjects[0].Samples),
                Is.EqualTo(new[] { "Gameplay/normal-hitnormal" }));
            Assert.That(
                nodeSampleNames(beatmap.HitObjects[0]),
                Is.EqualTo(new[]
                {
                    new[] { "Gameplay/normal-hitnormal" },
                    new[] { "Gameplay/soft-hitnormal" },
                    new[] { "Gameplay/drum-hitnormal" },
                }));
            Assert.That(
                nodeSampleNames(beatmap.HitObjects[1]),
                Is.EqualTo(new[]
                {
                    new[] { "Gameplay/soft-hitnormal" },
                    new[] { "Gameplay/drum-hitnormal" },
                }));
            Assert.That(beatmap.HitObjects[0].PlaySlidingSamples, Is.True);
            Assert.That(beatmap.HitObjects[1].PlaySlidingSamples, Is.True);
        });
    }

    [Test]
    public void ShortRepeatSliderMatchesLazerStairShape()
    {
        YokkoBeatmap beatmap =
            OsuManiaBeatmapIO.ReadBeatmap(sliderConvertSamples);

        Assert.Multiple(() =>
        {
            Assert.That(beatmap.KeyMode, Is.EqualTo(KeyMode.FiveKey));
            Assert.That(beatmap.HitObjects, Has.Count.EqualTo(3));
            assertObject(
                beatmap.HitObjects[0],
                0,
                8470,
                null,
                HitObjectKind.Tap);
            assertObject(
                beatmap.HitObjects[1],
                1,
                8626.470587768974,
                null,
                HitObjectKind.Tap);
            assertObject(
                beatmap.HitObjects[2],
                2,
                8782.941175537948,
                null,
                HitObjectKind.Tap);
            Assert.That(
                sampleNames(beatmap.HitObjects[0].Samples),
                Is.EqualTo(new[]
                {
                    "Gameplay/normal-hitnormal",
                    "Gameplay/normal-hitclap",
                }));
            Assert.That(
                sampleNames(beatmap.HitObjects[1].Samples),
                Is.EqualTo(new[] { "Gameplay/normal-hitnormal" }));
            Assert.That(
                sampleNames(beatmap.HitObjects[2].Samples),
                Is.EqualTo(new[]
                {
                    "Gameplay/normal-hitnormal",
                    "Gameplay/normal-hitclap",
                }));
        });
    }

    [Test]
    public void SpinnerMatchesLazerHoldShape()
    {
        YokkoBeatmap beatmap =
            OsuManiaBeatmapIO.ReadBeatmap(spinnerConvertSamples);
        YokkoHitObject generated = beatmap.HitObjects.Single();

        Assert.Multiple(() =>
        {
            Assert.That(beatmap.KeyMode, Is.EqualTo(KeyMode.SixKey));
            assertObject(
                generated,
                0,
                1000,
                8000,
                HitObjectKind.Hold);
            Assert.That(
                sampleNames(generated.Samples),
                Is.EqualTo(new[]
                {
                    "Gameplay/soft-hitnormal",
                    "Gameplay/soft-hitfinish",
                }));
            Assert.That(
                nodeSampleNames(generated),
                Is.EqualTo(new[]
                {
                    new[] { "Gameplay/soft-hitnormal" },
                    new[]
                    {
                        "Gameplay/soft-hitnormal",
                        "Gameplay/soft-hitfinish",
                    },
                }));
            Assert.That(generated.PlaySlidingSamples, Is.False);
        });
    }

    [Test]
    public void NativeManiaHoldSamplesMatchLazerPayload()
    {
        YokkoBeatmap beatmap =
            OsuManiaBeatmapIO.ReadBeatmap(nativeManiaSamples);
        YokkoBeatmap roundTrip = OsuManiaBeatmapIO.ReadBeatmap(
            OsuManiaBeatmapIO.WriteBeatmap(beatmap));

        Assert.That(beatmap.HitObjects, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(
                sampleNames(beatmap.HitObjects[0].Samples),
                Is.EqualTo(new[] { "Gameplay/normal-hitnormal" }));
            Assert.That(
                nodeSampleNames(beatmap.HitObjects[0]),
                Is.EqualTo(new[]
                {
                    new[] { "Gameplay/normal-hitnormal" },
                    Array.Empty<string>(),
                }));
            Assert.That(
                sampleNames(beatmap.HitObjects[1].Samples),
                Is.EqualTo(new[] { "Gameplay/drum-hitnormal" }));
            Assert.That(
                nodeSampleNames(beatmap.HitObjects[1]),
                Is.EqualTo(new[]
                {
                    new[] { "Gameplay/drum-hitnormal" },
                    Array.Empty<string>(),
                }));
            Assert.That(
                roundTrip.HitObjects
                         .Select(hitObject =>
                             sampleNames(hitObject.Samples)),
                Is.EqualTo(beatmap.HitObjects.Select(
                    hitObject => sampleNames(hitObject.Samples))));
        });
    }

    [Test]
    public void LegacyManiaSliderPassesThroughAsLazerHold()
    {
        YokkoBeatmap beatmap =
            OsuManiaBeatmapIO.ReadBeatmap(legacyManiaSlider);

        Assert.Multiple(() =>
        {
            Assert.That(beatmap.KeyMode, Is.EqualTo(KeyMode.FiveKey));
            Assert.That(beatmap.HitObjects, Has.Count.EqualTo(1));
            assertObject(
                beatmap.HitObjects[0],
                2,
                500,
                2500,
                HitObjectKind.Hold);
        });
    }

    [Test]
    public void LegacyManiaSpinnersUseLazerSeededColumns()
    {
        YokkoBeatmap beatmap =
            OsuManiaBeatmapIO.ReadBeatmap(legacyManiaSpinners);
        (int lane, double start, double end)[] expected =
        [
            (0, 11783, 15116),
            (0, 91545, 92735),
            (1, 152497, 153687),
            (3, 231545, 232974),
        ];

        Assert.That(beatmap.KeyMode, Is.EqualTo(KeyMode.FourKey));
        Assert.That(beatmap.HitObjects, Has.Count.EqualTo(expected.Length));
        for (int index = 0; index < expected.Length; index++)
        {
            assertObject(
                beatmap.HitObjects[index],
                expected[index].lane,
                expected[index].start,
                expected[index].end,
                HitObjectKind.Hold);
        }
    }

    private static void assertObject(
        YokkoHitObject actual,
        int lane,
        double start,
        double? end,
        HitObjectKind kind)
    {
        Assert.That(actual.Lane, Is.EqualTo(lane));
        Assert.That(
            actual.StartTimeMilliseconds,
            Is.EqualTo(start).Within(2));
        if (end is double expectedEnd)
        {
            Assert.That(
                actual.EndTimeMilliseconds,
                Is.EqualTo(expectedEnd).Within(2));
        }
        else
            Assert.That(actual.EndTimeMilliseconds, Is.Null);
        Assert.That(actual.Kind, Is.EqualTo(kind));
    }

    private static string[] sampleNames(
        IReadOnlyList<YokkoHitSample> samples) =>
        samples
            .Select(sample =>
                $"Gameplay/{sample.LookupNames().First()}")
            .ToArray();

    private static string[][] nodeSampleNames(
        YokkoHitObject hitObject) =>
        hitObject.NodeSamples.Select(sampleNames).ToArray();

    private const string convertSamples = """
osu file format v14

[Difficulty]
HPDrainRate:5
CircleSize:5
OverallDifficulty:5
ApproachRate:5
SliderMultiplier:1.4
SliderTickRate:1

[TimingPoints]
0,500,4,1,0,100,1,0

[HitObjects]
88,99,1000,6,0,L|306:259,2,245,0|0|0,1:0|2:0|3:0,0:0:0:0:
259,118,3750,1,0,1:0:0:0:
""";

    private const string basicConversion = """
osu file format v14

[Difficulty]
HPDrainRate:6
CircleSize:4
OverallDifficulty:7
ApproachRate:8.3
SliderMultiplier:1.6
SliderTickRate:1

[TimingPoints]
500,500,4,2,1,50,1,0
13426,-100,4,3,1,45,0,0
14884,-100,4,2,1,50,0,0

[HitObjects]
96,192,500,6,0,L|416:192,2,320
256,192,3000,12,0,4000,0:0:0:0:
256,192,4500,12,0,5500,0:0:0:0:
256,192,6000,12,0,6500,0:0:0:0:
256,128,7000,6,0,L|352:128,4,80
32,192,8500,6,0,B|32:384|256:384|256:192|256:192|256:0|512:0|512:192,1,800
256,192,11500,12,0,12000,0:0:0:0:
512,320,12500,6,0,B|0:256|0:256|512:96|512:96|256:32,1,1280
256,256,17000,6,0,L|160:256,4,80
256,192,18500,12,0,19450,0:0:0:0:
216,231,19875,6,0,B|216:135|280:135|344:135|344:199|344:263|248:327|248:327|120:327|120:327|56:39|408:39|408:39|472:150|408:342,1,1280
""";

    private const string zeroLengthSlider = """
osu file format v14

[General]
StackLeniency: 0.7
Mode: 0

[Difficulty]
HPDrainRate:1
CircleSize:4
OverallDifficulty:1
ApproachRate:9
SliderMultiplier:2.5
SliderTickRate:0.5

[TimingPoints]
34,431.654676258993,4,1,0,50,1,0
4782,-66.6666666666667,4,1,0,20,0,0

[HitObjects]
15,199,4836,22,0,L,1,46.8750017881394
""";

    private const string sliderConvertSamples = """
osu file format v14

[Difficulty]
HPDrainRate:6
CircleSize:4
OverallDifficulty:8
ApproachRate:9.5
SliderMultiplier:2.00000000596047
SliderTickRate:1

[TimingPoints]
0,312.941176470588,4,1,0,100,1,0

[HitObjects]
82,216,8470,6,0,P|52:161|99:113,2,100,8|0|8,1:0|1:0|1:0,0:0:0:0:
""";

    private const string spinnerConvertSamples = """
osu file format v14

[General]
Mode: 0

[Difficulty]
HPDrainRate:5
CircleSize:5
OverallDifficulty:5
ApproachRate:5
SliderMultiplier:1.4
SliderTickRate:1

[TimingPoints]
0,500,4,2,0,100,1,0

[HitObjects]
256,192,1000,8,4,8000,0:2:0:0:
""";

    private const string nativeManiaSamples = """
osu file format v14

[General]
Mode: 3

[Difficulty]
HPDrainRate:5
CircleSize:5
OverallDifficulty:5
ApproachRate:5
SliderMultiplier:1.4

[TimingPoints]
0,500,4,1,0,100,1,0
10000,-150,4,1,0,100,1,0

[HitObjects]
51,192,500,128,0,1500:1:0:0:0:
256,192,2000,128,0,3000:3:0:0:0:
""";

    private const string legacyManiaSlider = """
osu file format v14

[General]
Mode: 3

[Difficulty]
HPDrainRate:2
CircleSize:5
OverallDifficulty:2
SliderMultiplier:1

[TimingPoints]
355,476.190476190476,4,2,1,60,1,0

[HitObjects]
256,352,500,2,0,L|256:208,3,140
""";

    private const string legacyManiaSpinners = """
osu file format v14

[General]
Mode: 3

[Difficulty]
HPDrainRate:5
CircleSize:4
OverallDifficulty:5
ApproachRate:0
SliderMultiplier:2.6

[TimingPoints]
355,476.190476190476,4,2,1,60,1,0
60652,-100,4,2,1,60,0,1
92735,-100,4,2,1,60,0,0
121485,-100,4,2,1,60,0,1
153688,-100,4,2,1,60,0,0
182497,-100,4,2,1,60,0,1
213688,-100,4,2,1,60,0,0

[HitObjects]
256,192,11783,12,0,15116,0:0:0:0:
256,192,91545,12,0,92735,0:0:0:0:
256,192,152497,12,0,153687,0:0:0:0:
256,192,231545,12,0,232974,0:0:0:0:
""";
}
