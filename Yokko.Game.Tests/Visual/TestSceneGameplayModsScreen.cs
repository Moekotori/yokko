using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Screens;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests.Visual;

[TestFixture]
public partial class TestSceneGameplayModsScreen : YokkoTestScene
{
    [Resolved]
    private IRenderer renderer { get; set; }

    private readonly GameplayModsScreen modsScreen;
    private ManiaModSet observedMods;
    private bool screenshotSaved;

    public TestSceneGameplayModsScreen()
    {
        ManiaModSet initialMods = ManiaModSet.Empty
            .With(ManiaModId.HalfTime, true)
            .With(ManiaModId.Hidden, true);
        observedMods = initialMods;
        Add(new ScreenStack(modsScreen = new GameplayModsScreen(
            DemoBeatmaps.CreateFourKeyDemo(),
            initialMods,
            mods => observedMods = mods))
        {
            RelativeSizeAxes = Axes.Both,
        });
    }

    [Test]
    public void TestDedicatedModsInteractions()
    {
        AddAssert("difficulty workspace is default", () =>
            modsScreen.ActiveCategory
            == ManiaModCategory.DifficultyReduction);
        AddAssert("both difficulty groups are visible", () =>
            modsScreen.VisibleModCount == 15);
        AddAssert("initial mods are preserved", () =>
            modsScreen.SelectedMods.Contains(ManiaModId.HalfTime)
            && modsScreen.SelectedMods.Contains(ManiaModId.Hidden));
        AddStep("enable No Fail", () =>
            modsScreen.ToggleMod(ManiaModId.NoFail));
        AddAssert("selection callback receives No Fail", () =>
            observedMods.Contains(ManiaModId.NoFail));
        AddStep("show conversion category", () =>
            modsScreen.SetCategory(ManiaModCategory.Conversion));
        AddAssert("conversion catalogue is complete", () =>
            modsScreen.VisibleModCount == 18);
        AddStep("try unavailable native key conversion", () =>
            modsScreen.ToggleMod(ManiaModId.Key4));
        AddAssert("native chart key conversion stays disabled", () =>
            !modsScreen.SelectedMods.Contains(ManiaModId.Key4));
        AddStep("reset gameplay mods", modsScreen.ResetMods);
        AddAssert("reset clears selection and callback", () =>
            modsScreen.SelectedMods.Mods.Count == 0
            && observedMods.Mods.Count == 0);
    }

    [Test]
    public void TestGameplayModsLayout()
    {
        AddStep("restore reference state", () =>
        {
            modsScreen.ResetMods();
            modsScreen.SetCategory(ManiaModCategory.DifficultyReduction);
            modsScreen.ToggleMod(ManiaModId.Hidden);
            modsScreen.ToggleMod(ManiaModId.HalfTime);
        });
        AddWaitStep("wait for entrance animation", 30);
        AddStep("capture gameplay mods", captureScreenshot);
        AddUntilStep("screenshot saved", () => screenshotSaved);
    }

    private void captureScreenshot()
    {
        string outputPath = Environment.GetEnvironmentVariable(
            "YOKKO_MODS_SCREENSHOT");

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
    }
}
