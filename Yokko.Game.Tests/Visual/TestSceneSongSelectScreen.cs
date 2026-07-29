using NUnit.Framework;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osuTK;
using osuTK.Input;
using Yokko.Core.Beatmaps;
using Yokko.Core.Difficulty;
using Yokko.Core.Gameplay;
using Yokko.Core.Mods;
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
    private int? selectedRandomSeed;
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
        AddUntilStep("box mascot gif decoded", () =>
            songSelectScreen.MascotFrameCount > 1);
        AddAssert("ranking fits 16:9 stage", () =>
            SongSelectScreen.RankingFitsDesignedStage);
        AddAssert("ranking is above footer", () =>
            songSelectScreen.RankingFitsAboveFooter);
        AddAssert("ranking uses the available detail width", () =>
            songSelectScreen.RankingPanelSize == new Vector2(440, 190));
        AddAssert("ranking body uses its full height", () =>
            songSelectScreen.RankingContentSize == new Vector2(440, 152));
        AddAssert("search box is compact", () =>
            songSelectScreen.SearchBoxSize == new Vector2(360, 44));

        AddStep("select next song", songSelectScreen.SelectNext);
        AddAssert("selection wraps", () => songSelectScreen.SelectedEntry.Beatmap.Title == "Imported Four");

        AddStep("filter 7K", () => songSelectScreen.SetKeyModeFilter(KeyMode.SevenKey));
        AddAssert("one 7K song visible", () => songSelectScreen.VisibleEntryCount == 1);
        AddAssert("selection follows filter", () => songSelectScreen.SelectedEntry.Beatmap.KeyMode == KeyMode.SevenKey);

        AddStep("search imported seven", () => songSelectScreen.SetSearchQuery("Imported Seven"));
        AddAssert("one matching song", () => songSelectScreen.VisibleEntryCount == 1);

        AddStep("search no results", () => songSelectScreen.SetSearchQuery("not-a-real-song"));
        AddAssert("empty result is stable", () => songSelectScreen.VisibleEntryCount == 0);
        AddAssert("first escape dismisses search", songSelectScreen.DismissSearch);
        AddAssert("search query cleared", () => songSelectScreen.SearchQuery.Length == 0);
        AddAssert("empty search is not dismissed", () => !songSelectScreen.DismissSearch());

        AddStep("restore all songs", () =>
        {
            songSelectScreen.SetKeyModeFilter(null);
        });
        AddAssert("all imports restored", () => songSelectScreen.VisibleEntryCount == 2);

        AddAssert("ranking shown by default", () => songSelectScreen.ScoreView == SongSelectScoreView.GlobalRanking);
        AddStep("click ranking body", songSelectScreen.ActivateRankingPanel);
        AddAssert("personal record selected", () => songSelectScreen.ScoreView == SongSelectScoreView.Personal);
        AddStep("click personal record body", songSelectScreen.ActivateRankingPanel);
        AddAssert("ranking restored", () => songSelectScreen.ScoreView == SongSelectScoreView.GlobalRanking);
    }

    [Test]
    public void TestAltPlusMinusAdjustsSelectedRateAndDetails()
    {
        YokkoBeatmap beatmap =
            DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title = "Song Select Rate Shortcut",
            };
        ManiaStarRatingResult expectedFastRating =
            ManiaStarRatingCalculator.CalculateResult(
                beatmap,
                1.05);
        SongSelectSongRow originalRow = null;

        AddStep("start with one rate test chart", () =>
        {
            importedChartLibrary.Clear();
            importedChartLibrary.AddOrReplace(
            [
                result(beatmap.Title, beatmap),
            ],
            @"C:\Charts\rate-shortcut.osu");
        });
        AddUntilStep("rate test chart selected", () =>
            songSelectScreen.SelectedEntry?.Beatmap.Title
            == beatmap.Title);
        AddStep("capture current list row", () =>
            originalRow = songSelectScreen
                .ChildrenOfType<SongSelectSongRow>()
                .Single(row =>
                    row.Entry.Beatmap.Title == beatmap.Title));
        AddAssert("details start at normal rate", () =>
            songSelectScreen.SelectedMods.PlaybackRate == 1
            && songSelectScreen.DisplayedPlaybackRate == 1
            && songSelectScreen.DisplayedBpm == "120");
        AddStep("plain plus is ignored", () =>
            songSelectScreen.HandlePlaybackRateShortcut(
                Key.Plus,
                false));
        AddAssert("plain plus keeps normal rate", () =>
            songSelectScreen.SelectedMods.PlaybackRate == 1);
        AddStep("alt plus sets 1.05x", () =>
            songSelectScreen.HandlePlaybackRateShortcut(
                Key.Plus,
                true));
        AddAssert("fast rate refreshes bpm and stars", () =>
            songSelectScreen.SelectedMods.Contains(
                ManiaModId.DoubleTime)
            && songSelectScreen.SelectedMods.PlaybackRate == 1.05
            && songSelectScreen.DisplayedPlaybackRate == 1.05
            && songSelectScreen.DisplayedBpm == "126"
            && songSelectScreen.DisplayedStarRating?.Value
               == expectedFastRating.Value);
        AddAssert("rate change keeps the existing list row", () =>
            ReferenceEquals(
                originalRow,
                songSelectScreen
                    .ChildrenOfType<SongSelectSongRow>()
                    .Single(row =>
                        row.Entry.Beatmap.Title == beatmap.Title)));
        AddStep("alt keypad minus restores 1x", () =>
            songSelectScreen.HandlePlaybackRateShortcut(
                Key.KeypadMinus,
                true));
        AddAssert("normal rate removes fixed-rate mod", () =>
            songSelectScreen.SelectedMods.FixedRateMod == null
            && songSelectScreen.DisplayedPlaybackRate == 1
            && songSelectScreen.DisplayedBpm == "120");
        AddStep("alt minus sets 0.95x", () =>
            songSelectScreen.HandlePlaybackRateShortcut(
                Key.Minus,
                true));
        AddAssert("slow rate uses HT and updates bpm", () =>
            songSelectScreen.SelectedMods.Contains(
                ManiaModId.HalfTime)
            && songSelectScreen.SelectedMods.PlaybackRate == 0.95
            && songSelectScreen.DisplayedPlaybackRate == 0.95
            && songSelectScreen.DisplayedBpm == "114");
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
        AddAssert("mod panel starts closed", () =>
            !songSelectScreen.IsModPanelOpen);
        AddStep("open mod panel", songSelectScreen.ToggleModPanel);
        AddAssert("mod panel opened", () =>
            songSelectScreen.IsModPanelOpen);
        AddAssert("dedicated mods screen opened", () =>
            screenStack.CurrentScreen is GameplayModsScreen);
        AddStep("close mods screen", () =>
            screenStack.CurrentScreen.Exit());
        AddUntilStep("song select resumes", () =>
            screenStack.CurrentScreen == songSelectScreen);
        AddAssert("mod panel closed", () =>
            !songSelectScreen.IsModPanelOpen);
        AddStep("enable Muted", () =>
            songSelectScreen.ToggleMod(ManiaModId.Muted));
        AddStep("configure inverse Muted", () =>
        {
            songSelectScreen.SetMutedInverse(true);
            songSelectScreen.SetMutedComboCount(125);
            songSelectScreen.SetMutedMetronome(false);
            songSelectScreen.SetMutedAffectsHitSounds(false);
        });
        AddAssert("Muted settings are reflected", () =>
            songSelectScreen.SelectedMods.MutedInverse
            && songSelectScreen.SelectedMods.MutedComboCount == 125
            && !songSelectScreen.SelectedMods.MutedMetronome
            && !songSelectScreen.SelectedMods.MutedAffectsHitSounds
            && songSelectScreen.MutedSettings.ComboCount == 125);
        AddStep("enable Invert", () =>
            songSelectScreen.ToggleMod(ManiaModId.Invert));
        AddAssert("Invert selected", () =>
            songSelectScreen.SelectedMods.Contains(ManiaModId.Invert));
        AddStep("enable Cinema", () =>
            songSelectScreen.ToggleMod(ManiaModId.Cinema));
        AddAssert("Cinema selected as automation", () =>
            songSelectScreen.SelectedMods.Contains(ManiaModId.Cinema)
            && songSelectScreen.SelectedMods.IsAutomation);
        AddStep("enable Classic", () =>
            songSelectScreen.ToggleMod(ManiaModId.Classic));
        AddAssert("Classic selected", () =>
            songSelectScreen.SelectedMods.Contains(ManiaModId.Classic));
        AddStep("enable Wind Up", () =>
            songSelectScreen.ToggleMod(ManiaModId.WindUp));
        AddStep("configure Wind Up", () =>
        {
            songSelectScreen.SetTimeRampFinalRate(1.7);
            songSelectScreen.SetTimeRampAdjustPitch(false);
        });
        AddAssert("Wind Up settings are reflected", () =>
            songSelectScreen.SelectedMods.Contains(ManiaModId.WindUp)
            && songSelectScreen.SelectedMods.TimeRampFinalRate == 1.7
            && !songSelectScreen.SelectedMods.TimeRampAdjustPitch
            && songSelectScreen.TimeRampSettings.FinalRate == 1.7);
        AddStep("replace Wind Up with Wind Down", () =>
            songSelectScreen.ToggleMod(ManiaModId.WindDown));
        AddAssert("Wind Down gets its lazer defaults", () =>
            !songSelectScreen.SelectedMods.Contains(ManiaModId.WindUp)
            && songSelectScreen.SelectedMods.Contains(ManiaModId.WindDown)
            && songSelectScreen.SelectedMods.TimeRampInitialRate == 1
            && songSelectScreen.SelectedMods.TimeRampFinalRate == 0.75);
        AddStep("enable DT", () =>
            songSelectScreen.ToggleMod(ManiaModId.DoubleTime));
        AddAssert("DT selected", () =>
            songSelectScreen.SelectedMods.Contains(
                ManiaModId.DoubleTime)
            && !songSelectScreen.SelectedMods.Contains(
                ManiaModId.WindDown));
        AddStep("replace DT with HT", () =>
            songSelectScreen.ToggleMod(ManiaModId.HalfTime));
        AddAssert("HT replaces DT", () =>
            songSelectScreen.SelectedMods.Contains(
                ManiaModId.HalfTime)
            && songSelectScreen.SelectedMods.PlaybackRate == 0.75
            && songSelectScreen.ModInfoTitle.Contains("HT")
            && songSelectScreen.ModInfoDescription.Contains(
                "Replaced DT"));
        AddStep("configure HT like lazer", () =>
        {
            songSelectScreen.SetFixedRateSpeedChange(0.80);
            songSelectScreen.SetFixedRateAdjustPitch(true);
        });
        AddAssert("HT rate and pitch settings are reflected", () =>
            songSelectScreen.SelectedMods.PlaybackRate == 0.80
            && songSelectScreen.SelectedMods.FixedRateAdjustPitch
            && songSelectScreen.FixedRateSettings.SpeedChange == 0.80
            && songSelectScreen.FixedRateSettings.AdjustPitch);
        AddStep("replace HT with DC", () =>
            songSelectScreen.ToggleMod(ManiaModId.Daycore));
        AddStep("configure DC speed", () =>
            songSelectScreen.SetFixedRateSpeedChange(0.60));
        AddAssert("DC keeps lazer fixed frequency", () =>
            songSelectScreen.SelectedMods.Contains(
                ManiaModId.Daycore)
            && songSelectScreen.SelectedMods.PlaybackRate == 0.60
            && songSelectScreen.SelectedMods.FixedAudioFrequencyScale
               == 0.75
            && songSelectScreen.FixedRateSettings.SpeedChange == 0.60);
        AddStep("replace DT with NC", () =>
            songSelectScreen.ToggleMod(ManiaModId.Nightcore));
        AddStep("configure NC speed", () =>
            songSelectScreen.SetFixedRateSpeedChange(1.25));
        AddAssert("NC replaces slow rate", () =>
            songSelectScreen.SelectedMods.Contains(
                ManiaModId.Nightcore)
            && !songSelectScreen.SelectedMods.Contains(
                ManiaModId.Daycore)
            && songSelectScreen.SelectedMods.PlaybackRate == 1.25
            && songSelectScreen.SelectedMods.FixedAudioFrequencyScale
               == 1.5);
        AddStep("replace NC with Adaptive Speed", () =>
            songSelectScreen.ToggleMod(
                ManiaModId.AdaptiveSpeed));
        AddStep("configure Adaptive Speed", () =>
        {
            songSelectScreen.SetAdaptiveInitialRate(1.2);
            songSelectScreen.SetAdaptiveAdjustPitch(false);
        });
        AddAssert("Adaptive Speed settings are reflected", () =>
            songSelectScreen.SelectedMods.HasAdaptiveSpeed
            && songSelectScreen.SelectedMods.AdaptiveInitialRate == 1.2
            && !songSelectScreen.SelectedMods.AdaptiveAdjustPitch
            && songSelectScreen.AdaptiveSpeedSettings.InitialRate == 1.2);
        AddStep("combine Auto", () =>
            songSelectScreen.ToggleMod(ManiaModId.Autoplay));
        AddAssert("Auto replaces Cinema", () =>
            !songSelectScreen.SelectedMods.Contains(ManiaModId.Cinema)
            && !songSelectScreen.SelectedMods.HasAdaptiveSpeed);
        AddStep("restore Nightcore after Adaptive Speed", () =>
            songSelectScreen.ToggleMod(ManiaModId.Nightcore));
        AddStep("enable Mirror", () =>
            songSelectScreen.ToggleMod(ManiaModId.Mirror));
        AddStep("enable seeded Random", () =>
        {
            songSelectScreen.ToggleMod(ManiaModId.Random);
            selectedRandomSeed =
                songSelectScreen.SelectedMods.RandomSeed;
        });
        AddAssert("Random gets a persistent seed", () =>
            selectedRandomSeed.HasValue);
        AddStep("enable Hold Off", () =>
            songSelectScreen.ToggleMod(ManiaModId.HoldOff));
        AddStep("replace Hold Off with No Release", () =>
            songSelectScreen.ToggleMod(ManiaModId.NoRelease));
        AddAssert("No Release replaces Hold Off", () =>
            songSelectScreen.SelectedMods.Contains(
                ManiaModId.NoRelease)
            && !songSelectScreen.SelectedMods.Contains(
                ManiaModId.HoldOff));
        AddStep("restore Hold Off", () =>
            songSelectScreen.ToggleMod(ManiaModId.HoldOff));
        AddStep("enable Fade In", () =>
            songSelectScreen.ToggleMod(ManiaModId.FadeIn));
        AddStep("replace Fade In with Hidden", () =>
            songSelectScreen.ToggleMod(ManiaModId.Hidden));
        AddStep("replace Hidden with Cover", () =>
            songSelectScreen.ToggleMod(ManiaModId.Cover));
        AddStep("replace Cover with Flashlight", () =>
            songSelectScreen.ToggleMod(ManiaModId.Flashlight));
        AddAssert("visibility family is exclusive", () =>
            songSelectScreen.SelectedMods.Contains(
                ManiaModId.Flashlight)
            && !songSelectScreen.SelectedMods.Contains(
                ManiaModId.FadeIn)
            && !songSelectScreen.SelectedMods.Contains(
                ManiaModId.Hidden)
            && !songSelectScreen.SelectedMods.Contains(
                ManiaModId.Cover));
        AddStep("enable Easy", () =>
            songSelectScreen.ToggleMod(ManiaModId.Easy));
        AddStep("enable No Fail", () =>
            songSelectScreen.ToggleMod(ManiaModId.NoFail));
        AddStep("replace No Fail with Sudden Death", () =>
            songSelectScreen.ToggleMod(ManiaModId.SuddenDeath));
        AddStep("replace Sudden Death with Perfect", () =>
            songSelectScreen.ToggleMod(ManiaModId.Perfect));
        AddAssert("fail family is exclusive and Easy remains", () =>
            songSelectScreen.SelectedMods.Contains(ManiaModId.Easy)
            && songSelectScreen.SelectedMods.Contains(
                ManiaModId.Perfect)
            && !songSelectScreen.SelectedMods.Contains(
                ManiaModId.NoFail)
            && !songSelectScreen.SelectedMods.Contains(
                ManiaModId.SuddenDeath));
        AddStep("require perfect hits", () =>
            songSelectScreen.SetPerfectRequirePerfectHits(true));
        AddAssert("strict Perfect setting is canonical and visible", () =>
            songSelectScreen.SelectedMods.PerfectRequirePerfectHits
            && songSelectScreen.SelectedMods.Fingerprint.Contains(
                "perfect:require-perfect")
            && songSelectScreen.PerfectSettings.RequirePerfectHits);
        AddStep("replace Easy with Hard Rock", () =>
            songSelectScreen.ToggleMod(ManiaModId.HardRock));
        AddStep("enable Accuracy Challenge", () =>
            songSelectScreen.ToggleMod(
                ManiaModId.AccuracyChallenge));
        AddStep("set AC target to 97.5%", () =>
            songSelectScreen.SetAccuracyChallengeMinimum(0.975));
        AddStep("judge AC against current accuracy", () =>
            songSelectScreen.SetAccuracyChallengeMode(
                ManiaAccuracyMode.Standard));
        AddStep("combine Sudden Death with AC", () =>
            songSelectScreen.ToggleMod(ManiaModId.SuddenDeath));
        AddAssert("HR and configured AC are selected", () =>
            songSelectScreen.SelectedMods.Contains(
                ManiaModId.HardRock)
            && !songSelectScreen.SelectedMods.Contains(
                ManiaModId.Easy)
            && songSelectScreen.SelectedMods.Contains(
                ManiaModId.AccuracyChallenge)
            && songSelectScreen.SelectedMods.Contains(
                ManiaModId.SuddenDeath)
            && !songSelectScreen.SelectedMods.Contains(
                ManiaModId.Perfect)
            && songSelectScreen.SelectedMods.AccuracyChallengeMinimum
               == 0.975
            && songSelectScreen.SelectedMods.AccuracyChallengeMode
               == ManiaAccuracyMode.Standard
            && songSelectScreen.AccuracyChallengeSettings.MinimumAccuracy
               == 0.975
            && songSelectScreen.AccuracyChallengeSettings.Mode
               == ManiaAccuracyMode.Standard);
        AddStep("enable Constant Speed", () =>
            songSelectScreen.ToggleMod(ManiaModId.ConstantSpeed));
        AddStep("enable Difficulty Adjust", () =>
            songSelectScreen.ToggleMod(ManiaModId.DifficultyAdjust));
        AddStep("enable DA extended limits", () =>
            songSelectScreen.SetDifficultyAdjustExtendedLimits(true));
        AddStep("set DA HP to 7.5", () =>
            songSelectScreen.SetDifficultyAdjustDrainRate(7.5));
        AddStep("set DA OD to 12.0", () =>
            songSelectScreen.SetDifficultyAdjustOverallDifficulty(12));
        AddAssert("DA replaces HR and exposes configured values", () =>
            songSelectScreen.SelectedMods.Contains(
                ManiaModId.DifficultyAdjust)
            && !songSelectScreen.SelectedMods.Contains(
                ManiaModId.HardRock)
            && songSelectScreen.SelectedMods.Contains(
                ManiaModId.ConstantSpeed)
            && songSelectScreen.SelectedMods
                               .DifficultyAdjustDrainRate == 7.5
            && songSelectScreen.SelectedMods
                               .DifficultyAdjustOverallDifficulty == 12
            && songSelectScreen.SelectedMods
                               .DifficultyAdjustExtendedLimits
            && songSelectScreen.DifficultyAdjustSettings.DrainRate
               == 7.5
            && songSelectScreen.DifficultyAdjustSettings
                               .OverallDifficulty == 12
            && songSelectScreen.DifficultyAdjustSettings
                               .ExtendedLimits);
        AddStep("play selected song", songSelectScreen.PlaySelected);
        AddUntilStep("gameplay receives selected mods", () =>
            screenStack.CurrentScreen is GameplaySessionScreen session
            && session.CurrentGameplay is GameplayScreen gameplay
            && gameplay.Mods.Contains(ManiaModId.Nightcore)
            && gameplay.Mods.Contains(ManiaModId.Autoplay)
            && gameplay.Mods.Contains(ManiaModId.Mirror)
            && gameplay.Mods.Contains(ManiaModId.Random)
            && gameplay.Mods.RandomSeed == selectedRandomSeed
            && gameplay.Mods.Contains(ManiaModId.HoldOff)
            && gameplay.Mods.Contains(ManiaModId.Flashlight)
            && gameplay.Mods.Contains(ManiaModId.DifficultyAdjust)
            && gameplay.Mods.Contains(ManiaModId.ConstantSpeed)
            && gameplay.Mods.Contains(ManiaModId.Muted)
            && gameplay.Mods.Contains(ManiaModId.Classic)
            && gameplay.Mods.DifficultyAdjustDrainRate == 7.5
            && gameplay.Mods.DifficultyAdjustOverallDifficulty == 12
            && gameplay.Mods.DifficultyAdjustExtendedLimits
            && gameplay.Mods.Contains(
                ManiaModId.AccuracyChallenge)
            && gameplay.Mods.Contains(ManiaModId.SuddenDeath)
            && gameplay.Mods.AccuracyChallengeMinimum == 0.975
            && gameplay.Mods.AccuracyChallengeMode
               == ManiaAccuracyMode.Standard
            && !gameplay.AppliedBeatmap.HitObjects.Any(
                static hitObject => hitObject.Kind == HitObjectKind.Hold)
            && gameplay.AutoplayMode);
        AddStep("return to song select", () =>
            ((GameplaySessionScreen)screenStack.CurrentScreen)
            .CurrentGameplay.Exit());
        AddUntilStep("song select resumes", () => screenStack.CurrentScreen is SongSelectScreen);
    }

    [Test]
    public void TestEscapeClearsSearchBeforeReturning()
    {
        SongSelectScreen escapeScreen = null;

        AddStep("push fresh song select", () =>
            screenStack.Push(escapeScreen = new SongSelectScreen()));
        AddUntilStep("fresh song select is current", () =>
            screenStack.CurrentScreen == escapeScreen);
        AddStep("enter search query", () =>
            escapeScreen.SetSearchQuery("43"));
        AddStep("first escape", () => escapeScreen.HandleEscape());
        AddAssert("first escape clears query", () =>
            escapeScreen.SearchQuery.Length == 0);
        AddAssert("first escape stays in song select", () =>
            screenStack.CurrentScreen == escapeScreen);
        AddStep("second escape", () => escapeScreen.HandleEscape());
        AddUntilStep("second escape returns", () =>
            screenStack.CurrentScreen == songSelectScreen);
    }

    [Test]
    public void TestStandardSourceKeyConversion()
    {
        YokkoBeatmap standard = DemoBeatmaps.CreateSevenKeyDemo() with
        {
            SourceFormat = ChartSourceFormat.OsuStandard,
            ConversionSource = new ManiaConversionSource(
                4,
                8,
                9,
                6,
                [
                    new ManiaConversionHitObject(
                        32,
                        1000,
                        1000,
                        ManiaConversionObjectKind.Circle),
                    new ManiaConversionHitObject(
                        256,
                        1250,
                        1250,
                        ManiaConversionObjectKind.Circle),
                    new ManiaConversionHitObject(
                        480,
                        1500,
                        1500,
                        ManiaConversionObjectKind.Circle),
                ]),
        };

        AddStep("import standard conversion source", () =>
        {
            importedChartLibrary.Clear();
            importedChartLibrary.AddOrReplace(
                result("Standard Conversion", standard),
                @"C:\Charts\standard.osu");
        });
        AddUntilStep("standard source selected", () =>
            songSelectScreen.SelectedEntry?.Beatmap.SourceFormat
            == ChartSourceFormat.OsuStandard);
        AddAssert("key configuration is available", () =>
            songSelectScreen.KeyConversionSettings.CanConvert);
        AddStep("select 4K conversion", () =>
            songSelectScreen.ToggleMod(ManiaModId.Key4));
        AddAssert("4K target is reflected", () =>
            songSelectScreen.SelectedMods.KeyConversionTarget == 4
            && songSelectScreen.KeyConversionSettings.SelectedKeyCount == 4);
        AddStep("enable Dual Stages", () =>
            songSelectScreen.ToggleMod(ManiaModId.DualStages));
        AddAssert("dual target is reflected", () =>
            songSelectScreen.SelectedMods.HasDualStages);
        AddStep("play converted chart", songSelectScreen.PlaySelected);
        AddUntilStep("gameplay receives regenerated dual 4K chart", () =>
            screenStack.CurrentScreen is GameplaySessionScreen session
            && session.CurrentGameplay is GameplayScreen gameplay
            && gameplay.AppliedBeatmap.KeyMode == KeyMode.EightKey
            && gameplay.AppliedBeatmap.StageCount == 2
            && gameplay.AppliedBeatmap.KeysPerStage == 4
            && gameplay.AppliedBeatmap.HitObjects.All(hitObject =>
                hitObject.Lane is >= 0 and < 8));
    }

    private static ChartImportResult result(string title, YokkoBeatmap beatmap) =>
        new(beatmap with { Title = title }, []);
}
