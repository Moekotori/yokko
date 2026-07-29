using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Game.Importing;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.SongSelect;
using Yokko.Import;

namespace Yokko.Game.Tests.Visual;

[TestFixture]
public partial class TestSceneSongSelectScreen : YokkoTestScene
{
    private readonly ScreenStack screenStack;
    private readonly SongSelectScreen songSelectScreen;
    [Resolved]
    private ImportedChartLibrary importedChartLibrary { get; set; }

    public TestSceneSongSelectScreen()
    {
        Add(screenStack = new ScreenStack(songSelectScreen = new SongSelectScreen())
        {
            RelativeSizeAxes = Axes.Both,
        });
    }

    [Test]
    public void TestSongSelectInteractions()
    {
        AddAssert("song select is current", () => screenStack.CurrentScreen is SongSelectScreen);
        AddStep("start with empty library", () => importedChartLibrary.Clear());
        AddUntilStep("library is empty", () => songSelectScreen.VisibleEntryCount == 0);
        AddAssert("no built-in demo songs", () => songSelectScreen.VisibleEntryCount == 0);
        AddStep("import test charts", () => importedChartLibrary.AddOrReplace(
            [
                result("Imported Four", DemoBeatmaps.CreateFourKeyDemo()),
                result("Imported Seven", DemoBeatmaps.CreateSevenKeyDemo()),
            ],
            @"C:\Charts\test-pack.osz"));
        AddUntilStep("imported charts visible", () => songSelectScreen.VisibleEntryCount == 2);
        AddAssert("newest import selected", () => songSelectScreen.SelectedEntry.Beatmap.Title == "Imported Seven");

        AddStep("select next song", songSelectScreen.SelectNext);
        AddAssert("selection wraps", () => songSelectScreen.SelectedEntry.Beatmap.Title == "Imported Four");

        AddStep("filter 7K", () => songSelectScreen.SetKeyModeFilter(KeyMode.SevenKey));
        AddAssert("one 7K song visible", () => songSelectScreen.VisibleEntryCount == 1);
        AddAssert("selection follows filter", () => songSelectScreen.SelectedEntry.Beatmap.KeyMode == KeyMode.SevenKey);

        AddStep("search imported seven", () => songSelectScreen.SetSearchQuery("Imported Seven"));
        AddAssert("one matching song", () => songSelectScreen.VisibleEntryCount == 1);

        AddStep("search no results", () => songSelectScreen.SetSearchQuery("not-a-real-song"));
        AddAssert("empty result is stable", () => songSelectScreen.VisibleEntryCount == 0);

        AddStep("restore all songs", () =>
        {
            songSelectScreen.SetSearchQuery(string.Empty);
            songSelectScreen.SetKeyModeFilter(null);
        });
        AddAssert("all imports restored", () => songSelectScreen.VisibleEntryCount == 2);

        AddAssert("ranking shown by default", () => songSelectScreen.ScoreView == SongSelectScoreView.GlobalRanking);
        AddStep("show personal record", songSelectScreen.ToggleScoreView);
        AddAssert("personal record selected", () => songSelectScreen.ScoreView == SongSelectScoreView.Personal);
        AddStep("restore ranking", songSelectScreen.ToggleScoreView);
    }

    [Test]
    public void TestPlayPushesGameplay()
    {
        AddStep("start with empty library", () => importedChartLibrary.Clear());
        AddUntilStep("library is empty", () => songSelectScreen.VisibleEntryCount == 0);
        AddStep("ensure playable import", () => importedChartLibrary.AddOrReplace(
            result("Playable Import", DemoBeatmaps.CreateFourKeyDemo()),
            @"C:\Charts\playable.osu"));
        AddUntilStep("playable import selected", () => songSelectScreen.SelectedEntry?.Beatmap.Title == "Playable Import");
        AddStep("play selected song", songSelectScreen.PlaySelected);
        AddAssert("gameplay is pushed", () => screenStack.CurrentScreen is GameplayScreen);
        AddStep("return to song select", () => screenStack.CurrentScreen.Exit());
        AddUntilStep("song select resumes", () => screenStack.CurrentScreen is SongSelectScreen);
    }

    private static ChartImportResult result(string title, YokkoBeatmap beatmap) =>
        new(beatmap with { Title = title }, []);
}
