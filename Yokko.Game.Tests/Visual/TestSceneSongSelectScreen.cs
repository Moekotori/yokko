using NUnit.Framework;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osuTK;
using osuTK.Input;
using Yokko.Core.Beatmaps;
using Yokko.Core.Difficulty;
using Yokko.Core.Gameplay;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Game.Importing;
using Yokko.Game.Gameplay;
using Yokko.Game.Presentation;
using Yokko.Game.Scoring;
using Yokko.Game.Screens.ChartLibrary;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.Settings;
using Yokko.Game.Screens.SongSelect;
using Yokko.Import;
using Yokko.Import.Osu;

namespace Yokko.Game.Tests.Visual;

[TestFixture]
public partial class TestSceneSongSelectScreen : YokkoManualInputTestScene
{
    private readonly ScreenStack screenStack;
    private readonly SongSelectScreen songSelectScreen;
    private int? selectedRandomSeed;
    [Resolved]
    private ImportedChartLibrary importedChartLibrary { get; set; }
    [Resolved]
    private YokkoDisplaySettings displaySettings { get; set; }
    [Resolved]
    private YokkoGameplaySettings gameplaySettings { get; set; }
    [Resolved]
    private GameplayScoreStore scoreStore { get; set; }
    [Resolved]
    private YokkoExternalOsuSettings externalOsuSettings { get; set; }

    public TestSceneSongSelectScreen()
    {
        Add(screenStack = new ScreenStack(songSelectScreen = new SongSelectScreen())
        {
            RelativeSizeAxes = Axes.Both,
        });
    }

    [Test]
    public void TestF5ReloadsLibraryFromDisk()
    {
        AddStep("start with empty library", () => importedChartLibrary.Clear());
        AddUntilStep("library is empty", () =>
            songSelectScreen.VisibleEntryCount == 0);
        AddStep("add transient chart", () => importedChartLibrary.AddOrReplace(
            result("Transient", DemoBeatmaps.CreateFourKeyDemo()),
            @"C:\Charts\transient.osu"));
        AddUntilStep("transient chart is visible", () =>
            songSelectScreen.VisibleEntryCount == 1);
        AddStep("press physical F5", () => InputManager.Key(Key.F5));
        AddUntilStep("reload completes", () =>
            !songSelectScreen.LibraryReloadInProgress);
        AddUntilStep("disk snapshot replaces transient chart", () =>
            songSelectScreen.VisibleEntryCount == 0);
    }

    [Test]
    public void TestF5FindsNewAndRemovesMovedExternalChart()
    {
        string root = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"yokko-song-select-f5-moved-{Guid.NewGuid():N}");
        string songs = Path.Combine(root, "osu!", "Songs");
        string set = Path.Combine(songs, "100 F5 Moved Set");
        string chartPath = Path.Combine(set, "moved.osu");
        string addedChartPath = Path.Combine(set, "added.osu");
        Task<ExternalOsuLibraryResult> configureTask = null;

        AddStep("create external chart", () =>
        {
            importedChartLibrary.DisableExternalOsu();
            importedChartLibrary.Clear();
            Directory.CreateDirectory(set);
            string chart = OsuManiaBeatmapIO.WriteBeatmap(
                DemoBeatmaps.CreateFourKeyDemo() with
                {
                    Title = "F5 Moved External",
                    AudioPath = null,
                });
            File.WriteAllText(chartPath, chart, new UTF8Encoding(false));
            configureTask = importedChartLibrary.SetExternalOsuSongsPathAsync(songs);
        });
        AddUntilStep("external scan completes", () =>
            configureTask?.IsCompletedSuccessfully == true);
        AddUntilStep("external chart is selected", () =>
            songSelectScreen.SelectedEntry?.Beatmap.Title
                == "F5 Moved External");
        AddUntilStep("external gameplay is preloaded", () =>
            songSelectScreen.GameplayPreloadReady);
        AddStep("replace source then press F5 and Enter", () =>
        {
            File.Move(chartPath, Path.Combine(root, "moved.osu"));
            string addedChart = OsuManiaBeatmapIO.WriteBeatmap(
                DemoBeatmaps.CreateFourKeyDemo() with
                {
                    Title = "F5 Added External",
                    AudioPath = null,
                });
            File.WriteAllText(
                addedChartPath,
                addedChart,
                new UTF8Encoding(false));
            InputManager.Key(Key.F5);
            InputManager.Key(Key.Enter);
        });
        AddUntilStep("reload completes", () =>
            !songSelectScreen.LibraryReloadInProgress);
        AddUntilStep("new chart replaces moved chart", () =>
            songSelectScreen.VisibleEntryCount == 1
            && importedChartLibrary.ExternalOsuChartCount == 1
            && songSelectScreen.SelectedEntry?.Beatmap.Title
            == "F5 Added External");
        AddAssert("Enter during refresh never entered gameplay", () =>
            screenStack.CurrentScreen == songSelectScreen
            && songSelectScreen.PlayButtonAction != "LOADING...");
        AddStep("clean external fixture", () =>
        {
            importedChartLibrary.DisableExternalOsu();
            externalOsuSettings.SongsPath.Value = string.Empty;
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        });
    }

    [Test]
    public void TestCachedExternalSummaryMaterialisesBeforeImmediatePlay()
    {
        string root = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"yokko-song-select-external-summary-{Guid.NewGuid():N}");
        string songs = Path.Combine(root, "osu!", "Songs");
        string set = Path.Combine(songs, "100 External Summary Set");
        Task<ExternalOsuLibraryResult> scanTask = null;

        AddStep("create cached external summary", () =>
        {
            importedChartLibrary.DisableExternalOsu();
            importedChartLibrary.Clear();
            Directory.CreateDirectory(set);
            string chart = OsuManiaBeatmapIO.WriteBeatmap(
                DemoBeatmaps.CreateFourKeyDemo() with
                {
                    Title = "Cached External Summary",
                    AudioPath = null,
                });
            File.WriteAllText(
                Path.Combine(set, "summary.osu"),
                chart,
                new UTF8Encoding(false));
            scanTask = importedChartLibrary.SetExternalOsuSongsPathAsync(songs);
        });
        AddUntilStep("external summary is selected", () =>
            scanTask?.IsCompletedSuccessfully == true
            && songSelectScreen.SelectedEntry?.Beatmap.Title
            == "Cached External Summary"
            && songSelectScreen.SelectedEntry.Beatmap.HitObjects.Count == 0);
        AddStep("press Enter immediately", () => InputManager.Key(Key.Enter));
        AddUntilStep("full external chart enters gameplay", () =>
            screenStack.CurrentScreen is GameplaySessionScreen session
            && session.CurrentGameplay is GameplayScreen gameplay
            && gameplay.AppliedBeatmap.HitObjects.Count > 0);
        AddAssert("external summary never reaches gameplay", () =>
            ((GameplaySessionScreen)screenStack.CurrentScreen)
            .CurrentGameplay.AppliedBeatmap.HitObjects.Count
            == DemoBeatmaps.CreateFourKeyDemo().HitObjects.Count);
        AddStep("return from external gameplay", () =>
            ((GameplaySessionScreen)screenStack.CurrentScreen)
            .CurrentGameplay.Exit());
        AddUntilStep("song select resumes after external gameplay", () =>
            screenStack.CurrentScreen == songSelectScreen);
        AddStep("clean cached external fixture", () =>
        {
            importedChartLibrary.DisableExternalOsu();
            externalOsuSettings.SongsPath.Value = string.Empty;
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        });
    }

    [Test]
    public void TestExpandedExternalPackagePublishesEveryDifficultyWithoutSelection()
    {
        string root = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"yokko-song-select-package-difficulty-{Guid.NewGuid():N}");
        string realPackage = Environment.GetEnvironmentVariable(
            "YOKKO_TEST_EXTERNAL_OSU_PACKAGE");
        string songs = string.IsNullOrWhiteSpace(realPackage)
            ? Path.Combine(root, "osu!", "Songs")
            : realPackage;
        string set = Path.Combine(songs, "100 Automatic Package Ratings");
        int expectedChartCount = string.IsNullOrWhiteSpace(realPackage)
            ? 8
            : Directory.EnumerateFiles(realPackage, "*.osu").Count();
        Task<ExternalOsuLibraryResult> configureTask = null;
        Task difficultyTask = null;
        Action<ImportedChartLibraryChange> pauseBackground = null;

        AddStep("create eight-chart external package", () =>
        {
            importedChartLibrary.DisableExternalOsu();
            importedChartLibrary.Clear();
            if (string.IsNullOrWhiteSpace(realPackage))
            {
                Directory.CreateDirectory(set);
                for (int i = 0; i < expectedChartCount; i++)
                {
                    string chart = OsuManiaBeatmapIO.WriteBeatmap(
                        DemoBeatmaps.CreateFourKeyDemo() with
                        {
                            Title = $"Automatic Rating {i + 1}",
                            Artist = "VA",
                            DifficultyName = $"PACK {i + 1}",
                            AudioPath = null,
                        });
                    File.WriteAllText(
                        Path.Combine(set, $"automatic-{i + 1}.osu"),
                        chart,
                        new UTF8Encoding(false));
                }
            }

            pauseBackground = change =>
            {
                if ((change.Kind
                     & ImportedChartLibraryChangeKind.Structure) != 0)
                {
                    importedChartLibrary.SetExternalIndexingPaused(true);
                }
            };
            importedChartLibrary.LibraryChanged += pauseBackground;
            configureTask = importedChartLibrary.SetExternalOsuSongsPathAsync(
                songs);
        });
        AddUntilStep("external package loads", () =>
            configureTask?.IsCompletedSuccessfully == true
            && importedChartLibrary.GetCharts().Count == expectedChartCount
            && songSelectScreen.VisibleEntryCount == expectedChartCount);
        AddUntilStep("all visible rows receive ratings without selection", () =>
        {
            ImportedChart[] charts = importedChartLibrary.GetCharts().ToArray();
            SongSelectSongRow[] rows = songSelectScreen
                                       .ChildrenOfType<SongSelectSongRow>()
                                       .ToArray();
            return charts.Length == expectedChartCount
                   && charts.All(chart =>
                       chart.DifficultyRating?.IsSuccess == true
                       && chart.StarRating?.IsSuccess == true)
                   && rows.Length >= expectedChartCount
                   && rows.All(row =>
                       row.DisplayedDifficultyRatings?.EtternaMsd?.IsSuccess
                       == true
                       && row.DisplayedDifficultyRatings?.RebirthStars?.IsSuccess
                       == true);
        });
        AddStep("resume and clean external fixture", () =>
        {
            importedChartLibrary.LibraryChanged -= pauseBackground;
            importedChartLibrary.SetExternalIndexingPaused(false);
            difficultyTask = importedChartLibrary.ExternalDifficultyTask;
        });
        AddUntilStep("background task exits", () =>
            difficultyTask?.IsCompleted == true);
        AddStep("remove external fixture", () =>
        {
            importedChartLibrary.DisableExternalOsu();
            externalOsuSettings.SongsPath.Value = string.Empty;
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        });
    }

    [Test]
    public void TestF5ClearsMovedManagedLibraryAndBlocksPlay()
    {
        string movedLibrary = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"yokko-moved-managed-library-{Guid.NewGuid():N}");
        Task<int> initialLoad = null;

        AddStep("create managed multi-chart package", () =>
        {
            importedChartLibrary.DisableExternalOsu();
            importedChartLibrary.Clear();
            Directory.CreateDirectory(importedChartLibrary.LibraryPath);
            string package = Path.Combine(
                importedChartLibrary.LibraryPath,
                "GD PACK");
            Directory.CreateDirectory(package);
            foreach (string difficulty in new[]
                     {
                         "Electroman Adventures",
                         "Cold Sweat",
                     })
            {
                string text = OsuManiaBeatmapIO.WriteBeatmap(
                    DemoBeatmaps.CreateFourKeyDemo() with
                    {
                        Title = "GD PACK",
                        DifficultyName = difficulty,
                        AudioPath = null,
                    });
                File.WriteAllText(
                    Path.Combine(package, $"{difficulty}.osu"),
                    text,
                    new UTF8Encoding(false));
            }

            initialLoad = importedChartLibrary.LoadFromDiskAsync(true, true);
        });
        AddUntilStep("managed package loads", () =>
            initialLoad?.IsCompletedSuccessfully == true
            && songSelectScreen.VisibleEntryCount == 2);
        AddUntilStep("managed gameplay is preloaded", () =>
            songSelectScreen.GameplayPreloadReady);
        AddStep("move managed root then press F5 and Enter", () =>
        {
            Directory.Move(importedChartLibrary.LibraryPath, movedLibrary);
            InputManager.Key(Key.F5);
            InputManager.Key(Key.Enter);
        });
        AddUntilStep("managed reload completes", () =>
            !songSelectScreen.LibraryReloadInProgress);
        AddUntilStep("all moved managed charts disappear", () =>
            songSelectScreen.VisibleEntryCount == 0
            && importedChartLibrary.GetCharts().Count == 0);
        AddStep("press Enter after managed removal", () =>
            InputManager.Key(Key.Enter));
        AddAssert("managed stale chart never enters gameplay", () =>
            screenStack.CurrentScreen == songSelectScreen
            && songSelectScreen.PlayButtonAction != "LOADING...");
        AddStep("clean managed fixture", () =>
        {
            if (Directory.Exists(movedLibrary))
                Directory.Delete(movedLibrary, true);
            Directory.CreateDirectory(importedChartLibrary.LibraryPath);
        });
    }

    [Test]
    public void TestCachedManagedSummaryMaterialisesBeforeImmediatePlay()
    {
        string fixture = null;
        Task<int> cachedLoad = null;
        Task<int> cleanupLoad = null;

        AddStep("create cached managed summary", () =>
        {
            fixture = Path.Combine(
                importedChartLibrary.LibraryPath,
                $"cached-managed-summary-{Guid.NewGuid():N}.osz");
            importedChartLibrary.DisableExternalOsu();
            importedChartLibrary.Clear();
            Directory.CreateDirectory(importedChartLibrary.LibraryPath);
            string text = OsuManiaBeatmapIO.WriteBeatmap(
                DemoBeatmaps.CreateFourKeyDemo() with
                {
                    Title = "Cached Managed Summary",
                    AudioPath = null,
                });
            using (ZipArchive archive = ZipFile.Open(
                       fixture,
                       ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("summary.osu");
                using StreamWriter writer = new(
                    entry.Open(),
                    new UTF8Encoding(false));
                writer.Write(text);
            }
            cachedLoad = Task.Run(async () =>
            {
                await importedChartLibrary.LoadFromDiskAsync(true, true);
                return await importedChartLibrary.LoadFromDiskAsync(true, true);
            });
        });
        AddUntilStep("cached summary is selected", () =>
            cachedLoad?.IsCompletedSuccessfully == true
            && songSelectScreen.SelectedEntry?.Beatmap.Title
            == "Cached Managed Summary"
            && songSelectScreen.SelectedEntry.Beatmap.HitObjects.Count == 0);
        AddStep("press Enter immediately", () => InputManager.Key(Key.Enter));
        AddUntilStep("full managed chart enters gameplay", () =>
            screenStack.CurrentScreen is GameplaySessionScreen session
            && session.CurrentGameplay is GameplayScreen gameplay
            && gameplay.AppliedBeatmap.HitObjects.Count > 0);
        AddAssert("summary never reaches gameplay", () =>
            ((GameplaySessionScreen)screenStack.CurrentScreen)
            .CurrentGameplay.AppliedBeatmap.HitObjects.Count
            == DemoBeatmaps.CreateFourKeyDemo().HitObjects.Count);
        AddStep("return from managed gameplay", () =>
            ((GameplaySessionScreen)screenStack.CurrentScreen)
            .CurrentGameplay.Exit());
        AddUntilStep("song select resumes after managed gameplay", () =>
            screenStack.CurrentScreen == songSelectScreen);
        AddStep("clean cached managed fixture", () =>
        {
            if (File.Exists(fixture))
                File.Delete(fixture);
            cleanupLoad = importedChartLibrary.LoadFromDiskAsync(true, true);
        });
        AddUntilStep("cached managed fixture is removed", () =>
            cleanupLoad?.IsCompletedSuccessfully == true
            && songSelectScreen.VisibleEntryCount == 0);
    }

    [Test]
    public void TestSongSelectInteractions()
    {
        SongSelectEntry selectedBeforeSort = null;
        int rebuildVersionBeforeSort = 0;
        int sortPassesBeforeSearch = 0;

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
        AddAssert("ranking fits 16:9 stage", () =>
            SongSelectScreen.RankingFitsDesignedStage);
        AddAssert("ranking is above footer", () =>
            songSelectScreen.RankingFitsAboveFooter);
        AddAssert("empty ranking uses a compact detail strip", () =>
            songSelectScreen.RankingPanelSize == new Vector2(850, 166));
        AddAssert("empty ranking body is compact", () =>
            songSelectScreen.RankingContentSize == new Vector2(850, 112));
        AddAssert("compact ranking paper includes the header rail", () =>
            songSelectScreen.RankingPaperPosition == Vector2.Zero
            && songSelectScreen.RankingPaperSize == new Vector2(850, 156));
        AddUntilStep("search box uses the space freed by key filter", () =>
            songSelectScreen.SearchBoxSize == new Vector2(816, 48));
        AddAssert("key modes share one compact cycling button", () =>
        {
            SongSelectKeyModeFilterButton[] filters = songSelectScreen
                .ChildrenOfType<SongSelectKeyModeFilterButton>()
                .ToArray();
            return filters.Length == 1
                   && songSelectScreen.KeyFilterButtonSize
                      == new Vector2(130, 48)
                   && songSelectScreen.KeyFilterButtonValue == "ALL"
                   && filters[0].SelectionRailAlpha == 1;
        });
        AddAssert("top navigation keeps the brand lockup proportional", () =>
            Math.Abs(songSelectScreen.TopNavigationHeight - 72) < 0.01f
            && songSelectScreen.TopNavigationLogoPosition
               == new Vector2(24, 7)
            && songSelectScreen.TopNavigationLogoSize
               == new Vector2(168, 57)
            && songSelectScreen.TopNavigationProfileSize
               == new Vector2(210, 46));
        AddAssert("browser starts below search, rating and browse controls", () =>
            Math.Abs(songSelectScreen.SongBrowserTop - 184) < 0.01f);
        AddAssert("difficulty filter defaults to all MSD charts", () =>
            songSelectScreen.MinimumDifficultyFilter == 0
            && songSelectScreen.DifficultyFilterUnit == "MSD RANGE"
            && songSelectScreen.DifficultyFilterSize
               == new Vector2(520, 40));
        AddAssert("browse controls use one compact row", () =>
        {
            SongSelectBrowseToolButton[] controls = songSelectScreen
                                                    .ChildrenOfType<SongSelectBrowseToolButton>()
                                                    .OrderBy(control => control.X)
                                                    .ToArray();
            return controls.Length == 4
                   && controls.All(control =>
                       Math.Abs(control.Height - 40) < 0.01f)
                   && controls.All(control =>
                       Math.Abs(control.BorderThickness - 1) < 0.01f
                       && Math.Abs(control.CornerRadius - 7) < 0.01f)
                   && controls.Select(control => control.Width)
                              .OrderBy(width => width)
                              .SequenceEqual([184, 184, 200, 200])
                   && controls.All(control => control.Interactive);
        });
        AddAssert("browse controls span one aligned rounded row", () =>
            songSelectScreen.BrowseToolbarSize == new Vector2(980, 40));
        AddAssert("selected details separate chart facts from performance", () =>
            songSelectScreen.SelectedChartFactsPosition
                == new Vector2(310, 210)
            && songSelectScreen.SelectedChartFactsSize
                == new Vector2(522, 34)
            && songSelectScreen.SelectedPerformancePosition
                == new Vector2(310, 255)
            && songSelectScreen.SelectedPerformanceSize
                == new Vector2(522, 35)
            && SongSelectScreen.SelectedDetailsPanelSize
                == new Vector2(850, 320)
            && SongSelectScreen.SelectedArtworkSize
                == new Vector2(280)
            && Math.Abs(SongSelectScreen.SelectedArtworkRotation + 1.25f)
                < 0.01f
            && Math.Abs(SongSelectScreen.RankingTop - 340) < 0.01f);
        AddAssert("selected mods aligns with the ranking header", () =>
            songSelectScreen.SelectedModsButtonPosition
                == new Vector2(696, 340)
            && songSelectScreen.SelectedModsButtonSize
                == new Vector2(154, 40));
        AddAssert("retired inline mod panel is not built on entry", () =>
            !songSelectScreen.LegacyInlineModPanelMaterialised);
        AddAssert("footer uses aligned left and right action clusters", () =>
            songSelectScreen.FooterBackPosition == new Vector2(24, 24)
            && songSelectScreen.AccountCardPosition == new Vector2(246, 24)
            && songSelectScreen.AccountCardSize == new Vector2(520, 82)
            && songSelectScreen.FooterToolDockPosition
                == new Vector2(-436, -24)
            && songSelectScreen.FooterToolDockSize == new Vector2(462, 82)
            && songSelectScreen.FooterToolShadowCount == 1
            && songSelectScreen
               .ChildrenOfType<SongSelectFooterToolButton>()
               .All(button => button.Size == new Vector2(154, 82))
            && songSelectScreen
               .ChildrenOfType<SongSelectModsToggleButton>()
               .Single().Size == new Vector2(154, 82));
        AddAssert("demo profile is cleared and pp is removed", () =>
            songSelectScreen.AccountDisplayName == "YOKKO DEMO"
            && songSelectScreen.AccountLevelText == "LV.114514"
            && songSelectScreen.AccountMetricLabels.SequenceEqual(
                new[] { "PLAYS", "ACC", "GLOBAL" })
            && songSelectScreen.AccountMetricValues.SequenceEqual(
                new[] { "0", "0.00%", "#0" })
            && !songSelectScreen.AccountMetricLabels.Contains("PP"));
        AddAssert("large ui scale has collision-safe footer geometry", () =>
            SongSelectScreen.FooterToolDockSizeFor(YokkoUiScale.Large)
                == new Vector2(378, 82)
            && SongSelectScreen.FooterToolButtonWidthFor(
                YokkoUiScale.Large) == 126
            && SongSelectScreen.FooterToolButtonStepFor(
                YokkoUiScale.Large) == 126
            && SongSelectScreen.FooterToolDockSizeFor(
                YokkoUiScale.Comfortable) == new Vector2(462, 82)
            && SongSelectScreen.FooterToolButtonWidthFor(
                YokkoUiScale.Comfortable) == 154);
        AddAssert("high-frequency shortcuts are discoverable", () =>
            songSelectScreen.ShortcutLegendSize == new Vector2(220, 106)
            && songSelectScreen.PlayButtonEyebrow
                == "START SELECTED CHART"
            && songSelectScreen.PlayButtonAction == "PLAY");
        AddStep("show chart preparation feedback", () => songSelectScreen
            .ChildrenOfType<SongSelectPlayButton>()
            .Single()
            .SetPreparing("PREPARING EXTERNAL CHART"));
        AddAssert("play action communicates loading", () =>
            songSelectScreen.PlayButtonEyebrow
                == "PREPARING EXTERNAL CHART"
            && songSelectScreen.PlayButtonAction == "LOADING...");
        AddStep("show recoverable chart load failure", () => songSelectScreen
            .ChildrenOfType<SongSelectPlayButton>()
            .Single()
            .SetError());
        AddAssert("failed play action offers retry", () =>
            songSelectScreen.PlayButtonEyebrow
                == "CHART COULD NOT LOAD"
            && songSelectScreen.PlayButtonAction == "RETRY");
        AddStep("restore ready play action", () => songSelectScreen
            .ChildrenOfType<SongSelectPlayButton>()
            .Single()
            .SetReady());

        AddStep("select next song", songSelectScreen.SelectNext);
        AddAssert("selection wraps", () => songSelectScreen.SelectedEntry.Beatmap.Title == "Imported Four");
        AddStep("end jumps to last song", () =>
            songSelectScreen.HandleBrowseKey(Key.End));
        AddAssert("end selected newest boundary", () =>
            songSelectScreen.SelectedEntry.Beatmap.Title == "Imported Seven");
        AddStep("home jumps to first song", () =>
            songSelectScreen.HandleBrowseKey(Key.Home));
        AddAssert("home selected oldest boundary", () =>
            songSelectScreen.SelectedEntry.Beatmap.Title == "Imported Four");
        AddStep("page down clamps at last song", () =>
            songSelectScreen.HandleBrowseKey(Key.PageDown));
        AddAssert("page jump reaches last song", () =>
            songSelectScreen.SelectedEntry.Beatmap.Title == "Imported Seven");
        AddAssert("search holds keyboard focus by default", () =>
            songSelectScreen.SearchHasFocus);
        AddAssert("plain R is reserved for search", () =>
            !songSelectScreen.HandleBrowseKey(Key.R)
            && songSelectScreen.SelectedEntry.Beatmap.Title
            == "Imported Seven");
        AddStep("random shortcut avoids current song", () =>
            songSelectScreen.HandleBrowseKey(Key.F2));
        AddAssert("random shortcut selected the other song", () =>
            songSelectScreen.SelectedEntry.Beatmap.Title == "Imported Four");
        AddStep("shift F2 rewinds random selection", () =>
            songSelectScreen.HandleBrowseKey(
                Key.F2,
                shiftPressed: true));
        AddAssert("random rewind restores the previous song", () =>
            songSelectScreen.SelectedEntry.Beatmap.Title == "Imported Seven");
        AddStep("control f focuses search", () =>
            songSelectScreen.HandleBrowseKey(Key.F, controlPressed: true));
        AddAssert("search receives keyboard focus", () =>
            songSelectScreen.SearchHasFocus);

        AddStep("filter 7K", () => songSelectScreen.SetKeyModeFilter(KeyMode.SevenKey));
        AddUntilStep("one 7K song visible", () =>
            !songSelectScreen.FilterPending
            && songSelectScreen.VisibleEntryCount == 1);
        AddAssert("selection follows filter", () => songSelectScreen.SelectedEntry.Beatmap.KeyMode == KeyMode.SevenKey);
        AddStep("remember sorted library before search", () =>
            sortPassesBeforeSearch = songSelectScreen.FilterSortPassCount);

        AddStep("search imported seven", () => songSelectScreen.SetSearchQuery("Imported Seven"));
        AddUntilStep("one matching song", () =>
            !songSelectScreen.FilterPending
            && songSelectScreen.VisibleEntryCount == 1);
        AddAssert("filter summary names active criteria", () =>
            songSelectScreen.FiltersButtonValue == "SEARCH · 7K");
        AddAssert("search reuses the existing sort order", () =>
            songSelectScreen.FilterSortPassCount == sortPassesBeforeSearch);

        AddStep("search no results", () => songSelectScreen.SetSearchQuery("not-a-real-song"));
        AddUntilStep("empty result is stable", () =>
            !songSelectScreen.FilterPending
            && songSelectScreen.VisibleEntryCount == 0);
        AddAssert("empty state explains active filters", () =>
            songSelectScreen.NoResultsVisible
            && songSelectScreen.NoResultsTitle == "NO SONGS MATCH"
            && songSelectScreen.NoResultsSummary.Contains("not-a-real-song")
            && songSelectScreen.NoResultsSummary.Contains("7K")
            && songSelectScreen.NoResultsResetVisible
            && songSelectScreen.NoResultsPrimaryAction == "CLEAR SEARCH"
            && songSelectScreen.NoResultsResetAllVisible
            && songSelectScreen.PlayButtonAction == "PLAY PREVIOUS");
        AddStep("clear only the search query", () =>
            songSelectScreen.ActivateNoResultsPrimary());
        AddUntilStep("key filter survives query recovery", () =>
            !songSelectScreen.FilterPending
            && songSelectScreen.SearchQuery.Length == 0
            && songSelectScreen.KeyModeFilter == KeyMode.SevenKey
            && songSelectScreen.VisibleEntryCount == 1);
        AddStep("clear browse filters", songSelectScreen.ClearBrowseFilters);
        AddUntilStep("clear restores the complete library", () =>
            !songSelectScreen.FilterPending
            && songSelectScreen.VisibleEntryCount == 2
            && songSelectScreen.SearchQuery.Length == 0
            && songSelectScreen.KeyModeFilter == null
            && songSelectScreen.MinimumDifficultyFilter == 0
            && songSelectScreen.ShowConverts
            && songSelectScreen.FiltersButtonValue == "ALL SONGS"
            && songSelectScreen.PlayButtonEyebrow
               == "START SELECTED CHART"
            && !songSelectScreen.NoResultsVisible);
        AddStep("new search supersedes queued search", () =>
        {
            songSelectScreen.SetSearchQuery("not-a-real-song");
            songSelectScreen.SetSearchQuery("Imported Four");
        });
        AddUntilStep("only latest search commits", () =>
            !songSelectScreen.FilterPending
            && songSelectScreen.VisibleEntryCount == 1
            && songSelectScreen.SelectedEntry.Beatmap.Title == "Imported Four");
        AddStep("clear supersession search", songSelectScreen.ClearBrowseFilters);
        AddUntilStep("complete library returns after supersession", () =>
            !songSelectScreen.FilterPending
            && songSelectScreen.VisibleEntryCount == 2);
        AddAssert("empty search is not dismissed", () => !songSelectScreen.DismissSearch());

        AddStep("open consolidated filters from keyboard", () =>
            songSelectScreen.HandleBrowseKey(Key.F6));
        AddUntilStep("filters popover owns focus", () =>
            songSelectScreen.FiltersPopoverOpen
            && !songSelectScreen.SearchHasFocus
            && songSelectScreen.FiltersPopoverFocusedControl == "GROUP");
        AddAssert("filters surface explains current criteria", () =>
            songSelectScreen.FiltersPopoverSummary == "CURRENT · ALL SONGS");
        AddStep("plain typing is isolated from search", () =>
            songSelectScreen.HandleBrowseKey(Key.F));
        AddAssert("filter overlay did not mutate search", () =>
            songSelectScreen.SearchQuery.Length == 0);
        AddStep("move filter focus right", () =>
            songSelectScreen.HandleBrowseKey(Key.Right));
        AddAssert("keyboard reaches converts option", () =>
            songSelectScreen.FiltersPopoverFocusedControl == "CONVERTS");
        AddStep("F6 closes filters", () =>
            songSelectScreen.HandleBrowseKey(Key.F6));
        AddAssert("filters close without exiting", () =>
            !songSelectScreen.FiltersPopoverOpen
            && screenStack.CurrentScreen == songSelectScreen);
        AddUntilStep("search focus returns after filters close", () =>
            songSelectScreen.SearchHasFocus);

        AddStep("open full sort menu", () => songSelectScreen
            .ChildrenOfType<SongSelectBrowseToolButton>()
            .Single(control => Math.Abs(control.X - 780) < 0.01f)
            .TriggerClick());
        AddAssert("sort menu exposes all modes", () =>
            songSelectScreen.SortPopoverOpen
            && !songSelectScreen.SearchHasFocus
            && songSelectScreen.ChildrenOfType<SongSelectSortOptionButton>().Count() == 8);
        AddStep("keyboard targets first sort option", () =>
            songSelectScreen.HandleBrowseKey(Key.Home));
        AddStep("keyboard applies focused sort option", () =>
            songSelectScreen.HandleBrowseKey(Key.Enter));
        AddUntilStep("keyboard sort selection applies", () =>
            songSelectScreen.SortMode == SongSelectSortMode.Title);
        AddStep("escape closes sort menu", songSelectScreen.HandleEscape);
        AddAssert("sort menu closed without exiting", () =>
            !songSelectScreen.SortPopoverOpen
            && screenStack.CurrentScreen == songSelectScreen);
        AddStep("remember selection before sorting", () =>
        {
            selectedBeforeSort = songSelectScreen.SelectedEntry;
            rebuildVersionBeforeSort = songSelectScreen.SongListRebuildVersion;
        });
        AddStep("sort by bpm", () =>
            songSelectScreen.SetSortMode(SongSelectSortMode.Bpm));
        AddUntilStep("new numeric mode defaults descending and keeps selection", () =>
            songSelectScreen.SortMode == SongSelectSortMode.Bpm
            && songSelectScreen.SortDirection == SongSelectSortDirection.Descending
            && songSelectScreen.SortButtonValue.Contains("BPM")
            && ReferenceEquals(songSelectScreen.SelectedEntry, selectedBeforeSort)
            && songSelectScreen.SongListRebuildVersion == rebuildVersionBeforeSort + 1);
        AddStep("reverse bpm sort", () =>
            songSelectScreen.SetSortDirection(SongSelectSortDirection.Ascending));
        AddUntilStep("direction reverses with one rebuild and no reselection", () =>
            songSelectScreen.SortDirection == SongSelectSortDirection.Ascending
            && ReferenceEquals(songSelectScreen.SelectedEntry, selectedBeforeSort)
            && songSelectScreen.SongListRebuildVersion == rebuildVersionBeforeSort + 2);

        AddAssert("personal scores shown by default", () =>
            songSelectScreen.ScoreView == SongSelectScoreView.Personal);
        AddAssert("empty personal history stays on the compact paper", () =>
            songSelectScreen.RankingContentLayerCount == 1
            && songSelectScreen.RankingEmptyStateVisible);
        var detailScore = new SongSelectScore(
            1,
            "MOCHI",
            "yokko",
            ScoreRank.S,
            987_654,
            0.9876,
            432,
            ["HD"],
            true,
            DateTimeOffset.UtcNow,
            100,
            20,
            4,
            2,
            1,
            0);
        AddStep("open personal score result", () =>
            songSelectScreen.ShowScoreResult(detailScore));
        AddAssert("result page shows the selected real result", () =>
            songSelectScreen.ScoreResultVisible
            && songSelectScreen.ResultScore == detailScore
            && !songSelectScreen.ResultReplayAvailable);
        AddStep("close score result", songSelectScreen.HandleEscape);
        AddUntilStep("score result closes smoothly", () =>
            !songSelectScreen.ScoreResultVisible);
    }

    [Test]
    public void TestBrowseOverlaysOwnKeyboardAndExplainEmptyResults()
    {
        SongSelectEntry selectionBeforeRandom = null;

        AddStep("load overlay interaction charts", () =>
        {
            importedChartLibrary.Clear();
            importedChartLibrary.AddOrReplace(
            [
                result("Overlay Four", DemoBeatmaps.CreateFourKeyDemo()),
                result("Overlay Seven", DemoBeatmaps.CreateSevenKeyDemo()),
            ],
            @"C:\Charts\overlay-interactions.osz");
            songSelectScreen.ClearBrowseFilters();
        });
        AddUntilStep("overlay charts are ready", () =>
            !songSelectScreen.FilterPending
            && songSelectScreen.VisibleEntryCount == 2);
        AddAssert("empty ranking is compact", () =>
            songSelectScreen.RankingPanelSize == new Vector2(850, 166)
            && songSelectScreen.RankingContentSize == new Vector2(850, 112));

        AddStep("open filters with F6", () => InputManager.Key(Key.F6));
        AddUntilStep("filters own keyboard focus", () =>
            songSelectScreen.FiltersPopoverOpen
            && !songSelectScreen.SearchHasFocus
            && songSelectScreen.FiltersPopoverFocusedControl == "GROUP");
        AddStep("type while filters are open", () => InputManager.Key(Key.F));
        AddAssert("typing never leaks into search", () =>
            songSelectScreen.SearchQuery.Length == 0);
        AddStep("move to converts", () => InputManager.Key(Key.Right));
        AddAssert("filter focus moves visibly", () =>
            songSelectScreen.FiltersPopoverFocusedControl == "CONVERTS");
        AddStep("close filters with F6", () => InputManager.Key(Key.F6));
        AddUntilStep("search focus returns", () =>
            !songSelectScreen.FiltersPopoverOpen
            && songSelectScreen.SearchHasFocus);

        AddStep("filter to no results", () =>
        {
            songSelectScreen.SetKeyModeFilter(KeyMode.SevenKey);
            songSelectScreen.SetSearchQuery("not-an-overlay-chart");
        });
        AddUntilStep("no-result context settles", () =>
            !songSelectScreen.FilterPending
            && songSelectScreen.VisibleEntryCount == 0);
        AddAssert("criteria and ambient play are explicit", () =>
            songSelectScreen.FiltersButtonValue == "SEARCH · 7K"
            && songSelectScreen.NoResultsPrimaryAction == "CLEAR SEARCH"
            && songSelectScreen.NoResultsResetAllVisible
            && songSelectScreen.PlayButtonAction == "PLAY PREVIOUS");
        AddStep("clear only overlay search", () =>
            songSelectScreen.ActivateNoResultsPrimary());
        AddUntilStep("overlay key filter remains active", () =>
            !songSelectScreen.FilterPending
            && songSelectScreen.SearchQuery.Length == 0
            && songSelectScreen.KeyModeFilter == KeyMode.SevenKey
            && songSelectScreen.VisibleEntryCount == 1);

        AddStep("clear browse context", songSelectScreen.ClearBrowseFilters);
        AddUntilStep("normal play context returns", () =>
            !songSelectScreen.FilterPending
            && songSelectScreen.VisibleEntryCount == 2
            && songSelectScreen.FiltersButtonValue == "ALL SONGS"
            && songSelectScreen.PlayButtonEyebrow
               == "START SELECTED CHART");
        AddStep("remember selection before random", () =>
            selectionBeforeRandom = songSelectScreen.SelectedEntry);
        AddStep("F2 selects the next random chart", () =>
            InputManager.Key(Key.F2));
        AddAssert("random avoids the current chart", () =>
            !ReferenceEquals(
                selectionBeforeRandom,
                songSelectScreen.SelectedEntry));
        AddStep("shift F2 rewinds random history", () =>
        {
            InputManager.PressKey(Key.ShiftLeft);
            InputManager.Key(Key.F2);
            InputManager.ReleaseKey(Key.ShiftLeft);
        });
        AddAssert("rewind restores the previous chart", () =>
            ReferenceEquals(
                selectionBeforeRandom,
                songSelectScreen.SelectedEntry));

        AddStep("open sort menu", () => songSelectScreen
            .ChildrenOfType<SongSelectBrowseToolButton>()
            .Single(control => Math.Abs(control.X - 780) < 0.01f)
            .TriggerClick());
        AddUntilStep("sort menu owns keyboard", () =>
            songSelectScreen.SortPopoverOpen
            && !songSelectScreen.SearchHasFocus);
        AddStep("focus title sort", () => InputManager.Key(Key.Home));
        AddStep("apply title sort", () => InputManager.Key(Key.Enter));
        AddUntilStep("keyboard sort applies", () =>
            songSelectScreen.SortMode == SongSelectSortMode.Title);
        AddStep("close sort menu", () => InputManager.Key(Key.Escape));
        AddUntilStep("sort closes and search focus returns", () =>
            !songSelectScreen.SortPopoverOpen
            && songSelectScreen.SearchHasFocus);
        AddStep("F1 opens mods like osu", () => InputManager.Key(Key.F1));
        AddUntilStep("mods screen is current", () =>
            screenStack.CurrentScreen is GameplayModsScreen);
        AddStep("return from mods", () => screenStack.CurrentScreen.Exit());
        AddUntilStep("song select resumes after mods", () =>
            screenStack.CurrentScreen == songSelectScreen);
        AddStep("select a mod", () =>
            songSelectScreen.ToggleMod(ManiaModId.NoFail));
        AddAssert("mod is selected", () =>
            songSelectScreen.SelectedMods.Contains(ManiaModId.NoFail));
        AddStep("empty-search backspace clears mods", () =>
            InputManager.Key(Key.BackSpace));
        AddAssert("backspace follows osu deselect-all behaviour", () =>
            songSelectScreen.SelectedMods.Equals(ManiaModSet.Empty));
    }

    [Test]
    public void TestF3OpensBeatmapOptions()
    {
        double scrollSpeedBeforeSettings = 0;
        AddStep("remember scroll speed", () =>
            scrollSpeedBeforeSettings = gameplaySettings.ScrollSpeed.Value);
        AddStep("open options with osu F3 shortcut", () =>
            InputManager.Key(Key.F3));
        AddUntilStep("beatmap options are open", () =>
            songSelectScreen.BeatmapOptionsOpen
            && songSelectScreen.BeatmapOptionsTitle.Length > 0
            && !songSelectScreen.SearchHasFocus);
        AddAssert("F3 did not change gameplay scroll speed", () =>
            gameplaySettings.ScrollSpeed.Value == scrollSpeedBeforeSettings);
        AddStep("focus manage library action", () =>
            InputManager.Key(Key.Down));
        AddStep("open chart library from beatmap options", () =>
            InputManager.Key(Key.Enter));
        AddUntilStep("chart library is current", () =>
            screenStack.CurrentScreen is ChartLibraryScreen);
        AddStep("return from chart library", () =>
            screenStack.CurrentScreen.Exit());
        AddUntilStep("song select resumes from chart library", () =>
            screenStack.CurrentScreen == songSelectScreen);
        AddStep("reopen beatmap options", () =>
            InputManager.Key(Key.F3));
        AddUntilStep("beatmap options reopen", () =>
            songSelectScreen.BeatmapOptionsOpen);
        AddStep("close beatmap options with F3", () =>
            InputManager.Key(Key.F3));
        AddUntilStep("options close and search focus returns", () =>
            !songSelectScreen.BeatmapOptionsOpen
            && songSelectScreen.SearchHasFocus);
    }

    [Test]
    public void TestScoreResultReplayStarts()
    {
        string replayPath = Path.Combine(
            Path.GetTempPath(),
            $"song-select-score-{Guid.NewGuid():N}.ykr");
        SongSelectScore replayScore = null;

        AddStep("load replay chart", () =>
        {
            importedChartLibrary.Clear();
            importedChartLibrary.AddOrReplace(
                [result("Replay Chart", DemoBeatmaps.CreateFourKeyDemo())],
                @"C:\Charts\replay-chart.osu");
        });
        AddUntilStep("replay chart selected", () =>
            songSelectScreen.SelectedEntry?.Beatmap.Title == "Replay Chart");
        AddStep("write matching replay", () =>
        {
            YokkoBeatmap beatmap = songSelectScreen.SelectedEntry.Beatmap;
            YokkoReplayIO.WriteToFile(
                replayPath,
                beatmap,
                beatmap,
                new GameplayReplay(
                [
                    new GameplayReplayInput(0, true, 100),
                    new GameplayReplayInput(0, false, 140),
                ]));
            replayScore = new SongSelectScore(
                1,
                "MOCHI",
                "yokko",
                ScoreRank.S,
                900_000,
                0.95,
                120,
                [],
                true,
                DateTimeOffset.UtcNow,
                ReplayPath: replayPath);
        });
        AddStep("open replay score result", () =>
            songSelectScreen.ShowScoreResult(replayScore));
        AddAssert("matching replay is available", () =>
            songSelectScreen.ResultReplayAvailable);
        AddStep("watch replay", songSelectScreen.ActivateResultReplay);
        AddUntilStep("score replay enters gameplay", () =>
            screenStack.CurrentScreen is GameplaySessionScreen session
            && session.CurrentGameplay?.ReplayMode == true);
        AddStep("return from score replay", () =>
            ((GameplaySessionScreen)screenStack.CurrentScreen)
            .CurrentGameplay.Exit());
        AddUntilStep("song select resumes after score replay", () =>
            screenStack.CurrentScreen == songSelectScreen);
        AddAssert("score result remains after replay", () =>
            songSelectScreen.ScoreResultVisible);
        AddStep("close score result", songSelectScreen.HandleEscape);
        AddUntilStep("score result closes", () =>
            !songSelectScreen.ScoreResultVisible);
        AddStep("remove test replay", () => File.Delete(replayPath));
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
        bool hardRockWasEnabled = false;
        AddStep("change mods while difficulty filter is active", () =>
        {
            hardRockWasEnabled = songSelectScreen.SelectedMods.Contains(
                ManiaModId.HardRock);
            songSelectScreen.ToggleMod(ManiaModId.HardRock);
        });
        AddUntilStep("modded difficulty filter settles in background", () =>
            !songSelectScreen.FilterPending
            && songSelectScreen.VisibleEntryCount == 0);
        AddAssert("difficulty filter uses the changed mods", () =>
            songSelectScreen.SelectedMods.Contains(ManiaModId.HardRock)
                != hardRockWasEnabled);
        AddStep("restore difficulty mods", () =>
            songSelectScreen.ToggleMod(ManiaModId.HardRock));
        AddUntilStep("restored difficulty filter settles", () =>
            !songSelectScreen.FilterPending
            && songSelectScreen.VisibleEntryCount == 0
            && songSelectScreen.SelectedMods.Contains(ManiaModId.HardRock)
                == hardRockWasEnabled);
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
        GameplayPlaybackRateOverlay rateOverlay = null;

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
            == beatmap.Title
            && !songSelectScreen.FilterPending
            && songSelectScreen.ChildrenOfType<SongSelectSongRow>().Any(row =>
                row.Entry.Beatmap.Title == beatmap.Title));
        AddStep("capture current list row", () =>
            originalRow = songSelectScreen
                .ChildrenOfType<SongSelectSongRow>()
                .Single(row =>
                    row.Entry.Beatmap.Title == beatmap.Title));
        AddAssert("details start at normal rate", () =>
            songSelectScreen.SelectedMods.PlaybackRate == 1
            && songSelectScreen.DisplayedPlaybackRate == 1
            && songSelectScreen.DisplayedBpm == "120"
            && songSelectScreen.ChildrenOfType<SpriteText>().Any(text =>
                text.Text.ToString() == "ALT +/-"));
        AddStep("capture playback rate hint", () =>
            rateOverlay = songSelectScreen
                .ChildrenOfType<GameplayPlaybackRateOverlay>()
                .Single());
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
               == expectedFastRating.Value
            && rateOverlay.IsVisible
            && Math.Abs(rateOverlay.DisplayedRate - 1.05) < 0.000001
            && Math.Abs(rateOverlay.DisplayedBpm - 126) < 0.000001);
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
    public void TestScrollSpeedShortcutShowsCurrentValue()
    {
        double originalSpeed = 0;
        double originalRate = 0;
        ScrollSpeedAdjustmentMode originalMode = default;
        GameplayScrollSpeedOverlay speedOverlay = null;

        AddStep("prepare scroll speed", () =>
        {
            originalSpeed = gameplaySettings.ScrollSpeed.Value;
            originalRate = songSelectScreen.SelectedMods.PlaybackRate;
            originalMode = gameplaySettings.ScrollSpeedAdjustmentMode.Value;
            gameplaySettings.SetScrollSpeed(8);
            gameplaySettings.ScrollSpeedAdjustmentMode.Value =
                ScrollSpeedAdjustmentMode.OsuManiaScale;
            speedOverlay = songSelectScreen
                .ChildrenOfType<GameplayScrollSpeedOverlay>()
                .Single();
        });
        AddAssert("plain plus is ignored for scroll speed", () =>
            !songSelectScreen.HandleScrollSpeedShortcut(Key.Plus, false)
            && gameplaySettings.ScrollSpeed.Value == 8);
        AddStep("control plus increases scroll speed", () =>
            songSelectScreen.HandleScrollSpeedShortcut(Key.Plus, true));
        AddAssert("scroll speed hint shows current value", () =>
            gameplaySettings.ScrollSpeed.Value == 9
            && speedOverlay.Alpha > 0
            && speedOverlay.DisplayedSpeed == 9
            && speedOverlay.DisplayedTimeRangeMilliseconds
               == (int)Math.Round(OsuManiaScrollSpeed.ComputeScrollTime(9))
            && speedOverlay.DisplayedLabel == "SCROLL SPEED"
            && songSelectScreen.SelectedMods.PlaybackRate == originalRate);
        AddStep("bound decrease key restores scroll speed", () =>
            songSelectScreen.HandleScrollSpeedShortcut(
                gameplaySettings.DecreaseScrollSpeedKey.Value,
                false));
        AddAssert("bound key shows restored scroll speed", () =>
            gameplaySettings.ScrollSpeed.Value == 8
            && speedOverlay.DisplayedSpeed == 8);
        AddStep("restore scroll speed", () =>
        {
            gameplaySettings.SetScrollSpeed(originalSpeed);
            gameplaySettings.ScrollSpeedAdjustmentMode.Value = originalMode;
        });
    }

    [Test]
    public void TestFocusedPackageExpansionFollowsKeyboardSelection()
    {
        const string firstPackage = @"C:\Charts\focus-one.osz";
        const string secondPackage = @"C:\Charts\focus-two.osz";
        int rebuildVersionBeforeSearch = 0;

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
        AddStep("remember list before search", () =>
            rebuildVersionBeforeSearch = songSelectScreen.SongListRebuildVersion);
        AddStep("search into the collapsed package", () =>
            songSelectScreen.SetSearchQuery("Focus Two"));
        AddUntilStep("search keeps one focused package", () =>
            !songSelectScreen.FilterPending
            && songSelectScreen.VisibleEntryCount == 2
            && songSelectScreen.SelectedEntry?.PackageId == secondPackage
            && songSelectScreen.IsPackageCollapsed(firstPackage)
            && !songSelectScreen.IsPackageCollapsed(secondPackage)
            && songSelectScreen.IndexedSongListItemCount == 3
            && songSelectScreen.NavigableEntryCount == 2);
        AddAssert("search commits one list rebuild", () =>
            songSelectScreen.SongListRebuildVersion
                == rebuildVersionBeforeSearch + 1);
        AddStep("clear package search", () =>
            songSelectScreen.SetSearchQuery(string.Empty));
        AddUntilStep("focused expansion survives cleared search", () =>
            !songSelectScreen.FilterPending
            && songSelectScreen.VisibleEntryCount == 4
            && songSelectScreen.SelectedEntry?.PackageId == secondPackage
            && songSelectScreen.IsPackageCollapsed(firstPackage)
            && !songSelectScreen.IsPackageCollapsed(secondPackage)
            && songSelectScreen.IndexedSongListItemCount == 4);
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
        AddUntilStep("song list settles before mods", () =>
            !songSelectScreen.FilterPending);
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
            && !songSelectScreen.SelectedMods.MutedAffectsHitSounds);
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
            && !songSelectScreen.SelectedMods.TimeRampAdjustPitch);
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
            && !songSelectScreen.SelectedMods.Contains(
                ManiaModId.DoubleTime));
        AddStep("configure HT like lazer", () =>
        {
            songSelectScreen.SetFixedRateSpeedChange(0.80);
            songSelectScreen.SetFixedRateAdjustPitch(true);
        });
        AddAssert("HT rate and pitch settings are reflected", () =>
            songSelectScreen.SelectedMods.PlaybackRate == 0.80
            && songSelectScreen.SelectedMods.FixedRateAdjustPitch);
        AddStep("replace HT with DC", () =>
            songSelectScreen.ToggleMod(ManiaModId.Daycore));
        AddStep("configure DC speed", () =>
            songSelectScreen.SetFixedRateSpeedChange(0.60));
        AddAssert("DC keeps lazer fixed frequency", () =>
            songSelectScreen.SelectedMods.Contains(
                ManiaModId.Daycore)
            && songSelectScreen.SelectedMods.PlaybackRate == 0.60
            && songSelectScreen.SelectedMods.FixedAudioFrequencyScale
               == 0.75);
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
            && !songSelectScreen.SelectedMods.AdaptiveAdjustPitch);
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
                "perfect:require-perfect"));
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
                                .DifficultyAdjustExtendedLimits);
        AddUntilStep("selected gameplay is GPU-ready before play", () =>
            songSelectScreen.GameplayPreloadReady);
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
    public void TestScoreRefreshPreservesEntryIdentity()
    {
        SongSelectEntry selectedBeforeRefresh = null;

        AddStep("seed score refresh chart", () =>
            importedChartLibrary.AddOrReplace(
                result(
                    "Score Refresh Identity",
                    DemoBeatmaps.CreateFourKeyDemo()),
                @"C:\Charts\score-refresh.osu"));
        AddUntilStep("selected entry is ready", () =>
            songSelectScreen.SelectedEntry?.Beatmap.Title
                == "Score Refresh Identity");
        AddStep("remember selected entry", () =>
            selectedBeforeRefresh = songSelectScreen.SelectedEntry);
        AddStep("refresh imported replay scores", () =>
            songSelectScreen.RefreshImportedReplayScores(
                songSelectScreen.SelectedEntry.ChartId));
        AddUntilStep("score refresh settles", () =>
            !songSelectScreen.FilterPending);
        AddAssert("score refresh keeps entry identity", () =>
            ReferenceEquals(
                selectedBeforeRefresh,
                songSelectScreen.SelectedEntry));
    }

    [Test]
    public void TestReplayImportRefreshesItsChartWithoutChangingSelection()
    {
        SongSelectEntry replayTarget = null;
        SongSelectEntry selectedBeforeRefresh = null;
        string replayTargetChartId = null;
        string externalScoreId = "test:" + Guid.NewGuid().ToString("N");

        AddStep("seed replay target and selected chart", () =>
        {
            importedChartLibrary.AddOrReplace(
                result(
                    "Background Replay Target",
                    DemoBeatmaps.CreateFourKeyDemo()),
                @"C:\Charts\background-replay-target.osu");
            importedChartLibrary.AddOrReplace(
                result(
                    "Foreground Selection",
                    DemoBeatmaps.CreateSevenKeyDemo()),
                @"C:\Charts\foreground-selection.osu");
            replayTargetChartId = importedChartLibrary.GetSnapshot()
                .Charts.Single(chart =>
                    chart.Result.Beatmap.Title
                        == "Background Replay Target")
                .Id;
        });
        AddUntilStep("foreground chart remains selected", () =>
            songSelectScreen.SelectedEntry?.Beatmap.Title
                == "Foreground Selection");
        AddStep("remember replay target and selection", () =>
        {
            replayTarget = songSelectScreen.ImportedEntryForTest(
                replayTargetChartId);
            selectedBeforeRefresh = songSelectScreen.SelectedEntry;
        });
        AddAssert("background replay target was found", () =>
            replayTarget?.Beatmap.Title == "Background Replay Target");
        AddStep("import score for background chart", () =>
            scoreStore.ImportExternalScore(
                replayTarget.Beatmap,
                ManiaModSet.Empty,
                JudgementConfiguration.YokkoDefault,
                new ManiaScoreResult(
                    900_000,
                    0.95,
                    120,
                    ScoreRank.S,
                    100,
                    10,
                    2,
                    1,
                    0,
                    0),
                "REMOTE PLAYER",
                null,
                "test",
                externalScoreId,
                null));
        AddStep("refresh exact replay target", () =>
            songSelectScreen.RefreshImportedReplayScores(
                replayTarget.ChartId));
        AddUntilStep("targeted score refresh settles", () =>
            !songSelectScreen.FilterPending);
        AddAssert("background chart receives replay score", () =>
            replayTarget.Ranking.Any(score =>
                score.Score == 900_000));
        AddAssert("targeted refresh preserves current selection", () =>
            ReferenceEquals(
                selectedBeforeRefresh,
                songSelectScreen.SelectedEntry));
    }

    [Test]
    public void TestResumeRefreshesFallbackAfterSelectedChartRemoval()
    {
        YokkoBeatmap replacement = DemoBeatmaps.CreateFourKeyDemo() with
        {
            Title = "Resume Fallback",
        };

        AddStep("seed chart that will be removed", () =>
            importedChartLibrary.AddOrReplace(
                result(
                    "Removed While Suspended",
                    DemoBeatmaps.CreateSevenKeyDemo()),
                @"C:\Charts\removed-while-suspended.osu"));
        AddUntilStep("removable chart is selected", () =>
            songSelectScreen.SelectedEntry?.Beatmap.Title
                == "Removed While Suspended");
        AddStep("open settings above song select", () =>
            screenStack.Push(new SettingsScreen()));
        AddUntilStep("song select is suspended", () =>
            screenStack.CurrentScreen is SettingsScreen);
        AddStep("replace library and seed fallback score", () =>
        {
            importedChartLibrary.Clear();
            scoreStore.SaveBest(
                replacement,
                ManiaModSet.Empty,
                JudgementConfiguration.YokkoDefault,
                new ManiaScoreResult(
                    850_000,
                    0.9,
                    100,
                    ScoreRank.A,
                    80,
                    15,
                    3,
                    1,
                    1,
                    0));
            importedChartLibrary.AddOrReplace(
                result("Resume Fallback", replacement),
                @"C:\Charts\resume-fallback.osu");
        });
        AddStep("return to song select", () =>
            screenStack.CurrentScreen.Exit());
        AddUntilStep("fallback chart is selected", () =>
            screenStack.CurrentScreen == songSelectScreen
            && songSelectScreen.SelectedEntry?.Beatmap.Title
                == "Resume Fallback");
        AddAssert("fallback score refreshes on resume", () =>
            songSelectScreen.SelectedEntry.History.Any(score =>
                score.Score == 850_000));
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
            songSelectScreen.SelectedEntry.Beatmap.ConversionSource != null);
        AddStep("select 4K conversion", () =>
            songSelectScreen.ToggleMod(ManiaModId.Key4));
        AddAssert("4K target is reflected", () =>
            songSelectScreen.SelectedMods.KeyConversionTarget == 4);
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
