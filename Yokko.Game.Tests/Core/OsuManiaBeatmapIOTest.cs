using System;
using System.Collections.Generic;
using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Text;
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
        public void RejectsOsuFileAboveSafetyBudgetBeforeParsing()
        {
            string path = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                $"oversized-{Guid.NewGuid():N}.osu");
            try
            {
                using (FileStream stream = File.Create(path))
                {
                    stream.SetLength(
                        OsuManiaBeatmapIO.MaximumFileBytes + 1);
                }

                Assert.That(
                    () => OsuManiaBeatmapIO.ReadBeatmapFromFile(path),
                    Throws.TypeOf<InvalidDataException>());
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void RejectsOsuTextAboveLineBudget()
        {
            string source = new('\n',
                OsuManiaBeatmapIO.MaximumLineCount + 1);

            Assert.That(
                () => OsuManiaBeatmapIO.ReadBeatmap(source),
                Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public void RejectsOsuTextAboveHitObjectBudget()
        {
            var source = new StringBuilder()
                .AppendLine("osu file format v14")
                .AppendLine("[HitObjects]");
            for (int index = 0;
                 index <= OsuManiaBeatmapIO.MaximumHitObjectLineCount;
                 index++)
            {
                source.AppendLine("0,192,0,1,0,0:0:0:0:");
            }

            Assert.That(
                () => OsuManiaBeatmapIO.ReadBeatmap(source.ToString()),
                Throws.TypeOf<InvalidDataException>());
        }

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
            Assert.That(beatmap.DrainRate, Is.EqualTo(6));
            Assert.That(beatmap.AudioPath, Is.EqualTo("audio.mp3"));
            Assert.That(
                beatmap.PreviewTimeMilliseconds,
                Is.EqualTo(12345));
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

        [Test]
        public void ReadsAndExportsLocalAudioOffset()
        {
            string source = sampleOsu.Replace(
                "PreviewTime: 12345",
                "PreviewTime: 12345\nOffset: -36");

            YokkoBeatmap beatmap =
                OsuManiaBeatmapIO.ReadBeatmap(source);

            Assert.That(beatmap.LocalOffsetMilliseconds, Is.EqualTo(-36));

            string exported = OsuManiaBeatmapIO.WriteBeatmap(beatmap);
            Assert.That(exported, Does.Contain("Offset: -36"));
        }

        [TestCase(1, KeyMode.OneKey, 1)]
        [TestCase(6, KeyMode.SixKey, 1)]
        [TestCase(10, KeyMode.TenKey, 1)]
        [TestCase(12, KeyMode.TwelveKey, 2)]
        [TestCase(14, KeyMode.FourteenKey, 2)]
        [TestCase(20, KeyMode.TwentyKey, 2)]
        public void ReadsFullLazerManiaKeyRange(
            int keyCount,
            KeyMode expectedMode,
            int expectedStageCount)
        {
            string source = sampleOsu.Replace(
                "CircleSize:4",
                $"CircleSize:{keyCount}");
            YokkoBeatmap beatmap =
                OsuManiaBeatmapIO.ReadBeatmap(source);

            Assert.That(beatmap.KeyMode, Is.EqualTo(expectedMode));
            Assert.That(
                beatmap.StageCount,
                Is.EqualTo(expectedStageCount));
            Assert.That(
                beatmap.HitObjects.All(hitObject =>
                    hitObject.Lane >= 0
                    && hitObject.Lane < keyCount),
                Is.True);
        }

        [Test]
        public void ReadsAndRetainsOsuStandardConversionSource()
        {
            YokkoBeatmap beatmap =
                OsuManiaBeatmapIO.ReadBeatmap(sampleStandard);

            Assert.Multiple(() =>
            {
                Assert.That(
                    beatmap.SourceFormat,
                    Is.EqualTo(ChartSourceFormat.OsuStandard));
                Assert.That(
                    beatmap.KeyMode,
                    Is.EqualTo(KeyMode.SevenKey),
                    "lazer defaults low-special-object standard maps to 7K");
                Assert.That(beatmap.ConversionSource, Is.Not.Null);
                Assert.That(
                    beatmap.ConversionSource!.HitObjects,
                    Has.Count.EqualTo(5));
                Assert.That(
                    beatmap.ConversionSource.HitObjects[2].Kind,
                    Is.EqualTo(ManiaConversionObjectKind.Slider));
                Assert.That(
                    beatmap.ConversionSource.HitObjects[2]
                           .EndTimeMilliseconds,
                    Is.EqualTo(1500).Within(0.001));
                Assert.That(
                    beatmap.HitObjects.Count,
                    Is.GreaterThanOrEqualTo(5),
                    "legacy conversion may expand circles into chords");
                Assert.That(
                    beatmap.HitObjects.All(hitObject =>
                        hitObject.Lane is >= 0 and < 7),
                    Is.True);
                Assert.That(
                    beatmap.InitialScrollVelocity,
                    Is.EqualTo(1));
                Assert.That(
                    beatmap.ScrollVelocities,
                    Is.Empty,
                    "lazer ignores inherited SV when converting a non-mania beatmap");
            });
        }

        [Test]
        public void AppliesLegacyV4TimingOffsetToImportedTimeline()
        {
            string source = sampleOsu
                .Replace("osu file format v14", "osu file format v4")
                .Replace(
                    "[TimingPoints]",
                    "[Events]\n2,1100,1400\n\n[TimingPoints]");

            YokkoBeatmap beatmap =
                OsuManiaBeatmapIO.ReadBeatmap(source);

            Assert.Multiple(() =>
            {
                Assert.That(
                    beatmap.PreviewTimeMilliseconds,
                    Is.EqualTo(12369));
                Assert.That(
                    beatmap.TimingPoints.Select(static point =>
                        point.TimeMilliseconds),
                    Is.EqualTo(new[] { 24d, 1024d, 2024d }));
                Assert.That(
                    beatmap.HitObjects.Select(static hitObject =>
                        hitObject.StartTimeMilliseconds),
                    Is.EqualTo(new[] { 1024d, 1524d, 2024d }));
                Assert.That(
                    beatmap.HitObjects[1].EndTimeMilliseconds,
                    Is.EqualTo(1774));
                Assert.That(
                    beatmap.BreakPeriods,
                    Is.EqualTo(new[]
                    {
                        new YokkoBreakPeriod(1124, 1424),
                    }));
            });
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
            Assert.That(reparsed.DrainRate, Is.EqualTo(editable.DrainRate));
            Assert.That(exported, Does.Contain("OverallDifficulty:8"));
            Assert.That(exported, Does.Contain("HPDrainRate:6"));
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
        public void EditableRoundTripPreservesPreviewTimeAndBreakPeriods()
        {
            string source = sampleOsu.Replace(
                "[TimingPoints]",
                "[Events]\n2,1100,1400\n2,1900,2100\n\n[TimingPoints]");
            YokkoBeatmap imported =
                OsuManiaBeatmapIO.ReadBeatmap(source);
            EditableBeatmap editable =
                EditableBeatmap.FromBeatmap(imported);

            string exported =
                OsuManiaBeatmapIO.WriteEditableToString(editable);
            YokkoBeatmap reparsed =
                OsuManiaBeatmapIO.ReadBeatmap(exported);

            Assert.Multiple(() =>
            {
                Assert.That(
                    imported.BreakPeriods,
                    Is.EqualTo(new[]
                    {
                        new YokkoBreakPeriod(1100, 1400),
                        new YokkoBreakPeriod(1900, 2100),
                    }));
                Assert.That(
                    editable.PreviewTimeMilliseconds,
                    Is.EqualTo(12345));
                Assert.That(
                    editable.BreakPeriods,
                    Is.EqualTo(imported.BreakPeriods));
                Assert.That(
                    exported,
                    Does.Contain("PreviewTime: 12345"));
                Assert.That(
                    exported,
                    Does.Contain("2,1100,1400"));
                Assert.That(
                    exported,
                    Does.Contain("2,1900,2100"));
                Assert.That(
                    reparsed.PreviewTimeMilliseconds,
                    Is.EqualTo(12345));
                Assert.That(
                    reparsed.BreakPeriods,
                    Is.EqualTo(imported.BreakPeriods));
            });
        }

        [Test]
        public void EditableRoundTripPreservesOsuNamingMetadata()
        {
            string source = sampleOsu
                .Replace("Title:Test Song", "Title:Romanised Title\nTitleUnicode:Original Title")
                .Replace("Artist:Test Artist", "Artist:Romanised Artist\nArtistUnicode:Original Artist")
                .Replace(
                    "Version:4K",
                    "Version:4K\nSource:Soundtrack\nTags:tag one\nBeatmapID:123\nBeatmapSetID:456");
            EditableBeatmap editable = EditableBeatmap.FromBeatmap(
                OsuManiaBeatmapIO.ReadBeatmap(source));

            YokkoBeatmap reparsed = OsuManiaBeatmapIO.ReadBeatmap(
                OsuManiaBeatmapIO.WriteEditableToString(editable));

            Assert.Multiple(() =>
            {
                Assert.That(reparsed.Title, Is.EqualTo("Original Title"));
                Assert.That(reparsed.RomanisedTitle, Is.EqualTo("Romanised Title"));
                Assert.That(reparsed.Artist, Is.EqualTo("Original Artist"));
                Assert.That(reparsed.RomanisedArtist, Is.EqualTo("Romanised Artist"));
                Assert.That(reparsed.Source, Is.EqualTo("Soundtrack"));
                Assert.That(reparsed.Tags, Is.EqualTo("tag one"));
                Assert.That(reparsed.OnlineBeatmapId, Is.EqualTo(123));
                Assert.That(reparsed.OnlineBeatmapSetId, Is.EqualTo(456));
            });
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
PreviewTime: 12345
Mode: 3

[Metadata]
Title:Test Song
Artist:Test Artist
Creator:Mapper
Version:4K

[Difficulty]
HPDrainRate:6
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

        private const string sampleStandard = """
osu file format v14

[General]
AudioFilename: audio.mp3
Mode: 0

[Metadata]
Title:Standard Source
Artist:Test Artist
Creator:Mapper
Version:Insane

[Difficulty]
HPDrainRate:6
CircleSize:4
OverallDifficulty:8
ApproachRate:9
SliderMultiplier:1.4

[TimingPoints]
0,500,4,2,0,100,1,0
1000,-50,4,2,0,80,0,0

[HitObjects]
64,192,500,1,0,0:0:0:0:
192,192,750,1,0,0:0:0:0:
256,192,1000,2,0,B|320:192,2,140
384,192,1750,1,0,0:0:0:0:
448,192,2000,1,0,0:0:0:0:
""";
    }
}
