using NUnit.Framework;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Import;

namespace Yokko.Game.Tests.Core
{
    [TestFixture]
    public sealed class ChartImporterTest
    {
        [Test]
        public void RegistryAdvertisesAllSupportedFormats()
        {
            Assert.That(KnownChartImporters.FileExtensions, Does.Contain(".osu"));
            Assert.That(KnownChartImporters.FileExtensions, Does.Contain(".osz"));
            Assert.That(KnownChartImporters.FileExtensions, Does.Contain(".qua"));
            Assert.That(KnownChartImporters.FileExtensions, Does.Contain(".mc"));
            Assert.That(KnownChartImporters.FileExtensions, Does.Contain(".mcz"));
            Assert.That(KnownChartImporters.FileExtensions, Does.Contain(".sm"));
            Assert.That(KnownChartImporters.FileExtensions, Does.Contain(".ssc"));
            Assert.That(KnownChartImporters.FileExtensions, Does.Contain(".zip"));
            Assert.That(KnownChartImporters.FileExtensions, Does.Contain(".smzip"));
            Assert.That(KnownChartImporters.FileExtensions, Does.Contain(".bms"));
            Assert.That(KnownChartImporters.FileExtensions, Does.Contain(".bme"));
            Assert.That(KnownChartImporters.FileExtensions, Does.Contain(".bml"));
        }

        [Test]
        public void ImportsOsuPackageAndResolvesPackagedAudio()
        {
            string archivePath = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "chart-import",
                TestContext.CurrentContext.Test.ID,
                $"package-{Guid.NewGuid():N}.osz");
            Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);

            using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                writeEntry(archive, "audio.ogg", string.Empty);
                writeEntry(archive, "chart.osu", """
osu file format v14

[General]
AudioFilename: audio.ogg
Mode: 3

[Metadata]
Title:Packaged
Artist:Artist
Creator:Mapper
Version:4K

[Difficulty]
CircleSize:4

[TimingPoints]
0,500,4,2,0,100,1,0

[HitObjects]
64,192,500,1,0,0:0:0:0:
""");
            }

            ChartImportResult result = import(archivePath);

            Assert.That(result.Beatmap.Title, Is.EqualTo("Packaged"));
            Assert.That(result.Beatmap.AudioPath, Does.EndWith("audio.ogg"));
            Assert.That(File.Exists(result.Beatmap.AudioPath), Is.True);
        }

        [Test]
        public void OsuPackageSkipsUnsupportedChartsAndImportsCompatibleManiaChart()
        {
            string archivePath = createArchivePath("mixed-osu", ".osz");

            using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                writeEntry(archive, "00-standard.osu", """
osu file format v14

[General]
Mode: 0

[Difficulty]
CircleSize:4
""");
                writeEntry(archive, "01-mania.osu", """
osu file format v14

[General]
AudioFilename: audio.ogg
Mode: 3

[Metadata]
Title:Mania Package
Artist:Artist
Creator:Mapper
Version:7K

[Difficulty]
CircleSize:7

[TimingPoints]
0,500,4,2,0,100,1,0

[HitObjects]
36,192,500,1,0,0:0:0:0:
""");
                writeEntry(archive, "audio.ogg", string.Empty);
            }

            ChartImportResult result = import(archivePath);

            Assert.That(result.Beatmap.Title, Is.EqualTo("Mania Package"));
            Assert.That(result.Beatmap.KeyMode, Is.EqualTo(KeyMode.SevenKey));
            Assert.That(result.Beatmap.AudioPath, Does.EndWith("audio.ogg"));
            Assert.That(result.Warnings.Single(), Does.Contain("01-mania.osu"));
        }

        [Test]
        public void ImportsQuaverTapHoldAndTiming()
        {
            string path = writeChart("quaver", ".qua", """
AudioFile: audio.ogg
Mode: Keys4
Title: Test Qua
Artist: Test Artist
Creator: Mapper
DifficultyName: Hard
TimingPoints:
- StartTime: 0
  Bpm: 120
  TimeSignature: 4|4
- StartTime: 2000
  Bpm: 150
SliderVelocities: []
HitObjects:
- StartTime: 500
  Lane: 1
- StartTime: 1000
  Lane: 4
  EndTime: 1500
""");
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(path)!, "audio.ogg"), []);

            ChartImportResult result = import(path);

            Assert.That(result.Beatmap.SourceFormat, Is.EqualTo(ChartSourceFormat.Quaver));
            Assert.That(result.Beatmap.KeyMode, Is.EqualTo(KeyMode.FourKey));
            Assert.That(result.Beatmap.TimingPoints, Has.Count.EqualTo(2));
            Assert.That(result.Beatmap.HitObjects, Has.Count.EqualTo(2));
            Assert.That(result.Beatmap.HitObjects[1].Kind, Is.EqualTo(HitObjectKind.Hold));
            Assert.That(result.Beatmap.HitObjects[1].EndTimeMilliseconds, Is.EqualTo(1500));
            Assert.That(result.Beatmap.AudioPath, Does.EndWith("audio.ogg"));
        }

        [Test]
        public void ImportsNormalizedQuaverZeroAndNegativeSliderVelocities()
        {
            string path = writeChart("quaver-normalized-sv", ".qua", """
Mode: Keys4
Title: Normalized SV
BPMDoesNotAffectScrollVelocity: true
InitialScrollVelocity: 1.25
TimingPoints:
- StartTime: 0
  Bpm: 120
SliderVelocities:
- StartTime: 500
  Multiplier: 0
- StartTime: 1000
  Multiplier: -2
HitObjects:
- StartTime: 1500
  Lane: 1
""");

            ChartImportResult result = import(path);

            Assert.That(result.Warnings, Is.Empty);
            Assert.That(result.Beatmap.InitialScrollVelocity, Is.EqualTo(1.25));
            Assert.That(result.Beatmap.ScrollVelocities, Has.Count.EqualTo(2));
            Assert.That(result.Beatmap.ScrollVelocities[0], Is.EqualTo(
                new Yokko.Core.Timing.YokkoScrollVelocity(500, 0)));
            Assert.That(result.Beatmap.ScrollVelocities[1], Is.EqualTo(
                new Yokko.Core.Timing.YokkoScrollVelocity(1000, -2)));
        }

        [Test]
        public void NormalizesLegacyQuaverBpmAndSliderVelocityChanges()
        {
            string path = writeChart("quaver-denormalized-sv", ".qua", """
Mode: Keys4
Title: Legacy SV
TimingPoints:
- StartTime: 0
  Bpm: 120
- StartTime: 2000
  Bpm: 240
SliderVelocities:
- StartTime: 1000
  Multiplier: 0.5
HitObjects:
- StartTime: 3000
  Lane: 1
""");

            ChartImportResult result = import(path);

            Assert.That(result.Beatmap.InitialScrollVelocity, Is.EqualTo(1));
            Assert.That(result.Beatmap.ScrollVelocities, Has.Count.EqualTo(2));
            Assert.That(result.Beatmap.ScrollVelocities[0].Multiplier, Is.EqualTo(0.5));
            Assert.That(result.Beatmap.ScrollVelocities[1].TimeMilliseconds, Is.EqualTo(2000));
            Assert.That(result.Beatmap.ScrollVelocities[1].Multiplier, Is.EqualTo(2));
        }

        [Test]
        public void ParsesIndentedQuaverCollectionsWithoutOverwritingItems()
        {
            string path = writeChart("quaver-indented", ".qua", """
Mode: Keys4
Title: Indented Qua
BPMDoesNotAffectScrollVelocity: true
InitialScrollVelocity: 1
TimingPoints:
  - StartTime: 0
    Bpm: 120
  - StartTime: 2000
    Bpm: 180
SliderVelocities:
  - StartTime: 500
    Multiplier: 0.5
  - StartTime: 1000
    Multiplier: 2
ScrollSpeedFactors:
  - StartTime: 250
    Multiplier: 0.75
  - StartTime: 1250
    Multiplier: 1.25
HitObjects:
  - StartTime: 1500
    Lane: 1
  - StartTime: 2000
    Lane: 4
""");

            ChartImportResult result = import(path);

            Assert.That(result.Beatmap.TimingPoints, Has.Count.EqualTo(2));
            Assert.That(result.Beatmap.ScrollVelocities, Has.Count.EqualTo(2));
            Assert.That(result.Beatmap.ScrollSpeedFactors, Has.Count.EqualTo(2));
            Assert.That(
                result.Beatmap.ScrollSpeedFactors[1].Multiplier,
                Is.EqualTo(1.25));
            Assert.That(result.Beatmap.HitObjects, Has.Count.EqualTo(2));
        }

        [Test]
        public void NormalizesQuaverSvBeforeFirstTimingPointIntoInitialVelocity()
        {
            string path = writeChart("quaver-sv-before-timing", ".qua", """
Mode: Keys4
Title: Early SV
BPMDoesNotAffectScrollVelocity: false
TimingPoints:
  - StartTime: 0
    Bpm: 120
SliderVelocities:
  - StartTime: -10
    Multiplier: 10
HitObjects: []
""");

            ChartImportResult result = import(path);

            Assert.That(result.Beatmap.InitialScrollVelocity, Is.EqualTo(10));
            Assert.That(result.Beatmap.ScrollVelocities, Is.EqualTo(
            new[]
            {
                new Yokko.Core.Timing.YokkoScrollVelocity(0, 1),
            }));
        }

        [Test]
        public void NormalizesQuaverSvAtFirstTimingPointIntoInitialVelocity()
        {
            string path = writeChart("quaver-sv-at-timing", ".qua", """
Mode: Keys4
Title: Initial SV
BPMDoesNotAffectScrollVelocity: false
TimingPoints:
  - StartTime: 0
    Bpm: 120
SliderVelocities:
  - StartTime: 0
    Multiplier: 10
HitObjects: []
""");

            ChartImportResult result = import(path);

            Assert.That(result.Beatmap.InitialScrollVelocity, Is.EqualTo(10));
            Assert.That(result.Beatmap.ScrollVelocities, Is.Empty);
        }

        [Test]
        public void MatchesQuaverTimingPointOverrideNormalizationCorpus()
        {
            string path = writeChart("quaver-timing-overrides-sv", ".qua", """
Mode: Keys4
BPMDoesNotAffectScrollVelocity: false
TimingPoints:
  - StartTime: 0
    Bpm: 1
  - StartTime: 10
    Bpm: 2
SliderVelocities:
  - StartTime: 5
    Multiplier: 10
HitObjects:
  - StartTime: 0
    Lane: 1
  - StartTime: 11
    Lane: 1
""");

            ChartImportResult result = import(path);

            Assert.That(result.Beatmap.InitialScrollVelocity, Is.EqualTo(1));
            Assert.That(result.Beatmap.ScrollVelocities, Is.EqualTo(
            new[]
            {
                new Yokko.Core.Timing.YokkoScrollVelocity(5, 10),
                new Yokko.Core.Timing.YokkoScrollVelocity(10, 2),
            }));
        }

        [Test]
        public void ImportsQuaverTimingGroupsAndMergesGlobalSignals()
        {
            string path = writeChart("quaver-timing-groups", ".qua", """
Mode: Keys4
Title: Timing Groups
BPMDoesNotAffectScrollVelocity: true
InitialScrollVelocity: 1
TimingPoints:
  - StartTime: 0
    Bpm: 120
SliderVelocities:
  - StartTime: 500
    Multiplier: 2
TimingGroups:
  "$Global": !ScrollGroup
    ScrollVelocities:
      - StartTime: 750
        Multiplier: 0.5
  Reverse: !ScrollGroup
    ScrollVelocities:
      - StartTime: 750
        Multiplier: 8
      - StartTime: 1000
        Multiplier: -2
    ScrollSpeedFactors:
      - StartTime: 0
        Multiplier: 1.5
    InitialScrollVelocity: -1
HitObjects:
  - StartTime: 1500
    Lane: 1
  - StartTime: 1600
    Lane: 2
    TimingGroup: Reverse
""");

            ChartImportResult result = import(path);

            Assert.That(result.Warnings, Is.Empty);
            Assert.That(result.Beatmap.ScrollVelocities, Is.EqualTo(
            new[]
            {
                new Yokko.Core.Timing.YokkoScrollVelocity(500, 2),
                new Yokko.Core.Timing.YokkoScrollVelocity(750, 0.5),
            }));
            Assert.That(result.Beatmap.ScrollProfiles.Keys, Is.EqualTo(
                new[] { "Reverse" }));
            Yokko.Core.Timing.YokkoScrollProfile reverse =
                result.Beatmap.ScrollProfiles["Reverse"];
            Assert.That(reverse.InitialScrollVelocity, Is.EqualTo(-1));
            Assert.That(reverse.ScrollVelocities, Is.EqualTo(
            new[]
            {
                new Yokko.Core.Timing.YokkoScrollVelocity(750, 0.5),
                new Yokko.Core.Timing.YokkoScrollVelocity(1000, -2),
            }));
            Assert.That(reverse.ScrollSpeedFactors.Single().Multiplier,
                Is.EqualTo(1.5));
            Assert.That(result.Beatmap.HitObjects[0].ScrollProfileId, Is.Null);
            Assert.That(result.Beatmap.HitObjects[1].ScrollProfileId,
                Is.EqualTo("Reverse"));
        }

        [Test]
        public void WarnsAndFallsBackWhenQuaverTimingGroupIsMissing()
        {
            string path = writeChart("quaver-missing-timing-group", ".qua", """
Mode: Keys4
TimingPoints:
  - StartTime: 0
    Bpm: 120
HitObjects:
  - StartTime: 500
    Lane: 1
    TimingGroup: Missing
""");

            ChartImportResult result = import(path);

            Assert.That(result.Warnings.Single(), Does.Contain("Missing"));
            Assert.That(result.Beatmap.HitObjects.Single().ScrollProfileId,
                Is.EqualTo("Missing"));
            Assert.That(result.Beatmap.ScrollProfiles, Is.Empty);
        }

        [Test]
        public void ImportsMalodyFractionalBeatsAndAudioOffset()
        {
            string path = writeChart("malody", ".mc", """
{
  "meta": {
    "mode": 0,
    "creator": "Mapper",
    "version": "Normal",
    "song": { "title": "Test MC", "artist": "Artist" },
    "mode_ext": { "column": 4 }
  },
  "time": [
    { "beat": [0, 0, 1], "bpm": 120 },
    { "beat": [4, 0, 1], "bpm": 240 }
  ],
  "note": [
    { "beat": [1, 1, 2], "column": 0 },
    { "beat": [4, 0, 1], "endbeat": [5, 0, 1], "column": 3 },
    { "beat": [0, 0, 1], "sound": "song.ogg", "offset": 100, "type": 1 }
  ]
}
""");
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(path)!, "song.ogg"), []);

            ChartImportResult result = import(path);

            Assert.That(result.Beatmap.SourceFormat, Is.EqualTo(ChartSourceFormat.Malody));
            Assert.That(result.Beatmap.HitObjects, Has.Count.EqualTo(2));
            Assert.That(result.Beatmap.HitObjects[0].StartTimeMilliseconds, Is.EqualTo(650).Within(0.001));
            Assert.That(result.Beatmap.HitObjects[1].StartTimeMilliseconds, Is.EqualTo(1900).Within(0.001));
            Assert.That(result.Beatmap.HitObjects[1].EndTimeMilliseconds, Is.EqualTo(2150).Within(0.001));
        }

        [Test]
        public void ImportsStepManiaChartAndBakesStops()
        {
            string path = writeChart("etterna", ".sm", """
#TITLE:Test SM;
#ARTIST:Artist;
#CREDIT:Mapper;
#MUSIC:song.ogg;
#OFFSET:0;
#BPMS:0=120,4=240;
#STOPS:2=0.500;
#NOTES:
     dance-single:
     Mapper:
     Hard:
     10:
     0,0,0,0,0:
1000
0200
0000
0300
,
0001
0000
0000
0000
;
""");

            ChartImportResult result = import(path);

            Assert.That(result.Beatmap.SourceFormat, Is.EqualTo(ChartSourceFormat.Etterna));
            Assert.That(result.Beatmap.HitObjects, Has.Count.EqualTo(3));
            Assert.That(result.Beatmap.HitObjects[1].Kind, Is.EqualTo(HitObjectKind.Hold));
            Assert.That(result.Beatmap.HitObjects[1].StartTimeMilliseconds, Is.EqualTo(500).Within(0.001));
            Assert.That(result.Beatmap.HitObjects[1].EndTimeMilliseconds, Is.EqualTo(2000).Within(0.001));
            Assert.That(result.Beatmap.HitObjects[2].StartTimeMilliseconds, Is.EqualTo(2500).Within(0.001));
        }

        [Test]
        public void ImportsSscChartLevelTimingAndReportsWarps()
        {
            string path = writeChart("etterna-ssc", ".ssc", """
#VERSION:0.83;
#TITLE:Test SSC;
#ARTIST:Artist;
#MUSIC:song.ogg;
#BPMS:0=120;
#NOTEDATA:;
#CHARTNAME:7K Test;
#STEPSTYPE:kb7-single;
#DIFFICULTY:Challenge;
#METER:12;
#CREDIT:Mapper;
#BPMS:0=150;
#WARPS:8=2;
#NOTES:
1000000
0000000
0000001
0000000
;
""");

            ChartImportResult result = import(path);

            Assert.That(result.Beatmap.KeyMode, Is.EqualTo(KeyMode.SevenKey));
            Assert.That(result.Beatmap.DifficultyName, Is.EqualTo("7K Test"));
            Assert.That(result.Beatmap.TimingPoints[0].BeatsPerMinute, Is.EqualTo(150).Within(0.001));
            Assert.That(result.Beatmap.HitObjects, Has.Count.EqualTo(2));
            Assert.That(result.Warnings.Any(warning => warning.Contains("warps", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        [Test]
        public void UsesDifficultyAndMeterWhenSscChartNameIsEmpty()
        {
            string path = writeChart("etterna-ssc-name", ".ssc", """
#TITLE:Test SSC;
#ARTIST:Artist;
#BPMS:0=120;
#NOTEDATA:;
#CHARTNAME:;
#DESCRIPTION:;
#STEPSTYPE:dance-single;
#DIFFICULTY:Challenge;
#METER:12;
#NOTES:
1000
0000
0000
0000
;
""");

            ChartImportResult result = import(path);

            Assert.That(result.Beatmap.DifficultyName, Is.EqualTo("Challenge 12"));
        }

        [TestCase(".zip")]
        [TestCase(".smzip")]
        public void ImportsEtternaPackageAndResolvesNestedAudio(string extension)
        {
            string archivePath = createArchivePath("etterna-package", extension);

            using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                writeEntry(archive, "Pack/Song/00-unsupported.sm", """
#TITLE:Unsupported 6K;
#ARTIST:Artist;
#BPMS:0=120;
#NOTES:
     dance-solo:
     :
     Hard:
     9:
     0,0,0,0,0:
100000
000001
;
""");
                writeEntry(archive, "Pack/Song/01-supported.ssc", """
#TITLE:Packaged SSC;
#ARTIST:Artist;
#MUSIC:audio.ogg;
#BPMS:0=150;
#NOTEDATA:;
#CHARTNAME:;
#STEPSTYPE:dance-single;
#DIFFICULTY:Challenge;
#METER:11;
#CREDIT:Mapper;
#NOTES:
1000
0000
0001
0000
;
""");
                writeEntry(archive, "Pack/Song/audio.ogg", string.Empty);
            }

            ChartImportResult result = import(archivePath);

            Assert.That(result.Beatmap.Title, Is.EqualTo("Packaged SSC"));
            Assert.That(result.Beatmap.DifficultyName, Is.EqualTo("Challenge 11"));
            Assert.That(result.Beatmap.HitObjects, Has.Count.EqualTo(2));
            Assert.That(result.Beatmap.AudioPath, Does.EndWith("audio.ogg"));
            Assert.That(File.Exists(result.Beatmap.AudioPath), Is.True);
            Assert.That(result.Warnings[0], Does.Contain("2 simfiles"));
            Assert.That(result.Warnings[0], Does.Contain("01-supported.ssc"));
        }

        [Test]
        public void EtternaPackagePrefersSscOverMatchingSmFile()
        {
            string archivePath = createArchivePath("etterna-prefer-ssc", ".zip");

            using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                writeEntry(archive, "Pack/Song/chart.sm", """
#TITLE:SM Version;
#ARTIST:Artist;
#BPMS:0=120;
#NOTES:
     dance-single:
     :
     Hard:
     8:
     0,0,0,0,0:
1000
0000
0000
0000
;
""");
                writeEntry(archive, "Pack/Song/chart.ssc", """
#TITLE:SSC Version;
#ARTIST:Artist;
#BPMS:0=120;
#NOTEDATA:;
#STEPSTYPE:dance-single;
#DIFFICULTY:Hard;
#METER:9;
#NOTES:
0001
0000
0000
0000
;
""");
            }

            ChartImportResult result = import(archivePath);

            Assert.That(result.Beatmap.Title, Is.EqualTo("SSC Version"));
            Assert.That(result.Warnings[0], Does.Contain("chart.ssc"));

            ChartImportResult smResult = KnownChartImporters.ImportAsync(
                                                                  new ChartImportRequest(
                                                                      archivePath,
                                                                      PreferKeysounds: true,
                                                                      PreferSscSimfiles: false))
                                                              .AsTask()
                                                              .GetAwaiter()
                                                              .GetResult();

            Assert.That(smResult.Beatmap.Title, Is.EqualTo("SM Version"));
            Assert.That(smResult.Warnings[0], Does.Contain("chart.sm"));
        }

        [Test]
        public void RejectsUnsafeEtternaPackagePaths()
        {
            string archivePath = createArchivePath("unsafe-etterna", ".zip");

            using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
                writeEntry(archive, "../outside.ssc", "#TITLE:Unsafe;");

            Assert.That(
                () => import(archivePath),
                Throws.TypeOf<InvalidDataException>().With.Message.Contains("Unsafe path"));
        }

        [Test]
        public void ImportsBmsFourKeyLongNoteAndBpmChange()
        {
            string path = writeChart("bms", ".bms", """
#TITLE Test BMS
#ARTIST Artist
#SUBARTIST Mapper
#BPM 120
#BPM01 240
#WAV01 song.ogg
#00101:01
#00111:0100
#00112:0001
#00113:0100
#00114:0001
#00208:01
#00251:0101
""", Encoding.ASCII);
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(path)!, "song.ogg"), []);

            ChartImportResult result = import(path);

            Assert.That(result.Beatmap.SourceFormat, Is.EqualTo(ChartSourceFormat.Bms));
            Assert.That(result.Beatmap.KeyMode, Is.EqualTo(KeyMode.FourKey));
            Assert.That(result.Beatmap.HitObjects, Has.Count.EqualTo(5));
            Assert.That(result.Beatmap.HitObjects.Count(note => note.Kind == HitObjectKind.Hold), Is.EqualTo(1));
            Assert.That(result.Beatmap.HitObjects[0].StartTimeMilliseconds, Is.EqualTo(0).Within(0.001));
            Assert.That(result.Beatmap.TimingPoints, Has.Count.EqualTo(2));
            Assert.That(result.Beatmap.TimingPoints[1].BeatsPerMinute, Is.EqualTo(240).Within(0.001));
            Assert.That(result.Beatmap.AudioPath, Does.EndWith("song.ogg"));
            Assert.That(result.Beatmap.HitObjects.Any(note => note.SampleKey != null), Is.True);

            ChartImportResult withoutKeysounds = KnownChartImporters.ImportAsync(
                                                                      new ChartImportRequest(
                                                                          path,
                                                                          PreferKeysounds: false))
                                                                  .AsTask()
                                                                  .GetAwaiter()
                                                                  .GetResult();

            Assert.That(withoutKeysounds.Beatmap.HitObjects.All(note => note.SampleKey == null), Is.True);
            Assert.That(
                withoutKeysounds.Warnings.Any(warning => warning.Contains("preserved", StringComparison.OrdinalIgnoreCase)),
                Is.False);
        }

        private static ChartImportResult import(string path)
            => KnownChartImporters.ImportAsync(new ChartImportRequest(path, true))
                                  .AsTask()
                                  .GetAwaiter()
                                  .GetResult();

        private static string writeChart(string name, string extension, string content, Encoding encoding = null)
        {
            string directory = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "chart-import",
                TestContext.CurrentContext.Test.ID);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name + extension);
            File.WriteAllText(path, content, encoding ?? new UTF8Encoding(false));
            return path;
        }

        private static string createArchivePath(string name, string extension)
        {
            string directory = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "chart-import",
                TestContext.CurrentContext.Test.ID);
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, $"{name}-{Guid.NewGuid():N}{extension}");
        }

        private static void writeEntry(ZipArchive archive, string name, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name);
            using StreamWriter writer = new(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }
    }
}
