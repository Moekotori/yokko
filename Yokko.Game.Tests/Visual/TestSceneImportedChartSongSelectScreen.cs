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
                                                        Title = $"Actual song {index}",
                                                    },
                                                    []))
                                                .ToArray();

        AddStep("start with empty library", importedChartLibrary.Clear);
        AddStep("import seven-chart package", () =>
            importedChartLibrary.AddOrReplace(package, packagePath));
        AddUntilStep("all package charts visible", () =>
            songSelectScreen.VisibleEntryCount == 7);
        AddAssert("newest package chart selected", () =>
            songSelectScreen.SelectedEntry.Beatmap.Title == "Actual song 7");
        AddAssert("package rows use song metadata", () =>
            songSelectScreen.SelectedEntry.Beatmap.Title != "pack");
        AddStep("collapse package", () => songSelectScreen.TogglePackage(packagePath));
        AddAssert("package is collapsed", () =>
            songSelectScreen.IsPackageCollapsed(packagePath));
        AddAssert("package chart rows hidden", () =>
            songSelectScreen.VisibleRowCount == 0);
        AddStep("expand package", () => songSelectScreen.TogglePackage(packagePath));
        AddAssert("all package chart rows restored", () =>
            songSelectScreen.VisibleRowCount == 7);
    }
}
