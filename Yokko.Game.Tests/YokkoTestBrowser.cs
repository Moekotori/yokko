using System;
using System.IO;
using System.Linq;
using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osuTK;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Core.Timing;
using Yokko.Game.Gameplay;
using Yokko.Game.Importing;
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.ChartLibrary;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.SongSelect;
using Yokko.Game.Tests.Development;
using Yokko.Game.Tests.Visual;
using Yokko.Import;

namespace Yokko.Game.Tests
{
    public partial class YokkoTestBrowser : YokkoGameBase
    {
        private static readonly string[] previewEnvironmentVariables =
        [
            "YOKKO_PREVIEW_1080P",
            "YOKKO_LAYOUT_EDITOR_PREVIEW",
            "YOKKO_LAYOUT_EDITOR_AUTOPLAY_PREVIEW",
            "YOKKO_TIMING_BAR_PREVIEW",
            "YOKKO_SCROLL_SPEED_PREVIEW",
            "YOKKO_OSU_MANIA_SKIN_PREVIEW",
            "YOKKO_MODS_PREVIEW",
            "YOKKO_MAIN_PREVIEW",
            "YOKKO_CHART_LIBRARY_PREVIEW",
            "YOKKO_SONGSELECT_PREVIEW",
            "YOKKO_RESULT_PREVIEW",
            "YOKKO_PAUSE_PREVIEW",
            "YOKKO_EDITOR_PREVIEW",
            "YOKKO_SETTINGS_PREVIEW",
            "YOKKO_UI_LAB_PREVIEW",
        ];

        [Resolved]
        private IRenderer renderer { get; set; }

        [Resolved]
        private GameHost host { get; set; }

        private FrameworkConfigManager frameworkConfig;
        private YokkoThemeFileHotReload themeHotReload;

        [BackgroundDependencyLoader]
        private void load(FrameworkConfigManager frameworkConfig)
        {
            this.frameworkConfig = frameworkConfig;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (isPreviewRun())
                configurePreviewViewport();

            configureThemeHotReload();

            if (Environment.GetEnvironmentVariable(
                    "YOKKO_UI_LAB_PREVIEW") == "1")
            {
                frameworkConfig.SetValue(
                    FrameworkSetting.Locale,
                    Environment.GetEnvironmentVariable(
                        "YOKKO_PREVIEW_LOCALE")
                    is { Length: > 0 } labLocale
                        ? labLocale
                        : YokkoLocale.English);
                Add(new TestSceneYokkoUiLab
                {
                    RelativeSizeAxes = Axes.Both,
                });
                Add(new CursorContainer());
                schedulePreviewScreenshot();
                return;
            }

            if (Environment.GetEnvironmentVariable(
                    "YOKKO_LAYOUT_EDITOR_PREVIEW") == "1")
            {
                frameworkConfig.SetValue(
                    FrameworkSetting.WindowMode,
                    WindowMode.Windowed);
                frameworkConfig.SetValue(
                    FrameworkSetting.WindowedSize,
                    GetPreviewWindowSize());
                frameworkConfig.SetValue(
                    FrameworkSetting.Locale,
                    YokkoLocale.Chinese);
                string layoutSkinPath = Environment.GetEnvironmentVariable(
                    "YOKKO_LAYOUT_EDITOR_SKIN_SAMPLE");
                var gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo(),
                    skinPath: string.IsNullOrWhiteSpace(layoutSkinPath)
                        ? null
                        : layoutSkinPath);
                Add(new ScreenStack(gameplay)
                {
                    RelativeSizeAxes = Axes.Both,
                });
                Add(new CursorContainer());

                Action openEditorWhenReady = null;
                openEditorWhenReady = () =>
                {
                    if (!gameplay.IsPaused)
                        gameplay.TogglePause();

                    GameplayPauseOverlay pauseOverlay = gameplay
                        .ChildrenOfType<GameplayPauseOverlay>()
                        .SingleOrDefault();
                    if (pauseOverlay == null)
                    {
                        Scheduler.AddDelayed(openEditorWhenReady, 200);
                        return;
                    }

                    pauseOverlay.SelectNext();
                    pauseOverlay.SelectNext();
                    pauseOverlay.TriggerSelected();

                    GameplayLayoutEditorOverlay editor = gameplay
                        .ChildrenOfType<GameplayLayoutEditorOverlay>()
                        .Single();
                    gameplay.SetLayoutEditorLongNoteCutEnabledForTest(true);
                    gameplay.SetLayoutEditorLongNoteCutAmountForTest(1.2);
                    editor.MoveTimingBarForTest(new Vector2(70, -58));
                    editor.ResizeTimingBarForTest(new Vector2(58, 20));
                    if (!string.IsNullOrWhiteSpace(layoutSkinPath))
                    {
                        editor.MoveComboForTest(new Vector2(-150, 90));
                        editor.ResizeJudgementForTest(
                            new Vector2(64, 28));
                    }
                    if (Environment.GetEnvironmentVariable(
                            "YOKKO_LAYOUT_EDITOR_COVER_PREVIEW") == "1")
                    {
                        editor.SetTopCoverEnabledForTest(true);
                        editor.SetTopCoverHeightForTest(260);
                    }
                    if (Environment.GetEnvironmentVariable(
                            "YOKKO_LAYOUT_EDITOR_AUTOPLAY_PREVIEW") == "1")
                    {
                        gameplay.ResumeCountdownMillisecondsOverride = 0;
                        gameplay.BeginLayoutAutoplayDemoForTest();
                        schedulePreviewScreenshot(1400);
                    }
                    else
                        schedulePreviewScreenshot(900);
                };
                Scheduler.AddDelayed(openEditorWhenReady, 500);
                return;
            }

            if (Environment.GetEnvironmentVariable(
                    "YOKKO_TIMING_BAR_PREVIEW") == "1")
            {
                frameworkConfig.SetValue(
                    FrameworkSetting.Locale,
                    YokkoLocale.Chinese);
                var gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo());
                Add(new ScreenStack(gameplay)
                {
                    RelativeSizeAxes = Axes.Both,
                });
                Add(new CursorContainer());
                Scheduler.AddDelayed(() =>
                {
                    GameplayTimingBar timingBar = gameplay
                        .ChildrenOfType<GameplayTimingBar>()
                        .Single();
                    (double Error, JudgementRating Rating)[] presses =
                    [
                        (-82, JudgementRating.Good),
                        (-31, JudgementRating.Great),
                        (-8, JudgementRating.Perfect),
                        (11, JudgementRating.Perfect),
                        (46, JudgementRating.Great),
                        (95, JudgementRating.Ok),
                    ];
                    for (int i = 0; i < presses.Length; i++)
                    {
                        timingBar.Show(new JudgementInputEvent(
                            i,
                            i % 4,
                            1000 + i * 100,
                            1000 + i * 100 + presses[i].Error,
                            presses[i].Error,
                            presses[i].Rating,
                            JudgementPhase.Tap));
                    }

                    timingBar.Show(new JudgementInputEvent(
                        presses.Length,
                        2,
                        1800,
                        1825,
                        25,
                        JudgementRating.Perfect,
                        JudgementPhase.HoldTail,
                        BeatmapJudgementState.HoldReleaseWindowLenience));
                }, 300);
                schedulePreviewScreenshot(700);
                return;
            }

            if (Environment.GetEnvironmentVariable(
                    "YOKKO_SCROLL_SPEED_PREVIEW") == "1")
            {
                var gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo());
                Add(new ScreenStack(gameplay)
                {
                    RelativeSizeAxes = Axes.Both,
                });
                Add(new CursorContainer());
                Scheduler.AddDelayed(() =>
                    gameplay
                        .ChildrenOfType<GameplayScrollSpeedOverlay>()
                        .Single()
                        .Show(20, 600), 300);
                schedulePreviewScreenshot(700);
                return;
            }

            if (Environment.GetEnvironmentVariable(
                    "YOKKO_OSU_MANIA_SKIN_PREVIEW") == "1")
            {
                string skinPath = Environment.GetEnvironmentVariable(
                    "YOKKO_OSU_MANIA_SKIN_SAMPLE");
                if (string.IsNullOrWhiteSpace(skinPath)
                    || !File.Exists(skinPath)
                       && !Directory.Exists(skinPath))
                {
                    throw new InvalidOperationException(
                        "YOKKO_OSU_MANIA_SKIN_SAMPLE must point to a skin package or folder.");
                }

                bool sevenKey = Environment.GetEnvironmentVariable(
                    "YOKKO_OSU_MANIA_SKIN_SAMPLE_KEYS") == "7";
                var gameplay = new GameplayScreen(
                    sevenKey
                        ? DemoBeatmaps.CreateSevenKeyDemo()
                        : DemoBeatmaps.CreateFourKeyDemo(),
                    skinPath: skinPath);
                Add(new ScreenStack(gameplay)
                {
                    RelativeSizeAxes = Axes.Both,
                });
                Add(new CursorContainer());
                Scheduler.AddDelayed(() =>
                {
                    if (gameplay.IsPaused
                        && !gameplay.PauseTransitionInProgress)
                    {
                        gameplay.TogglePause();
                    }
                }, 500);
                schedulePreviewScreenshot(800);
                return;
            }

            if (Environment.GetEnvironmentVariable(
                    "YOKKO_MODS_PREVIEW") == "1")
            {
                frameworkConfig.SetValue(
                    FrameworkSetting.Locale,
                    Environment.GetEnvironmentVariable(
                        "YOKKO_PREVIEW_LOCALE")
                    is { Length: > 0 } modsLocale
                        ? modsLocale
                        : YokkoLocale.English);
                if (Enum.TryParse(
                        Environment.GetEnvironmentVariable(
                            "YOKKO_PREVIEW_UI_SCALE"),
                        true,
                        out YokkoUiScale previewScale))
                {
                    DisplaySettings.UiScale.Value = previewScale;
                }

                ManiaModSet mods = ManiaModSet.Empty
                    .With(ManiaModId.HalfTime, true)
                    .With(ManiaModId.Hidden, true);
                var modsScreen = new GameplayModsScreen(
                    DemoBeatmaps.CreateFourKeyDemo(),
                    mods,
                    _ => { });
                Add(new ScreenStack(modsScreen)
                {
                    RelativeSizeAxes = Axes.Both,
                });
                Add(new CursorContainer());
                Scheduler.AddDelayed(
                    () => applyModsPreviewState(modsScreen),
                    250);
                schedulePreviewScreenshot();
                return;
            }

            if (Environment.GetEnvironmentVariable(
                    "YOKKO_MAIN_PREVIEW") == "1")
            {
                frameworkConfig.SetValue(
                    FrameworkSetting.WindowMode,
                    WindowMode.Windowed);
                frameworkConfig.SetValue(
                    FrameworkSetting.WindowedSize,
                    new System.Drawing.Size(1920, 1080));
                if (Environment.GetEnvironmentVariable(
                        "YOKKO_PREVIEW_LOCALE")
                    is { Length: > 0 } previewLocale)
                {
                    frameworkConfig.SetValue(
                        FrameworkSetting.Locale,
                        previewLocale);
                }

                Add(new ScreenStack(
                        new Screens.Main.MainScreen(host.Exit))
                {
                    RelativeSizeAxes = Axes.Both,
                });
                Add(new CursorContainer());
                schedulePreviewScreenshot(1500);
                return;
            }

            if (Environment.GetEnvironmentVariable(
                    "YOKKO_CHART_LIBRARY_PREVIEW") == "1")
            {
                seedSongSelectPreview();
                frameworkConfig.SetValue(
                    FrameworkSetting.WindowMode,
                    WindowMode.Windowed);
                frameworkConfig.SetValue(
                    FrameworkSetting.WindowedSize,
                    GetPreviewWindowSize());
                frameworkConfig.SetValue(
                    FrameworkSetting.Locale,
                    Environment.GetEnvironmentVariable(
                        "YOKKO_PREVIEW_LOCALE")
                    is { Length: > 0 } libraryLocale
                        ? libraryLocale
                        : YokkoLocale.Chinese);
                if (Enum.TryParse(
                        Environment.GetEnvironmentVariable(
                            "YOKKO_PREVIEW_UI_SCALE"),
                        true,
                        out YokkoUiScale libraryScale))
                {
                    DisplaySettings.UiScale.Value = libraryScale;
                }

                Add(new ScreenStack(new ChartLibraryScreen())
                {
                    RelativeSizeAxes = Axes.Both,
                });
                Add(new CursorContainer());
                schedulePreviewScreenshot(1200);
                return;
            }

            if (Environment.GetEnvironmentVariable(
                    "YOKKO_SONGSELECT_PREVIEW") == "1")
            {
                seedSongSelectPreview();
                if (Environment.GetEnvironmentVariable(
                        "YOKKO_SONGSELECT_SCORE_PREVIEW") == "1")
                {
                    seedSongSelectScorePreview();
                }
                frameworkConfig.SetValue(
                    FrameworkSetting.WindowMode,
                    WindowMode.Windowed);
                frameworkConfig.SetValue(
                    FrameworkSetting.WindowedSize,
                    GetPreviewWindowSize());
                frameworkConfig.SetValue(
                    FrameworkSetting.Locale,
                    Environment.GetEnvironmentVariable(
                        "YOKKO_PREVIEW_LOCALE")
                    is { Length: > 0 } songSelectLocale
                        ? songSelectLocale
                        : YokkoLocale.English);
                var songSelect = new SongSelectScreen(
                    new Yokko.Audio.NullAudioEngine());
                Add(new ScreenStack(songSelect)
                {
                    RelativeSizeAxes = Axes.Both,
                });
                Add(new CursorContainer());
                double packageToggleDelay = double.TryParse(
                        Environment.GetEnvironmentVariable(
                            "YOKKO_SONGSELECT_TOGGLE_DELAY_MS"),
                        out double configuredToggleDelay)
                    ? Math.Max(0, configuredToggleDelay)
                    : 350;
                string packageToToggle = Environment.GetEnvironmentVariable(
                        "YOKKO_SONGSELECT_TOGGLE_PACKAGE")
                    ?? @"C:\Charts\Harmonic Bloom - Symphony of the Dreaming Petals.osz";
                Scheduler.AddDelayed(() =>
                {
                    for (int i = 0; i < 4; i++)
                        songSelect.SelectPrevious();
                    songSelect.SetKeyModeFilter(
                        Environment.GetEnvironmentVariable(
                            "YOKKO_SONGSELECT_DENSE_PACKAGE_PREVIEW") == "1"
                            ? KeyMode.FourKey
                            : KeyMode.SevenKey);
                }, 350);
                if (double.TryParse(
                        Environment.GetEnvironmentVariable(
                            "YOKKO_SONGSELECT_DIFFICULTY_MIN"),
                        out double difficultyMinimum))
                {
                    Scheduler.AddDelayed(
                        () => songSelect.SetMinimumDifficultyFilter(
                            difficultyMinimum),
                        520);
                }
                string searchQuery = Environment.GetEnvironmentVariable(
                    "YOKKO_SONGSELECT_SEARCH_QUERY");
                if (!string.IsNullOrWhiteSpace(searchQuery))
                {
                    Scheduler.AddDelayed(
                        () => songSelect.SetSearchQuery(searchQuery),
                        560);
                }
                if (Environment.GetEnvironmentVariable(
                        "YOKKO_SONGSELECT_HIDE_CONVERTS") == "1")
                {
                    Scheduler.AddDelayed(
                        songSelect.ToggleConvertedBeatmaps,
                        560);
                }
                if (Environment.GetEnvironmentVariable(
                        "YOKKO_SONGSELECT_ACTIVE_MODS") == "1")
                {
                    Scheduler.AddDelayed(() =>
                    {
                        foreach (ManiaModId mod in songSelect
                                                        .SelectedMods
                                                        .Mods
                                                        .ToArray())
                            songSelect.ToggleMod(mod);
                        songSelect.ToggleMod(ManiaModId.DoubleTime);
                        songSelect.ToggleMod(ManiaModId.Hidden);
                    }, 520);
                }
                Scheduler.AddDelayed(
                    () => songSelect.TogglePackage(packageToToggle),
                    packageToggleDelay);
                if (double.TryParse(
                        Environment.GetEnvironmentVariable(
                            "YOKKO_SONGSELECT_SECOND_TOGGLE_DELAY_MS"),
                        out double secondToggleDelay))
                {
                    Scheduler.AddDelayed(
                        () => songSelect.TogglePackage(packageToToggle),
                        Math.Max(0, secondToggleDelay));
                }
                if (double.TryParse(
                        Environment.GetEnvironmentVariable(
                            "YOKKO_SONGSELECT_CHANGE_DELAY_MS"),
                        out double selectionChangeDelay))
                {
                    int selectionChangeCount = int.TryParse(
                            Environment.GetEnvironmentVariable(
                                "YOKKO_SONGSELECT_CHANGE_COUNT"),
                            out int configuredSelectionChangeCount)
                        ? Math.Max(1, configuredSelectionChangeCount)
                        : 1;
                    Scheduler.AddDelayed(
                        () =>
                        {
                            for (int i = 0; i < selectionChangeCount; i++)
                                songSelect.SelectNext();
                        },
                        Math.Max(0, selectionChangeDelay));
                }
                if (Environment.GetEnvironmentVariable(
                        "YOKKO_SONGSELECT_AUTO_PLAY") == "1")
                {
                    Scheduler.AddDelayed(songSelect.PlaySelected, 700);
                }
                if (Environment.GetEnvironmentVariable(
                        "YOKKO_SONGSELECT_OPEN_MODS") == "1")
                {
                    Scheduler.AddDelayed(songSelect.ToggleModPanel, 700);
                }
                if (Environment.GetEnvironmentVariable(
                        "YOKKO_SONGSELECT_OPEN_OPTIONS") == "1")
                {
                    Scheduler.AddDelayed(songSelect.OpenOptions, 700);
                }
                if (Environment.GetEnvironmentVariable(
                        "YOKKO_SONGSELECT_PERSONAL_VIEW") == "1")
                {
                    Scheduler.AddDelayed(
                        songSelect.ActivateRankingPanel,
                        700);
                }
                string browseOverlay = Environment.GetEnvironmentVariable(
                    "YOKKO_SONGSELECT_PREVIEW_OVERLAY");
                if (string.Equals(
                        browseOverlay,
                        "filters",
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        browseOverlay,
                        "sort",
                        StringComparison.OrdinalIgnoreCase))
                {
                    float targetX = string.Equals(
                            browseOverlay,
                            "filters",
                            StringComparison.OrdinalIgnoreCase)
                        ? 570
                        : 780;
                    Scheduler.AddDelayed(() => songSelect
                        .ChildrenOfType<SongSelectBrowseToolButton>()
                        .Single(control => Math.Abs(control.X - targetX) < 0.01f)
                        .TriggerClick(), 700);
                }
                schedulePreviewScreenshot(1200);
                return;
            }

            if (Environment.GetEnvironmentVariable(
                    "YOKKO_RESULT_PREVIEW") == "1")
            {
                frameworkConfig.SetValue(
                    FrameworkSetting.Locale,
                    YokkoLocale.Chinese);
                frameworkConfig.SetValue(
                    FrameworkSetting.WindowMode,
                    WindowMode.Windowed);
                frameworkConfig.SetValue(
                    FrameworkSetting.WindowedSize,
                    GetPreviewWindowSize());
                bool etternaPreview = Environment.GetEnvironmentVariable(
                    "YOKKO_RESULT_ETTERNA_PREVIEW") == "1";
                YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
                {
                    Title = etternaPreview
                        ? "Etterna Grade Stress Test"
                        : "Afterimage",
                    DifficultyName = etternaPreview ? "Justice J4" : "Insane",
                };
                ManiaModSet mods = etternaPreview
                    ? ManiaModSet.Empty
                    : ManiaModSet.Empty
                        .With(ManiaModId.Hidden, true)
                        .WithFixedRate(ManiaModId.DoubleTime, 1.5);
                ManiaScoreResult result = etternaPreview
                    ? new ManiaScoreResult(
                        999_935,
                        0.999935,
                        1234,
                        ScoreRank.X,
                        1000,
                        20,
                        4,
                        2,
                        1,
                        0,
                        7,
                        3)
                    : new ManiaScoreResult(
                        537_761,
                        0.8251,
                        20,
                        ScoreRank.B,
                        8,
                        12,
                        0,
                        0,
                        0,
                        4);

                var resultOverlay = new GameplayResultOverlay(
                    beatmap,
                    result,
                    mods,
                    true,
                    () => { },
                    () => { },
                    () => { },
                    judgementConfiguration: etternaPreview
                        ? JudgementConfiguration.EtternaDefault
                        : JudgementConfiguration.YokkoDefault,
                    presentation: new GameplayResultPresentation(
                        "NYAFA",
                        "10248631",
                        new DateTimeOffset(
                            2026,
                            8,
                            2,
                            13,
                            46,
                            18,
                            TimeSpan.Zero),
                        531_540,
                        true,
                        new GameplayTimingStatistics(
                            1_942,
                            73,
                            1_796,
                            73,
                            -12.4,
                            13.1,
                            1.8,
                            82.4)));
                Add(resultOverlay);
                if (Environment.GetEnvironmentVariable(
                        "YOKKO_RESULT_INTERACTION_PREVIEW") == "1")
                {
                    Scheduler.AddDelayed(
                        () => resultOverlay
                            .SetScorePanelInteraction(true),
                        450);
                }

                schedulePreviewScreenshot();
                return;
            }

            if (Environment.GetEnvironmentVariable(
                    "YOKKO_PAUSE_PREVIEW") == "1")
            {
                DisplaySettings.UiScale.Value =
                    Enum.TryParse(
                        Environment.GetEnvironmentVariable(
                            "YOKKO_PREVIEW_UI_SCALE"),
                        true,
                        out YokkoUiScale previewScale)
                        ? previewScale
                        : YokkoUiScale.Comfortable;
                frameworkConfig.SetValue(
                    FrameworkSetting.Locale,
                    YokkoLocale.Chinese);
                bool longTitlePreview =
                    Environment.GetEnvironmentVariable(
                        "YOKKO_PAUSE_LONG_TITLE_PREVIEW") == "1";
                YokkoBeatmap beatmap =
                    DemoBeatmaps.CreateFourKeyDemo() with
                    {
                        Title = longTitlePreview
                            ? "Eternal Ending (aran Remix)"
                            : "Labyrinth",
                        Artist = longTitlePreview
                            ? "Kobaryo"
                            : ":Spiral_Eyes:",
                        DifficultyName = "4K Normal",
                    };
                var pauseOverlay = new GameplayPauseOverlay(
                    beatmap,
                    new YokkoGameplaySettings(),
                    new GameplayPauseSnapshot(
                        longTitlePreview ? 5_000 : 134_000,
                        longTitlePreview ? 268_000 : 228_000,
                        longTitlePreview ? 11_342 : 1_071_630,
                        longTitlePreview ? 1 : 0.9718,
                        longTitlePreview ? 41 : 3,
                        longTitlePreview ? 41 : 414,
                        longTitlePreview ? "SS" : "S",
                        longTitlePreview ? 41 : 287,
                        longTitlePreview ? 0 : 18,
                        longTitlePreview ? 0 : 2,
                        0,
                        0,
                        longTitlePreview ? 0 : 2,
                        longTitlePreview ? "AT" : "NM",
                        PauseCount: longTitlePreview ? 3 : 1),
                    () => { },
                    () => { },
                    () => { },
                    () => { });
                Add(pauseOverlay);
                if (Environment.GetEnvironmentVariable(
                        "YOKKO_PAUSE_SETTINGS_PREVIEW") == "1")
                {
                    Scheduler.AddDelayed(
                        pauseOverlay.TogglePauseSettings,
                        350);
                }
                Add(new CursorContainer());
                schedulePreviewScreenshot();
                return;
            }

            if (Environment.GetEnvironmentVariable(
                    "YOKKO_EDITOR_PREVIEW") == "1")
            {
                Add(new ScreenStack(new Screens.Editor.EditorScreen())
                {
                    RelativeSizeAxes = Axes.Both,
                });
                Add(new CursorContainer());
                schedulePreviewScreenshot(1200);
                return;
            }

            if (Environment.GetEnvironmentVariable(
                    "YOKKO_SETTINGS_PREVIEW") == "1")
            {
                if (Environment.GetEnvironmentVariable(
                        "YOKKO_PREVIEW_LOCALE")
                    is { Length: > 0 } settingsLocale)
                {
                    frameworkConfig.SetValue(
                        FrameworkSetting.Locale,
                        settingsLocale);
                }

                if (Enum.TryParse(
                        Environment.GetEnvironmentVariable(
                            "YOKKO_PREVIEW_UI_SCALE"),
                        true,
                        out YokkoUiScale settingsScale))
                {
                    DisplaySettings.UiScale.Value = settingsScale;
                }

                var settingsScreen = new Screens.Settings.SettingsScreen();
                Add(new ScreenStack(settingsScreen)
                {
                    RelativeSizeAxes = Axes.Both,
                });
                Add(new CursorContainer());

                if (Enum.TryParse(
                        Environment.GetEnvironmentVariable(
                            "YOKKO_SETTINGS_PAGE"),
                        true,
                        out Screens.Settings.SettingsPageKind settingsPage))
                {
                    Scheduler.AddDelayed(
                        () => settingsScreen.OpenPage(settingsPage),
                        400);
                }

                if (Enum.TryParse(
                        Environment.GetEnvironmentVariable(
                            "YOKKO_SETTINGS_GAMEPLAY_SECTION"),
                        true,
                        out Screens.Settings.GameplaySettingsSection
                            gameplaySection))
                {
                    Scheduler.AddDelayed(() =>
                    {
                        settingsScreen.OpenPage(
                            Screens.Settings.SettingsPageKind.Gameplay);
                        ((Screens.Settings.GameplaySettingsPanel)
                            settingsScreen.ActivePanel)
                            .SelectSection(gameplaySection);
                    }, 500);
                }

                schedulePreviewScreenshot(1200);
                return;
            }

            AddRange(new Drawable[]
            {
                new TestBrowser("Yokko"),
                new CursorContainer()
            });
        }

        internal static System.Drawing.Size GetPreviewWindowSize() =>
            new(
                (int)YokkoDisplaySettings.ReferenceLayoutSize.X,
                (int)YokkoDisplaySettings.ReferenceLayoutSize.Y);

        private static bool isPreviewRun() =>
            previewEnvironmentVariables.Any(variable =>
                Environment.GetEnvironmentVariable(variable) == "1");

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                themeHotReload?.Dispose();

            base.Dispose(isDisposing);
        }

        private void configureThemeHotReload()
        {
            string path = Environment.GetEnvironmentVariable(
                "YOKKO_UI_THEME_FILE");
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                themeHotReload = new YokkoThemeFileHotReload(
                    path,
                    UiThemeStore,
                    action => Scheduler.Add(action));
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or InvalidOperationException
                    or ArgumentException)
            {
                UiThemeStore.ReportLoadError(path, exception.Message);
            }
        }

        private void configurePreviewViewport()
        {
            frameworkConfig.SetValue(
                FrameworkSetting.WindowMode,
                WindowMode.Windowed);
            frameworkConfig.SetValue(
                FrameworkSetting.WindowedSize,
                GetPreviewWindowSize());
            DisplaySettings.UiScale.Value = Enum.TryParse(
                    Environment.GetEnvironmentVariable(
                        "YOKKO_PREVIEW_UI_SCALE"),
                    true,
                    out YokkoUiScale previewScale)
                ? previewScale
                : YokkoUiScale.Comfortable;
        }

        public override void SetHost(GameHost host)
        {
            base.SetHost(host);
            host.Window.CursorState |= CursorState.Hidden;
        }

        private void schedulePreviewScreenshot(double delay = 1200)
        {
            if (double.TryParse(
                    Environment.GetEnvironmentVariable(
                        "YOKKO_PREVIEW_SCREENSHOT_DELAY_MS"),
                    out double configuredDelay))
                delay = Math.Max(0, configuredDelay);

            string outputPath = Environment.GetEnvironmentVariable(
                "YOKKO_PREVIEW_SCREENSHOT");

            if (string.IsNullOrWhiteSpace(outputPath))
                return;

            Scheduler.AddDelayed(() =>
            {
                MethodInfo takeScreenshot = renderer.GetType().GetMethod(
                    "TakeScreenshot",
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException(
                        "The active renderer does not expose screenshot capture.");
                using var screenshot = (Image<Rgba32>)takeScreenshot.Invoke(
                    renderer,
                    null);
                ensureUsableScreenshot(screenshot);
                Directory.CreateDirectory(
                    Path.GetDirectoryName(outputPath)
                    ?? throw new InvalidOperationException(
                        "Screenshot path has no parent directory."));
                screenshot.SaveAsPng(outputPath);
                host.Exit();
            }, delay);
        }

        private static void ensureUsableScreenshot(Image<Rgba32> screenshot)
        {
            ArgumentNullException.ThrowIfNull(screenshot);
            if (screenshot.Width <= 1 || screenshot.Height <= 1)
            {
                throw new InvalidOperationException(
                    $"Renderer returned an unusable {screenshot.Width}x{screenshot.Height} screenshot.");
            }
        }

        private void seedSongSelectPreview()
        {
            ImportedCharts.Clear();
            string harmonicPackagePath = Environment.GetEnvironmentVariable(
                    "YOKKO_SONGSELECT_LONG_PACKAGE_PREVIEW") == "1"
                ? @"C:\Charts\Harmonic Bloom - Symphony of the Dreaming Petals Beyond the Infinite Starlight Archive of the Last Celestial Horizon.osz"
                : @"C:\Charts\Harmonic Bloom - Symphony of the Dreaming Petals.osz";
            ImportedCharts.AddOrReplace(
                [
                    previewChart(
                        "Eternal Echoes of the Fractured Sky Beyond the Shattered Horizon",
                        "Voidwalkers",
                        "DJ K1RA",
                        "Normal",
                        KeyMode.SevenKey,
                        156),
                    previewChart(
                        "Celestial Reverie",
                        "Voidwalkers",
                        "DJ K1RA",
                        "Hard",
                        KeyMode.SevenKey,
                        168),
                    previewChart(
                        "Celestial Reverie",
                        "Voidwalkers",
                        "DJ K1RA",
                        "Marathon x1.2",
                        KeyMode.SevenKey,
                        176),
                    previewChart(
                        "Eternal Echoes of the Fractured Sky Beyond the Shattered Horizon",
                        "Voidwalkers",
                        "DJ K1RA",
                        "Marathon x1.3",
                        KeyMode.SevenKey,
                        182),
                ],
                @"C:\Charts\Celestial Reverie - Chronicles of the Infinite Arcadia.osz");
            ImportedCharts.AddOrReplace(
                [
                    previewChart(
                        "Neon Pulse Overdrive",
                        "Synthion",
                        "Echo",
                        "Hard",
                        KeyMode.SevenKey,
                        174),
                    previewChart(
                        "Neon Pulse Overdrive",
                        "Synthion",
                        "Echo",
                        "Marathon x1.2",
                        KeyMode.SevenKey,
                        190),
                ],
                @"C:\Charts\Neon Pulse Overdrive - Ultra Resonance Protocol.osz");
            ChartImportResult[] harmonicPreviewCharts =
                Environment.GetEnvironmentVariable(
                    "YOKKO_SONGSELECT_DENSE_PACKAGE_PREVIEW") == "1"
                    ?
                    [
                        previewChart("Aether", "Kaitendaentai", "Yorusen Haneoto", "240 edit", KeyMode.FourKey, 240),
                        previewChart("Aether", "Kaitendaentai", "Yorusen Haneoto", "x (210bpm)", KeyMode.FourKey, 210),
                        previewChart("Aether", "Kaitendaentai", "Yorusen Haneoto", "x (220bpm)", KeyMode.FourKey, 220),
                        previewChart("Aether", "Kaitendaentai", "Yorusen Haneoto", "x (230bpm)", KeyMode.FourKey, 230),
                        previewChart("Aether", "Kaitendaentai", "Yorusen Haneoto", "x (240bpm)", KeyMode.FourKey, 240),
                        previewChart("Aether", "Kaitendaentai", "Yorusen Haneoto", "x@LNise", KeyMode.FourKey, 250),
                        previewChart("Aether", "Kaitendaentai", "Yorusen Haneoto", "x", KeyMode.FourKey, 260),
                        previewChart("Aether", "Kaitendaentai", "Yorusen Haneoto", "x (270bpm)", KeyMode.FourKey, 270),
                        previewChart("Aether", "Kaitendaentai", "Yorusen Haneoto", "x (280bpm)", KeyMode.FourKey, 280),
                        previewChart("Aether", "Kaitendaentai", "Yorusen Haneoto", "x (290bpm)", KeyMode.FourKey, 290),
                        previewChart("Aether", "Kaitendaentai", "Yorusen Haneoto", "x (300bpm)", KeyMode.FourKey, 300),
                        previewChart("Aether", "Kaitendaentai", "Yorusen Haneoto", "x (310bpm)", KeyMode.FourKey, 310),
                    ]
                    :
                    [
                    previewChart(
                        "Harmonic Bloom: Symphony of the Dreaming Petals",
                        "Koharu",
                        "Yokko Team",
                        "Normal",
                        KeyMode.SevenKey,
                        162),
                    previewChart(
                        "Petals at Daybreak",
                        "Koharu",
                        "Yokko Team",
                        "Hard",
                        KeyMode.SevenKey,
                        178),
                    ];
            ImportedCharts.AddOrReplace(
                harmonicPreviewCharts,
                harmonicPackagePath);
            if (Environment.GetEnvironmentVariable(
                    "YOKKO_SONGSELECT_STANDALONE_PREVIEW") == "1")
            {
                ImportedCharts.AddOrReplace(
                    [
                        previewChart(
                            "Solo Skyline",
                            "Aster Lane",
                            "Yokko Team",
                            "Starlight",
                            KeyMode.SevenKey,
                            166),
                    ],
                    @"C:\Charts\Solo Skyline.osz");
            }
        }

        private void seedSongSelectScorePreview()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (ImportedChart chart in ImportedCharts.GetCharts())
            {
                YokkoBeatmap beatmap = chart.Result.Beatmap;
                if (ScoreStoreForTesting.GetHistory(
                        beatmap,
                        JudgementConfiguration.YokkoDefault,
                        1).Count > 0)
                    continue;

                for (int attempt = 0; attempt < 7; attempt++)
                {
                    DateTimeOffset playedAt = now.AddMinutes(-attempt * 17);
                    ManiaModSet mods = attempt switch
                    {
                        1 => ManiaModSet.Empty.With(ManiaModId.Hidden, true),
                        2 => ManiaModSet.Empty.With(ManiaModId.DoubleTime, true),
                        _ => ManiaModSet.Empty,
                    };
                    var replay = new GameplayReplay(
                    [
                        new GameplayReplayInput(0, true, 900 + attempt * 2),
                        new GameplayReplayInput(0, false, 940 + attempt * 2),
                    ], mods);
                    string replayPath = ReplayStoreForTesting.Save(
                        beatmap,
                        ManiaBeatmapModTransformer.Apply(beatmap, mods),
                        replay,
                        chart.Result.SourceHash,
                        playedAt);
                    ScoreStoreForTesting.SaveBest(
                        beatmap,
                        mods,
                        JudgementConfiguration.YokkoDefault,
                        new ManiaScoreResult(
                            986_420 - attempt * 21_700,
                            0.9932 - attempt * 0.0074,
                            612 - attempt * 37,
                            attempt < 3 ? ScoreRank.S : ScoreRank.A,
                            520 - attempt * 18,
                            84 + attempt * 9,
                            12 + attempt * 3,
                            4 + attempt,
                            attempt,
                            attempt / 2),
                        replayPath,
                        playedAt);
                }
            }
        }

        private static ChartImportResult previewChart(
            string title,
            string artist,
            string creator,
            string difficulty,
            KeyMode mode,
            double bpm)
        {
            int laneCount = (int)mode;
            YokkoHitObject[] previewObjects = Enumerable.Range(0, 84)
                .Select(index => new YokkoHitObject(
                    index % laneCount,
                    800 + index * (30000 / bpm),
                    null,
                    HitObjectKind.Tap))
                .ToArray();

            // Keep the fixture compact while retaining enough real notes for
            // the production difficulty calculators to render numeric values.
            var beatmap = new YokkoBeatmap(
                title,
                artist,
                creator,
                difficulty,
                mode,
                ChartSourceFormat.Yokko,
                [new YokkoTimingPoint(0, 60000 / bpm)],
                null,
                previewObjects);
            string artwork = Path.GetFullPath(Path.Combine(
                "Yokko.Resources",
                "Textures",
                "SongSelect",
                title.StartsWith("Neon", StringComparison.Ordinal)
                    || title.StartsWith("Harmonic", StringComparison.Ordinal)
                    ? "waterfall-cute.png"
                    : "blue-signal.png"));
            return new ChartImportResult(beatmap, [], artwork);
        }

        private static void applyModsPreviewState(
            GameplayModsScreen modsScreen)
        {
            switch (Environment.GetEnvironmentVariable(
                        "YOKKO_MODS_PREVIEW_STATE")
                    ?.Trim()
                    .ToLowerInvariant())
            {
                case "config":
                    modsScreen.ResetMods();
                    modsScreen.SetCategory(
                        ManiaModCategory.DifficultyIncrease);
                    modsScreen.CycleOrbitMod(
                        ManiaModId.AccuracyChallenge);
                    modsScreen.FocusOrbitModForTest(
                        ManiaModId.AccuracyChallenge);
                    break;

                case "config-muted":
                    modsScreen.ResetMods();
                    modsScreen.SetCategory(ManiaModCategory.Fun);
                    modsScreen.CycleOrbitMod(ManiaModId.Muted);
                    modsScreen.FocusOrbitModForTest(ManiaModId.Muted);
                    modsScreen.SetMutedInverse(true);
                    modsScreen.SetMutedMetronome(true);
                    modsScreen.SetMutedComboCount(150);
                    break;

                case "config-time-ramp":
                    modsScreen.ResetMods();
                    modsScreen.SetCategory(ManiaModCategory.Fun);
                    modsScreen.CycleOrbitMod(ManiaModId.WindUp);
                    modsScreen.FocusOrbitModForTest(ManiaModId.WindUp);
                    modsScreen.SetTimeRampInitialRate(0.85);
                    modsScreen.SetTimeRampFinalRate(1.5);
                    break;

                case "config-random":
                    modsScreen.ResetMods();
                    modsScreen.SetCategory(ManiaModCategory.Conversion);
                    modsScreen.CycleOrbitMod(ManiaModId.Random);
                    modsScreen.FocusOrbitModForTest(ManiaModId.Random);
                    modsScreen.SetRandomSeed(20260801);
                    break;

                case "developer-autoplay":
                    modsScreen.ResetMods();
                    modsScreen.SetCategory(ManiaModCategory.Automation);
                    modsScreen.CycleOrbitMod(ManiaModId.Autoplay);
                    modsScreen.FocusOrbitModForTest(ManiaModId.Autoplay);
                    break;

                case "no-pause":
                    modsScreen.ToggleMod(ManiaModId.NoPause);
                    modsScreen.SetNoPauseAllowedPauses(1);
                    break;

                case "conversion":
                    modsScreen.SetCategory(
                        ManiaModCategory.Conversion);
                    break;

                case "empty":
                    modsScreen.ResetMods();
                    break;

                case "dense-active":
                    modsScreen.ResetMods();
                    modsScreen.ToggleMod(ManiaModId.HardRock);
                    modsScreen.ToggleMod(ManiaModId.DoubleTime);
                    modsScreen.ToggleMod(ManiaModId.Hidden);
                    modsScreen.ToggleMod(ManiaModId.Mirror);
                    modsScreen.ToggleMod(ManiaModId.ConstantSpeed);
                    modsScreen.ToggleMod(ManiaModId.NoPause);
                    break;

                case "scroll":
                    modsScreen.NavigatePageByScroll(-1);
                    break;

                case "orbit":
                    modsScreen.ResetMods();
                    modsScreen.ToggleMod(ManiaModId.HalfTime);
                    modsScreen.ToggleMod(ManiaModId.HardRock);
                    modsScreen.SetCategory(
                        ManiaModCategory.DifficultyIncrease);
                    modsScreen.SetPreviewRateVisual(1.0);
                    break;
            }
        }
    }
}
