using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Platform;
using Yokko.Core.Beatmaps;
using Yokko.Game.Importing;
using Yokko.Game.Resources;
using Yokko.Game.Skinning.OsuMania;
using Yokko.Import;
using Yokko.Import.Osu;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class YokkoResourceStorageTest
{
    private string testRoot;

    [SetUp]
    public void SetUp()
    {
        testRoot = Path.Combine(
            Path.GetTempPath(),
            $"yokko-resource-storage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(testRoot))
            Directory.Delete(testRoot, true);
    }

    [Test]
    public async Task MigratesBeatmapsAndSkinsToCustomFolderAndBack()
    {
        string sourceDirectory = Path.Combine(testRoot, "source");
        Directory.CreateDirectory(sourceDirectory);
        string audioPath = Path.Combine(sourceDirectory, "song.mp3");
        string chartPath = Path.Combine(sourceDirectory, "chart.osu");
        File.WriteAllBytes(audioPath, []);
        File.WriteAllText(
            chartPath,
            OsuManiaBeatmapIO.WriteBeatmap(
                DemoBeatmaps.CreateFourKeyDemo() with
                {
                    Title = "Migrated chart",
                    AudioPath = audioPath,
                }));

        string skinSource = Path.Combine(sourceDirectory, "skin");
        Directory.CreateDirectory(skinSource);
        File.WriteAllText(
            Path.Combine(skinSource, "skin.ini"),
            """
            [General]
            Name: Migrated skin

            [Mania]
            Keys: 4
            """);

        var resourceSettings = new YokkoResourceSettings();
        var importSettings = new YokkoImportSettings();
        var chartLibrary = new ImportedChartLibrary();
        var skinLibrary = new OsuManiaSkinLibrary();
        var skinSettings = new YokkoSkinSettings();
        var resources = new YokkoResourceStorage();
        resources.Initialise(
            new NativeStorage(testRoot),
            resourceSettings,
            importSettings,
            chartLibrary,
            skinLibrary,
            skinSettings);

        await chartLibrary.ImportAsync(new ChartImportRequest(chartPath, true));
        SkinImportResult skin = skinLibrary.Import(skinSource);
        Assert.That(skin.Success, Is.True, skin.Message);

        string defaultRoot = resources.RootPath;
        string customRoot = Path.Combine(testRoot, "custom-resources");
        string conflictingSkin = Path.Combine(customRoot, "Skins", "skin");
        Directory.CreateDirectory(conflictingSkin);
        File.WriteAllText(
            Path.Combine(conflictingSkin, "skin.ini"),
            """
            [General]
            Name: Existing skin

            [Mania]
            Keys: 4
            """);
        ResourceMigrationResult migrated = await resources.MigrateAsync(customRoot);

        Assert.Multiple(() =>
        {
            Assert.That(migrated.Success, Is.True, migrated.Message);
            Assert.That(resources.RootPath, Is.EqualTo(Path.GetFullPath(customRoot)));
            Assert.That(resourceSettings.RootPath.Value, Is.EqualTo(Path.GetFullPath(customRoot)));
            Assert.That(chartLibrary.LibraryPath, Is.EqualTo(Path.Combine(customRoot, "Beatmaps")));
            Assert.That(skinLibrary.LibraryPath, Is.EqualTo(Path.Combine(customRoot, "Skins")));
            Assert.That(chartLibrary.GetCharts().Single().Result.Beatmap.Title, Is.EqualTo("Migrated chart"));
            Assert.That(
                File.Exists(chartLibrary.GetCharts().Single().Result.Beatmap.AudioPath),
                Is.True);
            Assert.That(skinLibrary.GetInstalledSkins(), Has.Count.EqualTo(2));
            Assert.That(
                skinLibrary.GetInstalledSkins()
                           .Single(entry => skinLibrary.IsSelected(entry.Id))
                           .Name,
                Is.EqualTo("Migrated skin"));
            Assert.That(Directory.Exists(defaultRoot), Is.False);
        });

        ResourceMigrationResult restored =
            await resources.MigrateToDefaultAsync();

        Assert.Multiple(() =>
        {
            Assert.That(restored.Success, Is.True, restored.Message);
            Assert.That(resources.RootPath, Is.EqualTo(defaultRoot));
            Assert.That(resourceSettings.RootPath.Value, Is.Empty);
            Assert.That(chartLibrary.GetCharts(), Has.Count.EqualTo(1));
            Assert.That(skinLibrary.GetInstalledSkins(), Has.Count.EqualTo(2));
            Assert.That(Directory.Exists(customRoot), Is.False);
        });
    }

    [Test]
    public void CustomRootPathPersistsInConfig()
    {
        string configured = Path.Combine(testRoot, "configured-resources");

        using (var config = new Yokko.Game.Configuration.YokkoConfigManager(
                   new NativeStorage(testRoot)))
        {
            var settings = new YokkoResourceSettings();
            config.BindResourceSettings(settings);
            settings.RootPath.Value = configured;
            config.Save();
        }

        using (var config = new Yokko.Game.Configuration.YokkoConfigManager(
                   new NativeStorage(testRoot)))
        {
            var settings = new YokkoResourceSettings();
            config.BindResourceSettings(settings);
            Assert.That(settings.RootPath.Value, Is.EqualTo(configured));
        }
    }
}
