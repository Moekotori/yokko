using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Platform;
using Yokko.Core.Beatmaps;
using Yokko.Core.Difficulty;
using Yokko.Game.Importing;
using Yokko.Game.Configuration;
using Yokko.Import;
using Yokko.Import.Osu;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class ImportedChartLibraryTest
{
    [Test]
    public async Task ExternalOsuSongsLoadsOnlyManiaWithoutWritingSource()
    {
        string root = createTestRoot("external-osu-readonly");
        string yokkoRoot = Path.Combine(root, "Yokko");
        string songs = Path.Combine(root, "osu!", "Songs");
        string maniaSet = Path.Combine(songs, "100 Artist - Mania Song");
        string standardSet = Path.Combine(songs, "200 Artist - Standard Song");
        Directory.CreateDirectory(maniaSet);
        Directory.CreateDirectory(standardSet);

        try
        {
            string maniaPath = writeOsuChart(maniaSet, "Mania", 3);
            writeOsuChart(standardSet, "Standard", 0);
            File.WriteAllBytes(Path.Combine(maniaSet, "audio.mp3"), [1, 2, 3]);
            string[] originalFiles = snapshotFiles(songs);
            DateTime originalWriteTime = File.GetLastWriteTimeUtc(maniaPath);
            byte[] originalContent = File.ReadAllBytes(maniaPath);

            var settings = new YokkoExternalOsuSettings();
            settings.SongsPath.Value = songs;
            using var library = new ImportedChartLibrary();
            var storage = new NativeStorage(yokkoRoot);
            library.Initialise(storage);
            library.ConfigureExternalOsu(storage, settings);
            int refreshCount = 0;
            library.LibraryChanged += _ => refreshCount++;

            ExternalOsuLibraryResult result =
                await library.RefreshExternalOsuAsync();
            await library.ExternalDifficultyTask;
            ImportedChart firstIndexedChart = library.GetCharts().Single();
            long firstRevision = library.Revision;
            int refreshCountAfterDifficulty = refreshCount;
            ExternalOsuLibraryResult unchanged =
                await library.RefreshExternalOsuAsync();
            ImportedChart indexedChart = library.GetCharts().Single();
            YokkoBeatmap playableBeatmap =
                await library.GetPlayableBeatmapAsync(indexedChart.Id);
            YokkoBeatmap cachedPlayableBeatmap =
                await library.GetPlayableBeatmapAsync(indexedChart.Id);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.ChartCount, Is.EqualTo(1));
                Assert.That(result.ContentReadCount, Is.EqualTo(2));
                Assert.That(unchanged.ContentReadCount, Is.Zero);
                Assert.That(library.Revision, Is.EqualTo(firstRevision),
                    "An unchanged metadata scan must not publish a new library snapshot.");
                Assert.That(refreshCount,
                    Is.EqualTo(refreshCountAfterDifficulty));
                Assert.That(library.GetCharts().Single(),
                    Is.SameAs(firstIndexedChart),
                    "Unchanged charts must retain object identity for song-select caches.");
                Assert.That(library.GetCharts(), Has.Count.EqualTo(1));
                Assert.That(
                    library.GetCharts().Single().SourceKind,
                    Is.EqualTo(ImportedChartSourceKind.ExternalOsu));
                Assert.That(library.GetCharts().Single().IsReadOnly, Is.True);
                Assert.That(
                    indexedChart.Result.Beatmap.SourceFormat,
                    Is.EqualTo(ChartSourceFormat.OsuMania));
                Assert.That(firstIndexedChart.DifficultyRating.IsSuccess,
                    Is.True,
                    "The staged background worker must eventually publish MSD.");
                Assert.That(firstIndexedChart.StarRating.IsSuccess,
                    Is.True,
                    "The staged background worker must eventually publish stars.");
                Assert.That(indexedChart.Result.Beatmap.HitObjects, Is.Empty,
                    "The persistent index should keep only a lightweight beatmap summary.");
                Assert.That(playableBeatmap.HitObjects, Is.Not.Empty,
                    "Selecting an external chart must materialise its full source beatmap.");
                Assert.That(cachedPlayableBeatmap, Is.SameAs(playableBeatmap),
                    "Repeated async materialisation must use the bounded beatmap LRU.");
                Assert.That(snapshotFiles(songs), Is.EqualTo(originalFiles));
                Assert.That(File.ReadAllBytes(maniaPath), Is.EqualTo(originalContent));
                Assert.That(File.GetLastWriteTimeUtc(maniaPath), Is.EqualTo(originalWriteTime));
                Assert.That(
                    Directory.EnumerateFiles(
                        songs,
                        "*yokko*",
                        SearchOption.AllDirectories),
                    Is.Empty);
                Assert.That(
                    Directory.EnumerateFiles(
                        yokkoRoot,
                        "library-index.json",
                        SearchOption.AllDirectories),
                    Is.Not.Empty);
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task ExternalDifficultyCompletionPublishesOnlyFinalSnapshot()
    {
        string root = createTestRoot("external-osu-difficulty-publish");
        string yokkoRoot = Path.Combine(root, "Yokko");
        string songs = Path.Combine(root, "osu!", "Songs");
        const int chartCount = 130;

        try
        {
            for (int index = 0; index < chartCount; index++)
            {
                string set = Path.Combine(songs, $"{index:D3} Publish Test");
                Directory.CreateDirectory(set);
                writeOsuChart(set, $"Chart {index:D3}", 3);
            }

            var settings = new YokkoExternalOsuSettings();
            settings.SongsPath.Value = songs;
            using var library = new ImportedChartLibrary();
            var storage = new NativeStorage(yokkoRoot);
            library.Initialise(storage);
            library.ConfigureExternalOsu(storage, settings);
            var publishedChanges = new List<ImportedChartLibraryChange>();
            library.LibraryChanged += change => publishedChanges.Add(change);

            ExternalOsuLibraryResult result =
                await library.RefreshExternalOsuAsync();
            await library.ExternalDifficultyTask;

            Assert.Multiple(() =>
            {
                Assert.That(result.ChartCount, Is.EqualTo(chartCount));
                Assert.That(library.GetCharts(), Has.Count.EqualTo(chartCount));
                Assert.That(publishedChanges, Has.Count.EqualTo(2),
                    "The initial index and final ratings should each publish once.");
                Assert.That(
                    publishedChanges.Select(change => change.Kind),
                    Is.EqualTo(new[]
                    {
                        ImportedChartLibraryChangeKind.Structure,
                        ImportedChartLibraryChangeKind.DifficultyRatings,
                    }));
                Assert.That(
                    publishedChanges[1].StructureRevision,
                    Is.EqualTo(publishedChanges[0].StructureRevision),
                    "Difficulty completion must not invalidate a prepared song list.");
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task ExternalOsuSetUsesMetadataNameInsteadOfFolderName()
    {
        string root = createTestRoot("external-osu-metadata-name");
        string yokkoRoot = Path.Combine(root, "Yokko");
        string songs = Path.Combine(root, "osu!", "Songs");
        string set = Path.Combine(songs, "999 Renamed Folder");
        Directory.CreateDirectory(set);

        try
        {
            writeOsuChart(set, "Metadata Song", 3, "Easy", "easy");
            writeOsuChart(set, "Metadata Song", 3, "Hard", "hard");

            var settings = new YokkoExternalOsuSettings();
            settings.SongsPath.Value = songs;
            using var library = new ImportedChartLibrary();
            var storage = new NativeStorage(yokkoRoot);
            library.Initialise(storage);
            library.ConfigureExternalOsu(storage, settings);

            await library.RefreshExternalOsuAsync();
            ImportedChart[] charts = library.GetCharts().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(charts, Has.Length.EqualTo(2));
                Assert.That(
                    charts.Select(chart => chart.PackageId).Distinct().Count(),
                    Is.EqualTo(1));
                Assert.That(charts.Select(chart => chart.PackageName),
                    Is.All.EqualTo("Yokko - Metadata Song"));
                Assert.That(charts.Select(chart => chart.IsPackage), Is.All.True);
                Assert.That(charts.Select(chart => chart.Result.Beatmap.DifficultyName),
                    Is.EqualTo(new[] { "Easy", "Hard" }));
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task ExternalOsuIndexingPausesAndResumesForGameplay()
    {
        string root = createTestRoot("external-osu-pause");
        string songs = Path.Combine(root, "osu!", "Songs");
        string set = Path.Combine(songs, "100 Pause Test");
        string yokkoRoot = Path.Combine(root, "Yokko");
        Directory.CreateDirectory(set);

        try
        {
            writeOsuChart(set, "Paused", 3);
            var settings = new YokkoExternalOsuSettings();
            settings.SongsPath.Value = songs;
            using var library = new ImportedChartLibrary();
            var storage = new NativeStorage(yokkoRoot);
            library.Initialise(storage);
            library.ConfigureExternalOsu(storage, settings);
            library.SetExternalIndexingPaused(true);

            Task<ExternalOsuLibraryResult> refresh =
                library.RefreshExternalOsuAsync();
            await Task.Delay(50);
            Assert.That(refresh.IsCompleted, Is.False,
                "Gameplay must keep background parsing and difficulty work paused.");

            library.SetExternalIndexingPaused(false);
            ExternalOsuLibraryResult result = await refresh;
            Assert.That(result.ChartCount, Is.EqualTo(1));
            await library.ExternalDifficultyTask;
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task NewExternalOsuRefreshSupersedesPausedRefresh()
    {
        string root = createTestRoot("external-osu-refresh-supersede");
        string songs = Path.Combine(root, "osu!", "Songs");
        string set = Path.Combine(songs, "100 Supersede Test");
        string yokkoRoot = Path.Combine(root, "Yokko");
        Directory.CreateDirectory(set);

        try
        {
            writeOsuChart(set, "Superseded", 3);
            var settings = new YokkoExternalOsuSettings();
            settings.SongsPath.Value = songs;
            using var library = new ImportedChartLibrary();
            var storage = new NativeStorage(yokkoRoot);
            library.Initialise(storage);
            library.ConfigureExternalOsu(storage, settings);
            library.SetExternalIndexingPaused(true);

            Task<ExternalOsuLibraryResult> first =
                library.RefreshExternalOsuAsync();
            await Task.Delay(50);
            Task<ExternalOsuLibraryResult> latest =
                library.RefreshExternalOsuAsync();

            ExternalOsuLibraryResult superseded = await first;
            Assert.That(
                superseded.Message,
                Does.Contain("superseded").IgnoreCase);

            library.SetExternalIndexingPaused(false);
            ExternalOsuLibraryResult result = await latest;
            Assert.That(result.ChartCount, Is.EqualTo(1));
            await library.ExternalDifficultyTask;
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task DisablingExternalOsuWhileIndexingPreventsLateLoad()
    {
        string root = createTestRoot("external-osu-disable-indexing");
        string songs = Path.Combine(root, "osu!", "Songs");
        string set = Path.Combine(songs, "100 Disable Test");
        string yokkoRoot = Path.Combine(root, "Yokko");
        Directory.CreateDirectory(set);

        try
        {
            writeOsuChart(set, "Disabled", 3);
            var settings = new YokkoExternalOsuSettings();
            settings.SongsPath.Value = songs;
            using var library = new ImportedChartLibrary();
            var storage = new NativeStorage(yokkoRoot);
            library.Initialise(storage);
            library.ConfigureExternalOsu(storage, settings);
            library.SetExternalIndexingPaused(true);

            Task<ExternalOsuLibraryResult> refresh =
                library.RefreshExternalOsuAsync();
            await Task.Delay(50);
            library.DisableExternalOsu();
            library.SetExternalIndexingPaused(false);

            ExternalOsuLibraryResult result = await refresh;
            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(settings.SongsPath.Value, Is.Empty);
                Assert.That(library.ExternalOsuChartCount, Is.Zero);
                Assert.That(library.GetCharts(), Is.Empty);
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task ExternalOsuIndexRestoresChartsWhenSongsIsTemporarilyUnavailable()
    {
        string root = createTestRoot("external-osu-restore");
        string yokkoRoot = Path.Combine(root, "Yokko");
        string songs = Path.Combine(root, "osu!", "Songs");
        string set = Path.Combine(songs, "300 Artist - Persistent Song");
        Directory.CreateDirectory(set);

        try
        {
            writeOsuChart(set, "Persistent", 3);
            var settings = new YokkoExternalOsuSettings();
            settings.SongsPath.Value = songs;
            var storage = new NativeStorage(yokkoRoot);

            using (var first = new ImportedChartLibrary())
            {
                first.Initialise(storage);
                first.ConfigureExternalOsu(storage, settings);
                Assert.That(
                    (await first.RefreshExternalOsuAsync()).ChartCount,
                    Is.EqualTo(1));
                await first.ExternalDifficultyTask;
            }

            string disconnected = Path.Combine(root, "disconnected-Songs");
            Directory.Move(songs, disconnected);

            using var restored = new ImportedChartLibrary();
            restored.Initialise(storage);
            restored.ConfigureExternalOsu(storage, settings);
            int count = await restored.BeginStartupLoad(true, true);

            Assert.Multiple(() =>
            {
                Assert.That(count, Is.EqualTo(1));
                Assert.That(restored.ExternalOsuChartCount, Is.EqualTo(1));
                Assert.That(
                    restored.GetCharts().Single().Result.Beatmap.Title,
                    Is.EqualTo("Persistent"));
                Assert.That(settings.SongsPath.Value, Is.EqualTo(songs));
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task ExternalOsuRefreshProcessesOnlyChangedFiles()
    {
        string root = createTestRoot("external-osu-incremental");
        string yokkoRoot = Path.Combine(root, "Yokko");
        string songs = Path.Combine(root, "osu!", "Songs");
        string set = Path.Combine(songs, "400 Artist - Incremental Song");
        Directory.CreateDirectory(set);

        try
        {
            string firstPath = writeOsuChart(set, "First", 3);
            var settings = new YokkoExternalOsuSettings();
            settings.SongsPath.Value = songs;
            using var library = new ImportedChartLibrary();
            var storage = new NativeStorage(yokkoRoot);
            library.Initialise(storage);
            library.ConfigureExternalOsu(storage, settings);

            Assert.That(
                (await library.RefreshExternalOsuAsync()).ChartCount,
                Is.EqualTo(1));

            writeOsuChart(set, "Second", 3);
            writeOsuChart(set, "Ignored", 2);
            Assert.That(
                (await library.RefreshExternalOsuAsync()).ChartCount,
                Is.EqualTo(2));

            string firstText = File.ReadAllText(firstPath)
                                   .Replace("Mode: 3", "Mode: 0");
            File.WriteAllText(firstPath, firstText);
            File.SetLastWriteTimeUtc(
                firstPath,
                DateTime.UtcNow.AddSeconds(2));

            Assert.Multiple(() =>
            {
                Assert.That(
                    library.RefreshExternalOsuAsync().GetAwaiter().GetResult()
                           .ChartCount,
                    Is.EqualTo(1));
                Assert.That(
                    library.GetCharts().Single().Result.Beatmap.Title,
                    Is.EqualTo("Second"));
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public void ExternalOsuSongsPathPersistsInYokkoConfig()
    {
        string root = createTestRoot("external-osu-config");
        string songs = Path.Combine(root, "osu!", "Songs");
        Directory.CreateDirectory(songs);

        try
        {
            using (var firstConfig = new YokkoConfigManager(
                       new NativeStorage(root)))
            {
                var firstSettings = new YokkoExternalOsuSettings();
                firstConfig.BindExternalOsuSettings(firstSettings);
                firstSettings.SongsPath.Value = songs;
                firstConfig.Save();
            }

            using var restoredConfig = new YokkoConfigManager(
                new NativeStorage(root));
            var restoredSettings = new YokkoExternalOsuSettings();
            restoredConfig.BindExternalOsuSettings(restoredSettings);

            Assert.That(restoredSettings.SongsPath.Value, Is.EqualTo(songs));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task ExternalOsuRealCorpusReadOnlySmokeTest()
    {
        string songs = Environment.GetEnvironmentVariable(
            "YOKKO_TEST_EXTERNAL_OSU_SONGS");
        if (string.IsNullOrWhiteSpace(songs))
            Assert.Ignore("Set YOKKO_TEST_EXTERNAL_OSU_SONGS to run this read-only corpus test.");

        string yokkoRoot = createTestRoot("external-osu-real-corpus-cache");
        try
        {
            Dictionary<string, string> before = snapshotOsuHashes(songs);
            var settings = new YokkoExternalOsuSettings();
            settings.SongsPath.Value = songs;
            using var library = new ImportedChartLibrary();
            var storage = new NativeStorage(yokkoRoot);
            library.Initialise(storage);
            library.ConfigureExternalOsu(storage, settings);

            ExternalOsuLibraryResult first =
                await library.RefreshExternalOsuAsync();
            ExternalOsuLibraryResult unchanged =
                await library.RefreshExternalOsuAsync();
            Dictionary<string, string> after = snapshotOsuHashes(songs);

            Assert.Multiple(() =>
            {
                Assert.That(first.Success, Is.True);
                Assert.That(first.ChartCount, Is.GreaterThan(0));
                Assert.That(
                    library.GetCharts().Where(chart => chart.IsReadOnly)
                           .Select(chart => chart.Result.Beatmap.SourceFormat),
                    Is.All.EqualTo(ChartSourceFormat.OsuMania));
                Assert.That(unchanged.ContentReadCount, Is.Zero);
                Assert.That(after, Is.EqualTo(before));
                Assert.That(
                    Path.GetFullPath(yokkoRoot).StartsWith(
                        Path.GetFullPath(songs),
                        StringComparison.OrdinalIgnoreCase),
                    Is.False);
            });
        }
        finally
        {
            if (Directory.Exists(yokkoRoot))
                Directory.Delete(yokkoRoot, true);
        }
    }

    [Test]
    public async Task ExternalOsuTenThousandFileIncrementalScaleTest()
    {
        if (Environment.GetEnvironmentVariable("YOKKO_TEST_EXTERNAL_OSU_10K")
            != "1")
        {
            Assert.Ignore(
                "Set YOKKO_TEST_EXTERNAL_OSU_10K=1 to run the 10,000-file scale test.");
        }

        string root = createTestRoot("external-osu-10k");
        string songs = Path.Combine(root, "osu!", "Songs");
        string yokkoRoot = Path.Combine(root, "Yokko");
        const string standardHeader = """
            osu file format v14

            [General]
            AudioFilename: audio.mp3
            Mode: 0

            [Metadata]
            Title:Scale Probe
            Artist:Yokko
            Creator:Test
            Version:Standard
            """;

        try
        {
            for (int setIndex = 0; setIndex < 100; setIndex++)
            {
                string set = Path.Combine(
                    songs,
                    $"{setIndex:D5} Scale Set");
                Directory.CreateDirectory(set);
                for (int chartIndex = 0; chartIndex < 100; chartIndex++)
                {
                    File.WriteAllText(
                        Path.Combine(set, $"chart-{chartIndex:D3}.osu"),
                        standardHeader,
                        Encoding.UTF8);
                }
            }

            var settings = new YokkoExternalOsuSettings();
            settings.SongsPath.Value = songs;
            using var library = new ImportedChartLibrary();
            var storage = new NativeStorage(yokkoRoot);
            library.Initialise(storage);
            library.ConfigureExternalOsu(storage, settings);

            ExternalOsuLibraryResult first =
                await library.RefreshExternalOsuAsync();
            ExternalOsuLibraryResult unchanged =
                await library.RefreshExternalOsuAsync();

            Assert.Multiple(() =>
            {
                Assert.That(first.Success, Is.True);
                Assert.That(first.ScannedFileCount, Is.EqualTo(10_000));
                Assert.That(first.ContentReadCount, Is.EqualTo(10_000));
                Assert.That(first.ChartCount, Is.Zero,
                    "Non-mania files must stay out of the library.");
                Assert.That(unchanged.ScannedFileCount, Is.EqualTo(10_000));
                Assert.That(unchanged.ContentReadCount, Is.Zero,
                    "An unchanged large library must use only its metadata index.");
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task StartupLoadIsSharedAndCompletesBeforeConsumersProceed()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"yokko-startup-library-{Guid.NewGuid():N}");

        try
        {
            var library = new ImportedChartLibrary();
            library.Initialise(new NativeStorage(root));

            Task<int> first = library.BeginStartupLoad(true, true);
            Task<int> second = library.BeginStartupLoad(false, false);

            Assert.Multiple(() =>
            {
                Assert.That(second, Is.SameAs(first));
                Assert.That(library.StartupLoadTask, Is.SameAs(first));
            });
            Assert.That(await first, Is.Zero);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

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
    public async Task RemovingManagedChartDeletesOnlyItsOwnedImportDirectory()
    {
        string root = createTestRoot("managed-remove");
        string libraryRoot = Path.Combine(root, "Beatmaps");
        string removedDirectory = Path.Combine(libraryRoot, "first-import");
        string retainedDirectory = Path.Combine(libraryRoot, "second-import");
        string removedSource = Path.Combine(removedDirectory, "pack.osz");
        string retainedSource = Path.Combine(retainedDirectory, "chart.osu");
        Directory.CreateDirectory(removedDirectory);
        Directory.CreateDirectory(retainedDirectory);
        File.WriteAllText(removedSource, "package");
        File.WriteAllText(Path.Combine(removedDirectory, "audio.ogg"), "audio");
        File.WriteAllText(retainedSource, "chart");

        try
        {
            using var library = new ImportedChartLibrary();
            library.Initialise(libraryRoot);
            library.AddOrReplace(
                [
                    new ChartImportResult(DemoBeatmaps.CreateFourKeyDemo(), []),
                    new ChartImportResult(DemoBeatmaps.CreateSevenKeyDemo(), []),
                ],
                removedSource);
            library.AddOrReplace(
                new ChartImportResult(
                    DemoBeatmaps.CreateFourKeyDemo() with { Title = "Retained" },
                    []),
                retainedSource);
            int refreshCount = 0;
            library.LibraryChanged += _ => refreshCount++;

            string removedId = library.GetCharts()
                                      .First(chart => chart.SourcePath == removedSource)
                                      .Id;
            ManagedChartRemovalResult result =
                await library.RemoveManagedChartAsync(removedId);

            Assert.Multiple(() =>
            {
                Assert.That(result.RemovedChartCount, Is.EqualTo(2));
                Assert.That(Directory.Exists(removedDirectory), Is.False);
                Assert.That(Directory.Exists(retainedDirectory), Is.True);
                Assert.That(File.Exists(retainedSource), Is.True);
                Assert.That(library.GetCharts(), Has.Count.EqualTo(1));
                Assert.That(
                    library.GetCharts().Single().Result.Beatmap.Title,
                    Is.EqualTo("Retained"));
                Assert.That(refreshCount, Is.EqualTo(1));
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task RemovingExternalOsuChartIsRejectedWithoutTouchingSource()
    {
        string root = createTestRoot("external-remove-rejected");
        string yokkoRoot = Path.Combine(root, "Yokko");
        string songs = Path.Combine(root, "osu!", "Songs");
        string setDirectory = Path.Combine(songs, "100 Artist - Song");
        Directory.CreateDirectory(setDirectory);

        try
        {
            string sourcePath = writeOsuChart(setDirectory, "External", 3);
            var settings = new YokkoExternalOsuSettings();
            settings.SongsPath.Value = songs;
            using var library = new ImportedChartLibrary();
            var storage = new NativeStorage(yokkoRoot);
            library.Initialise(storage);
            library.ConfigureExternalOsu(storage, settings);
            await library.RefreshExternalOsuAsync();
            ImportedChart external = library.GetCharts().Single();

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await library.RemoveManagedChartAsync(external.Id));
            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(sourcePath), Is.True);
                Assert.That(library.GetCharts(), Has.Count.EqualTo(1));
                Assert.That(library.GetCharts().Single().IsReadOnly, Is.True);
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public void PackageAddsAllChartsAndRefreshesOnce()
    {
        var library = new ImportedChartLibrary();
        int refreshCount = 0;
        library.LibraryChanged += _ => refreshCount++;

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
    public void PackagePreservesOsuTitleAndVersionMetadata()
    {
        var library = new ImportedChartLibrary();
        YokkoBeatmap source = DemoBeatmaps.CreateFourKeyDemo() with
        {
            Title = "GD PACK (clear 2 out of 7 maps)",
            Artist = "Various Artists",
            SourceFormat = ChartSourceFormat.OsuMania,
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
                Is.All.EqualTo("GD PACK (clear 2 out of 7 maps)"));
            Assert.That(
                charts.Select(chart => chart.Result.Beatmap.DifficultyName),
                Is.EqualTo(new[] { "Cold Sweat", "Dear Nostalgists" }));
            Assert.That(charts.Select(chart => chart.PackageName),
                Is.All.EqualTo("Various Artists - GD PACK (clear 2 out of 7 maps)"));
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
    public async Task ManagedWarmStartUsesPersistedIndexWithoutParsingSources()
    {
        string root = createTestRoot("managed-warm-start");
        try
        {
            using var first = new ImportedChartLibrary();
            first.Initialise(new NativeStorage(root));
            string firstDirectory = Path.Combine(first.LibraryPath, "First");
            string secondDirectory = Path.Combine(first.LibraryPath, "Second");
            Directory.CreateDirectory(firstDirectory);
            Directory.CreateDirectory(secondDirectory);
            writeOsuChart(firstDirectory, "First", 3);
            writeOsuChart(secondDirectory, "Second", 3);

            Assert.That(await first.LoadFromDiskAsync(true, true), Is.EqualTo(2));
            Assert.That(first.LastManagedContentReadCount, Is.EqualTo(2));
            string[] expectedIds = first.GetCharts()
                .Select(chart => chart.Id)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            using var reloaded = new ImportedChartLibrary();
            reloaded.Initialise(new NativeStorage(root));
            Assert.That(await reloaded.BeginStartupLoad(true, true), Is.EqualTo(2));

            Assert.Multiple(() =>
            {
                Assert.That(reloaded.LastManagedScannedFileCount, Is.EqualTo(2));
                Assert.That(reloaded.LastManagedCacheHitCount, Is.EqualTo(2));
                Assert.That(reloaded.LastManagedContentReadCount, Is.Zero);
                Assert.That(
                    reloaded.GetCharts().Select(chart => chart.Id)
                        .Order(StringComparer.OrdinalIgnoreCase),
                    Is.EqualTo(expectedIds));
                Assert.That(
                    reloaded.GetCharts().Select(chart => chart.Result.Beatmap.Title),
                    Is.EquivalentTo(new[] { "First", "Second" }));
            });

            YokkoBeatmap playable = await reloaded.GetPlayableBeatmapAsync(
                reloaded.GetCharts().Single(chart =>
                    chart.Result.Beatmap.Title == "Second").Id);
            Assert.That(playable.HitObjects, Is.Not.Empty);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task ManagedIncrementalLoadParsesOnlyAddedAndChangedSources()
    {
        string root = createTestRoot("managed-incremental");
        try
        {
            using var first = new ImportedChartLibrary();
            first.Initialise(new NativeStorage(root));
            string unchangedDirectory = Path.Combine(first.LibraryPath, "Unchanged");
            string changedDirectory = Path.Combine(first.LibraryPath, "Changed");
            string removedDirectory = Path.Combine(first.LibraryPath, "Removed");
            Directory.CreateDirectory(unchangedDirectory);
            Directory.CreateDirectory(changedDirectory);
            Directory.CreateDirectory(removedDirectory);
            string unchanged = writeOsuChart(
                unchangedDirectory,
                "Unchanged",
                3);
            string changed = writeOsuChart(
                changedDirectory,
                "Changed",
                3);
            string removed = writeOsuChart(
                removedDirectory,
                "Removed",
                3);
            await first.LoadFromDiskAsync(true, true);
            string unchangedId = first.GetCharts().Single(chart =>
                chart.SourcePath == unchanged).Id;

            File.Delete(removed);
            YokkoBeatmap changedBeatmap =
                OsuManiaBeatmapIO.ReadBeatmapFromFile(changed) with
                {
                    Title = "Changed v2",
                };
            File.WriteAllText(changed, OsuManiaBeatmapIO.WriteBeatmap(changedBeatmap));
            File.SetLastWriteTimeUtc(changed, DateTime.UtcNow.AddSeconds(2));
            string addedDirectory = Path.Combine(first.LibraryPath, "Added");
            Directory.CreateDirectory(addedDirectory);
            writeOsuChart(addedDirectory, "Added", 3);

            using var reloaded = new ImportedChartLibrary();
            reloaded.Initialise(new NativeStorage(root));
            Assert.That(await reloaded.LoadFromDiskAsync(true, true), Is.EqualTo(3));

            Assert.Multiple(() =>
            {
                Assert.That(reloaded.LastManagedScannedFileCount, Is.EqualTo(3));
                Assert.That(reloaded.LastManagedCacheHitCount, Is.EqualTo(1));
                Assert.That(reloaded.LastManagedContentReadCount, Is.EqualTo(2));
                Assert.That(
                    reloaded.GetCharts().Select(chart => chart.Result.Beatmap.Title),
                    Is.EquivalentTo(new[] { "Unchanged", "Changed v2", "Added" }));
                Assert.That(
                    reloaded.GetCharts().Single(chart =>
                        chart.Result.Beatmap.Title == "Unchanged").Id,
                    Is.EqualTo(unchangedId));
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task CorruptManagedIndexFallsBackToSourceParsing()
    {
        string root = createTestRoot("managed-corrupt-index");
        try
        {
            using var first = new ImportedChartLibrary();
            first.Initialise(new NativeStorage(root));
            string chartDirectory = Path.Combine(first.LibraryPath, "Chart");
            Directory.CreateDirectory(chartDirectory);
            writeOsuChart(chartDirectory, "Chart", 3);
            await first.LoadFromDiskAsync(true, true);
            File.WriteAllText(
                Path.Combine(
                    first.LibraryPath,
                    ".yokko-cache",
                    ManagedChartLibraryIndex.FileName),
                "{ truncated");

            using var reloaded = new ImportedChartLibrary();
            reloaded.Initialise(new NativeStorage(root));
            Assert.That(await reloaded.LoadFromDiskAsync(true, true), Is.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(reloaded.LastManagedCacheHitCount, Is.Zero);
                Assert.That(reloaded.LastManagedContentReadCount, Is.EqualTo(1));
                Assert.That(reloaded.GetCharts().Single().Result.Beatmap.Title,
                    Is.EqualTo("Chart"));
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task ManagedImportPreferenceChangeInvalidatesIndex()
    {
        string root = createTestRoot("managed-preference-change");
        try
        {
            using var first = new ImportedChartLibrary();
            first.Initialise(new NativeStorage(root));
            string chartDirectory = Path.Combine(first.LibraryPath, "Chart");
            Directory.CreateDirectory(chartDirectory);
            writeOsuChart(chartDirectory, "Chart", 3);
            await first.LoadFromDiskAsync(true, true);

            using var reloaded = new ImportedChartLibrary();
            reloaded.Initialise(new NativeStorage(root));
            Assert.That(await reloaded.LoadFromDiskAsync(false, true), Is.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(reloaded.LastManagedCacheHitCount, Is.Zero);
                Assert.That(reloaded.LastManagedContentReadCount, Is.EqualTo(1));
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task ManagedDependencyChangeInvalidatesOnlyItsSource()
    {
        string root = createTestRoot("managed-dependency-change");
        try
        {
            using var first = new ImportedChartLibrary();
            first.Initialise(new NativeStorage(root));
            string firstDirectory = Path.Combine(first.LibraryPath, "First");
            string secondDirectory = Path.Combine(first.LibraryPath, "Second");
            Directory.CreateDirectory(firstDirectory);
            Directory.CreateDirectory(secondDirectory);
            string firstChart = writeOsuChart(firstDirectory, "First", 3);
            writeOsuChart(secondDirectory, "Second", 3);
            string artwork = Path.Combine(firstDirectory, "background.jpg");
            File.WriteAllBytes(artwork, [1, 2, 3]);
            File.WriteAllText(
                firstChart,
                File.ReadAllText(firstChart).Replace(
                    "//Background and Video events",
                    "//Background and Video events"
                    + Environment.NewLine
                    + "0,0,\"background.jpg\",0,0"));
            await first.LoadFromDiskAsync(true, true);

            File.WriteAllBytes(artwork, [1, 2, 3, 4]);
            File.SetLastWriteTimeUtc(artwork, DateTime.UtcNow.AddSeconds(2));

            using var reloaded = new ImportedChartLibrary();
            reloaded.Initialise(new NativeStorage(root));
            Assert.That(await reloaded.LoadFromDiskAsync(true, true), Is.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(reloaded.LastManagedCacheHitCount, Is.EqualTo(1));
                Assert.That(reloaded.LastManagedContentReadCount, Is.EqualTo(1));
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task ManagedWarmStartScalesToConfiguredLibrary()
    {
        if (!int.TryParse(
                Environment.GetEnvironmentVariable("YOKKO_TEST_MANAGED_LIBRARY_COUNT"),
                out int sourceCount)
            || sourceCount <= 0)
        {
            Assert.Ignore(
                "Set YOKKO_TEST_MANAGED_LIBRARY_COUNT to run the large managed-library benchmark.");
        }

        string root = createTestRoot("managed-large-library");
        try
        {
            using var first = new ImportedChartLibrary();
            first.Initialise(new NativeStorage(root));
            for (int i = 0; i < sourceCount; i++)
            {
                string directory = Path.Combine(first.LibraryPath, $"Set-{i:D5}");
                Directory.CreateDirectory(directory);
                writeOsuChart(directory, $"Chart {i:D5}", 3);
            }

            var cold = Stopwatch.StartNew();
            Assert.That(
                await first.LoadFromDiskAsync(true, true),
                Is.EqualTo(sourceCount));
            cold.Stop();

            using var reloaded = new ImportedChartLibrary();
            reloaded.Initialise(new NativeStorage(root));
            var warm = Stopwatch.StartNew();
            Assert.That(
                await reloaded.LoadFromDiskAsync(true, true),
                Is.EqualTo(sourceCount));
            warm.Stop();
            TestContext.Progress.WriteLine(
                $"Managed library {sourceCount}: cold={cold.Elapsed.TotalMilliseconds:0} ms, "
                + $"warm={warm.Elapsed.TotalMilliseconds:0} ms.");

            Assert.Multiple(() =>
            {
                Assert.That(reloaded.LastManagedScannedFileCount, Is.EqualTo(sourceCount));
                Assert.That(reloaded.LastManagedCacheHitCount, Is.EqualTo(sourceCount));
                Assert.That(reloaded.LastManagedContentReadCount, Is.Zero);
                Assert.That(warm.Elapsed, Is.LessThan(cold.Elapsed));
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task ImportFolderRecursivelyImportsSupportedChartFiles()
    {
        string root = createTestRoot("folder-import");
        string source = Path.Combine(root, "source-pack");
        string firstSet = Path.Combine(source, "First set");
        string secondSet = Path.Combine(source, "Nested", "Second set");
        Directory.CreateDirectory(firstSet);
        Directory.CreateDirectory(secondSet);

        try
        {
            writeOsuChart(firstSet, "First", 3);
            writeOsuChart(secondSet, "Second", 3);
            File.WriteAllBytes(Path.Combine(firstSet, "audio.mp3"), []);
            File.WriteAllBytes(Path.Combine(secondSet, "audio.mp3"), []);
            File.WriteAllText(Path.Combine(source, "notes.txt"), "not a chart");

            using var library = new ImportedChartLibrary();
            library.Initialise(new NativeStorage(Path.Combine(root, "Yokko")));
            int refreshCount = 0;
            library.LibraryChanged += _ => refreshCount++;

            FolderChartImportResult result = await library.ImportFolderAsync(
                source,
                true,
                true);

            Assert.Multiple(() =>
            {
                Assert.That(result.SourceFileCount, Is.EqualTo(2));
                Assert.That(result.ImportedChartCount, Is.EqualTo(2));
                Assert.That(result.FailedFileCount, Is.Zero);
                Assert.That(refreshCount, Is.EqualTo(1));
                Assert.That(library.GetCharts(), Has.Count.EqualTo(2));
                Assert.That(
                    library.GetCharts().Select(chart => chart.Result.Beatmap.Title),
                    Is.EquivalentTo(new[] { "First", "Second" }));
                Assert.That(
                    library.GetCharts().Select(chart => chart.SourcePath),
                    Is.All.StartsWith(library.LibraryPath));
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task ImportFolderPublishesOnceAndIsolatesInvalidFiles()
    {
        string root = createTestRoot("folder-import-failure");
        string source = Path.Combine(root, "source-pack");
        string validSet = Path.Combine(source, "Valid set");
        Directory.CreateDirectory(validSet);

        try
        {
            writeOsuChart(validSet, "Valid", 3);
            File.WriteAllBytes(Path.Combine(validSet, "audio.mp3"), []);
            string invalid = Path.Combine(source, "invalid.osu");
            using (FileStream stream = File.Create(invalid))
                stream.SetLength(OsuManiaBeatmapIO.MaximumFileBytes + 1);

            using var library = new ImportedChartLibrary();
            library.Initialise(new NativeStorage(Path.Combine(root, "Yokko")));
            int refreshCount = 0;
            library.LibraryChanged += _ => refreshCount++;

            FolderChartImportResult result = await library.ImportFolderAsync(
                source,
                true,
                true);

            Assert.Multiple(() =>
            {
                Assert.That(result.SourceFileCount, Is.EqualTo(2));
                Assert.That(result.ImportedChartCount, Is.EqualTo(1));
                Assert.That(result.FailedFileCount, Is.EqualTo(1));
                Assert.That(refreshCount, Is.EqualTo(1));
                Assert.That(library.GetCharts(), Has.Count.EqualTo(1));
                Assert.That(
                    library.GetCharts().Single().Result.Beatmap.Title,
                    Is.EqualTo("Valid"));
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task ImportConfiguredChartFolderCorpusDoesNotRejectAnyFiles()
    {
        string source = Environment.GetEnvironmentVariable(
            "YOKKO_TEST_CHART_FOLDER_CORPUS");
        if (string.IsNullOrWhiteSpace(source) || !Directory.Exists(source))
        {
            Assert.Ignore(
                "Set YOKKO_TEST_CHART_FOLDER_CORPUS to run this real-folder import test.");
        }

        string root = createTestRoot("folder-import-corpus");
        try
        {
            using var library = new ImportedChartLibrary();
            library.Initialise(new NativeStorage(root));

            FolderChartImportResult result = await library.ImportFolderAsync(
                source,
                true,
                true);
            TestContext.Progress.WriteLine(
                $"Imported {result.ImportedChartCount} charts from "
                + $"{result.SourceFileCount} files; "
                + $"{result.FailedFileCount} failed.");

            Assert.Multiple(() =>
            {
                Assert.That(result.SourceFileCount, Is.GreaterThan(0));
                Assert.That(result.ImportedChartCount, Is.GreaterThan(0));
                Assert.That(result.FailedFileCount, Is.Zero);
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task ImportConfiguredChartFileCorpusDoesNotCrash()
    {
        string source = Environment.GetEnvironmentVariable(
            "YOKKO_TEST_CHART_FILE_CORPUS");
        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
        {
            Assert.Ignore(
                "Set YOKKO_TEST_CHART_FILE_CORPUS to run this real-file import test.");
        }

        string root = createTestRoot("file-import-corpus");
        try
        {
            using var library = new ImportedChartLibrary();
            library.Initialise(new NativeStorage(root));

            IReadOnlyList<ChartImportResult> results = await library.ImportAsync(
                new ChartImportRequest(source, true, true));

            Assert.Multiple(() =>
            {
                Assert.That(results, Is.Not.Empty);
                Assert.That(library.GetCharts(), Has.Count.EqualTo(results.Count));
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task BmsScheduledSamplesReloadFromResourceDirectory()
    {
        string root = createTestRoot("bms-scheduled-library");
        string source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);

        try
        {
            string chartPath = Path.Combine(source, "scheduled.bms");
            File.WriteAllText(chartPath, """
#TITLE Scheduled BMS
#BPM 120
#WAV01 bg-1.wav
#WAV02 bg-2.wav
#00001:0102
#00111:01
""", Encoding.ASCII);
            File.WriteAllBytes(Path.Combine(source, "bg-1.wav"), []);
            File.WriteAllBytes(Path.Combine(source, "bg-2.wav"), []);

            using var first = new ImportedChartLibrary();
            first.Initialise(new NativeStorage(root));
            IReadOnlyList<ChartImportResult> results =
                await first.ImportAsync(new ChartImportRequest(chartPath, true));

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Beatmap.ScheduledSamples, Has.Count.EqualTo(2));
            Assert.That(
                results[0].Beatmap.ScheduledSamples.Select(sample => sample.Path),
                Is.All.Matches<string>(path =>
                    path.StartsWith(first.LibraryPath, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(path)));

            Directory.Delete(source, true);

            using var reloaded = new ImportedChartLibrary();
            reloaded.Initialise(new NativeStorage(root));
            int count = await reloaded.LoadFromDiskAsync(true, true);
            ImportedChart reloadedChart = reloaded.GetCharts().Single();
            YokkoBeatmap beatmap = await reloaded.GetPlayableBeatmapAsync(
                reloadedChart.Id);

            Assert.Multiple(() =>
            {
                Assert.That(count, Is.EqualTo(1));
                Assert.That(beatmap.ScheduledSamples, Has.Count.EqualTo(2));
                Assert.That(
                    beatmap.ScheduledSamples.Select(sample => sample.Path),
                    Is.All.Matches<string>(File.Exists));
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
            YokkoBeatmap[] playable = await Task.WhenAll(
                reloaded.GetCharts().Select(chart =>
                    reloaded.GetPlayableBeatmapAsync(chart.Id)));
            Assert.That(
                playable.Select(beatmap => beatmap.DifficultyName),
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

    private static string createTestRoot(string name)
    {
        string artifactsRoot = Environment.GetEnvironmentVariable(
            "YOKKO_TEST_ARTIFACTS");
        if (string.IsNullOrWhiteSpace(artifactsRoot))
            artifactsRoot = Path.GetTempPath();

        Directory.CreateDirectory(artifactsRoot);
        return Path.Combine(
            artifactsRoot,
            $"yokko-{name}-{Guid.NewGuid():N}");
    }

    private static string writeOsuChart(
        string directory,
        string title,
        int mode,
        string difficultyName = null,
        string fileName = null)
    {
        string path = Path.Combine(directory, $"{fileName ?? title}.osu");
        string text = OsuManiaBeatmapIO.WriteBeatmap(
            DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title = title,
                DifficultyName = difficultyName
                                 ?? DemoBeatmaps.CreateFourKeyDemo().DifficultyName,
                AudioPath = Path.Combine(directory, "audio.mp3"),
            });
        if (mode != 3)
            text = text.Replace("Mode: 3", $"Mode: {mode}");
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return path;
    }

    private static string[] snapshotFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                 .Select(path => Path.GetRelativePath(root, path))
                 .Order(StringComparer.OrdinalIgnoreCase)
                 .ToArray();

    private static Dictionary<string, string> snapshotOsuHashes(string root) =>
        Directory.EnumerateFiles(root, "*.osu", SearchOption.AllDirectories)
                 .ToDictionary(
                     path => Path.GetRelativePath(root, path),
                     path => Convert.ToHexString(
                         SHA256.HashData(File.ReadAllBytes(path))),
                     StringComparer.OrdinalIgnoreCase);

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
