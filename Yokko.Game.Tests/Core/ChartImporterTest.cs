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

        private static void writeEntry(ZipArchive archive, string name, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name);
            using StreamWriter writer = new(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }
    }
}
