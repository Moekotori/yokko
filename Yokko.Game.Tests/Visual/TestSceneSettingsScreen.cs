using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osuTK.Input;
using Yokko.Audio;
using Yokko.Core.Gameplay;
using Yokko.Game.Gameplay;
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
            foreach (SettingsPageKind page in System.Enum.GetValues<SettingsPageKind>())
            {
                SettingsPageKind capturedPage = page;
                AddStep($"open {page}", () => settingsScreen.OpenPage(capturedPage));
                AddAssert($"{page} is current", () => settingsScreen.CurrentPage == capturedPage);
            }
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
        public void TestTransientInteractionsDismissInOrder()
        {
            GameplaySettingsPanel gameplay = null;
            DisplaySettingsPanel display = null;

            AddStep("open Gameplay", () => settingsScreen.OpenPage(SettingsPageKind.Gameplay));
            AddStep("capture Gameplay", () => gameplay = (GameplaySettingsPanel)settingsScreen.ActivePanel);
            AddStep("capture first lane", () => gameplay.BeginKeyCapture(0));
            AddAssert("key capture active", () => gameplay.IsCapturingKey);
            AddAssert("Esc layer dismisses capture", settingsScreen.DismissTransientUi);
            AddAssert("key capture dismissed", () => !gameplay.IsCapturingKey);

            AddStep("open Display", () => settingsScreen.OpenPage(SettingsPageKind.Display));
            AddStep("capture display", () => display = (DisplaySettingsPanel)settingsScreen.ActivePanel);
            AddStep("open resolution menu", () => display.ToggleResolutionMenu());
            AddAssert("resolution menu open", () => display.IsResolutionMenuOpen);
            AddAssert("Esc layer dismisses menu", settingsScreen.DismissTransientUi);
            AddAssert("resolution menu closed", () => !display.IsResolutionMenuOpen);
        }

        [Test]
        public void TestGameplayPreferencesAreInteractive()
        {
            GameplaySettingsPanel gameplay = null;
            double originalSpeed = OsuManiaScrollSpeed.Default;
            bool originalLaneFeedback = true;

            AddStep("open Gameplay", () => settingsScreen.OpenPage(SettingsPageKind.Gameplay));
            AddStep("capture Gameplay preferences", () =>
            {
                gameplay = (GameplaySettingsPanel)settingsScreen.ActivePanel;
                originalSpeed = gameplay.CurrentScrollSpeed;
                originalLaneFeedback = gameplay.ShowLanePressFeedback;
            });
            AddStep("open timing", () =>
                gameplay.SelectSection(GameplaySettingsSection.Timing));
            AddAssert("timing selected", () =>
                gameplay.CurrentSection == GameplaySettingsSection.Timing);
            AddStep("set osu mania speed 26", () =>
                gameplay.SetScrollSpeed(26));
            AddAssert("speed changed", () =>
                gameplay.CurrentScrollSpeed == 26);
            AddStep("open feedback", () =>
                gameplay.SelectSection(GameplaySettingsSection.Feedback));
            AddStep("disable lane feedback", () =>
                gameplay.SetLanePressFeedback(false));
            AddAssert("feedback disabled", () =>
                !gameplay.ShowLanePressFeedback);
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
                gameplay.SetLanePressFeedback(originalLaneFeedback);
            });
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
            AddAssert("all supported extensions shown", () => import.FileTypeCount == 12);

            AddStep("disable keysounds", () => import.SetPreferKeysounds(false));
            AddAssert("keysounds disabled", () => !import.PreferKeysounds);
            AddStep("disable SSC preference", () => import.SetPreferSscSimfiles(false));
            AddAssert("SSC preference disabled", () => !import.PreferSscSimfiles);
            AddStep("disable warnings", () => import.SetShowCompatibilityWarnings(false));
            AddAssert("warnings disabled", () => !import.ShowCompatibilityWarnings);

            AddStep("restore import defaults", () =>
            {
                import.SetPreferKeysounds(true);
                import.SetPreferSscSimfiles(true);
                import.SetShowCompatibilityWarnings(true);
            });
            AddAssert("import defaults restored", () =>
                import.PreferKeysounds &&
                import.PreferSscSimfiles &&
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
