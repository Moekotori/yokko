using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Game.Screens.Gameplay;

namespace Yokko.Game.Tests.Visual;

public partial class TestSceneGameplayResultOverlay : YokkoTestScene
{
    [Resolved]
    private IRenderer renderer { get; set; }

    private GameplayResultOverlay overlay;
    private bool screenshotSaved;

    [Test]
    public void TestResultScreenVisual()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
        {
            Title = "Afterimage",
            DifficultyName = "Insane",
        };
        var result = new ManiaScoreResult(
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
        ManiaModSet mods = ManiaModSet.Empty
            .With(ManiaModId.Hidden, true)
            .WithFixedRate(ManiaModId.DoubleTime, 1.5);

        AddStep("show result screen", () =>
        {
            Add(overlay = new GameplayResultOverlay(
                beatmap,
                result,
                mods,
                true,
                () => { },
                () => { },
                () => { }));
        });
        AddUntilStep("mascot texture loaded", () =>
            overlay?.MascotReady == true);
        AddAssert("mod chip is visible", () =>
            overlay?.RenderedModChipCount >= 3);
        AddAssert("mod labels are preserved", () =>
            overlay?.DisplayedMods.Contains("HD") == true
            && overlay.DisplayedMods.Contains("DT"));
        AddUntilStep("entrance animation completes", () =>
            overlay?.EntranceComplete == true);
        AddStep("capture result screen", () =>
        {
            string outputPath = Environment.GetEnvironmentVariable(
                "YOKKO_RESULT_SCREENSHOT");

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                screenshotSaved = true;
                return;
            }

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
            screenshotSaved = true;
        });
        AddUntilStep("screenshot saved", () => screenshotSaved);
    }
}
