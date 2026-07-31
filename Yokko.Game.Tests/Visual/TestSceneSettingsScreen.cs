using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osuTK.Input;
using Yokko.Audio;
using Yokko.Core.Gameplay;
using Yokko.Core.Scoring;
using Yokko.Game.Gameplay;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Settings;

namespace Yokko.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneSettingsScreen : YokkoTestScene
    {
        private readonly ScreenStack screenStack;
        private readonly SettingsScreen settingsScreen;

        public TestSceneSettingsScreen()
        {
            Add(screenStack = new ScreenStack(settingsScreen = new SettingsScreen()) { RelativeSizeAxes = Axes.Both });
        }

        [Test]
        public void TestSettingsScreen()
        {
            AddAssert("settings screen is current", () => screenStack.CurrentScreen is SettingsScreen);
        }

        [Test]
        public void TestEverySettingsPageCanOpen()
        {
            AddAssert("settings navigation supports keyboard focus", () =>
                settingsScreen.ChildrenOfType<SettingsNavItem>()
                              .All(item => item.AcceptsFocus));
            foreach (SettingsPageKind page in System.Enum.GetValues<SettingsPageKind>())
            {
                SettingsPageKind capturedPage = page;
                AddStep($"open {page}", () => settingsScreen.OpenPage(capturedPage));
                AddAssert($"{page} is current", () => settingsScreen.CurrentPage == capturedPage);
            }
        }

        [Test]
        public void TestSidebarSearchAndKeyboardNavigation()
        {
            SettingsSidebar sidebar = null;

            AddStep("capture sidebar", () =>
                sidebar = settingsScreen
                    .ChildrenOfType<SettingsSidebar>()
                    .Single());
            AddAssert("Ctrl+F focuses search", () =>
                settingsScreen.HandleNavigationShortcut(
                    Key.F,
                    true)
                && sidebar.SearchHasFocus);
            AddStep("search audio", () =>
                sidebar.SetSearchQuery(
                    SettingsPages
                        .Get(SettingsPageKind.Audio)
                        .Title
                        .ToString()));
            AddAssert("search narrows to audio", () =>
                sidebar.VisiblePageCount == 1);
            AddAssert("Enter action opens first result", () =>
                sidebar.SubmitSearch());
            AddAssert("audio opens and search clears", () =>
                settingsScreen.CurrentPage == SettingsPageKind.Audio
                && sidebar.SearchQuery.Length == 0
                && sidebar.FocusedPage == SettingsPageKind.Audio);
            AddStep("search for no result", () =>
                sidebar.SetSearchQuery("__missing_setting__"));
            AddAssert("empty result cannot submit", () =>
                sidebar.VisiblePageCount == 0
                && !sidebar.SubmitSearch());
            AddStep("clear search", () =>
                sidebar.SetSearchQuery(string.Empty));
            AddAssert("down enters first navigation item", () =>
                sidebar.FocusAdjacentPage(null, 1)
                && sidebar.FocusedPage == SettingsPageKind.General);
            AddAssert("up wraps to last navigation item", () =>
                sidebar.FocusAdjacentPage(
                    SettingsPageKind.General,
                    -1)
                && sidebar.FocusedPage == SettingsPageKind.About);
        }

        [Test]
        public void TestCommonSettingsControlsSupportKeyboardFocus()
        {
            DisplaySettingsPanel display = null;

            AddStep("open Display", () =>
            {
                settingsScreen.OpenPage(SettingsPageKind.Display);
                display =
                    (DisplaySettingsPanel)settingsScreen.ActivePanel;
            });
            AddAssert("display selectors accept focus", () =>
                settingsScreen.ActivePanel
                    .ChildrenOfType<SettingsSegmentedChoiceButton>()
                    .All(control => control.AcceptsFocus)
                && settingsScreen.ActivePanel
                    .ChildrenOfType<SettingsFrameLimitChoiceButton>()
                    .All(control => control.AcceptsFocus)
                && settingsScreen.ActivePanel
                    .ChildrenOfType<DisplayPerformanceReadoutToggle>()
                    .All(control => control.AcceptsFocus)
                && settingsScreen.ActivePanel
                    .ChildrenOfType<SettingsDropdownHeader>()
                    .Single()
                    .AcceptsFocus
                    == display.IsResolutionSelectionEnabled);
            AddStep("open Audio", () =>
                settingsScreen.OpenPage(SettingsPageKind.Audio));
            AddAssert("audio controls accept focus", () =>
                settingsScreen.ActivePanel
                    .ChildrenOfType<SettingsSegmentedChoiceButton>()
                    .All(control => control.AcceptsFocus)
                && settingsScreen.ActivePanel
                    .ChildrenOfType<SettingsDropdownHeader>()
                    .All(control => control.AcceptsFocus)
                && settingsScreen.ActivePanel
                    .ChildrenOfType<SettingsVolumeSlider>()
                    .All(control => control.AcceptsFocus)
                && settingsScreen.ActivePanel
                    .ChildrenOfType<SettingsAudioTestButton>()
                    .All(control => control.AcceptsFocus)
                && settingsScreen.ActivePanel
                    .ChildrenOfType<SettingsAudioToggle>()
                    .All(control => control.AcceptsFocus)
                && settingsScreen.ActivePanel
                    .ChildrenOfType<SettingsOffsetStepper>()
                    .All(control => control.AcceptsFocus));
            AddStep("open Import", () =>
                settingsScreen.OpenPage(SettingsPageKind.Import));
            AddAssert("import preferences accept focus", () =>
                settingsScreen.ActivePanel
                    .ChildrenOfType<ImportPreferenceCard>()
                    .All(control => control.AcceptsFocus));
            AddStep("open About", () =>
                settingsScreen.OpenPage(SettingsPageKind.About));
            AddAssert("placeholder sections accept focus", () =>
                settingsScreen.ActivePanel
                    .ChildrenOfType<SettingsPlaceholderSection>()
                    .All(control => control.AcceptsFocus));
        }

        [Test]
        public void TestStatusCardIconsAreCentred()
        {
            AddStep("open Display", () => settingsScreen.OpenPage(SettingsPageKind.Display));
            AddAssert("display status icon is centred", () =>
            {
                Circle badge = settingsScreen.ActivePanel.ChildrenOfType<Circle>().Single(candidate => candidate.Size.X == 56);
                SpriteIcon icon = settingsScreen.ActivePanel.ChildrenOfType<SpriteIcon>().Single(candidate => candidate.Size.X == 26);

                return badge.Origin == Anchor.Centre &&
                       icon.Origin == Anchor.Centre &&
                       badge.Position == icon.Position;
            });

            AddStep("open Audio", () => settingsScreen.OpenPage(SettingsPageKind.Audio));
            AddAssert("audio status icon is centred", () =>
            {
                Circle badge = settingsScreen.ActivePanel.ChildrenOfType<Circle>().Single(candidate => candidate.Size.X == 56);
                SpriteIcon icon = settingsScreen.ActivePanel.ChildrenOfType<SpriteIcon>().Single(candidate => candidate.Size.X == 26);

                return badge.Origin == Anchor.Centre &&
                       icon.Origin == Anchor.Centre &&
                       badge.Position == icon.Position;
            });

            AddStep("open General", () => settingsScreen.OpenPage(SettingsPageKind.General));
            AddAssert("language status icon is centred", () =>
            {
                Circle badge = settingsScreen.ActivePanel.ChildrenOfType<Circle>().Single(candidate => candidate.Size.X == 56);
                SpriteIcon icon = settingsScreen.ActivePanel.ChildrenOfType<SpriteIcon>().Single(candidate => candidate.Size.X == 26);

                return badge.Origin == Anchor.Centre &&
                       icon.Origin == Anchor.Centre &&
                       badge.Position == icon.Position;
            });
        }

        [Test]
        public void TestSettingsTypographyHasReadableMinimumSize()
        {
            foreach (SettingsPageKind page in System.Enum.GetValues<SettingsPageKind>())
            {
                SettingsPageKind capturedPage = page;
                AddStep($"open {page}", () => settingsScreen.OpenPage(capturedPage));
                AddAssert($"{page} has no tiny text", () =>
                    settingsScreen.ChildrenOfType<SpriteText>().All(text => text.Font.Size >= 14));
            }
        }

        [Test]
        public void TestInterfaceScaleIsInteractive()
        {
            DisplaySettingsPanel display = null;
            YokkoUiScale originalScale = YokkoUiScale.Comfortable;

            AddStep("open Display", () => settingsScreen.OpenPage(SettingsPageKind.Display));
            AddStep("capture display scale", () =>
            {
                display = (DisplaySettingsPanel)settingsScreen.ActivePanel;
                originalScale = display.CurrentUiScale;
            });
            AddStep("select large interface", () => display.SelectUiScale(YokkoUiScale.Large));
            AddAssert("large interface selected", () => display.CurrentUiScale == YokkoUiScale.Large);
            AddAssert("five display rows fit above footer", () =>
            {
                Container[] rows = display.ChildrenOfType<Container>()
                                          .Where(container =>
                                              container.Position.X == 378
                                              && container.Size.X == 840
                                              && container.Size.Y == 60)
                                          .ToArray();
                return rows.Length == 5
                       && rows.All(row => row.Y + row.Height < 651);
            });
            AddStep("select compact interface", () => display.SelectUiScale(YokkoUiScale.Compact));
            AddAssert("compact interface selected", () => display.CurrentUiScale == YokkoUiScale.Compact);
            AddStep("restore interface scale", () => display.SelectUiScale(originalScale));
        }

        [Test]
        public void TestAudioVolumeAndHitSoundsAreInteractive()
        {
            AudioSettingsPanel audio = null;
            double originalVolume = 1;
            double originalMusicVolume = 1;
            double originalHitSoundVolume = 1;
            bool originalHitSounds = true;

            AddStep("open Audio", () =>
                settingsScreen.OpenPage(SettingsPageKind.Audio));
            AddStep("capture audio preferences", () =>
            {
                audio = (AudioSettingsPanel)settingsScreen.ActivePanel;
                originalVolume = audio.CurrentMasterVolume;
                originalMusicVolume = audio.CurrentMusicVolume;
                originalHitSoundVolume = audio.CurrentHitSoundVolume;
                originalHitSounds = audio.HitSoundsEnabled;
            });
            AddStep("set master volume to 65%", () =>
                audio.SetMasterVolume(0.65));
            AddAssert("master volume changed", () =>
                audio.CurrentMasterVolume == 0.65);
            AddStep("set music volume to 55%", () =>
                audio.SetMusicVolume(0.55));
            AddAssert("music volume changed", () =>
                audio.CurrentMusicVolume == 0.55);
            AddStep("set hitsound volume to 40%", () =>
                audio.SetHitSoundVolume(0.4));
            AddAssert("hitsound volume changed", () =>
                audio.CurrentHitSoundVolume == 0.4);
            AddStep("disable hitsounds", () =>
                audio.SetHitSoundsEnabled(false));
            AddAssert("hitsounds disabled", () =>
                !audio.HitSoundsEnabled);
            AddAssert("three volume sliders are visible", () =>
                audio.ChildrenOfType<SettingsVolumeSlider>().Count() == 3);
            AddAssert("seven audio rows fit above footer", () =>
            {
                Container[] rows = audio.ChildrenOfType<Container>()
                                        .Where(container =>
                                            container.Position.X == 378
                                            && container.Size.X == 840
                                            && container.Size.Y == 54)
                                        .ToArray();
                return rows.Length == 7
                       && rows.All(row => row.Y + row.Height < 651);
            });
            AddStep("restore audio preferences", () =>
            {
                audio.SetMasterVolume(originalVolume);
                audio.SetMusicVolume(originalMusicVolume);
                audio.SetHitSoundVolume(originalHitSoundVolume);
                audio.SetHitSoundsEnabled(originalHitSounds);
            });
        }

        [Test]
        public void TestTransientInteractionsDismissInOrder()
        {
            GameplaySettingsPanel gameplay = null;
            AudioSettingsPanel audio = null;

            AddStep("open Gameplay", () => settingsScreen.OpenPage(SettingsPageKind.Gameplay));
            AddStep("capture Gameplay", () => gameplay = (GameplaySettingsPanel)settingsScreen.ActivePanel);
            AddStep("capture first lane", () => gameplay.BeginKeyCapture(0));
            AddAssert("key capture active", () => gameplay.IsCapturingKey);
            AddAssert("Esc layer dismisses capture", settingsScreen.DismissTransientUi);
            AddAssert("key capture dismissed", () => !gameplay.IsCapturingKey);

            AddStep("open Audio", () =>
                settingsScreen.OpenPage(SettingsPageKind.Audio));
            AddStep("capture audio", () =>
                audio = (AudioSettingsPanel)settingsScreen.ActivePanel);
            AddStep("open device menu", () => audio.ToggleDeviceMenu());
            AddAssert("device menu open", () => audio.IsDeviceMenuOpen);
            AddAssert("Esc layer dismisses menu", settingsScreen.DismissTransientUi);
            AddAssert("device menu closed", () => !audio.IsDeviceMenuOpen);
        }

        [Test]
        public void TestGameplayPreferencesAreInteractive()
        {
            GameplaySettingsPanel gameplay = null;
            double originalSpeed = OsuManiaScrollSpeed.Default;
            bool originalLaneFeedback = true;
            bool originalTimingBar = true;
            bool originalKeysoundsEnabled = true;
            bool originalMinesEnabled = true;
            bool originalPauseWhenUnfocused = true;
            JudgementMode originalJudgementMode =
                JudgementMode.Yokko;
            int originalEtternaJustice =
                JudgementConfiguration.DefaultEtternaJustice;
            AudioPitchMode originalPlaybackRatePitchMode =
                AudioPitchMode.Preserve;

            AddStep("open Gameplay", () => settingsScreen.OpenPage(SettingsPageKind.Gameplay));
            AddStep("capture Gameplay preferences", () =>
            {
                gameplay = (GameplaySettingsPanel)settingsScreen.ActivePanel;
                originalSpeed = gameplay.CurrentScrollSpeed;
                originalLaneFeedback = gameplay.ShowLanePressFeedback;
                originalTimingBar = gameplay.ShowTimingBar;
                originalKeysoundsEnabled = gameplay.KeysoundsEnabled;
                originalMinesEnabled = gameplay.MinesEnabled;
                originalPauseWhenUnfocused =
                    gameplay.PauseWhenUnfocused;
                originalJudgementMode =
                    gameplay.CurrentJudgementMode;
                originalEtternaJustice =
                    gameplay.CurrentEtternaJustice;
                originalPlaybackRatePitchMode =
                    gameplay.ManualPlaybackRatePitchMode;
            });
            AddStep("open timing", () =>
                gameplay.SelectSection(GameplaySettingsSection.Timing));
            AddAssert("timing selected", () =>
                gameplay.CurrentSection == GameplaySettingsSection.Timing);
            AddStep("set osu mania speed 26", () =>
                gameplay.SetScrollSpeed(26));
            AddAssert("speed changed", () =>
                gameplay.CurrentScrollSpeed == 26);
            AddStep("open playback rate", () =>
                gameplay.SelectSection(
                    GameplaySettingsSection.PlaybackRate));
            AddStep("select Nightcore shortcut rate mode", () =>
                gameplay.SetManualPlaybackRatePitchMode(
                    AudioPitchMode.ScaleWithRate));
            AddAssert("Nightcore shortcut rate mode selected", () =>
                gameplay.ManualPlaybackRatePitchMode
                    == AudioPitchMode.ScaleWithRate);
            AddStep("open judgement", () =>
                gameplay.SelectSection(
                    GameplaySettingsSection.Judgement));
            AddStep("select Yokko judgement", () =>
                gameplay.SetJudgementMode(JudgementMode.Yokko));
            AddAssert("Etterna Judge control is disabled", () =>
                !gameplay.IsEtternaJusticeControlEnabled);
            AddStep("select Etterna J8", () =>
            {
                gameplay.SetJudgementMode(JudgementMode.Etterna);
                gameplay.SetEtternaJustice(8);
            });
            AddAssert("Etterna J8 selected", () =>
                gameplay.CurrentJudgementMode
                    == JudgementMode.Etterna
                && gameplay.CurrentEtternaJustice == 8
                && gameplay.IsEtternaJusticeControlEnabled);
            AddStep("open feedback", () =>
                gameplay.SelectSection(GameplaySettingsSection.Feedback));
            AddStep("disable lane feedback", () =>
                gameplay.SetLanePressFeedback(false));
            AddAssert("feedback disabled", () =>
                !gameplay.ShowLanePressFeedback);
            AddStep("disable timing bar", () =>
                gameplay.SetShowTimingBar(false));
            AddAssert("timing bar disabled", () =>
                !gameplay.ShowTimingBar);
            AddStep("disable gameplay keysounds", () =>
                gameplay.SetKeysoundsEnabled(false));
            AddAssert("gameplay keysounds disabled", () =>
                !gameplay.KeysoundsEnabled);
            AddStep("disable mines", () =>
                gameplay.SetMinesEnabled(false));
            AddAssert("mines disabled", () =>
                !gameplay.MinesEnabled);
            AddStep("disable pause when unfocused", () =>
                gameplay.SetPauseWhenUnfocused(false));
            AddAssert("pause when unfocused disabled", () =>
                !gameplay.PauseWhenUnfocused);
            AddStep("disable resume countdown", () =>
                gameplay.SetResumeCountdownEnabled(false));
            AddAssert("resume countdown disabled", () =>
                !gameplay.ResumeCountdownEnabled);
            AddStep("set countdown duration to 1500", () =>
                gameplay.SetResumeCountdownMilliseconds(1500));
            AddAssert("countdown duration applied", () =>
                gameplay.ResumeCountdownMilliseconds == 1500);
            AddStep("open 7K bindings", () =>
            {
                gameplay.SelectSection(GameplaySettingsSection.Input);
                gameplay.SelectKeyMode(KeyMode.SevenKey);
            });
            AddAssert("7K selected", () =>
                gameplay.SelectedKeyMode == KeyMode.SevenKey);
            AddStep("start binding capture", () =>
                gameplay.BeginKeyCapture(3));
            AddStep("bind centre lane", () =>
                gameplay.HandleKeyDown(Key.V));
            AddAssert("capture completes", () => !gameplay.IsCapturingKey);
            AddStep("restore preferences", () =>
            {
                gameplay.ResetSelectedBindings();
                gameplay.SetScrollSpeed(originalSpeed);
                gameplay.SetJudgementMode(originalJudgementMode);
                gameplay.SetEtternaJustice(originalEtternaJustice);
                gameplay.SetManualPlaybackRatePitchMode(
                    originalPlaybackRatePitchMode);
                gameplay.SetLanePressFeedback(originalLaneFeedback);
                gameplay.SetShowTimingBar(originalTimingBar);
                gameplay.SetKeysoundsEnabled(originalKeysoundsEnabled);
                gameplay.SetMinesEnabled(originalMinesEnabled);
                gameplay.SetPauseWhenUnfocused(
                    originalPauseWhenUnfocused);
                gameplay.SetResumeCountdownEnabled(true);
                gameplay.SetResumeCountdownMilliseconds(
                    YokkoGameplaySettings
                        .DefaultResumeCountdownMilliseconds);
            });
        }

        [Test]
        public void TestGameplayOverflowContentCanScroll()
        {
            GameplaySettingsPanel gameplay = null;
            ScrollContainer<Drawable> content = null;

            AddStep("open Gameplay feedback", () =>
            {
                settingsScreen.OpenPage(SettingsPageKind.Gameplay);
                gameplay =
                    (GameplaySettingsPanel)settingsScreen.ActivePanel;
                gameplay.SelectSection(GameplaySettingsSection.Feedback);
                content = gameplay
                    .ChildrenOfType<ScrollContainer<Drawable>>()
                    .Single();
            });
            AddAssert("feedback content fits comfortably", () =>
                gameplay.ContentScrollableExtent == 0);
            AddStep("try to scroll fitting feedback", () =>
                gameplay.ScrollContentBy(1000));
            AddAssert("fitting feedback stays at top", () =>
                gameplay.ContentScrollPosition == 0);
            AddStep("simulate future overflow", () =>
                content.Child.Height = content.Height + 64);
            AddAssert("larger content becomes scrollable", () =>
                gameplay.ContentScrollableExtent > 0);
            AddStep("scroll feedback content", () =>
                gameplay.ScrollContentBy(1000));
            AddAssert("feedback content scrolls", () =>
                gameplay.ContentScrollPosition > 0);
            AddStep("switch to timing", () =>
                gameplay.SelectSection(GameplaySettingsSection.Timing));
            AddAssert("new section starts at top", () =>
                gameplay.ContentScrollPosition == 0);
        }

        [Test]
        public void TestGameplayKeysCanBeCapturedAsASequence()
        {
            GameplaySettingsPanel gameplay = null;

            AddStep("open Gameplay", () =>
                settingsScreen.OpenPage(SettingsPageKind.Gameplay));
            AddStep("capture Gameplay", () =>
                gameplay = (GameplaySettingsPanel)settingsScreen.ActivePanel);
            AddStep("select 4K", () =>
                gameplay.SelectKeyMode(KeyMode.FourKey));
            AddStep("start sequential capture", () =>
                gameplay.BeginSequentialKeyCapture());
            AddAssert("sequence starts at first lane", () =>
                gameplay.IsSequentialCapture &&
                gameplay.SequentialCaptureIndex == 0);
            AddStep("capture Z and X", () =>
            {
                gameplay.HandleKeyDown(Key.Z);
                gameplay.HandleKeyDown(Key.X);
            });
            AddAssert("sequence advances to third lane", () =>
                gameplay.IsSequentialCapture &&
                gameplay.SequentialCaptureIndex == 2);
            AddStep("duplicate key is ignored", () =>
                gameplay.HandleKeyDown(Key.X));
            AddAssert("duplicate keeps current lane active", () =>
                gameplay.SequentialCaptureIndex == 2);
            AddStep("finish with period and slash", () =>
            {
                gameplay.HandleKeyDown(Key.Period);
                gameplay.HandleKeyDown(Key.Slash);
            });
            AddAssert("sequence completes", () =>
                !gameplay.IsCapturingKey &&
                !gameplay.IsSequentialCapture);
            AddAssert("whole profile saved in order", () =>
                gameplay.GetBinding(KeyMode.FourKey, 0) == Key.Z &&
                gameplay.GetBinding(KeyMode.FourKey, 1) == Key.X &&
                gameplay.GetBinding(KeyMode.FourKey, 2) == Key.Period &&
                gameplay.GetBinding(KeyMode.FourKey, 3) == Key.Slash);
            AddStep("restore 4K defaults", () =>
                gameplay.ResetSelectedBindings());
        }

        [Test]
        public void TestEveryManiaKeyModeAndStandaloneShortcutsCanBeEdited()
        {
            GameplaySettingsPanel gameplay = null;
            ShortcutSettingsPanel shortcuts = null;

            AddStep("open Gameplay", () =>
                settingsScreen.OpenPage(SettingsPageKind.Gameplay));
            AddStep("capture Gameplay", () =>
                gameplay = (GameplaySettingsPanel)settingsScreen.ActivePanel);
            AddStep("select 10K + 10K", () =>
                gameplay.SelectKeyMode(KeyMode.TwentyKey));
            AddAssert("all dual-stage lanes are visible", () =>
                gameplay.SelectedKeyMode == KeyMode.TwentyKey
                && gameplay.VisibleBindingCardCount == 20);
            AddStep("bind final dual-stage lane", () =>
            {
                gameplay.BeginKeyCapture(19);
                gameplay.HandleKeyDown(Key.Slash);
            });
            AddAssert("20K custom key saved", () =>
                gameplay.GetBinding(KeyMode.TwentyKey, 19) == Key.Slash);
            AddStep("next wraps to 1K", () =>
                gameplay.SelectAdjacentKeyMode(1));
            AddAssert("mode picker wrapped", () =>
                gameplay.SelectedKeyMode == KeyMode.OneKey
                && gameplay.VisibleBindingCardCount == 1);
            AddStep("restore 10K + 10K defaults", () =>
            {
                gameplay.SelectKeyMode(KeyMode.TwentyKey);
                gameplay.ResetSelectedBindings();
            });
            AddStep("open standalone Shortcuts", () =>
                settingsScreen.OpenPage(SettingsPageKind.Shortcuts));
            AddStep("capture Shortcuts", () =>
                shortcuts =
                    (ShortcutSettingsPanel)settingsScreen.ActivePanel);
            AddAssert("shortcut controls support keyboard focus", () =>
                shortcuts.ChildrenOfType<GameplayCompactButton>()
                         .Where(button => button.IsEnabled)
                         .All(button => button.AcceptsFocus));
            AddStep("capture custom speed down key", () =>
                shortcuts.BeginShortcutCapture(
                    ManiaShortcutAction.DecreaseScrollSpeed));
            AddAssert("shortcut capture active", () =>
                shortcuts.IsCapturingShortcut);
            AddStep("bind speed down to F7", () =>
                shortcuts.HandleKeyDown(Key.F7));
            AddAssert("custom Mania shortcut saved", () =>
                shortcuts.GetShortcutBinding(
                    ManiaShortcutAction.DecreaseScrollSpeed) == Key.F7);
            AddStep("customise pause shortcut", () =>
            {
                shortcuts.BeginShortcutCapture(
                    ManiaShortcutAction.PauseOrBack);
                shortcuts.HandleKeyDown(Key.F10);
            });
            AddStep("open results shortcuts", () =>
                shortcuts.SelectShortcutPage(ManiaShortcutPage.Results));
            AddAssert("results shortcut page selected", () =>
                shortcuts.CurrentShortcutPage == ManiaShortcutPage.Results);
            AddStep("customise replay shortcut", () =>
            {
                shortcuts.BeginShortcutCapture(
                    ManiaShortcutAction.WatchReplay);
                shortcuts.HandleKeyDown(Key.F11);
            });
            AddStep("restore replay shortcut only", () =>
                shortcuts.ResetShortcutBinding(
                    ManiaShortcutAction.WatchReplay));
            AddAssert("single shortcut restored", () =>
                shortcuts.GetShortcutBinding(
                    ManiaShortcutAction.WatchReplay) == Key.V
                && shortcuts.GetShortcutBinding(
                    ManiaShortcutAction.PauseOrBack) == Key.F10);
            AddAssert("custom shortcut count is visible to the page", () =>
                shortcuts.ModifiedShortcutCount == 2);
            AddStep("request restore all Mania shortcuts", () =>
                shortcuts.RequestResetShortcutBindings());
            AddAssert("restore all waits for confirmation", () =>
                shortcuts.IsResetAllPending
                && shortcuts.GetShortcutBinding(
                    ManiaShortcutAction.PauseOrBack) == Key.F10);
            AddStep("confirm restore all Mania shortcuts", () =>
                shortcuts.RequestResetShortcutBindings());
            AddAssert("all shortcut defaults restored", () =>
                shortcuts.GetShortcutBinding(
                    ManiaShortcutAction.PauseOrBack) == Key.Escape
                && shortcuts.GetShortcutBinding(
                    ManiaShortcutAction.DecreaseScrollSpeed) == Key.F3
                && shortcuts.CanUndoResetAll);
            AddStep("undo restore all", () =>
                shortcuts.UndoResetShortcutBindings());
            AddAssert("custom shortcuts restored by undo", () =>
                shortcuts.GetShortcutBinding(
                    ManiaShortcutAction.PauseOrBack) == Key.F10
                && shortcuts.GetShortcutBinding(
                    ManiaShortcutAction.DecreaseScrollSpeed) == Key.F7);
            AddStep("restore all after undo", () =>
            {
                shortcuts.RequestResetShortcutBindings();
                shortcuts.RequestResetShortcutBindings();
            });
            AddAssert("final defaults restored", () =>
                shortcuts.ModifiedShortcutCount == 0);
        }

        [Test]
        public void TestGameplayInputMonitorPresetsAndCalibrationState()
        {
            GameplaySettingsPanel gameplay = null;
            IReadOnlyList<Key> originalFour = null;
            IReadOnlyList<Key> originalSeven = null;

            AddStep("open Gameplay input", () =>
            {
                settingsScreen.OpenPage(SettingsPageKind.Gameplay);
                gameplay =
                    (GameplaySettingsPanel)settingsScreen.ActivePanel;
                gameplay.SelectSection(GameplaySettingsSection.Input);
                gameplay.SelectKeyMode(KeyMode.FourKey);
                originalFour = Enumerable.Range(0, 4)
                    .Select(lane =>
                        gameplay.GetBinding(KeyMode.FourKey, lane))
                    .ToArray();
                originalSeven = Enumerable.Range(0, 7)
                    .Select(lane =>
                        gameplay.GetBinding(KeyMode.SevenKey, lane))
                    .ToArray();
            });
            AddStep("press a bound key", () =>
                gameplay.HandleKeyDown(originalFour[0]));
            AddAssert("live monitor tracks key down", () =>
                gameplay.PressedKeyCount == 1);
            AddStep("release the bound key", () =>
                gameplay.HandleKeyUp(originalFour[0]));
            AddAssert("live monitor clears key up", () =>
                gameplay.PressedKeyCount == 0);
            AddStep("bind lane one to lane two key", () =>
            {
                gameplay.BeginKeyCapture(0);
                gameplay.HandleKeyDown(originalFour[1]);
            });
            AddAssert("duplicate binding swaps lanes", () =>
                gameplay.GetBinding(KeyMode.FourKey, 0) == originalFour[1]
                && gameplay.GetBinding(KeyMode.FourKey, 1) ==
                originalFour[0]);
            AddStep("apply split preset", () =>
                gameplay.ApplyBindingPreset(GameplayKeyPreset.Split));
            AddAssert("split preset applied", () =>
                gameplay.GetBinding(KeyMode.FourKey, 0) == Key.Z
                && gameplay.GetBinding(KeyMode.FourKey, 3) == Key.Slash);
            AddStep("copy 4K to 7K", () =>
                gameplay.CopySelectedBindings());
            AddAssert("central lanes copied to 7K", () =>
                gameplay.GetBinding(KeyMode.SevenKey, 1) == Key.Z
                && gameplay.GetBinding(KeyMode.SevenKey, 2) == Key.X
                && gameplay.GetBinding(KeyMode.SevenKey, 4) == Key.Period
                && gameplay.GetBinding(KeyMode.SevenKey, 5) == Key.Slash);
            AddStep("start calibration state", () =>
                gameplay.StartCalibrationForTest(0));
            AddAssert("calibration is active", () =>
                gameplay.IsCalibrationActive);
            AddAssert("Esc layer cancels calibration", () =>
                gameplay.DismissTransientUi());
            AddAssert("calibration is cancelled", () =>
                !gameplay.IsCalibrationActive);
            AddStep("restore original profiles", () =>
            {
                gameplay.SelectKeyMode(KeyMode.FourKey);
                gameplay.BeginSequentialKeyCapture();
                foreach (Key key in originalFour)
                    gameplay.HandleKeyDown(key);
                gameplay.SelectKeyMode(KeyMode.SevenKey);
                gameplay.BeginSequentialKeyCapture();
                foreach (Key key in originalSeven)
                    gameplay.HandleKeyDown(key);
            });
        }

        [Test]
        public void TestLanguageCanBeChangedImmediately()
        {
            GeneralSettingsPanel general = null;

            AddStep("open General", () => settingsScreen.OpenPage(SettingsPageKind.General));
            AddStep("capture General", () => general = (GeneralSettingsPanel)settingsScreen.ActivePanel);
            AddStep("select English", () => general.SelectLanguage("en"));
            AddAssert("English selected", () => general.CurrentLocale == "en");
            AddStep("select Chinese", () => general.SelectLanguage("zh"));
            AddAssert("Chinese selected", () => general.CurrentLocale == "zh");
            AddStep("select Japanese", () => general.SelectLanguage("ja"));
            AddAssert("Japanese selected", () => general.CurrentLocale == "ja");
            AddStep("restore English", () => general.SelectLanguage("en"));
            AddAssert("English restored", () => general.CurrentLocale == "en");
        }

        [Test]
        public void TestScrollSpeedCanBeAdjustedFromGeneral()
        {
            GeneralSettingsPanel general = null;
            double originalSpeed = OsuManiaScrollSpeed.Default;

            AddStep("open General", () =>
                settingsScreen.OpenPage(SettingsPageKind.General));
            AddStep("capture General", () =>
            {
                general = (GeneralSettingsPanel)settingsScreen.ActivePanel;
                originalSpeed = general.CurrentScrollSpeed;
            });
            AddStep("set osu mania speed 24", () =>
                general.SetScrollSpeed(24));
            AddAssert("general speed changed", () =>
                general.CurrentScrollSpeed == 24);
            AddStep("restore speed", () =>
                general.SetScrollSpeed(originalSpeed));
        }

        [Test]
        public void TestImportPageShowsCapabilitiesAndUpdatesPreferences()
        {
            ImportSettingsPanel import = null;

            AddStep("open Import", () => settingsScreen.OpenPage(SettingsPageKind.Import));
            AddStep("capture Import", () => import = (ImportSettingsPanel)settingsScreen.ActivePanel);
            AddAssert("all importer families shown", () => import.FormatFamilyCount == 5);
            AddAssert("all supported extensions shown", () => import.FileTypeCount == 13);

            AddStep("disable keysounds", () => import.SetPreferKeysounds(false));
            AddAssert("keysounds disabled", () => !import.PreferKeysounds);
            AddStep("disable SSC preference", () => import.SetPreferSscSimfiles(false));
            AddAssert("SSC preference disabled", () => !import.PreferSscSimfiles);
            AddStep("enable BMS scratch", () => import.SetEnableBmsScratch(true));
            AddAssert("BMS scratch enabled", () => import.EnableBmsScratch);
            AddStep("disable warnings", () => import.SetShowCompatibilityWarnings(false));
            AddAssert("warnings disabled", () => !import.ShowCompatibilityWarnings);

            AddStep("restore import defaults", () =>
            {
                import.SetPreferKeysounds(true);
                import.SetPreferSscSimfiles(true);
                import.SetEnableBmsScratch(false);
                import.SetShowCompatibilityWarnings(true);
            });
            AddAssert("import defaults restored", () =>
                import.PreferKeysounds &&
                import.PreferSscSimfiles &&
                !import.EnableBmsScratch &&
                import.ShowCompatibilityWarnings);
        }

        [Test]
        public void TestAudioPreferencesApplyToSharedTruth()
        {
            AudioSettingsPanel audio = null;
            AudioBackendKind originalBackend = default;
            int originalBuffer = 0;
            double originalOffset = 0;

            AddStep("open Audio", () => settingsScreen.OpenPage(SettingsPageKind.Audio));
            AddStep("capture Audio", () =>
            {
                audio = (AudioSettingsPanel)settingsScreen.ActivePanel;
                originalBackend = audio.CurrentBackend;
                originalBuffer = audio.CurrentBufferSize;
                originalOffset = audio.CurrentOffsetMilliseconds;
            });
            AddStep("select shared output", () => audio.SelectBackend(AudioBackendKind.SharedWasapi));
            AddAssert("shared output selected", () => audio.CurrentBackend == AudioBackendKind.SharedWasapi);
            AddStep("select 256-frame profile", () => audio.SelectBufferSize(256));
            AddAssert("256-frame profile selected", () => audio.CurrentBufferSize == 256);
            AddStep("set +12 ms offset", () => audio.SetOffset(12));
            AddAssert("offset selected", () => audio.CurrentOffsetMilliseconds == 12);
            AddStep("restore audio preferences", () =>
            {
                audio.SelectBackend(originalBackend);
                audio.SelectBufferSize(originalBuffer);
                audio.SetOffset(originalOffset);
            });
        }
    }
}
