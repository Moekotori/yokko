using System;
using System.IO;
using System.Reflection;
using osu.Framework.Allocation;
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

        protected override void LoadComplete()
        {
            base.LoadComplete();

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
                var gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo() with
                    {
                        Title = "Pulse Bloom",
                        DifficultyName = "4K Normal",
                    });

                Add(new ScreenStack(gameplay)
                {
                    RelativeSizeAxes = Axes.Both,
                });
                Add(new CursorContainer());
                Scheduler.AddDelayed(gameplay.TogglePause, 500);
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

        private void schedulePreviewScreenshot()
        {
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
            }, 1200);
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
                    modsScreen.NavigateByScroll(-1);
                    break;
            }
        }
    }
}
