using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using Yokko.Core.Beatmaps;
using Yokko.Game.Importing;
using Yokko.Game.Screens.SongSelect;
using Yokko.Import;

namespace Yokko.Game.Tests.Visual;

[TestFixture]
public partial class TestSceneImportedChartSongSelectScreen : YokkoTestScene
{
    private readonly SongSelectScreen songSelectScreen;

    [Resolved]
    private ImportedChartLibrary importedChartLibrary { get; set; }

    public TestSceneImportedChartSongSelectScreen()
    {
        Add(new ScreenStack(songSelectScreen = new SongSelectScreen())
        {
            RelativeSizeAxes = Axes.Both,
        });
    }

    [Test]
    public void TestPackageRefreshesOnceAndSelectsNewestChart()
    {
        const string packagePath = @"C:\Charts\pack.osz";
        ChartImportResult[] package = Enumerable.Range(1, 7)
                                                .Select(index => new ChartImportResult(
                                                    DemoBeatmaps.CreateFourKeyDemo() with
                                                    {
                                                        Title = "GD PACK (clear 2 out of 7 maps)",
                                                        DifficultyName = $"Actual song {index}",
                                                        AudioPath = $@"C:\Audio\song-{index}.ogg",
                                                    },
                                                    []))
                                                .ToArray();

        AddStep("start with empty library", () => importedChartLibrary.Clear());
        AddStep("import seven-chart package", () =>
            importedChartLibrary.AddOrReplace(package, packagePath));
        AddUntilStep("all package charts visible", () =>
            songSelectScreen.VisibleEntryCount == 7);
        AddAssert("newest package chart selected", () =>
            songSelectScreen.SelectedEntry.Beatmap.Title == "Actual song 7");
        AddAssert("package rows use song metadata", () =>
            songSelectScreen.SelectedEntry.Beatmap.Title != "pack");
        AddUntilStep("package rows show song titles", () =>
            songSelectScreen.MaterialisedCompactPrimaryTexts.Contains(
                "Actual song 7"));
        AddStep("collapse package", () => songSelectScreen.TogglePackage(packagePath));
        AddAssert("package is collapsed", () =>
            songSelectScreen.IsPackageCollapsed(packagePath));
        AddAssert("package chart rows hidden", () =>
            songSelectScreen.VisibleRowCount == 0);
        AddStep("expand package", () => songSelectScreen.TogglePackage(packagePath));
        AddAssert("all package chart rows restored", () =>
            songSelectScreen.VisibleRowCount == 7);
    }

    [Test]
    public void TestLargeLibraryDeltaIsAppliedAcrossFrames()
    {
        const int chartCount = 512;
        const string packagePath = @"C:\Charts\large-pack.osz";
        ImportedChart[] charts = Enumerable.Range(1, chartCount)
                                           .Select(index =>
                                           {
                                               var result = new ChartImportResult(
                                                   DemoBeatmaps.CreateFourKeyDemo() with
                                                   {
                                                       Title = $"Large package {index}",
                                                       DifficultyName = $"Chart {index}",
                                                       AudioPath = $@"C:\Audio\large-{index}.ogg",
                                                   },
                                                   []);
                                               return new ImportedChart(
                                                   $"large-{index}",
                                                   packagePath,
                                                   result,
                                                   null,
                                                   default,
                                                   default,
                                                   packagePath,
                                                   "Large package",
                                                   true);
                                           })
                                           .ToArray();

        AddStep("publish large delta", () =>
            songSelectScreen.ApplyChartLibraryChange(
                new ImportedChartLibraryChange(
                    songSelectScreen.LibraryRevision + 1,
                    songSelectScreen.LibraryStructureRevision + 1,
                    ImportedChartLibraryChangeKind.Structure,
                    chartCount,
                    new ImportedChartLibraryDelta(charts, []))));
        AddUntilStep("large delta settled", () =>
            !songSelectScreen.LibraryDeltaApplyInProgress
            && songSelectScreen.VisibleEntryCount == chartCount);
        AddAssert("delta used multiple frames", () =>
            songSelectScreen.LastLibraryDeltaApplyFrameCount > 1);
        AddStep("publish large removal delta", () =>
            songSelectScreen.ApplyChartLibraryChange(
                new ImportedChartLibraryChange(
                    songSelectScreen.LibraryRevision + 1,
                    songSelectScreen.LibraryStructureRevision + 1,
                    ImportedChartLibraryChangeKind.Structure,
                    0,
                    new ImportedChartLibraryDelta(
                        [],
                        charts.Select(chart => chart.Id).ToArray()))));
        AddUntilStep("large removal settled", () =>
            !songSelectScreen.LibraryDeltaApplyInProgress
            && songSelectScreen.VisibleEntryCount == 0);
        AddAssert("removal delta used multiple frames", () =>
            songSelectScreen.LastLibraryDeltaApplyFrameCount > 1);
    }
}
