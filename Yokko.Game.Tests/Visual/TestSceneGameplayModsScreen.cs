using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Screens;
using osuTK;
using osuTK.Input;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Game.Gameplay;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests.Visual;

[TestFixture]
public partial class TestSceneGameplayModsScreen : YokkoTestScene
{
    [Resolved]
    private IRenderer renderer { get; set; }
    [Resolved]
    private YokkoManiaModPreferences modPreferences { get; set; }

    private readonly GameplayModsScreen modsScreen;
    private ManiaModSet observedMods;
    private int observedCommitCount;
    private int commitsBeforePreview;
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
            mods =>
            {
                observedMods = mods;
                observedCommitCount++;
            }))
        {
            RelativeSizeAxes = Axes.Both,
        });
    }

    [Test]
    public void TestDedicatedModsInteractions()
    {
        AddStep("restore interaction fixture", () =>
        {
            modPreferences.SerializedConfiguration.Value = string.Empty;
            modsScreen.ResetMods();
            modsScreen.ToggleMod(ManiaModId.HalfTime);
            modsScreen.ToggleMod(ManiaModId.Hidden);
            modsScreen.SetCategory(
                ManiaModCategory.DifficultyReduction);
        });
        AddAssert("difficulty workspace is default", () =>
            modsScreen.ActiveCategory
            == ManiaModCategory.DifficultyReduction);
        AddAssert("both difficulty groups are visible", () =>
            modsScreen.VisibleModCount == 15);
        AddAssert("initial mods are preserved", () =>
            modsScreen.SelectedMods.Contains(ManiaModId.HalfTime)
            && modsScreen.SelectedMods.Contains(ManiaModId.Hidden));
        AddAssert("reset is enabled for active mods", () =>
            modsScreen.ResetEnabled);
        AddStep("Enter removes focused Half Time", () =>
            modsScreen.HandleInteractionKey(Key.Enter));
        AddAssert("Enter acts on focus instead of closing", () =>
            !modsScreen.SelectedMods.Contains(ManiaModId.HalfTime));
        AddStep("Enter restores focused Half Time", () =>
            modsScreen.HandleInteractionKey(Key.Enter));
        AddAssert("focused Half Time is active again", () =>
            modsScreen.SelectedMods.Contains(ManiaModId.HalfTime));
        AddStep("plus adjusts focused rate", () =>
            modsScreen.HandleInteractionKey(Key.Plus));
        AddAssert("keyboard rate adjustment is precise", () =>
            modsScreen.SelectedMods.FixedRateSpeedChange == 0.76);
        AddStep("configure Half Time", () =>
        {
            modsScreen.SetFixedRateSpeedChange(0.80);
            modsScreen.SetFixedRateAdjustPitch(true);
        });
        AddAssert("fixed-rate configuration reaches page state", () =>
            modsScreen.SelectedMods.PlaybackRate == 0.80
            && modsScreen.SelectedMods.FixedRateAdjustPitch);
        AddStep("preview configurable mod", () =>
            modsScreen.ToggleMod(ManiaModId.AccuracyChallenge));
        AddAssert("config panel owns the settings row", () =>
            modsScreen.SettingsHost.ActivePage
            == ManiaModId.AccuracyChallenge
            && !modsScreen.DetailHintVisible);
        AddStep("preview plain mod", () =>
            modsScreen.ToggleMod(ManiaModId.Easy));
        AddWaitStep("wait for hidden slider state", 10);
        AddAssert("plain detail keeps shortcut and clear spacing", () =>
            modsScreen.DetailHintVisible
            && modsScreen.SettingsHeaderY == 170
            && modsScreen.FixedRatePanelY == 200
            && !modsScreen.FixedRateSliderVisible
            && !modsScreen.FixedRateTicksVisible);
        AddStep("enable No Fail", () =>
            modsScreen.ToggleMod(ManiaModId.NoFail));
        AddAssert("page selection receives No Fail", () =>
            modsScreen.SelectedMods.Contains(ManiaModId.NoFail));
        AddStep("show conversion category", () =>
            modsScreen.SetCategory(ManiaModCategory.Conversion));
        AddAssert("conversion catalogue is complete", () =>
            modsScreen.VisibleModCount == 18
            && modsScreen.DetailMod == ManiaModId.Random);
        AddStep("global wheel moves to next category", () =>
            modsScreen.NavigatePageByScroll(-1));
        AddAssert("wheel changes page instead of focused Mod", () =>
            modsScreen.ActiveCategory == ManiaModCategory.Automation
            && modsScreen.DetailMod == ManiaModId.Autoplay);
        AddStep("Tab category cycle", () =>
            modsScreen.CycleCategory(1));
        AddAssert("category cycle focuses relevant first Mod", () =>
            modsScreen.ActiveCategory == ManiaModCategory.Fun
            && modsScreen.DetailMod == ManiaModId.WindUp);
        AddStep("Shift Tab category cycle", () =>
            modsScreen.HandleInteractionKey(Key.Tab, true));
        AddAssert("reverse category cycle is predictable", () =>
            modsScreen.ActiveCategory == ManiaModCategory.Automation
            && modsScreen.DetailMod == ManiaModId.Autoplay);
        AddStep("return to conversion category", () =>
            modsScreen.SetCategory(ManiaModCategory.Conversion));
        AddStep("try unavailable native key conversion", () =>
            modsScreen.ToggleMod(ManiaModId.Key4));
        AddAssert("native chart key conversion stays disabled", () =>
            !modsScreen.SelectedMods.Contains(ManiaModId.Key4)
            && modsScreen.InteractionHintText.Contains(
                "REQUIRES OSU!STANDARD CHART"));
        AddStep("reset gameplay mods", modsScreen.ResetMods);
        AddAssert("reset clears page selection", () =>
            modsScreen.SelectedMods.Mods.Count == 0
            && !modsScreen.ResetEnabled);
        AddStep("commit final page selection", modsScreen.CommitSelection);
        AddAssert("page commits to Song Select once requested", () =>
            observedMods.Mods.Count == 0);
        AddStep("prepare inactive Half Time slider", () =>
        {
            modsScreen.SetCategory(
                ManiaModCategory.DifficultyReduction);
            modsScreen.ToggleMod(ManiaModId.HalfTime);
            modsScreen.ResetMods();
            commitsBeforePreview = observedCommitCount;
        });
        AddAssert("inactive rate control explains activation", () =>
            modsScreen.FixedRateSliderHeight == 28
            && modsScreen.FixedRateTicksVisible
            && modsScreen.DetailHintText.Contains("DRAG RATE")
            && modsScreen.NavigationHintVisible);
        AddStep("drag preview activates rate Mod", () =>
            modsScreen.PreviewFixedRateSpeedChange(0.82));
        AddAssert("drag is immediate and does not rebuild Song Select", () =>
            modsScreen.SelectedMods.FixedRateMod
            == ManiaModId.HalfTime
            && modsScreen.SelectedMods.FixedRateSpeedChange == 0.82
            && observedCommitCount == commitsBeforePreview);
        AddStep("finish slider interaction", () =>
            modsScreen.CompleteFixedRateInteraction());
        AddAssert("slider release stays local", () =>
            observedCommitCount == commitsBeforePreview);
        AddStep("commit slider result on page handoff",
            modsScreen.CommitSelection);
        AddAssert("page handoff commits the final rate once", () =>
            observedCommitCount == commitsBeforePreview + 1
            && observedMods.FixedRateMod == ManiaModId.HalfTime
            && observedMods.FixedRateSpeedChange == 0.82);
    }

    [Test]
    public void TestConfigurableModPreferenceRestoresOnEnable()
    {
        AddStep("store global Half Time preference", () =>
        {
            modPreferences.SerializedConfiguration.Value = string.Empty;
            modPreferences.Remember(
                ManiaModSet.Empty.WithFixedRate(
                    ManiaModId.HalfTime,
                    0.84,
                    true));
            modsScreen.ResetMods();
        });
        AddStep("enable Half Time from clean selection", () =>
            modsScreen.ToggleMod(ManiaModId.HalfTime));
        AddAssert("saved Half Time settings restored", () =>
            modsScreen.SelectedMods.FixedRateMod
            == ManiaModId.HalfTime
            && modsScreen.SelectedMods.FixedRateSpeedChange == 0.84
            && modsScreen.SelectedMods.FixedRateAdjustPitch);
        AddStep("clear global preference fixture", () =>
            modPreferences.SerializedConfiguration.Value = string.Empty);
    }

    [Test]
    public void TestLazerVisibilitySettingsAreConfigurableAndRemembered()
    {
        AddStep("clear visibility preferences", () =>
        {
            modPreferences.SerializedConfiguration.Value = string.Empty;
            modsScreen.ResetMods();
        });
        AddStep("enable Cover", () =>
            modsScreen.ToggleMod(ManiaModId.Cover));
        AddAssert("Cover opens configuration page", () =>
            modsScreen.SettingsHost.ActivePage == ManiaModId.Cover);
        AddStep("configure Cover", () =>
        {
            modsScreen.SetCoverCoverage(0.7);
            modsScreen.SetCoverDirection(
                ManiaCoverDirection.AgainstScroll);
        });
        AddAssert("Cover settings reach session", () =>
            modsScreen.SelectedMods.Contains(ManiaModId.Cover)
            && modsScreen.SelectedMods.CoverCoverage == 0.7
            && modsScreen.SelectedMods.CoverDirection
               == ManiaCoverDirection.AgainstScroll);
        AddStep("replace with Flashlight", () =>
            modsScreen.ToggleMod(ManiaModId.Flashlight));
        AddAssert("Flashlight opens configuration page", () =>
            modsScreen.SettingsHost.ActivePage
            == ManiaModId.Flashlight);
        AddStep("configure Flashlight", () =>
        {
            modsScreen.SetFlashlightSizeMultiplier(1.8);
            modsScreen.SetFlashlightComboBasedSize(true);
        });
        AddAssert("Flashlight settings reach session", () =>
            modsScreen.SelectedMods.Contains(ManiaModId.Flashlight)
            && modsScreen.SelectedMods.FlashlightSizeMultiplier == 1.8
            && modsScreen.SelectedMods.FlashlightComboBasedSize);
        AddStep("restore Cover", () =>
            modsScreen.ToggleMod(ManiaModId.Cover));
        AddAssert("Cover preference is restored independently", () =>
            modsScreen.SelectedMods.Contains(ManiaModId.Cover)
            && modsScreen.SelectedMods.CoverCoverage == 0.7
            && modsScreen.SelectedMods.CoverDirection
               == ManiaCoverDirection.AgainstScroll);
        AddStep("clear visibility preference fixture", () =>
            modPreferences.SerializedConfiguration.Value = string.Empty);
    }

    [Test]
    public void TestLazerRandomCustomSeedIsApplied()
    {
        AddStep("reset and enable Random", () =>
        {
            modsScreen.ResetMods();
            modsScreen.ToggleMod(ManiaModId.Random);
        });
        AddAssert("Random opens seed configuration", () =>
            modsScreen.SettingsHost.ActivePage
            == ManiaModId.Random);
        AddStep("set signed custom seed", () =>
            modsScreen.SetRandomSeed(-123456789));
        AddAssert("custom seed reaches replay-owned Mod set", () =>
            modsScreen.SelectedMods.Contains(ManiaModId.Random)
            && modsScreen.SelectedMods.RandomSeed == -123456789);
    }

    [Test]
    public void TestGameplayModsLayout()
    {
        AddAssert("uses the complete scaled workspace", () =>
            GameplayModsScreen.CalculateResponsiveStageSize(
                new Vector2(2000, 1250))
            == new Vector2(2000, 1250));
        AddAssert("never collapses below authored layout", () =>
            GameplayModsScreen.CalculateResponsiveStageSize(
                new Vector2(960, 540))
            == new Vector2(1280, 720));
        AddAssert("browser gains columns as UI size shrinks", () =>
            GameplayModsScreen.CalculateBrowserColumnCount(550) == 2
            && GameplayModsScreen.CalculateBrowserColumnCount(870) == 3
            && GameplayModsScreen.CalculateBrowserColumnCount(1270) == 4);
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

    [Test]
    public void TestConfigurableSettingsUseLightWorkspace()
    {
        AddStep("show Difficulty Adjust settings", () =>
        {
            modsScreen.ResetMods();
            modsScreen.SetCategory(ManiaModCategory.Conversion);
            modsScreen.ToggleMod(ManiaModId.DifficultyAdjust);
        });
        AddAssert("configuration card uses the light workspace", () =>
            modsScreen.ConfigurablePanelColour.R > 0.9f
            && modsScreen.ConfigurablePanelColour.G > 0.9f
            && modsScreen.ConfigurablePanelColour.B > 0.9f);
        AddWaitStep("wait for settings transition", 10);
        AddStep("capture light settings workspace", captureScreenshot);
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
