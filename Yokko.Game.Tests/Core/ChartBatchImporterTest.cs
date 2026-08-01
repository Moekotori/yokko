using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Yokko.Import;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class ChartBatchImporterTest
{
    [Test]
    public void ImportsEveryCompatibleChartFromOsuPackage()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "chart-batch-import",
            TestContext.CurrentContext.Test.ID);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"{Guid.NewGuid():N}.osz");

        using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            writeChart(archive, "first.osu", "First", 4);
            writeChart(archive, "second.osu", "Second", 7);
        }

        IReadOnlyList<ChartImportResult> results =
            KnownChartImporters.ImportAllAsync(new ChartImportRequest(path, true))
                               .AsTask()
                               .GetAwaiter()
                               .GetResult();

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(
            results.Select(result => result.Beatmap.Title),
            Is.EquivalentTo(new[] { "First", "Second" }));
    }

    [Test]
    public void ImportsEveryCompatibleChartAndAssetsFromQuaverPackage()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "chart-batch-import",
            TestContext.CurrentContext.Test.ID);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"{Guid.NewGuid():N}.qp");

        using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            writeTextEntry(archive, "Mapset/audio.ogg", string.Empty);
            writeTextEntry(archive, "Mapset/background.jpg", "art");
            writeQuaverChart(archive, "Mapset/first.qua", "Easy", 4);
            writeQuaverChart(archive, "Mapset/second.qua", "Hard", 7);
        }

        IReadOnlyList<ChartImportResult> results =
            KnownChartImporters.ImportAllAsync(new ChartImportRequest(path, true))
                               .AsTask()
                               .GetAwaiter()
                               .GetResult();

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(
                results.Select(result => result.Beatmap.DifficultyName),
                Is.EquivalentTo(new[] { "Easy", "Hard" }));
            Assert.That(
                results.Select(result => result.Beatmap.AudioPath),
                Is.All.Matches<string>(File.Exists));
            Assert.That(
                results.Select(result => result.ArtworkPath),
                Is.All.Matches<string>(File.Exists));
        });
    }

    [Test]
    public void ImportsEveryCompatibleChartAndAssetsFromMalodyPackage()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "chart-batch-import",
            TestContext.CurrentContext.Test.ID);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"{Guid.NewGuid():N}.mcz");

        using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            writeTextEntry(archive, "Mapset/song.ogg", string.Empty);
            writeTextEntry(archive, "Mapset/cover.jpg", "art");
            writeMalodyChart(archive, "Mapset/easy.mc", "Easy", 4, 0);
            writeMalodyChart(archive, "Mapset/hard.mc", "Hard", 7, 0);
            writeMalodyChart(archive, "Mapset/taiko.mc", "Taiko", 4, 5);
        }

        IReadOnlyList<ChartImportResult> results =
            KnownChartImporters.ImportAllAsync(new ChartImportRequest(path, true))
                               .AsTask()
                               .GetAwaiter()
                               .GetResult();

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(
                results.Select(result => result.Beatmap.DifficultyName),
                Is.EquivalentTo(new[] { "Easy", "Hard" }));
            Assert.That(
                results.Select(result => result.Beatmap.AudioPath),
                Is.All.Matches<string>(File.Exists));
            Assert.That(
                results.Select(result => result.ArtworkPath),
                Is.All.Matches<string>(File.Exists));
            Assert.That(
                results.SelectMany(result => result.Warnings)
                       .Any(warning => warning.Contains(
                           "Skipped 1",
                           StringComparison.OrdinalIgnoreCase)),
                Is.True);
        });
    }

    private static void writeChart(
        ZipArchive archive,
        string path,
        string title,
        int keys)
    {
        using Stream stream = archive.CreateEntry(path).Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write($"""
osu file format v14

[General]
Mode: 3

[Metadata]
Title:{title}
Artist:Artist
Creator:Mapper
Version:{keys}K

[Difficulty]
CircleSize:{keys}

[TimingPoints]
0,500,4,2,0,100,1,0

[HitObjects]
64,192,500,1,0,0:0:0:0:
""");
    }

    private static void writeQuaverChart(
        ZipArchive archive,
        string path,
        string difficulty,
        int keys)
    {
        writeTextEntry(archive, path, $"""
AudioFile: audio.ogg
BackgroundFile: background.jpg
Mode: Keys{keys}
Title: Quaver Package
Artist: Artist
Creator: Mapper
DifficultyName: {difficulty}
TimingPoints:
- StartTime: 0
  Bpm: 120
HitObjects:
- StartTime: 500
  Lane: 1
""");
    }

    private static void writeMalodyChart(
        ZipArchive archive,
        string path,
        string difficulty,
        int keys,
        int mode)
    {
        writeTextEntry(archive, path, $$"""
{
  "meta": {
    "mode": {{mode}},
    "version": "{{difficulty}}",
    "background": "cover.jpg",
    "song": { "title": "Malody Package", "artist": "Artist" },
    "mode_ext": { "column": {{keys}} }
  },
  "time": [{ "beat": [0, 0, 1], "bpm": 120 }],
  "note": [
    { "beat": [1, 0, 1], "column": 0 },
    { "beat": [0, 0, 1], "sound": "song.ogg", "type": 1 }
  ]
}
""");
    }

    private static void writeTextEntry(
        ZipArchive archive,
        string path,
        string content)
    {
        using Stream stream = archive.CreateEntry(path).Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }
}
