using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using Yokko.Core.Gameplay;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests.Visual;

[TestFixture]
public partial class TestSceneSongSelectScreen : YokkoTestScene
{
    private readonly ScreenStack screenStack;
    private readonly SongSelectScreen songSelectScreen;

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
        AddAssert("Blue Signal selected by default", () => songSelectScreen.SelectedEntry.Beatmap.Title == "Blue Signal");
        AddAssert("five demo songs visible", () => songSelectScreen.VisibleEntryCount == 5);

        AddStep("select next song", songSelectScreen.SelectNext);
        AddAssert("Neon Pulse selected", () => songSelectScreen.SelectedEntry.Beatmap.Title == "Neon Pulse");

        AddStep("filter 7K", () => songSelectScreen.SetKeyModeFilter(KeyMode.SevenKey));
        AddAssert("two 7K songs visible", () => songSelectScreen.VisibleEntryCount == 2);
        AddAssert("selection follows filter", () => songSelectScreen.SelectedEntry.Beatmap.KeyMode == KeyMode.SevenKey);

        AddStep("search Circuit", () => songSelectScreen.SetSearchQuery("Circuit"));
        AddAssert("one matching song", () => songSelectScreen.VisibleEntryCount == 1);
        AddAssert("Circuit Bloom selected", () => songSelectScreen.SelectedEntry.Beatmap.Title == "Circuit Bloom");

        AddStep("search no results", () => songSelectScreen.SetSearchQuery("not-a-real-song"));
        AddAssert("empty result is stable", () => songSelectScreen.VisibleEntryCount == 0);

        AddStep("restore all songs", () =>
        {
            songSelectScreen.SetSearchQuery(string.Empty);
            songSelectScreen.SetKeyModeFilter(null);
        });
        AddAssert("all songs restored", () => songSelectScreen.VisibleEntryCount == 5);

        AddAssert("ranking shown by default", () => songSelectScreen.ScoreView == SongSelectScoreView.GlobalRanking);
        AddStep("show personal record", songSelectScreen.ToggleScoreView);
        AddAssert("personal record selected", () => songSelectScreen.ScoreView == SongSelectScoreView.Personal);
        AddStep("restore ranking", songSelectScreen.ToggleScoreView);
    }

    [Test]
    public void TestPlayPushesGameplay()
    {
        AddStep("play selected song", songSelectScreen.PlaySelected);
        AddAssert("gameplay is pushed", () => screenStack.CurrentScreen is GameplayScreen);
        AddStep("return to song select", () => screenStack.CurrentScreen.Exit());
        AddUntilStep("song select resumes", () => screenStack.CurrentScreen is SongSelectScreen);
    }
}
