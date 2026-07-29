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
        });
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
}
