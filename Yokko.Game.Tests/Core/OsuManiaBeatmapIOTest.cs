using System;
using System.Collections.Generic;
using NUnit.Framework;
using System.IO;
using System.Linq;
using Yokko.Core.Beatmaps;
using Yokko.Core.Editing;
using Yokko.Core.Gameplay;
using Yokko.Core.Timing;
using Yokko.Import.Osu;

namespace Yokko.Game.Tests.Core
{
    [TestFixture]
    public sealed class OsuManiaBeatmapIOTest
    {
        [Test]
        public void ReadsOsuManiaTapAndHoldObjects()
        {
            var beatmap = OsuManiaBeatmapIO.ReadBeatmap(sampleOsu);

            Assert.That(beatmap.Title, Is.EqualTo("Test Song"));
            Assert.That(beatmap.Artist, Is.EqualTo("Test Artist"));
            Assert.That(beatmap.Creator, Is.EqualTo("Mapper"));
            Assert.That(beatmap.DifficultyName, Is.EqualTo("4K"));
            Assert.That(beatmap.KeyMode, Is.EqualTo(KeyMode.FourKey));
            Assert.That(beatmap.OverallDifficulty, Is.EqualTo(8));
            Assert.That(beatmap.AudioPath, Is.EqualTo("audio.mp3"));
            Assert.That(beatmap.TimingPoints, Has.Count.EqualTo(3));
            Assert.That(beatmap.TimingPoints[0].BeatLengthMilliseconds, Is.EqualTo(500));
            Assert.That(beatmap.TimingPoints[1].Uninherited, Is.False);
            Assert.That(beatmap.TimingPoints[2].BeatLengthMilliseconds, Is.EqualTo(400));
            Assert.That(beatmap.InitialScrollVelocity, Is.EqualTo(1));
            Assert.That(beatmap.ScrollVelocities, Has.Count.EqualTo(2));
            Assert.That(beatmap.ScrollVelocities[0].TimeMilliseconds, Is.EqualTo(1000));
            Assert.That(beatmap.ScrollVelocities[0].Multiplier, Is.EqualTo(2));
            Assert.That(beatmap.ScrollVelocities[1].TimeMilliseconds, Is.EqualTo(2000));
            Assert.That(beatmap.ScrollVelocities[1].Multiplier, Is.EqualTo(1.25));
            Assert.That(beatmap.HitObjects, Has.Count.EqualTo(3));
            Assert.That(beatmap.HitObjects[0].Lane, Is.EqualTo(0));
            Assert.That(beatmap.HitObjects[0].Kind, Is.EqualTo(HitObjectKind.Tap));
            Assert.That(beatmap.HitObjects[1].Lane, Is.EqualTo(1));
            Assert.That(beatmap.HitObjects[1].Kind, Is.EqualTo(HitObjectKind.Hold));
            Assert.That(beatmap.HitObjects[1].EndTimeMilliseconds, Is.EqualTo(1750));
            Assert.That(beatmap.HitObjects[2].Lane, Is.EqualTo(3));
        }

        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("-1")]
        [TestCase("11")]
        public void RejectsInvalidOverallDifficulty(string overallDifficulty)
        {
            string source = sampleOsu.Replace(
                "OverallDifficulty:8",
                $"OverallDifficulty:{overallDifficulty}");

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OsuManiaBeatmapIO.ReadBeatmap(source));
        }

        [Test]
        public void ExportsEditableBeatmapAsReadableOsuMania()
        {
            EditableBeatmap editable = EditableBeatmap.FromBeatmap(OsuManiaBeatmapIO.ReadBeatmap(sampleOsu));

            string exported = OsuManiaBeatmapIO.WriteEditableToString(editable);
            var reparsed = OsuManiaBeatmapIO.ReadBeatmap(exported);

            Assert.That(exported, Does.Contain("Mode: 3"));
            Assert.That(exported, Does.Contain("CircleSize:4"));
            Assert.That(reparsed.KeyMode, Is.EqualTo(KeyMode.FourKey));
            Assert.That(reparsed.OverallDifficulty, Is.EqualTo(editable.OverallDifficulty));
            Assert.That(exported, Does.Contain("OverallDifficulty:8"));
            Assert.That(reparsed.HitObjects, Has.Count.EqualTo(3));
            Assert.That(reparsed.HitObjects[1].Kind, Is.EqualTo(HitObjectKind.Hold));
            Assert.That(reparsed.HitObjects[1].EndTimeMilliseconds, Is.EqualTo(1750));
            Assert.That(reparsed.TimingPoints, Is.EqualTo(editable.TimingPoints));
            Assert.That(reparsed.InitialScrollVelocity, Is.EqualTo(
                editable.InitialScrollVelocity));
            Assert.That(reparsed.ScrollVelocities, Is.EqualTo(
                editable.ScrollVelocities));
        }

        [Test]
        public void ExportsPositiveNormalizedScrollVelocitiesAsInheritedPoints()
        {
            var source = new YokkoBeatmap(
                "SV export",
                "Artist",
                "Mapper",
                "4K",
                KeyMode.FourKey,
                ChartSourceFormat.Quaver,
                [
                    new YokkoTimingPoint(0, 500),
                    new YokkoTimingPoint(2000, 250),
                ],
                null,
                [new YokkoHitObject(0, 3000, null, HitObjectKind.Tap)],
                ScrollVelocities:
                [
                    new YokkoScrollVelocity(1000, 0.5),
                    new YokkoScrollVelocity(2500, 1.5),
                ]);

            string exported = OsuManiaBeatmapIO.WriteBeatmap(source);
            YokkoBeatmap reparsed = OsuManiaBeatmapIO.ReadBeatmap(exported);

            Assert.That(
                reparsed.TimingPoints.Any(point =>
                    !point.Uninherited
                    && point.TimeMilliseconds == 2000),
                Is.True,
                "BPM changes need a compensating inherited point.");
            Assert.That(
                reparsed.InitialScrollVelocity,
                Is.EqualTo(source.InitialScrollVelocity).Within(0.000001));
            Assert.That(reparsed.ScrollVelocities, Has.Count.EqualTo(2));
            Assert.That(
                reparsed.ScrollVelocities[0].Multiplier,
                Is.EqualTo(0.5).Within(0.000001));
            Assert.That(
                reparsed.ScrollVelocities[1].Multiplier,
                Is.EqualTo(1.5).Within(0.000001));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RejectsScrollVelocitiesOsuCannotRepresent(double multiplier)
        {
            var source = new YokkoBeatmap(
                "Unsupported SV",
                "Artist",
                "Mapper",
                "4K",
                KeyMode.FourKey,
                ChartSourceFormat.Quaver,
                [YokkoTimingPoint.Default],
                null,
                [new YokkoHitObject(0, 1000, null, HitObjectKind.Tap)],
                ScrollVelocities:
                [
                    new YokkoScrollVelocity(500, multiplier),
                ]);

            Assert.That(
                () => OsuManiaBeatmapIO.WriteBeatmap(source),
                Throws.TypeOf<InvalidDataException>()
                      .With.Message.Contains(
                          "cannot represent zero or negative"));
        }

        [Test]
        public void RejectsQuaverScrollSpeedFactorsDuringOsuExport()
        {
            var source = new YokkoBeatmap(
                "SSF",
                "Artist",
                "Mapper",
                "4K",
                KeyMode.FourKey,
                ChartSourceFormat.Quaver,
                [YokkoTimingPoint.Default],
                null,
                [new YokkoHitObject(0, 1000, null, HitObjectKind.Tap)],
                ScrollSpeedFactors:
                [
                    new YokkoScrollSpeedFactor(500, 2),
                ]);

            Assert.That(
                () => OsuManiaBeatmapIO.WriteBeatmap(source),
                Throws.TypeOf<InvalidDataException>()
                      .With.Message.Contains("scroll speed factors"));
        }

        [Test]
        public void RejectsQuaverTimingGroupsDuringOsuExport()
        {
            var source = new YokkoBeatmap(
                "Timing group",
                "Artist",
                "Mapper",
                "4K",
                KeyMode.FourKey,
                ChartSourceFormat.Quaver,
                [YokkoTimingPoint.Default],
                null,
                [
                    new YokkoHitObject(
                        0,
                        1000,
                        null,
                        HitObjectKind.Tap,
                        ScrollProfileId: "Reverse"),
                ],
                ScrollProfiles: new Dictionary<string, YokkoScrollProfile>
                {
                    ["Reverse"] = new YokkoScrollProfile(-1, [], []),
                });

            Assert.That(
                () => OsuManiaBeatmapIO.WriteBeatmap(source),
                Throws.TypeOf<InvalidDataException>()
                      .With.Message.Contains("timing groups"));
        }

        [Test]
        public void ReadEditableFromFileResolvesAdjacentAudio()
        {
            string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "osu-import", TestContext.CurrentContext.Test.ID);
            Directory.CreateDirectory(directory);
            string audioPath = Path.Combine(directory, "audio.mp3");
            string beatmapPath = Path.Combine(directory, "chart.osu");

            File.WriteAllBytes(audioPath, []);
            File.WriteAllText(beatmapPath, sampleOsu);

            EditableBeatmap editable = OsuManiaBeatmapIO.ReadEditableFromFile(beatmapPath);

            Assert.That(editable.AudioPath, Is.EqualTo(audioPath));
        }

        [Test]
        public void ExportWritesAudioFilenameForAbsoluteAudioPath()
        {
            EditableBeatmap editable = EditableBeatmap.FromBeatmap(OsuManiaBeatmapIO.ReadBeatmap(sampleOsu));
            editable.AudioPath = Path.Combine("C:", "songs", "audio.mp3");

            string exported = OsuManiaBeatmapIO.WriteEditableToString(editable);

            Assert.That(exported, Does.Contain("AudioFilename: audio.mp3"));
        }

        private const string sampleOsu = """
osu file format v14

[General]
AudioFilename: audio.mp3
Mode: 3

[Metadata]
Title:Test Song
Artist:Test Artist
Creator:Mapper
Version:4K

[Difficulty]
CircleSize:4
OverallDifficulty:8

[TimingPoints]
0,500,4,2,0,100,1,0
1000,-50,4,2,0,80,0,1
2000,400,3,2,1,70,1,0

[HitObjects]
64,192,1000,1,0,0:0:0:0:
192,192,1500,128,0,1750:0:0:0:0:
448,192,2000,1,0,0:0:0:0:
""";
    }
}
