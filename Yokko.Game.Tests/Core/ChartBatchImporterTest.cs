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
}
