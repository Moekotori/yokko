using NUnit.Framework;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using Yokko.Core.Beatmaps;
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
        AddAssert("mod panel starts closed", () =>
            !songSelectScreen.IsModPanelOpen);
        AddStep("open mod panel", songSelectScreen.ToggleModPanel);
        AddAssert("mod panel opened", () =>
            songSelectScreen.IsModPanelOpen);
        AddStep("close mod panel", songSelectScreen.ToggleModPanel);
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
            && songSelectScreen.SelectedMods.PlaybackRate == 0.75);
        AddStep("replace HT with DC", () =>
            songSelectScreen.ToggleMod(ManiaModId.Daycore));
        AddAssert("DC changes pitch", () =>
            songSelectScreen.SelectedMods.Contains(
                ManiaModId.Daycore)
            && songSelectScreen.SelectedMods.ChangesAudioPitch);
        AddStep("replace DT with NC", () =>
            songSelectScreen.ToggleMod(ManiaModId.Nightcore));
        AddAssert("NC replaces slow rate", () =>
            songSelectScreen.SelectedMods.Contains(
                ManiaModId.Nightcore)
            && !songSelectScreen.SelectedMods.Contains(
                ManiaModId.Daycore));
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
        AddAssert("gameplay receives selected mods", () =>
            screenStack.CurrentScreen is GameplayScreen gameplay
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
        AddStep("return to song select", () => screenStack.CurrentScreen.Exit());
        AddUntilStep("song select resumes", () => screenStack.CurrentScreen is SongSelectScreen);
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
        AddAssert("gameplay receives regenerated dual 4K chart", () =>
            screenStack.CurrentScreen is GameplayScreen gameplay
            && gameplay.AppliedBeatmap.KeyMode == KeyMode.EightKey
            && gameplay.AppliedBeatmap.StageCount == 2
            && gameplay.AppliedBeatmap.KeysPerStage == 4
            && gameplay.AppliedBeatmap.HitObjects.All(hitObject =>
                hitObject.Lane is >= 0 and < 8));
    }

    private static ChartImportResult result(string title, YokkoBeatmap beatmap) =>
        new(beatmap with { Title = title }, []);
}
