using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osuTK;
using osuTK.Input;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Game.Gameplay;
using Yokko.Game.Presentation;
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
        bool residualWheelAccepted = true;

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
            modsScreen.VisibleModCount == 16);
        AddStep("search by name", () =>
            modsScreen.SetSearchQuery("half time"));
        AddAssert("search narrows the catalogue", () =>
            modsScreen.SearchQuery == "half time"
            && modsScreen.VisibleModCount == 1
            && modsScreen.DetailMod == ManiaModId.HalfTime);
        AddStep("Escape clears search before leaving", () =>
            modsScreen.HandleInteractionKey(Key.Escape));
        AddAssert("cleared search restores active category", () =>
            modsScreen.SearchQuery.Length == 0
            && modsScreen.VisibleModCount == 16);
        AddAssert("initial mods are preserved", () =>
            modsScreen.SelectedMods.Contains(ManiaModId.HalfTime)
            && modsScreen.SelectedMods.Contains(ManiaModId.Hidden));
        AddAssert("reset is enabled for active mods", () =>
            modsScreen.ResetEnabled);
        AddStep("Enter advances focused Half Time to Daycore", () =>
            modsScreen.HandleInteractionKey(Key.Enter));
        AddAssert("Enter advances the shared slow-rate switch", () =>
            !modsScreen.SelectedMods.Contains(ManiaModId.HalfTime)
            && modsScreen.SelectedMods.Contains(ManiaModId.Daycore));
        AddStep("Enter turns the shared slow-rate switch off", () =>
            modsScreen.HandleInteractionKey(Key.Enter));
        AddAssert("second Enter removes Daycore", () =>
            !modsScreen.SelectedMods.Contains(ManiaModId.HalfTime)
            && !modsScreen.SelectedMods.Contains(ManiaModId.Daycore));
        AddStep("Enter restores focused Half Time", () =>
            modsScreen.HandleInteractionKey(Key.Enter));
        AddAssert("focused Half Time is active again", () =>
            modsScreen.SelectedMods.Contains(ManiaModId.HalfTime));
        AddStep("plus does not adjust fixed Half Time", () =>
            Assert.That(
                modsScreen.HandleInteractionKey(Key.Plus),
                Is.False));
        AddAssert("Half Time stays canonical without settings", () =>
            modsScreen.SelectedMods.FixedRateSpeedChange == 0.75
            && !modsScreen.SelectedMods.FixedRateAdjustPitch);
        AddStep("preview configurable mod", () =>
            modsScreen.ToggleMod(ManiaModId.AccuracyChallenge));
        AddWaitStep("wait for compact accuracy control", 10);
        AddAssert("Accuracy Challenge stays in the hero", () =>
            modsScreen.OrbitAccuracyControlVisible
            && modsScreen.OrbitAccuracyValueText == "90.0%"
            && !modsScreen.OrbitSettingsPanelVisible);
        AddStep("adjust compact accuracy target", () =>
            modsScreen.SetAccuracyChallengeMinimum(0.975));
        AddAssert("compact accuracy reaches page state", () =>
            modsScreen.SelectedMods.AccuracyChallengeMinimum == 0.975
            && modsScreen.SelectedMods.AccuracyChallengeMode
               == ManiaAccuracyMode.MaximumAchievable
            && modsScreen.OrbitAccuracyValueText == "97.5%");
        AddStep("preview plain mod", () =>
            modsScreen.ToggleMod(ManiaModId.Easy));
        AddWaitStep("wait for hidden slider state", 10);
        AddAssert("plain detail keeps shortcut and clear spacing", () =>
            modsScreen.DetailHintVisible
            && !modsScreen.OrbitSettingsPanelVisible
            && modsScreen.SettingsHeaderY == 116
            && modsScreen.FixedRatePanelY == 138
            && !modsScreen.FixedRateSliderVisible
            && !modsScreen.FixedRateTicksVisible);
        AddStep("enable No Fail", () =>
            modsScreen.ToggleMod(ManiaModId.NoFail));
        AddAssert("page selection receives No Fail", () =>
            modsScreen.SelectedMods.Contains(ManiaModId.NoFail));
        AddStep("show conversion category", () =>
            modsScreen.SetCategory(ManiaModCategory.Conversion));
        AddAssert("key conversion Mods stay hidden", () =>
            modsScreen.VisibleModCount == 8
            && !modsScreen.IsModVisible(ManiaModId.Key1)
            && !modsScreen.IsModVisible(ManiaModId.Key4)
            && !modsScreen.IsModVisible(ManiaModId.Key7)
            && !modsScreen.IsModVisible(ManiaModId.Key10)
            && modsScreen.DetailMod == ManiaModId.Random);
        AddStep("global wheel moves to next category", () =>
            modsScreen.ProcessScrollGesture(-1, 1000));
        AddAssert("wheel starts a real page transition", () =>
            modsScreen.IsPageTransitioning);
        AddWaitStep("wait for wheel page transition", 25);
        AddAssert("wheel enters next page instead of focused Mod", () =>
            !modsScreen.IsPageTransitioning
            && modsScreen.ActiveCategory == ManiaModCategory.Automation
            && modsScreen.DetailMod == ManiaModId.Autoplay
            && Math.Abs(modsScreen.OrbitContentX - 335) < 0.01f);
        AddStep("reject residual wheel momentum", () =>
            residualWheelAccepted =
                modsScreen.ProcessScrollGesture(-1, 1200));
        AddAssert("one wheel gesture moves exactly one page", () =>
            !residualWheelAccepted
            && modsScreen.ActiveCategory
            == ManiaModCategory.Automation);
        AddStep("Tab category cycle", () =>
            modsScreen.HandleInteractionKey(Key.Tab));
        AddWaitStep("wait for Tab page transition", 25);
        AddAssert("category cycle focuses relevant first Mod", () =>
            modsScreen.ActiveCategory == ManiaModCategory.Fun
            && modsScreen.DetailMod == ManiaModId.WindUp
            && Math.Abs(modsScreen.OrbitContentX - 335) < 0.01f);
        AddStep("Shift Tab category cycle", () =>
            modsScreen.HandleInteractionKey(Key.Tab, true));
        AddWaitStep("wait for reverse page transition", 25);
        AddAssert("reverse category cycle is predictable", () =>
            modsScreen.ActiveCategory == ManiaModCategory.Automation
            && modsScreen.DetailMod == ManiaModId.Autoplay
            && Math.Abs(modsScreen.OrbitContentX - 335) < 0.01f);
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
        AddStep("prepare inactive Daycore slider", () =>
        {
            modsScreen.SetCategory(
                ManiaModCategory.DifficultyReduction);
            modsScreen.ToggleMod(ManiaModId.Daycore);
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
            == ManiaModId.Daycore
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
            && observedMods.FixedRateMod == ManiaModId.Daycore
            && observedMods.FixedRateSpeedChange == 0.82);
    }

    [Test]
    public void TestFixedRateModUsesCanonicalRateOnEnable()
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
        AddAssert("Half Time starts at its canonical rate", () =>
            modsScreen.SelectedMods.FixedRateMod
            == ManiaModId.HalfTime
            && modsScreen.SelectedMods.FixedRateSpeedChange == 0.75
            && !modsScreen.SelectedMods.FixedRateAdjustPitch);
        AddStep("clear global preference fixture", () =>
            modPreferences.SerializedConfiguration.Value = string.Empty);
    }

    [Test]
    public void TestCoverSettingsRemainWhileFlashlightStaysSimple()
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
        AddWaitStep("wait for Flashlight simple state", 10);
        AddAssert("Flashlight does not open a settings page", () =>
            modsScreen.SelectedMods.Contains(ManiaModId.Flashlight)
            && !modsScreen.OrbitSettingsPanelVisible);
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
    public void TestOrbitHeroTogglesFocusedMod()
    {
        OrbitHeroPanel hero = null;
        ManiaModId focused = default;
        AddStep("prepare orbit hero interaction", () =>
        {
            modsScreen.ResetMods();
            modsScreen.SetCategory(ManiaModCategory.DifficultyReduction);
            focused = modsScreen.DetailMod;
            hero = this.ChildrenOfType<OrbitHeroPanel>().Single();
        });
        AddStep("activate focused mod from hero", () =>
            hero.ActivateForTest());
        AddAssert("hero toggles the focused mod", () =>
            modsScreen.SelectedMods.Contains(focused));
    }

    [Test]
    public void TestModBrowserUsesRightHandArc()
    {
        OrbitModNode[] nodes = null;
        AddStep("show dense Mod category", () =>
        {
            modsScreen.ResetMods();
            modsScreen.SetCategory(ManiaModCategory.DifficultyIncrease);
            nodes = this.ChildrenOfType<OrbitModNode>().ToArray();
        });
        AddAssert("every visible Mod uses circular node geometry", () =>
            nodes.Length == 6
            && nodes.All(node =>
                node.Width == 284
                && node.Height == 86));
        AddAssert("nodes follow the authored right-hand arc", () =>
            nodes.Select(node => node.Position)
                .SequenceEqual(Enumerable.Range(0, 6)
                    .Select(GameplayModsOrbitWorkspace.CalculateModArcPosition)));
        AddAssert("adjacent nodes retain their signal connectors", () =>
            this.ChildrenOfType<OrbitConnector>().Count() == 5);
    }

    [Test]
    public void TestOrbitQuickInteractions()
    {
        OrbitEmptySlot emptySlot = null;
        OrbitRatePresetButton fastPreset = null;
        OrbitRateSlider rateSlider = null;
        bool activationObserved = false;
        ManiaModId focused = default;
        AddStep("prepare quick interaction controls", () =>
        {
            modsScreen.ResetMods();
            modsScreen.SetCategory(ManiaModCategory.DifficultyReduction);
            focused = modsScreen.DetailMod;
            emptySlot = this.ChildrenOfType<OrbitEmptySlot>().First();
            fastPreset = this.ChildrenOfType<OrbitRatePresetButton>()
                .Single(button => Math.Abs(button.Value - 1.5) < 0.005);
            rateSlider = this.ChildrenOfType<OrbitRateSlider>().Single();
        });
        AddAssert("rate slider has a forgiving pointer target", () =>
            rateSlider.Height == 44);
        AddStep("add focused mod from empty slot", () =>
        {
            emptySlot.ActivateForTest();
            activationObserved = this.ChildrenOfType<OrbitModNode>()
                .Single(node => node.ModId == focused)
                .ActivationTransitionRunning;
        });
        AddAssert("empty slot activates focused mod", () =>
            modsScreen.SelectedMods.Contains(focused));
        AddAssert("activation has a visible transition", () =>
            activationObserved);
        AddStep("remove focused mod again", () =>
            modsScreen.ToggleMod(focused));
        AddAssert("orbit interaction removes focused mod", () =>
            !modsScreen.SelectedMods.Contains(focused));
        AddStep("select 1.50x rate preset", () =>
            fastPreset.ActivateForTest());
        AddAssert("rate preset applies Double Time rate", () =>
            modsScreen.SelectedMods.FixedRateMod == ManiaModId.DoubleTime
            && Math.Abs(modsScreen.SelectedMods.PlaybackRate - 1.5) < 0.005);
    }

    [Test]
    public void TestOrbitSidebarSwitchUsesLatestSelection()
    {
        AddStep("prepare first category", () =>
            modsScreen.SetCategory(
                ManiaModCategory.DifficultyReduction));
        AddStep("switch twice without waiting", () =>
        {
            modsScreen.NavigateToCategoryPage(
                ManiaModCategory.DifficultyIncrease);
            modsScreen.NavigateToCategoryPage(
                ManiaModCategory.Fun);
        });
        AddAssert("latest switch remains in motion", () =>
            modsScreen.IsPageTransitioning);
        AddWaitStep("wait for interruptible transition", 20);
        AddAssert("latest sidebar selection wins", () =>
            !modsScreen.IsPageTransitioning
            && modsScreen.ActiveCategory == ManiaModCategory.Fun
            && modsScreen.DetailMod == ManiaModId.WindUp
            && Math.Abs(modsScreen.OrbitContentX - 335) < 0.01f);
    }

    [Test]
    public void TestOrbitDifficultyPagesUseTheirRealCategories()
    {
        AddStep("show difficulty reduction", () =>
            modsScreen.SetCategory(
                ManiaModCategory.DifficultyReduction));
        AddAssert("reduction page excludes increase mods", () =>
            modsScreen.VisibleOrbitModCount == 4
            && modsScreen.IsOrbitModVisible(ManiaModId.Easy)
            && modsScreen.IsOrbitModVisible(ManiaModId.HalfTime)
            && !modsScreen.IsOrbitModVisible(ManiaModId.Daycore)
            && modsScreen.OrbitRepresentsMod(ManiaModId.Daycore)
            && !modsScreen.IsOrbitModVisible(ManiaModId.HardRock)
            && !modsScreen.IsOrbitModVisible(ManiaModId.SuddenDeath));

        AddStep("cycle shared slow-rate switch", () =>
        {
            modsScreen.ResetMods();
            modsScreen.CycleOrbitMod(ManiaModId.HalfTime);
        });
        AddWaitStep("wait for fixed Half Time presentation", 10);
        AddAssert("slow-rate switch starts with fixed Half Time", () =>
            modsScreen.SelectedMods.Contains(ManiaModId.HalfTime)
            && modsScreen.SelectedMods.PlaybackRate == 0.75
            && !modsScreen.OrbitSettingsPanelVisible
            && this.ChildrenOfType<OrbitModNode>()
                .Single(node => node.ModId == ManiaModId.HalfTime)
                .FamilyIndicatorText == "1/2");
        AddStep("cycle slow-rate switch to Daycore", () =>
            modsScreen.CycleOrbitMod(ManiaModId.HalfTime));
        AddWaitStep("wait for Daycore settings", 10);
        AddAssert("Daycore replaces Half Time in the shared switch", () =>
            !modsScreen.SelectedMods.Contains(ManiaModId.HalfTime)
            && modsScreen.SelectedMods.Contains(ManiaModId.Daycore)
            && modsScreen.DetailMod == ManiaModId.Daycore
            && modsScreen.OrbitSettingsPanelVisible
            && this.ChildrenOfType<OrbitModNode>()
                .Single(node => node.ModId == ManiaModId.HalfTime)
                .FamilyIndicatorText == "2/2");
        AddStep("cycle slow-rate switch off", () =>
            modsScreen.CycleOrbitMod(ManiaModId.HalfTime));
        AddAssert("shared slow-rate switch turns off after Daycore", () =>
            !modsScreen.SelectedMods.Contains(ManiaModId.HalfTime)
            && !modsScreen.SelectedMods.Contains(ManiaModId.Daycore));

        AddStep("show difficulty increase", () =>
            modsScreen.SetCategory(
                ManiaModCategory.DifficultyIncrease));
        AddAssert("increase page exposes No Pause only with increase mods", () =>
            modsScreen.IsOrbitModVisible(ManiaModId.HardRock)
            && modsScreen.IsOrbitModVisible(ManiaModId.NoPause)
            && !modsScreen.IsOrbitModVisible(ManiaModId.Easy)
            && !modsScreen.IsOrbitModVisible(ManiaModId.HalfTime));
        AddAssert("all increase mods remain reachable", () =>
            modsScreen.VisibleOrbitModCount == 6
            && !modsScreen.IsOrbitModVisible(ManiaModId.Perfect)
            && modsScreen.OrbitRepresentsMod(ManiaModId.Perfect)
            && !modsScreen.IsOrbitModVisible(ManiaModId.Nightcore)
            && modsScreen.OrbitRepresentsMod(ManiaModId.Nightcore)
            && !modsScreen.IsOrbitModVisible(ManiaModId.Flashlight)
            && modsScreen.OrbitRepresentsMod(ManiaModId.Flashlight)
            && modsScreen.OrbitRepresentsMod(ManiaModId.FadeIn)
            && modsScreen.OrbitRepresentsMod(ManiaModId.Cover)
            && modsScreen.IsOrbitModVisible(ManiaModId.AccuracyChallenge));

        AddStep("cycle shared visibility switch", () =>
        {
            modsScreen.ResetMods();
            modsScreen.CycleOrbitMod(ManiaModId.Hidden);
        });
        AddAssert("visibility switch starts with Hidden", () =>
            modsScreen.SelectedMods.Contains(ManiaModId.Hidden)
            && this.ChildrenOfType<OrbitModNode>()
                .Single(node => node.ModId == ManiaModId.Hidden)
                .FamilyIndicatorText == "1/4");
        AddStep("cycle visibility switch to Flashlight", () =>
            modsScreen.CycleOrbitMod(ManiaModId.Hidden));
        AddAssert("visibility switch replaces Hidden with Flashlight", () =>
            !modsScreen.SelectedMods.Contains(ManiaModId.Hidden)
            && modsScreen.SelectedMods.Contains(ManiaModId.Flashlight)
            && this.ChildrenOfType<OrbitModNode>()
                .Single(node => node.ModId == ManiaModId.Hidden)
                .FamilyIndicatorText == "2/4");
        AddStep("keyboard advances the focused visibility family", () =>
            modsScreen.HandleInteractionKey(Key.Space));
        AddAssert("keyboard reaches Fade In instead of removing the family", () =>
            !modsScreen.SelectedMods.Contains(ManiaModId.Flashlight)
            && modsScreen.SelectedMods.Contains(ManiaModId.FadeIn)
            && modsScreen.DetailMod == ManiaModId.FadeIn);
        AddStep("cycle visibility switch through remaining choices", () =>
        {
            modsScreen.CycleOrbitMod(ManiaModId.Hidden);
            modsScreen.CycleOrbitMod(ManiaModId.Hidden);
        });
        AddAssert("visibility switch turns off after Cover", () =>
            !modsScreen.SelectedMods.Mods.Any(mod => mod is
                ManiaModId.Hidden
                or ManiaModId.Flashlight
                or ManiaModId.FadeIn
                or ManiaModId.Cover));

        AddStep("show conversion", () =>
            modsScreen.SetCategory(ManiaModCategory.Conversion));
        AddAssert("conversion does not truncate late mods", () =>
            modsScreen.VisibleOrbitModCount <= 6
            && modsScreen.IsOrbitModVisible(ManiaModId.ConstantSpeed)
            && !modsScreen.IsOrbitModVisible(ManiaModId.HoldOff)
            && modsScreen.OrbitRepresentsMod(ManiaModId.HoldOff));

        AddStep("cycle shared conversion switch", () =>
        {
            modsScreen.ResetMods();
            modsScreen.CycleOrbitMod(ManiaModId.Invert);
        });
        AddAssert("conversion switch starts with Invert", () =>
            modsScreen.SelectedMods.Contains(ManiaModId.Invert));
        AddStep("cycle conversion switch to Hold Off", () =>
            modsScreen.CycleOrbitMod(ManiaModId.Invert));
        AddAssert("conversion switch replaces Invert with Hold Off", () =>
            !modsScreen.SelectedMods.Contains(ManiaModId.Invert)
            && modsScreen.SelectedMods.Contains(ManiaModId.HoldOff));

        AddStep("show automation", () =>
            modsScreen.SetCategory(ManiaModCategory.Automation));
        AddAssert("automation mods keep separate nodes", () =>
            modsScreen.IsOrbitModVisible(ManiaModId.Autoplay)
            && modsScreen.IsOrbitModVisible(ManiaModId.Cinema));

        AddStep("show fun", () =>
            modsScreen.SetCategory(ManiaModCategory.Fun));
        AddAssert("fun mods keep separate nodes", () =>
            modsScreen.IsOrbitModVisible(ManiaModId.WindUp)
            && modsScreen.IsOrbitModVisible(ManiaModId.WindDown)
            && modsScreen.IsOrbitModVisible(ManiaModId.AdaptiveSpeed));

        AddStep("select more than five compatible mods", () =>
        {
            modsScreen.ResetMods();
            modsScreen.ToggleMod(ManiaModId.HardRock);
            modsScreen.ToggleMod(ManiaModId.DoubleTime);
            modsScreen.ToggleMod(ManiaModId.Hidden);
            modsScreen.ToggleMod(ManiaModId.Mirror);
            modsScreen.ToggleMod(ManiaModId.ConstantSpeed);
            modsScreen.ToggleMod(ManiaModId.NoPause);
        });
        AddAssert("active rail reports the real count", () =>
            modsScreen.OrbitActiveCountText == "(6 ACTIVE)"
            && modsScreen.OrbitCapacityTelemetryText
                == "MOD BUS // 06 ACTIVE");
        AddAssert("active rail keeps every selected Mod discoverable", () =>
            this.ChildrenOfType<OrbitActiveModRow>().Count() == 6);

        AddStep("restore corrected increase page", () =>
            modsScreen.SetCategory(
                ManiaModCategory.DifficultyIncrease));
        AddWaitStep("settle corrected increase page", 20);
        AddStep("capture corrected increase page", captureScreenshot);
        AddUntilStep("screenshot saved", () => screenshotSaved);
    }

    [Test]
    public void TestDoubleTimeCyclesThroughNightcore()
    {
        AddStep("prepare Double Time cycle", () =>
        {
            modPreferences.SerializedConfiguration.Value = string.Empty;
            modPreferences.Remember(
                ManiaModSet.Empty.WithFixedRate(
                    ManiaModId.DoubleTime,
                    1.15));
            modsScreen.ResetMods();
            modsScreen.SetCategory(ManiaModCategory.DifficultyIncrease);
        });
        AddStep("enable Double Time", () =>
            modsScreen.ToggleMod(ManiaModId.DoubleTime));
        AddAssert("first press enables Double Time", () =>
            modsScreen.SelectedMods.Contains(ManiaModId.DoubleTime)
            && modsScreen.SelectedMods.FixedRateSpeedChange == 1.5
            && !modsScreen.SelectedMods.Contains(ManiaModId.Nightcore));
        AddWaitStep("wait for Double Time simple state", 10);
        AddAssert("Double Time does not open a settings page", () =>
            !modsScreen.OrbitSettingsPanelVisible);
        AddStep("cycle Double Time to Nightcore", () =>
            modsScreen.ToggleMod(ManiaModId.DoubleTime));
        AddAssert("second press enables Nightcore", () =>
            !modsScreen.SelectedMods.Contains(ManiaModId.DoubleTime)
            && modsScreen.SelectedMods.Contains(ManiaModId.Nightcore)
            && modsScreen.SelectedMods.FixedRateSpeedChange == 1.5
            && this.ChildrenOfType<OrbitModNode>()
                .Single(node => node.ModId == ManiaModId.DoubleTime)
                .PresentationMod == ManiaModId.Nightcore);
        AddStep("cycle Nightcore off", () =>
            modsScreen.ToggleMod(ManiaModId.DoubleTime));
        AddAssert("third press disables the speed mod", () =>
            !modsScreen.SelectedMods.Contains(ManiaModId.DoubleTime)
            && !modsScreen.SelectedMods.Contains(ManiaModId.Nightcore));
        AddStep("clear Double Time preference fixture", () =>
            modPreferences.SerializedConfiguration.Value = string.Empty);
    }

    [Test]
    public void TestSimpleIncreaseModsAvoidLargeSettingsPanel()
    {
        AddStep("prepare difficulty increase", () =>
        {
            modPreferences.SerializedConfiguration.Value = string.Empty;
            modsScreen.ResetMods();
            modsScreen.SetCategory(ManiaModCategory.DifficultyIncrease);
        });
        AddStep("enable and focus Perfect", () =>
        {
            modsScreen.ToggleMod(ManiaModId.Perfect);
            modsScreen.FocusOrbitModForTest(ManiaModId.Perfect);
        });
        AddWaitStep("settle Perfect state", 10);
        AddAssert("Perfect has no large settings panel", () =>
            modsScreen.SelectedMods.Contains(ManiaModId.Perfect)
            && !modsScreen.OrbitSettingsPanelVisible);
        AddStep("replace with Flashlight", () =>
        {
            modsScreen.ToggleMod(ManiaModId.Flashlight);
            modsScreen.FocusOrbitModForTest(ManiaModId.Flashlight);
        });
        AddWaitStep("settle Flashlight state", 10);
        AddAssert("Flashlight has no large settings panel", () =>
            modsScreen.SelectedMods.Contains(ManiaModId.Flashlight)
            && !modsScreen.OrbitSettingsPanelVisible);
        AddStep("enable and focus No Pause", () =>
        {
            modsScreen.ToggleMod(ManiaModId.NoPause);
            modsScreen.FocusOrbitModForTest(ManiaModId.NoPause);
            modsScreen.SetNoPauseAllowedPauses(2);
        });
        AddWaitStep("settle No Pause compact control", 10);
        AddAssert("No Pause uses only its compact control", () =>
            modsScreen.SelectedMods.NoPauseAllowedPauses == 2
            && modsScreen.OrbitNoPauseControlVisible
            && !modsScreen.OrbitSettingsPanelVisible);
    }

    [Test]
    public void TestActiveModRowsFocusBeforeRemoving()
    {
        OrbitActiveModRow activeRow = null;
        AddStep("prepare active row", () =>
        {
            modsScreen.ResetMods();
            modsScreen.ToggleMod(ManiaModId.HalfTime);
            modsScreen.SetCategory(ManiaModCategory.Fun);
            activeRow = this.ChildrenOfType<OrbitActiveModRow>()
                .Single(row => row.ModId == ManiaModId.HalfTime);
        });
        AddStep("activate the active row", () =>
            activeRow.ActivateForTest());
        AddAssert("row focuses without removing", () =>
            modsScreen.ActiveCategory
                == ManiaModCategory.DifficultyReduction
            && modsScreen.DetailMod == ManiaModId.HalfTime
            && modsScreen.SelectedMods.Contains(ManiaModId.HalfTime));
        AddStep("remove from dedicated control", () =>
            activeRow.RemoveForTest());
        AddWaitStep("wait for removal motion", 12);
        AddAssert("dedicated remove control disables mod", () =>
            !modsScreen.SelectedMods.Contains(ManiaModId.HalfTime));
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
            == YokkoDisplaySettings.ReferenceLayoutSize);
        AddAssert("browser gains columns as UI size shrinks", () =>
            GameplayModsScreen.CalculateBrowserColumnCount(1032) == 2
            && GameplayModsScreen.CalculateBrowserColumnCount(1210) == 3
            && GameplayModsScreen.CalculateBrowserColumnCount(1700) == 4);
        AddStep("restore reference state", () =>
        {
            modsScreen.ResetMods();
            modsScreen.ToggleMod(ManiaModId.HalfTime);
            modsScreen.ToggleMod(ManiaModId.HardRock);
            modsScreen.SetCategory(ManiaModCategory.DifficultyIncrease);
        });
        AddWaitStep("wait for entrance animation", 30);
        AddStep("capture gameplay mods", captureScreenshot);
        AddUntilStep("screenshot saved", () => screenshotSaved);
    }

    [Test]
    public void TestModCardActivationMotionFrame()
    {
        AddStep("prepare inactive mod card", () =>
        {
            modsScreen.ResetMods();
            modsScreen.SetCategory(
                ManiaModCategory.DifficultyReduction);
        });
        AddStep("activate Half Time card", () =>
            modsScreen.ToggleMod(ManiaModId.HalfTime));
        AddWaitStep("advance into card activation", 6);
        AddStep("capture active card", captureScreenshot);
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
    }
}
