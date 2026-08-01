using NUnit.Framework;
using System;
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
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.Settings;
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
    [Resolved]
    private YokkoDisplaySettings displaySettings { get; set; }

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
        AddAssert("empty library state does not offer a filter reset", () =>
            songSelectScreen.NoResultsVisible
            && songSelectScreen.NoResultsTitle
                == "NO SONGS IN YOUR LIBRARY"
            && songSelectScreen.NoResultsSummary
                == "IMPORT A BEATMAP TO START PLAYING"
            && !songSelectScreen.NoResultsResetVisible);
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
            songSelectScreen.RankingPanelSize == new Vector2(850, 448));
        AddAssert("ranking body uses its full height", () =>
            songSelectScreen.RankingContentSize == new Vector2(850, 390));
        AddAssert("ranking paper includes the header rail", () =>
            songSelectScreen.RankingPaperPosition == Vector2.Zero
            && songSelectScreen.RankingPaperSize == new Vector2(850, 434));
        AddAssert("search box leaves room for key filters", () =>
            songSelectScreen.SearchBoxSize == new Vector2(564, 48));
        AddAssert("top navigation keeps the brand lockup proportional", () =>
            Math.Abs(songSelectScreen.TopNavigationHeight - 72) < 0.01f
            && songSelectScreen.TopNavigationLogoPosition
               == new Vector2(24, 7)
            && songSelectScreen.TopNavigationLogoSize
               == new Vector2(168, 57)
            && songSelectScreen.TopNavigationProfileSize
               == new Vector2(172, 46));
        AddAssert("browser starts below search, rating and browse controls", () =>
            Math.Abs(songSelectScreen.SongBrowserTop - 220) < 0.01f);
        AddAssert("difficulty filter defaults to all MSD charts", () =>
            songSelectScreen.MinimumDifficultyFilter == 0
            && songSelectScreen.DifficultyFilterUnit == "MSD RANGE");
        AddAssert("browse controls use one compact row", () =>
        {
            SongSelectBrowseToolButton[] controls = songSelectScreen
                                                    .ChildrenOfType<SongSelectBrowseToolButton>()
                                                    .OrderBy(control => control.X)
                                                    .ToArray();
            return controls.Length == 4
                   && controls.All(control =>
                       Math.Abs(control.Height - 34) < 0.01f)
                   && controls.All(control =>
                       Math.Abs(control.BorderThickness - 1) < 0.01f
                       && Math.Abs(control.CornerRadius - 7) < 0.01f)
                   && controls.Select(control => control.X)
                              .SequenceEqual([0, 184, 388, 614])
                   && controls.Select(control => control.Width)
                              .SequenceEqual([176, 196, 218, 236])
                   && controls.Count(control => control.Interactive) == 3;
        });
        AddAssert("browse controls span one aligned rounded row", () =>
            songSelectScreen.BrowseToolbarSize == new Vector2(850, 34));
        AddAssert("selected details separate chart facts from performance", () =>
            songSelectScreen.SelectedChartFactsPosition
                == new Vector2(260, 169)
            && songSelectScreen.SelectedChartFactsSize
                == new Vector2(572, 34)
            && songSelectScreen.SelectedPerformancePosition
                == new Vector2(260, 213)
            && songSelectScreen.SelectedPerformanceSize
                == new Vector2(572, 35)
            && SongSelectScreen.SelectedDetailsPanelSize
                == new Vector2(850, 256)
            && SongSelectScreen.SelectedArtworkSize
                == new Vector2(220)
            && Math.Abs(SongSelectScreen.RankingTop - 294) < 0.01f);
        AddAssert("selected mods aligns with the ranking header", () =>
            songSelectScreen.SelectedModsButtonPosition
                == new Vector2(696, 294)
            && songSelectScreen.SelectedModsButtonSize
                == new Vector2(154, 40));
        AddAssert("footer tools use three aligned standalone cards", () =>
            songSelectScreen.FooterToolDockSize == new Vector2(560, 94)
            && songSelectScreen.FooterToolShadowCount == 3
            && songSelectScreen
               .ChildrenOfType<SongSelectFooterToolButton>()
               .All(button => button.Size == new Vector2(176, 82))
            && songSelectScreen
               .ChildrenOfType<SongSelectModsToggleButton>()
               .Single().Size == new Vector2(176, 82));
        AddAssert("large ui scale has collision-safe footer geometry", () =>
            SongSelectScreen.FooterToolDockSizeFor(YokkoUiScale.Large)
                == new Vector2(410, 94)
            && SongSelectScreen.FooterToolButtonWidthFor(
                YokkoUiScale.Large) == 126
            && SongSelectScreen.FooterToolButtonStepFor(
                YokkoUiScale.Large) == 134
            && SongSelectScreen.FooterToolDockSizeFor(
                YokkoUiScale.Comfortable) == new Vector2(560, 94)
            && SongSelectScreen.FooterToolButtonWidthFor(
                YokkoUiScale.Comfortable) == 176);

        AddStep("select next song", songSelectScreen.SelectNext);
        AddAssert("selection wraps", () => songSelectScreen.SelectedEntry.Beatmap.Title == "Imported Four");

        AddStep("filter 7K", () => songSelectScreen.SetKeyModeFilter(KeyMode.SevenKey));
        AddAssert("one 7K song visible", () => songSelectScreen.VisibleEntryCount == 1);
        AddAssert("selection follows filter", () => songSelectScreen.SelectedEntry.Beatmap.KeyMode == KeyMode.SevenKey);

        AddStep("search imported seven", () => songSelectScreen.SetSearchQuery("Imported Seven"));
        AddAssert("one matching song", () => songSelectScreen.VisibleEntryCount == 1);

        AddStep("search no results", () => songSelectScreen.SetSearchQuery("not-a-real-song"));
        AddAssert("empty result is stable", () => songSelectScreen.VisibleEntryCount == 0);
        AddAssert("empty state explains active filters", () =>
            songSelectScreen.NoResultsVisible
            && songSelectScreen.NoResultsTitle == "NO SONGS MATCH"
            && songSelectScreen.NoResultsSummary.Contains("not-a-real-song")
            && songSelectScreen.NoResultsSummary.Contains("7K")
            && songSelectScreen.NoResultsResetVisible);
        AddStep("clear browse filters", songSelectScreen.ClearBrowseFilters);
        AddAssert("clear restores the complete library", () =>
            songSelectScreen.VisibleEntryCount == 2
            && songSelectScreen.SearchQuery.Length == 0
            && songSelectScreen.KeyModeFilter == null
            && songSelectScreen.MinimumDifficultyFilter == 0
            && songSelectScreen.ShowConverts
            && !songSelectScreen.NoResultsVisible);
        AddAssert("empty search is not dismissed", () => !songSelectScreen.DismissSearch());

        AddAssert("ranking shown by default", () => songSelectScreen.ScoreView == SongSelectScoreView.GlobalRanking);
        int rankingTransitionVersion = 0;
        AddStep("remember ranking transition", () =>
            rankingTransitionVersion = songSelectScreen
                .RankingContentTransitionVersion);
        AddStep("click ranking body", songSelectScreen.ActivateRankingPanel);
        AddAssert("personal record selected", () => songSelectScreen.ScoreView == SongSelectScoreView.Personal);
        AddUntilStep("personal transition settles", () =>
            songSelectScreen.RankingContentLayerCount == 1);
        AddAssert("empty personal history stays on the paper", () =>
            songSelectScreen.RankingEmptyStateVisible
            && songSelectScreen.RankingContentTransitionVersion
                == rankingTransitionVersion + 1);
        AddStep("click personal record body", songSelectScreen.ActivateRankingPanel);
        AddAssert("ranking restored", () => songSelectScreen.ScoreView == SongSelectScoreView.GlobalRanking);
        AddUntilStep("global transition settles", () =>
            songSelectScreen.RankingContentLayerCount == 1);
        AddAssert("global ranking replaces the empty state", () =>
            !songSelectScreen.RankingEmptyStateVisible
            && songSelectScreen.RankingContentTransitionVersion
                == rankingTransitionVersion + 2);
    }

    [Test]
    public void TestFooterOptionsOpensSettings()
    {
        int listVersionBeforeSettings = 0;
        AddStep("remember list before settings", () =>
            listVersionBeforeSettings = songSelectScreen
                .SongListRebuildVersion);
        AddStep("open options from song select", () =>
            songSelectScreen.OpenOptions());
        AddUntilStep("settings is current", () =>
            screenStack.CurrentScreen is SettingsScreen);
        AddStep("return from settings", () =>
            screenStack.CurrentScreen.Exit());
        AddUntilStep("song select resumes", () =>
            screenStack.CurrentScreen == songSelectScreen);
        AddAssert("settings return refreshes list once", () =>
            songSelectScreen.SongListRebuildVersion
                == listVersionBeforeSettings + 1);
    }

    [Test]
    public void TestConvertedBeatmapFilterIsFunctional()
    {
        YokkoBeatmap converted = DemoBeatmaps.CreateFourKeyDemo() with
        {
            SourceFormat = ChartSourceFormat.OsuStandard,
            ConversionSource = new ManiaConversionSource(
                4,
                8,
                9,
                6,
                []),
        };

        AddStep("import native and converted charts", () =>
        {
            importedChartLibrary.Clear();
            importedChartLibrary.AddOrReplace(
                [
                    result(
                        "Native Mania",
                        DemoBeatmaps.CreateFourKeyDemo()),
                    result("Converted Standard", converted),
                ],
                @"C:\Charts\mixed-pack.osz");
        });
        AddUntilStep("both chart types shown by default", () =>
            songSelectScreen.VisibleEntryCount == 2
            && songSelectScreen.ShowConverts);
        AddStep("hide converted charts", () =>
            songSelectScreen.ToggleConvertedBeatmaps());
        AddUntilStep("only native chart remains", () =>
            songSelectScreen.VisibleEntryCount == 1);
        AddAssert("converted filter state is visible", () =>
            !songSelectScreen.ShowConverts
            && songSelectScreen.SelectedEntry.Beatmap.ConversionSource == null);
        AddStep("show converted charts again", () =>
            songSelectScreen.ToggleConvertedBeatmaps());
        AddUntilStep("converted chart returns", () =>
            songSelectScreen.VisibleEntryCount == 2
            && songSelectScreen.ShowConverts);
    }

    [Test]
    public void TestDifficultyRatingDisplayCanSwitch()
    {
        ManiaDifficultyRatingMode originalMode =
            ManiaDifficultyRatingMode.EtternaMsd;

        AddStep("remember difficulty display mode", () =>
            originalMode =
                displaySettings.DifficultyRatingMode.Value);
        AddStep("load rating test chart", () =>
        {
            displaySettings.DifficultyRatingMode.Value =
                ManiaDifficultyRatingMode.EtternaMsd;
            importedChartLibrary.Clear();
            importedChartLibrary.AddOrReplace(
                [
                    result(
                        "Rating switch",
                        DemoBeatmaps.CreateFourKeyDemo()),
                ],
                @"C:\Charts\rating-switch.osz");
        });
        AddUntilStep("MSD mode is visible", () =>
            songSelectScreen.DisplayedDifficultyRatingMode
                == ManiaDifficultyRatingMode.EtternaMsd
            && songSelectScreen.DisplayedMsdRating?.IsSuccess
                == true
            && songSelectScreen.ChildrenOfType<
                    osu.Framework.Graphics.Sprites.SpriteText>()
                .Any(text => text.Text.ToString() == "MSD"));
        AddStep("switch to Rebirth stars", () =>
            displaySettings.DifficultyRatingMode.Value =
                ManiaDifficultyRatingMode.RebirthStars);
        AddUntilStep("star mode is visible", () =>
            songSelectScreen.DisplayedDifficultyRatingMode
                == ManiaDifficultyRatingMode.RebirthStars
            && songSelectScreen.DisplayedStarRating?.IsSuccess
                == true
            && songSelectScreen.ChildrenOfType<
                    osu.Framework.Graphics.Sprites.SpriteText>()
                .Any(text => text.Text.ToString() == "STAR"));
        AddStep("restore difficulty display mode", () =>
            displaySettings.DifficultyRatingMode.Value =
                originalMode);
    }

    [Test]
    public void TestDifficultyMinimumFilterIsFunctionalAndModeSpecific()
    {
        ManiaDifficultyRatingMode originalMode =
            ManiaDifficultyRatingMode.EtternaMsd;

        AddStep("load charts for difficulty filtering", () =>
        {
            originalMode = displaySettings.DifficultyRatingMode.Value;
            displaySettings.DifficultyRatingMode.Value =
                ManiaDifficultyRatingMode.EtternaMsd;
            songSelectScreen.SetMinimumDifficultyFilter(0);
            importedChartLibrary.Clear();
            importedChartLibrary.AddOrReplace(
                [
                    result("Filter Four", DemoBeatmaps.CreateFourKeyDemo()),
                    result("Filter Seven", DemoBeatmaps.CreateSevenKeyDemo()),
                ],
                @"C:\Charts\difficulty-filter.osz");
        });
        AddUntilStep("all charts start visible", () =>
            songSelectScreen.VisibleEntryCount == 2);
        AddStep("raise MSD minimum", () =>
            songSelectScreen.SetMinimumDifficultyFilter(30));
        AddUntilStep("easy charts are filtered", () =>
            songSelectScreen.VisibleEntryCount == 0
            && songSelectScreen.MinimumDifficultyFilter == 30);
        AddStep("switch to star rating", () =>
            displaySettings.DifficultyRatingMode.Value =
                ManiaDifficultyRatingMode.RebirthStars);
        AddUntilStep("star rating keeps its own all value", () =>
            songSelectScreen.VisibleEntryCount == 2
            && songSelectScreen.MinimumDifficultyFilter == 0
            && songSelectScreen.DifficultyFilterUnit == "STAR RANGE");
        AddStep("raise star minimum", () =>
            songSelectScreen.SetMinimumDifficultyFilter(10));
        AddUntilStep("star threshold filters charts", () =>
            songSelectScreen.VisibleEntryCount == 0
            && songSelectScreen.MinimumDifficultyFilter == 10);
        AddStep("switch back to MSD", () =>
            displaySettings.DifficultyRatingMode.Value =
                ManiaDifficultyRatingMode.EtternaMsd);
        AddUntilStep("MSD threshold is remembered", () =>
            songSelectScreen.VisibleEntryCount == 0
            && songSelectScreen.MinimumDifficultyFilter == 30);
        AddStep("reset both thresholds and mode", () =>
        {
            songSelectScreen.SetMinimumDifficultyFilter(0);
            displaySettings.DifficultyRatingMode.Value =
                ManiaDifficultyRatingMode.RebirthStars;
            songSelectScreen.SetMinimumDifficultyFilter(0);
            displaySettings.DifficultyRatingMode.Value = originalMode;
        });
        AddUntilStep("filter reset restores charts", () =>
            songSelectScreen.VisibleEntryCount == 2);
    }

    [Test]
    public void TestAltPlusMinusAdjustsSelectedRateAndDetails()
    {
        YokkoBeatmap beatmap =
            DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title = "Song Select Rate Shortcut",
            };
        ManiaMsdResult expectedFastRating =
            ManiaMsdCalculator.CalculateResult(
                beatmap,
                1.05);
        ManiaMsdResult expectedSlowRating =
            ManiaMsdCalculator.CalculateResult(
                beatmap,
                0.95);
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
        AddAssert("fast rate refreshes bpm and MSD", () =>
            songSelectScreen.SelectedMods.Contains(
                ManiaModId.DoubleTime)
            && songSelectScreen.SelectedMods.PlaybackRate == 1.05
            && songSelectScreen.DisplayedPlaybackRate == 1.05
            && songSelectScreen.DisplayedBpm == "126"
            && songSelectScreen.DisplayedMsdRating?.Value
               == expectedFastRating.Value);
        AddAssert("rate change keeps the existing list row", () =>
            ReferenceEquals(
                originalRow,
                songSelectScreen
                    .ChildrenOfType<SongSelectSongRow>()
                    .Single(row =>
                        row.Entry.Beatmap.Title == beatmap.Title))
            && originalRow.DisplayedDifficultyRatings.EtternaMsd.Value
               == expectedFastRating.Value);
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
            && songSelectScreen.DisplayedBpm == "114"
            && originalRow.DisplayedDifficultyRatings.EtternaMsd.Value
               == expectedSlowRating.Value);
    }

    [Test]
    public void TestFocusedPackageExpansionFollowsKeyboardSelection()
    {
        const string firstPackage = @"C:\Charts\focus-one.osz";
        const string secondPackage = @"C:\Charts\focus-two.osz";

        AddStep("import two multi-chart packages", () =>
        {
            importedChartLibrary.Clear();
            importedChartLibrary.AddOrReplace(
            [
                result(
                    "Focus One Easy",
                    DemoBeatmaps.CreateSevenKeyDemo() with
                    {
                        DifficultyName = "Easy",
                    }),
                result(
                    "Focus One Hard",
                    DemoBeatmaps.CreateSevenKeyDemo() with
                    {
                        DifficultyName = "Hard",
                    }),
            ], firstPackage);
            importedChartLibrary.AddOrReplace(
            [
                result(
                    "Focus Two Easy",
                    DemoBeatmaps.CreateSevenKeyDemo() with
                    {
                        DifficultyName = "Easy",
                    }),
                result(
                    "Focus Two Hard",
                    DemoBeatmaps.CreateSevenKeyDemo() with
                    {
                        DifficultyName = "Hard",
                    }),
            ], secondPackage);
        });
        AddUntilStep("newest package is focused", () =>
            songSelectScreen.VisibleEntryCount == 4
            && songSelectScreen.SelectedEntry?.PackageId == secondPackage
            && songSelectScreen.UsesFocusedPackageExpansion);
        AddAssert("only focused package charts are materialised", () =>
            songSelectScreen.IsPackageCollapsed(firstPackage)
            && !songSelectScreen.IsPackageCollapsed(secondPackage)
            && songSelectScreen.IndexedSongListItemCount == 4
            && songSelectScreen.NavigableEntryCount == 4);
        AddUntilStep("collapsed neighbour uses compact package chrome", () =>
        {
            SongSelectPackageHeader[] headers = songSelectScreen
                .ChildrenOfType<SongSelectPackageHeader>()
                .ToArray();
            return headers.Count(header => header.IsExpanded) == 1
                   && headers.Count(header => !header.IsExpanded) == 1
                   && Math.Abs(headers.Single(header => header.IsExpanded).Height
                               - SongSelectPackageHeader.ExpandedHeight) < 0.05f
                   && Math.Abs(headers.Single(header => !header.IsExpanded).Height
                               - SongSelectPackageHeader.CollapsedHeight) < 0.05f;
        });
        AddStep("wrap selection into first package", () =>
            songSelectScreen.SelectNext());
        AddUntilStep("focus follows keyboard selection", () =>
            songSelectScreen.SelectedEntry?.PackageId == firstPackage
            && !songSelectScreen.IsPackageCollapsed(firstPackage)
            && songSelectScreen.IsPackageCollapsed(secondPackage)
            && songSelectScreen.IndexedSongListItemCount == 4
            && songSelectScreen.NavigableEntryCount == 4);
        AddUntilStep("compact and expanded heights transfer with focus", () =>
        {
            SongSelectPackageHeader[] headers = songSelectScreen
                .ChildrenOfType<SongSelectPackageHeader>()
                .ToArray();
            return headers.Count(header => header.IsExpanded) == 1
                   && headers.Count(header => !header.IsExpanded) == 1
                   && Math.Abs(headers.Single(header => header.IsExpanded).Height
                               - SongSelectPackageHeader.ExpandedHeight) < 0.05f
                   && Math.Abs(headers.Single(header => !header.IsExpanded).Height
                               - SongSelectPackageHeader.CollapsedHeight) < 0.05f;
        });
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
        AddAssert("selected mods button reflects restored state", () =>
            songSelectScreen.SelectedModsButtonCount
                == songSelectScreen.SelectedMods.Mods.Count
            && (songSelectScreen.SelectedMods.Mods.Count > 0
                || songSelectScreen.SelectedModsButtonSummary == "NONE"));
        AddAssert("mod panel starts closed", () =>
            !songSelectScreen.IsModPanelOpen);
        int listVersionBeforeMods = 0;
        int detailsVersionBeforeMods = 0;
        SongSelectEntry selectionBeforeMods = null;
        AddStep("remember song select state before mods", () =>
        {
            listVersionBeforeMods = songSelectScreen
                .SongListRebuildVersion;
            detailsVersionBeforeMods = songSelectScreen
                .DetailsTransitionVersion;
            selectionBeforeMods = songSelectScreen.SelectedEntry;
        });
        AddStep("open mod panel from selected mods", () =>
            songSelectScreen.ActivateSelectedModsButton());
        AddAssert("mod panel opened", () =>
            songSelectScreen.IsModPanelOpen);
        AddAssert("dedicated mods screen opened", () =>
            screenStack.CurrentScreen is GameplayModsScreen);
        AddAssert("mods screen keeps preview playing", () =>
            SongSelectScreen.KeepsPreviewPlaying(
                screenStack.CurrentScreen));
        AddStep("close mods screen", () =>
            screenStack.CurrentScreen.Exit());
        AddUntilStep("song select resumes", () =>
            screenStack.CurrentScreen == songSelectScreen);
        AddAssert("mod panel closed", () =>
            !songSelectScreen.IsModPanelOpen);
        AddAssert("unchanged mods return preserves song select", () =>
            songSelectScreen.SongListRebuildVersion
                == listVersionBeforeMods
            && songSelectScreen.DetailsTransitionVersion
                == detailsVersionBeforeMods
            && ReferenceEquals(
                selectionBeforeMods,
                songSelectScreen.SelectedEntry));
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
        AddAssert("selected mods button reflects active mod", () =>
            songSelectScreen.SelectedModsButtonCount
                == songSelectScreen.SelectedMods.Mods.Count
            && songSelectScreen.SelectedModsButtonSummary.Contains("MU"));
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
        AddAssert("one-shot mods do not carry to the next song", () =>
            !songSelectScreen.SelectedMods.Contains(
                ManiaModId.Autoplay)
            && !songSelectScreen.SelectedMods.Contains(
                ManiaModId.AccuracyChallenge)
            && !songSelectScreen.SelectedMods.Contains(
                ManiaModId.SuddenDeath)
            && songSelectScreen.SelectedMods.Contains(
                ManiaModId.Nightcore)
            && songSelectScreen.SelectedMods.Contains(
                ManiaModId.Mirror)
            && songSelectScreen.SelectedMods.Contains(
                ManiaModId.Random));
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
