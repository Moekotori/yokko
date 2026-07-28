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
            double originalSpeed = 1;
            bool originalHud = true;
            bool originalHitError = true;
            bool originalLaneFeedback = true;

            AddStep("open Gameplay", () => settingsScreen.OpenPage(SettingsPageKind.Gameplay));
            AddStep("capture Gameplay preferences", () =>
            {
                gameplay = (GameplaySettingsPanel)settingsScreen.ActivePanel;
                originalSpeed = gameplay.CurrentScrollSpeed;
                originalHud = gameplay.ShowGameplayHud;
                originalHitError = gameplay.ShowHitError;
                originalLaneFeedback = gameplay.ShowLanePressFeedback;
            });
            AddStep("open timing", () =>
                gameplay.SelectSection(GameplaySettingsSection.Timing));
            AddAssert("timing selected", () =>
                gameplay.CurrentSection == GameplaySettingsSection.Timing);
            AddStep("set 1.35 speed", () => gameplay.SetScrollSpeed(1.35));
            AddAssert("speed changed", () =>
                gameplay.CurrentScrollSpeed == 1.35);
            AddStep("open feedback", () =>
                gameplay.SelectSection(GameplaySettingsSection.Feedback));
            AddStep("disable feedback", () =>
                gameplay.SetFeedback(false, false, false));
            AddAssert("feedback disabled", () =>
                !gameplay.ShowGameplayHud &&
                !gameplay.ShowHitError &&
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
                gameplay.SetFeedback(
                    originalHud,
                    originalHitError,
                    originalLaneFeedback);
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
