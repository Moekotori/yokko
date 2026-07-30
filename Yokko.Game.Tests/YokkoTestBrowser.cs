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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Game.Gameplay;
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests
{
    public partial class YokkoTestBrowser : YokkoGameBase
    {
        [Resolved]
        private IRenderer renderer { get; set; }

        [Resolved]
        private GameHost host { get; set; }

        private FrameworkConfigManager frameworkConfig;

        [BackgroundDependencyLoader]
        private void load(FrameworkConfigManager frameworkConfig)
        {
            this.frameworkConfig = frameworkConfig;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

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
                        timingBar.Show(new JudgementEvent(
                            i,
                            i % 4,
                            1000 + i * 100,
                            1000 + i * 100 + presses[i].Error,
                            presses[i].Error,
                            presses[i].Rating));
                    }

                    timingBar.Show(new JudgementEvent(
                        presses.Length,
                        2,
                        1800,
                        1825,
                        25,
                        JudgementRating.Perfect,
                        JudgementPhase.HoldTail));
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
                    "YOKKO_RESULT_PREVIEW") == "1")
            {
                YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
                {
                    Title = "Afterimage",
                    DifficultyName = "Insane",
                };

                Add(new GameplayResultOverlay(
                    beatmap,
                    new ManiaScoreResult(
                        537_761,
                        0.8251,
                        20,
                        ScoreRank.B,
                        8,
                        12,
                        0,
                        0,
                        0,
                        4),
                    true,
                    () => { },
                    () => { },
                    () => { }));
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
                        : YokkoUiScale.Large;
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
                Add(new GameplayPauseOverlay(
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
                        longTitlePreview ? "AT" : "NM"),
                    () => { },
                    () => { },
                    () => { },
                    () => { }));
                Add(new CursorContainer());
                schedulePreviewScreenshot();
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

                schedulePreviewScreenshot(1200);
                return;
            }

            AddRange(new Drawable[]
            {
                new TestBrowser("Yokko"),
                new CursorContainer()
            });
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
                Directory.CreateDirectory(
                    Path.GetDirectoryName(outputPath)
                    ?? throw new InvalidOperationException(
                        "Screenshot path has no parent directory."));
                screenshot.SaveAsPng(outputPath);
                host.Exit();
            }, delay);
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
                    modsScreen.ToggleMod(
                        ManiaModId.AccuracyChallenge);
                    break;

                case "conversion":
                    modsScreen.SetCategory(
                        ManiaModCategory.Conversion);
                    break;

                case "empty":
                    modsScreen.ResetMods();
                    break;

                case "scroll":
                    modsScreen.NavigatePageByScroll(-1);
                    break;
            }
        }
    }
}
