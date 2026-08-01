using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Framework.Testing;
using Yokko.Core.Beatmaps;
using Yokko.Game.Importing;
using Yokko.Game.Screens.ChartLibrary;
using Yokko.Import;

namespace Yokko.Game.Tests.Visual;

[TestFixture]
public partial class TestSceneChartLibraryScreen : YokkoTestScene
{
    private readonly ScreenStack screenStack;
    private readonly ChartLibraryScreen libraryScreen;

    [Resolved]
    private ImportedChartLibrary importedChartLibrary { get; set; }

    public TestSceneChartLibraryScreen()
    {
        Add(screenStack = new ScreenStack(
            libraryScreen = new ChartLibraryScreen())
        {
            RelativeSizeAxes = Axes.Both,
        });
    }

    [Test]
    public void TestLibraryBrowsingAndFiltering()
    {
        AddAssert("chart library is current", () =>
            screenStack.CurrentScreen is ChartLibraryScreen);
        AddAssert("file selector is available", () =>
            libraryScreen.IsFileSelectorAvailable);
        AddStep("start with empty library", () => importedChartLibrary.Clear());
        AddUntilStep("empty state is visible", () =>
            libraryScreen.FilteredChartCount == 0);
        AddStep("add managed charts", () => importedChartLibrary.AddOrReplace(
            [
                result("Imported Four", DemoBeatmaps.CreateFourKeyDemo()),
                result("Imported Seven", DemoBeatmaps.CreateSevenKeyDemo()),
            ],
            @"C:\Charts\management-test.osz"));
        AddUntilStep("charts appear", () =>
            libraryScreen.FilteredChartCount == 2
            && libraryScreen.ManagedChartCount == 2);
        AddStep("search for seven", () =>
            libraryScreen.SetSearchQuery("Seven"));
        AddUntilStep("search narrows the list", () =>
            libraryScreen.FilteredChartCount == 1);
        AddStep("clear search and show managed", () =>
        {
            libraryScreen.SetSearchQuery(string.Empty);
            libraryScreen.SetSourceFilter(ChartLibrarySourceFilter.Managed);
        });
        AddUntilStep("managed filter is active", () =>
            libraryScreen.CurrentSourceFilter == ChartLibrarySourceFilter.Managed
            && libraryScreen.FilteredChartCount == 2);
        AddStep("external filter has no managed rows", () =>
            libraryScreen.SetSourceFilter(
                ChartLibrarySourceFilter.ExternalOsu));
        AddUntilStep("external filter is empty", () =>
            libraryScreen.FilteredChartCount == 0);
    }

    private static ChartImportResult result(
        string title,
        YokkoBeatmap beatmap) => new(
        beatmap with { Title = title },
        []);
}
