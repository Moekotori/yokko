using System;
using System.Collections.Generic;
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

            ExternalOsuLibraryResult result =
                await library.RefreshExternalOsuAsync();
            ExternalOsuLibraryResult unchanged =
                await library.RefreshExternalOsuAsync();
            ImportedChart indexedChart = library.GetCharts().Single();
            YokkoBeatmap playableBeatmap =
                library.GetPlayableBeatmap(indexedChart.Id);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.ChartCount, Is.EqualTo(1));
                Assert.That(result.ContentReadCount, Is.EqualTo(2));
                Assert.That(unchanged.ContentReadCount, Is.Zero);
                Assert.That(library.GetCharts(), Has.Count.EqualTo(1));
                Assert.That(
                    library.GetCharts().Single().SourceKind,
                    Is.EqualTo(ImportedChartSourceKind.ExternalOsu));
                Assert.That(library.GetCharts().Single().IsReadOnly, Is.True);
                Assert.That(
                    indexedChart.Result.Beatmap.SourceFormat,
                    Is.EqualTo(ChartSourceFormat.OsuMania));
                Assert.That(indexedChart.Result.Beatmap.HitObjects, Is.Empty,
                    "The persistent index should keep only a lightweight beatmap summary.");
                Assert.That(playableBeatmap.HitObjects, Is.Not.Empty,
                    "Selecting an external chart must materialise its full source beatmap.");
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

    private static string createTestRoot(string name) => Path.Combine(
        Path.GetTempPath(),
        $"yokko-{name}-{Guid.NewGuid():N}");

    private static string writeOsuChart(
        string directory,
        string title,
        int mode)
    {
        string path = Path.Combine(directory, $"{title}.osu");
        string text = OsuManiaBeatmapIO.WriteBeatmap(
            DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title = title,
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
