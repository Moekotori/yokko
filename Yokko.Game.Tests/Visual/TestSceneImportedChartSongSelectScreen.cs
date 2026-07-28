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
        ChartImportResult[] package = Enumerable.Range(1, 7)
                                                .Select(index => new ChartImportResult(
                                                    DemoBeatmaps.CreateFourKeyDemo() with
                                                    {
                                                        Title = $"Package chart {index}",
                                                    },
                                                    []))
                                                .ToArray();

        AddStep("import seven-chart package", () =>
            importedChartLibrary.AddOrReplace(package, @"C:\Charts\pack.osz"));
        AddUntilStep("all package charts visible", () =>
            songSelectScreen.VisibleEntryCount == 12);
        AddAssert("newest package chart selected", () =>
            songSelectScreen.SelectedEntry.Beatmap.Title == "Package chart 7");
    }
}
