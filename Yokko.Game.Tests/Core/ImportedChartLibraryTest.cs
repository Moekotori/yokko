using System;
using System.IO;
using System.Linq;
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
            Assert.That(library.GetCharts()[0].StarRating, Is.Not.Null);
            Assert.That(
                library.GetCharts()[0].StarRating,
                Is.EqualTo(ManiaStarRatingCalculator.Calculate(
                    replacement.Beatmap)));
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
        Assert.That(refreshCount, Is.EqualTo(1));
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
}
