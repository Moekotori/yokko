using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Timing;
using Yokko.Import.Osu;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class OsuSvGimmickCorpusTest
{
    [Test]
    public void GreenPointOverridesRedPointAtSameTimeRegardlessOfFileOrder()
    {
        YokkoBeatmap beatmap = readCorpus(
            "same-time-green-before-red.osu");

        Assert.Multiple(() =>
        {
            Assert.That(
                beatmap.InitialScrollVelocity,
                Is.EqualTo(0.5).Within(0.000001));
            Assert.That(
                beatmap.ScrollVelocities,
                Is.EqualTo(new[]
                {
                    new YokkoScrollVelocity(1000, 2),
                    new YokkoScrollVelocity(2000, 0.01),
                    new YokkoScrollVelocity(3000, 10),
                }));
        });
    }

    [Test]
    public void AbnormalRedLinesUseOsuControlPointBoundsAndEffects()
    {
        YokkoBeatmap beatmap = readCorpus("abnormal-red-lines.osu");

        double[] redBeatLengths = beatmap.TimingPoints
                                         .Where(static point =>
                                             point.Uninherited)
                                         .Select(static point =>
                                             point.BeatLengthMilliseconds)
                                         .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                redBeatLengths,
                Is.EqualTo(new[] { 500d, 6d, 6d, 60_000d }));
            Assert.That(
                beatmap.InitialScrollVelocity,
                Is.EqualTo(0.012).Within(0.000001));
            Assert.That(
                beatmap.ScrollVelocities,
                Is.EqualTo(new[]
                {
                    new YokkoScrollVelocity(1000, 1),
                    new YokkoScrollVelocity(2000, 2),
                    new YokkoScrollVelocity(3000, 0.0001),
                }));
        });
    }

    [Test]
    public void GreenNanResetsScrollSpeedButRedNanLineIsSkipped()
    {
        const string header = """
osu file format v14

[General]
Mode: 3

[Difficulty]
CircleSize:4

[TimingPoints]
0,500,4,2,0,100,1,0
1000,-50,4,2,0,100,0,0
""";
        const string objects = """

[HitObjects]
64,192,3000,1,0,0:0:0:0:
""";

        YokkoBeatmap greenNan = OsuManiaBeatmapIO.ReadBeatmap(
            header
            + "\n2000,NaN,4,2,0,100,0,0\n"
            + objects);

        YokkoBeatmap redNan = OsuManiaBeatmapIO.ReadBeatmap(
            header
            + "\n2000,NaN,4,2,0,100,1,0\n"
            + objects);

        Assert.Multiple(() =>
        {
            Assert.That(
                greenNan.ScrollVelocities,
                Is.EqualTo(new[]
                {
                    new YokkoScrollVelocity(1000, 2),
                    new YokkoScrollVelocity(2000, 1),
                }));
            Assert.That(
                redNan.ScrollVelocities,
                Is.EqualTo(new[]
                {
                    new YokkoScrollVelocity(1000, 2),
                }));
        });
    }

    [Test]
    public void ImportsOfficialOsuNanControlPointFixture()
    {
        YokkoBeatmap beatmap = readCorpus(
            "upstream-nan-control-points.osu");

        Assert.Multiple(() =>
        {
            Assert.That(
                beatmap.TimingPoints
                       .Where(static point => point.Uninherited)
                       .Select(static point =>
                           point.BeatLengthMilliseconds),
                Is.EqualTo(new[] { 500d }));
            Assert.That(beatmap.InitialScrollVelocity, Is.EqualTo(1));
            Assert.That(beatmap.ScrollVelocities, Is.Empty);
        });
    }

    private static YokkoBeatmap readCorpus(string fileName) =>
        OsuManiaBeatmapIO.ReadBeatmapFromFile(
            Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "Resources",
                "Testing",
                "Beatmaps",
                "SvGimmicks",
                fileName));
}
