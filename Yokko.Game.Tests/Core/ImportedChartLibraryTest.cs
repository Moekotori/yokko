using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Platform;
using Yokko.Core.Beatmaps;
using Yokko.Core.Difficulty;
using Yokko.Game.Importing;
using Yokko.Import;
using Yokko.Import.Osu;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class ImportedChartLibraryTest
{
    [Test]
    public void NativeReplayFindsExactImportedGameplayModel()
    {
        var library = new ImportedChartLibrary();
        YokkoBeatmap expected = DemoBeatmaps.CreateFourKeyDemo();
        library.AddOrReplace(
            new ChartImportResult(expected, [], SourceHash: "SOURCE"),
            @"C:\Charts\replay-target.osu");

        ImportedChart restored =
            library.FindByBeatmapFingerprint(
                YokkoBeatmapFingerprint.Compute(expected));

        Assert.Multiple(() =>
        {
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.Result.Beatmap, Is.SameAs(expected));
            Assert.That(restored.Result.SourceHash, Is.EqualTo("SOURCE"));
            Assert.That(
                library.FindByBeatmapFingerprint(
                    new string('0', 64)),
                Is.Null);
        });
    }

    [Test]
    public void ReimportingSamePathReplacesExistingChart()
    {
        var library = new ImportedChartLibrary();
        var first = new ChartImportResult(DemoBeatmaps.CreateFourKeyDemo(), []);
        var replacement = new ChartImportResult(
            DemoBeatmaps.CreateFourKeyDemo() with { Title = "Replacement" },
            []);

        library.AddOrReplace(first, @"C:\Charts\example.osu");
        library.AddOrReplace(replacement, @"c:\charts\EXAMPLE.osu");

        Assert.That(library.GetCharts(), Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(
                library.GetCharts()[0].Result.Beatmap.Title,
                Is.EqualTo("Replacement"));
            Assert.That(
                library.GetCharts()[0].DifficultyRating.IsSuccess,
                Is.True);
            Assert.That(
                library.GetCharts()[0].DifficultyRating.Value,
                Is.EqualTo(ManiaMsdCalculator.CalculateResult(
                    replacement.Beatmap).Value));
        });
    }

    [Test]
    public void PackageAddsAllChartsAndRefreshesOnce()
    {
        var library = new ImportedChartLibrary();
        int refreshCount = 0;
        library.LibraryChanged += () => refreshCount++;

        library.AddOrReplace(
            [
                new ChartImportResult(DemoBeatmaps.CreateFourKeyDemo(), []),
                new ChartImportResult(DemoBeatmaps.CreateSevenKeyDemo(), []),
            ],
            @"C:\Charts\pack.osz");

        Assert.That(library.GetCharts(), Has.Count.EqualTo(2));
        Assert.That(library.GetCharts().Select(chart => chart.Id), Is.Unique);
        Assert.That(library.GetCharts().Select(chart => chart.PackageName), Is.All.EqualTo("pack"));
        Assert.That(library.GetCharts().Select(chart => chart.IsPackage), Is.All.True);
        Assert.That(refreshCount, Is.EqualTo(1));
    }

    [Test]
    public void SingleChartArchiveIsNotAPackage()
    {
        // 单曲 .osz 只是一首歌，不能仅凭扩展名就当成图包。
        var library = new ImportedChartLibrary();

        library.AddOrReplace(
            new ChartImportResult(DemoBeatmaps.CreateFourKeyDemo(), []),
            @"C:\Charts\single-song.osz");

        Assert.Multiple(() =>
        {
            Assert.That(library.GetCharts(), Has.Count.EqualTo(1));
            Assert.That(library.GetCharts()[0].IsPackage, Is.False);
            Assert.That(library.GetCharts()[0].PackageName, Is.EqualTo("single-song"));
        });
    }

    [Test]
    public void MultiSongPackageUsesChartVersionAsSongTitle()
    {
        var library = new ImportedChartLibrary();
        YokkoBeatmap source = DemoBeatmaps.CreateFourKeyDemo() with
        {
            Title = "GD PACK (clear 2 out of 7 maps)",
        };

        library.AddOrReplace(
            [
                new ChartImportResult(source with
                {
                    DifficultyName = "Cold Sweat",
                    AudioPath = @"C:\Audio\cold-sweat.ogg",
                }, []),
                new ChartImportResult(source with
                {
                    DifficultyName = "Dear Nostalgists",
                    AudioPath = @"C:\Audio\dear-nostalgists.ogg",
                }, []),
            ],
            @"C:\Charts\VA - GD PACK.osz");

        ImportedChart[] charts = library.GetCharts().ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(
                charts.Select(chart => chart.Result.Beatmap.Title),
                Is.EqualTo(new[] { "Cold Sweat", "Dear Nostalgists" }));
            Assert.That(
                charts.Select(chart => chart.Result.Beatmap.DifficultyName),
                Is.All.EqualTo("PACK"));
            Assert.That(charts.Select(chart => chart.PackageName), Is.All.EqualTo("VA - GD PACK"));
        });
    }

    [Test]
    public void GenericBeatmapSetFileNameUsesReadableMetadataName()
    {
        var library = new ImportedChartLibrary();
        YokkoBeatmap source = DemoBeatmaps.CreateFourKeyDemo() with
        {
            Title = "Deathtrill Compilation",
            Artist = "Various Artists",
        };

        library.AddOrReplace(
            [
                new ChartImportResult(source with
                {
                    DifficultyName = "Marathon x1.0",
                }, []),
                new ChartImportResult(source with
                {
                    DifficultyName = "Marathon x1.1",
                }, []),
            ],
            @"C:\Charts\beatmapset_1620880.osz");

        Assert.That(
            library.GetCharts().Select(chart => chart.PackageName),
            Is.All.EqualTo("Various Artists - Deathtrill Compilation"));
    }

    [Test]
    public async Task ImportedChartAndAudioReloadFromResourceDirectory()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"yokko-chart-library-{Guid.NewGuid():N}");
        string source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);

        try
        {
            string audioPath = Path.Combine(source, "song.mp3");
            string artworkPath = Path.Combine(source, "background.jpg");
            string chartPath = Path.Combine(source, "persistent.osu");
            File.WriteAllBytes(audioPath, []);
            File.WriteAllBytes(artworkPath, [1, 2, 3]);
            string chartText = OsuManiaBeatmapIO.WriteBeatmap(
                DemoBeatmaps.CreateFourKeyDemo() with
                {
                    Title = "Persistent chart",
                    AudioPath = audioPath,
                }).Replace(
                    "//Background and Video events",
                    "//Background and Video events"
                    + Environment.NewLine
                    + "0,0,\"background.jpg\",0,0");
            File.WriteAllText(
                chartPath,
                chartText);

            var first = new ImportedChartLibrary();
            first.Initialise(new NativeStorage(root));
            await first.ImportAsync(new ChartImportRequest(chartPath, true));

            ImportedChart imported = first.GetCharts().Single();
            Assert.Multiple(() =>
            {
                Assert.That(
                    first.LibraryPath,
                    Does.EndWith(Path.Combine("Resources", "Beatmaps")));
                Assert.That(imported.SourcePath, Does.StartWith(first.LibraryPath));
                Assert.That(imported.Result.Beatmap.AudioPath, Does.StartWith(first.LibraryPath));
                Assert.That(File.Exists(imported.Result.Beatmap.AudioPath), Is.True);
                Assert.That(imported.ArtworkPath, Does.StartWith(first.LibraryPath));
                Assert.That(File.Exists(imported.ArtworkPath), Is.True);
            });

            File.Delete(chartPath);
            File.Delete(audioPath);

            var reloaded = new ImportedChartLibrary();
            reloaded.Initialise(new NativeStorage(root));
            int count = await reloaded.LoadFromDiskAsync(true, true);

            Assert.Multiple(() =>
            {
                Assert.That(count, Is.EqualTo(1));
                Assert.That(
                    reloaded.GetCharts().Single().Result.Beatmap.Title,
                    Is.EqualTo("Persistent chart"));
                Assert.That(
                    File.Exists(
                        reloaded.GetCharts().Single().Result.Beatmap.AudioPath),
                    Is.True);
                Assert.That(
                    File.Exists(reloaded.GetCharts().Single().ArtworkPath),
                    Is.True);
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task QuaverPackageDragImportPersistsEveryDifficulty()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"yokko-quaver-library-{Guid.NewGuid():N}");
        string source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        string packagePath = Path.Combine(source, "mapset.qp");

        try
        {
            using (ZipArchive archive =
                   ZipFile.Open(packagePath, ZipArchiveMode.Create))
            {
                writeEntry(archive, "Mapset/audio.ogg", string.Empty);
                writeEntry(archive, "Mapset/background.jpg", "art");
                writeQuaEntry(archive, "Mapset/easy.qua", "Easy", 4);
                writeQuaEntry(archive, "Mapset/hard.qua", "Hard", 7);
            }

            var first = new ImportedChartLibrary();
            first.Initialise(new NativeStorage(root));
            IReadOnlyList<ChartImportResult> results =
                await first.ImportAsync(
                    new ChartImportRequest(packagePath, true));

            Assert.That(results, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(first.GetCharts(), Has.Count.EqualTo(2));
                Assert.That(
                    first.GetCharts().Select(chart => chart.IsPackage),
                    Is.All.True);
                Assert.That(
                    first.GetCharts().Select(
                        chart => chart.Result.Beatmap.AudioPath),
                    Is.All.Matches<string>(File.Exists));
                Assert.That(
                    first.GetCharts().Select(chart => chart.ArtworkPath),
                    Is.All.Matches<string>(File.Exists));
            });

            File.Delete(packagePath);

            var reloaded = new ImportedChartLibrary();
            reloaded.Initialise(new NativeStorage(root));
            int count = await reloaded.LoadFromDiskAsync(true, true);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(
                reloaded.GetCharts().Select(
                    chart => chart.Result.Beatmap.DifficultyName),
                Is.EquivalentTo(new[] { "Easy", "Hard" }));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static void writeQuaEntry(
        ZipArchive archive,
        string path,
        string difficulty,
        int keys)
    {
        writeEntry(archive, path, $"""
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

    private static void writeEntry(
        ZipArchive archive,
        string path,
        string content)
    {
        using Stream stream = archive.CreateEntry(path).Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }
}
