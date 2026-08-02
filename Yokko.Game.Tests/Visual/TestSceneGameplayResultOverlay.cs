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
                () => { },
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
                    new GameplayTimingSummary(
                        73,
                        1_796,
                        73,
                        1.8,
                        82.4))));
        });
        AddUntilStep("character stage ready", () =>
            overlay?.CharacterStageReady == true);
        AddUntilStep("stage decorations ready", () =>
            overlay?.StageDecorationsReady == true);
        AddUntilStep("character texture ready", () =>
            overlay?.CharacterTextureReady == true);
        AddUntilStep("rank seal ready", () =>
            overlay?.RankSealReady == true);
        AddAssert("player identity is displayed", () =>
            overlay?.DisplayedPlayerName == "NYAFA"
            && overlay.DisplayedPlayerId == "10248631");
        AddUntilStep("song underline clears title", () =>
            overlay?.SongTitleUnderlineClearance >= 6);
        AddAssert("mod chip is visible", () =>
            overlay?.RenderedModChipCount >= 3);
        AddAssert("mod labels are preserved", () =>
            overlay?.DisplayedMods.Contains("HD") == true
            && overlay.DisplayedMods.Contains("DT"));
        AddUntilStep("entrance animation completes", () =>
            overlay?.EntranceComplete == true);
        AddUntilStep("rank label fits its seal", () =>
            overlay?.RankSealLabelFits == true);
        AddStep("preview score-panel interaction", () =>
            overlay.SetScorePanelInteraction(true));
        AddAssert("score-panel interaction is active", () =>
            overlay.ScorePanelInteractionActive);
        AddStep("release score-panel interaction", () =>
            overlay.SetScorePanelInteraction(false));
        AddAssert("score-panel interaction is released", () =>
            !overlay.ScorePanelInteractionActive);
        AddWaitStep("interaction settles", 10);
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
            if (screenshot.Width <= 1 || screenshot.Height <= 1)
            {
                throw new InvalidOperationException(
                    $"Renderer returned an unusable {screenshot.Width}x{screenshot.Height} screenshot.");
            }
            Directory.CreateDirectory(
                Path.GetDirectoryName(outputPath)
                ?? throw new InvalidOperationException(
                    "Screenshot path has no parent directory."));
            screenshot.SaveAsPng(outputPath);
            screenshotSaved = true;
        });
        AddUntilStep("screenshot saved", () => screenshotSaved);
    }

    [Test]
    public void TestEtternaResultScreenLoads()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
        {
            Title = "Etterna result",
            DifficultyName = "Justice",
        };
        var result = new ManiaScoreResult(
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
            3);

        AddStep("show Etterna result screen", () =>
        {
            Add(overlay = new GameplayResultOverlay(
                beatmap,
                result,
                ManiaModSet.Empty,
                true,
                () => { },
                () => { },
                () => { },
                judgementConfiguration:
                    JudgementConfiguration.EtternaDefault));
        });
        AddUntilStep("Etterna result stage loads", () =>
            overlay?.CharacterStageReady == true);
        AddUntilStep("Etterna rank seal loads", () =>
            overlay?.RankSealReady == true);
        AddAssert("Etterna judgement is shown", () =>
            overlay?.DisplayedMods.Contains("ETTERNA J4") == true);
        AddAssert("Etterna long grade is preserved", () =>
            overlay?.DisplayedRank == "AAAAA");
        AddAssert("Etterna rank seal is identified", () =>
            overlay?.RankSealEyebrow == "WIFE3 GRADE"
            && overlay.RankSealFooter == "ETTERNA // J4");
        AddUntilStep("Etterna result entrance completes", () =>
            overlay?.EntranceComplete == true);
        AddUntilStep("Etterna rank label fits its seal", () =>
            overlay?.RankSealLabelFits == true);
    }
}
