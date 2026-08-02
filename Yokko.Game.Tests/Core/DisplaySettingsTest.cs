using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Configuration;
using osu.Framework.Platform;
using Yokko.Core.Difficulty;
using Yokko.Desktop.Platform;
using Yokko.Game.Audio;
using Yokko.Game.Configuration;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Editor;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.Main;
using Yokko.Game.Screens.Settings;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class DisplaySettingsTest
{
    [Test]
    public void WindowsGameStorageUsesRoamingUserDirectory()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Ignore("Windows desktop storage policy only applies on Windows.");

        string expected = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData,
                Environment.SpecialFolderOption.Create),
            "Yokko");

        Assert.That(WindowsPersistentStorage.RootPath, Is.EqualTo(expected));
        Assert.That(Path.IsPathFullyQualified(expected), Is.True);
    }

    [TestCase(true, 15, 15)]
    [TestCase(true, 85, 85)]
    [TestCase(true, 240, 240)]
    [TestCase(true, 0, 0)]
    [TestCase(true, 500, 240)]
    [TestCase(false, 85, 0)]
    public void BackgroundFrameRateMapsToInactiveHostLimit(
        bool dynamicBackgroundFrameRate,
        double frameRate,
        double expected)
    {
        Assert.That(
            YokkoDesktopBehaviourController.GetMaximumInactiveHz(
                dynamicBackgroundFrameRate,
                frameRate),
            Is.EqualTo(expected));
    }

    [Test]
    public void BackgroundAudioPolicyChangesEffectiveMix()
    {
        var settings = new YokkoAudioSettings();
        settings.MasterVolume.Value = 0.5;
        settings.MusicVolume.Value = 0.8;

        settings.SetApplicationActive(false);
        settings.BackgroundVolume.Value = 0.2;
        Assert.That(settings.EffectiveMusicVolume, Is.EqualTo(0.08).Within(0.0001));

        settings.BackgroundVolume.Value = 0;
        Assert.That(settings.EffectiveMusicVolume, Is.Zero);

        settings.BackgroundVolume.Value = 0.37;
        Assert.That(settings.EffectiveMusicVolume, Is.EqualTo(0.148).Within(0.0001));

        settings.BackgroundVolume.Value = 1;
        Assert.That(settings.EffectiveMusicVolume, Is.EqualTo(0.4).Within(0.0001));

        settings.SetApplicationActive(true);
        settings.BackgroundVolume.Value = 0;
        Assert.That(settings.EffectiveMusicVolume, Is.EqualTo(0.4).Within(0.0001));
    }

    [Test]
    public void DesktopPreferencesPersistAcrossConfigInstances()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "desktop-settings-config",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var firstDisplay = new YokkoDisplaySettings();
            var firstAudio = new YokkoAudioSettings();
            using (var config = new YokkoConfigManager(new NativeStorage(directory)))
            {
                config.BindDisplaySettings(firstDisplay);
                config.BindAudioSettings(firstAudio);
                firstDisplay.FastAltTab.Value = false;
                firstDisplay.DynamicBackgroundFrameRate.Value = false;
                firstDisplay.BackgroundFrameRate.Value = 85;
                firstAudio.BackgroundVolume.Value = 0.37;
                config.SetWindowMaximised(true);
                Assert.That(config.Save(), Is.True);
            }

            var restoredDisplay = new YokkoDisplaySettings();
            var restoredAudio = new YokkoAudioSettings();
            using (var config = new YokkoConfigManager(new NativeStorage(directory)))
            {
                config.BindDisplaySettings(restoredDisplay);
                config.BindAudioSettings(restoredAudio);
                Assert.Multiple(() =>
                {
                    Assert.That(restoredDisplay.FastAltTab.Value, Is.False);
                    Assert.That(
                        restoredDisplay.DynamicBackgroundFrameRate.Value,
                        Is.False);
                    Assert.That(restoredDisplay.BackgroundFrameRate.Value, Is.EqualTo(85));
                    Assert.That(restoredAudio.BackgroundVolume.Value, Is.EqualTo(0.37));
                    Assert.That(config.GetWindowMaximised(), Is.True);
                });
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Test]
    public void DesktopSlidersExposeContinuousValuesAndUnlimitedEndpoint()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                DesktopSettingsSlider.FrameRateFromProgress(0),
                Is.EqualTo(YokkoDisplaySettings.MinimumBackgroundFrameRate));
            Assert.That(
                DesktopSettingsSlider.FrameRateFromProgress(1),
                Is.EqualTo(YokkoDisplaySettings.UnlimitedBackgroundFrameRate));
            Assert.That(
                DesktopSettingsSlider.FrameRateProgress(0),
                Is.EqualTo(1));
            Assert.That(
                DesktopSettingsSlider.AdjustFrameRate(60, 1),
                Is.EqualTo(65));
            Assert.That(
                DesktopSettingsSlider.PercentageFromProgress(0.374),
                Is.EqualTo(0.37));
        });
    }

    [Test]
    public void LegacyDesktopChoicesMigrateToSliderValues()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "desktop-slider-migration",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            using (var legacy = new YokkoConfigManager(new NativeStorage(directory)))
            {
                legacy.SetValue(
                    YokkoSetting.DisplayBackgroundFrameRate,
                    YokkoBackgroundFrameRate.Fps60);
                legacy.SetValue(
                    YokkoSetting.AudioBackgroundMode,
                    BackgroundAudioMode.Dim);
                Assert.That(legacy.Save(), Is.True);
            }

            var display = new YokkoDisplaySettings();
            var audio = new YokkoAudioSettings();
            using (var migrated = new YokkoConfigManager(new NativeStorage(directory)))
            {
                migrated.BindDisplaySettings(display);
                migrated.BindAudioSettings(audio);
                Assert.Multiple(() =>
                {
                    Assert.That(display.BackgroundFrameRate.Value, Is.EqualTo(60));
                    Assert.That(audio.BackgroundVolume.Value, Is.EqualTo(0.2));
                });
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Test]
    public void CorruptedWindowSizeUsesSafeHighDpiFallback()
    {
        Assert.That(
            YokkoWindowSizeGuard.CalculateSafeWindowedSize(
                new System.Drawing.Size(5094, 7929),
                new System.Drawing.Size(3200, 2000),
                2),
            Is.EqualTo(new System.Drawing.Size(1280, 720)));
    }

    [Test]
    public void NormalWindowSizeSurvivesHighDpiValidation()
    {
        Assert.That(
            YokkoWindowSizeGuard.CalculateSafeWindowedSize(
                new System.Drawing.Size(1280, 720),
                new System.Drawing.Size(3200, 2000),
                2),
            Is.EqualTo(new System.Drawing.Size(1280, 720)));
    }

    [Test]
    public void OversizedLandscapeWindowIsFittedToDisplay()
    {
        System.Drawing.Size corrected =
            YokkoWindowSizeGuard.CalculateSafeWindowedSize(
            new System.Drawing.Size(2560, 1440),
            new System.Drawing.Size(1920, 1080),
            1);

        Assert.Multiple(() =>
        {
            Assert.That(corrected.Width, Is.LessThanOrEqualTo(1888));
            Assert.That(corrected.Height, Is.LessThanOrEqualTo(1000));
            Assert.That(
                corrected.Width / (float)corrected.Height,
                Is.EqualTo(16f / 9f).Within(0.01f));
        });
    }

    [Test]
    public void InvalidDpiScaleFallsBackToOne()
    {
        Assert.That(
            YokkoWindowSizeGuard.CalculateSafeWindowedSize(
                new System.Drawing.Size(1600, 900),
                new System.Drawing.Size(1920, 1080),
                float.NaN),
            Is.EqualTo(new System.Drawing.Size(1600, 900)));
    }

    [Test]
    public void LastSettingsPagePersistsAcrossConfigInstances()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "settings-page-config",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            using (var firstConfig = new YokkoConfigManager(new NativeStorage(directory)))
                firstConfig.SetLastSettingsPage(SettingsPageKind.Gameplay.ToString());

            using (var restoredConfig = new YokkoConfigManager(new NativeStorage(directory)))
            {
                Assert.That(
                    restoredConfig.GetLastSettingsPage(),
                    Is.EqualTo(SettingsPageKind.Gameplay.ToString()));
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Test]
    public void LocalPlayerIdentityIsCreatedAndPersists()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "player-identity-config",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string playerName;
            string playerId;
            using (var firstConfig = new YokkoConfigManager(
                       new NativeStorage(directory)))
            {
                playerName = firstConfig.Get<string>(
                    YokkoSetting.PlayerDisplayName);
                playerId = firstConfig.Get<string>(YokkoSetting.PlayerId);
                Assert.That(firstConfig.Save(), Is.True);
            }

            Assert.Multiple(() =>
            {
                Assert.That(playerName, Is.Not.Empty);
                Assert.That(playerId, Does.Match("^[0-9]{8}$"));
            });

            using var restoredConfig = new YokkoConfigManager(
                new NativeStorage(directory));
            Assert.Multiple(() =>
            {
                Assert.That(
                    restoredConfig.Get<string>(YokkoSetting.PlayerDisplayName),
                    Is.EqualTo(playerName));
                Assert.That(
                    restoredConfig.Get<string>(YokkoSetting.PlayerId),
                    Is.EqualTo(playerId));
            });
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Test]
    public void InterfaceScalePersistsAcrossConfigInstances()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "interface-scale-config",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var firstSettings = new YokkoDisplaySettings();
            using (var firstConfig = new YokkoConfigManager(new NativeStorage(directory)))
            {
                firstConfig.BindDisplaySettings(firstSettings);
                Assert.That(firstSettings.UiScale.Value, Is.EqualTo(YokkoUiScale.Comfortable));
                firstSettings.UiScale.Value = YokkoUiScale.Large;
                Assert.That(firstConfig.Save(), Is.True);
            }

            var restoredSettings = new YokkoDisplaySettings();
            using (var restoredConfig = new YokkoConfigManager(new NativeStorage(directory)))
            {
                restoredConfig.BindDisplaySettings(restoredSettings);
                Assert.That(restoredSettings.UiScale.Value, Is.EqualTo(YokkoUiScale.Large));
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Test]
    public void InterfaceScaleTargetsRemainSixteenByNineAndOrdered()
    {
        var settings = new YokkoDisplaySettings();

        settings.UiScale.Value = YokkoUiScale.Large;
        var large = settings.TargetDrawSize;
        settings.UiScale.Value = YokkoUiScale.Comfortable;
        var comfortable = settings.TargetDrawSize;
        settings.UiScale.Value = YokkoUiScale.Compact;
        var compact = settings.TargetDrawSize;

        Assert.Multiple(() =>
        {
            Assert.That(
                large,
                Is.EqualTo(
                    YokkoDisplaySettings.ReferenceLayoutSize / 1.1f));
            Assert.That(
                comfortable,
                Is.EqualTo(YokkoDisplaySettings.ReferenceLayoutSize));
            Assert.That(
                compact,
                Is.EqualTo(
                    YokkoDisplaySettings.ReferenceLayoutSize / 0.9f));
            Assert.That(large.X / large.Y, Is.EqualTo(16f / 9f).Within(0.001f));
            Assert.That(comfortable.X / comfortable.Y, Is.EqualTo(16f / 9f).Within(0.001f));
            Assert.That(compact.X / compact.Y, Is.EqualTo(16f / 9f).Within(0.001f));
            Assert.That(large.X, Is.LessThan(comfortable.X));
            Assert.That(comfortable.X, Is.LessThan(compact.X));
            Assert.That(YokkoDisplaySettings.GetScalePercentage(YokkoUiScale.Large), Is.EqualTo(110));
            Assert.That(YokkoDisplaySettings.GetScalePercentage(YokkoUiScale.Comfortable), Is.EqualTo(100));
            Assert.That(YokkoDisplaySettings.GetScalePercentage(YokkoUiScale.Compact), Is.EqualTo(90));
        });
    }

    [TestCase(YokkoUiScale.Large, 1.1f)]
    [TestCase(YokkoUiScale.Comfortable, 1f)]
    [TestCase(YokkoUiScale.Compact, 0.9f)]
    public void InterfaceScaleChangesAtAuthoredResolution(
        YokkoUiScale scale,
        float expected)
    {
        Assert.That(
            YokkoDisplaySettings.CalculateContentScale(
                new osuTK.Vector2(1920, 1080),
                scale),
            Is.EqualTo(expected).Within(0.001f));
    }

    [TestCase(YokkoUiScale.Large, 2.2f)]
    [TestCase(YokkoUiScale.Comfortable, 2f)]
    [TestCase(YokkoUiScale.Compact, 1.8f)]
    public void InterfaceScaleFollowsDesktopResolution(
        YokkoUiScale scale,
        float expected)
    {
        Assert.That(
            YokkoDisplaySettings.CalculateContentScale(
                new osuTK.Vector2(3840, 2346),
                scale),
            Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void InterfaceScaleUsesFullFourKViewport()
    {
        Assert.That(
            YokkoDisplaySettings.CalculateContentScale(
                new osuTK.Vector2(3840, 2160),
                YokkoUiScale.Large),
            Is.EqualTo(2.2f).Within(0.001f));
    }

    [Test]
    public void VisualPreviewsDefaultToShared1080PReference()
    {
        Assert.That(
            YokkoTestBrowser.GetPreviewWindowSize(),
            Is.EqualTo(new System.Drawing.Size(1920, 1080)));
    }

    [Test]
    public void EditorPreservesItsLegacyCanvasScaleAt1080P()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                EditorScreen.CalculateResponsiveStageScale(
                    YokkoDisplaySettings.ReferenceLayoutSize),
                Is.EqualTo(1.45f).Within(0.001f));
            Assert.That(
                EditorScreen.CalculateResponsiveStageScale(
                    new osuTK.Vector2(960, 540)),
                Is.EqualTo(960f / 1122f).Within(0.001f));
        });
    }

    [Test]
    public void MainScreenUsesFullScaledViewportWithoutCappingLayout()
    {
        var stage = MainScreen.CalculateResponsiveStageSize(
            new osuTK.Vector2(2133, 1303));

        Assert.Multiple(() =>
        {
            Assert.That(stage, Is.EqualTo(new osuTK.Vector2(2133, 1303)));
            Assert.That(
                MainScreen.CalculateRightStageOffset(stage),
                Is.EqualTo(new osuTK.Vector2(853, 291.5f)));
            Assert.That(
                MainScreen.CalculateMusicPlayerY(stage),
                Is.EqualTo(871.5f));
            Assert.That(
                MainScreen.CalculateMusicPlayerY(
                    new osuTK.Vector2(1280, 720)),
                Is.EqualTo(580));
            Assert.That(
                MainScreen.CalculateResponsiveStageSize(
                    new osuTK.Vector2(1280, 720)),
                Is.EqualTo(new osuTK.Vector2(1280, 720)));
        });
    }

    [Test]
    public void SettingsScreenUsesFullScaledViewport()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                SettingsScreen.CalculateResponsiveStageSize(
                    YokkoDisplaySettings.ReferenceLayoutSize),
                Is.EqualTo(
                    YokkoDisplaySettings.ReferenceLayoutSize
                    / SettingsScreen.ReferenceLayoutScale));
            Assert.That(
                SettingsScreen.CalculateResponsiveStageSize(
                    YokkoDisplaySettings.GetTargetDrawSize(
                        YokkoUiScale.Comfortable)),
                Is.EqualTo(
                    YokkoDisplaySettings.ReferenceLayoutSize
                    / SettingsScreen.ReferenceLayoutScale));
            Assert.That(
                SettingsScreen.CalculateResponsiveStageSize(
                    YokkoDisplaySettings.GetTargetDrawSize(
                        YokkoUiScale.Compact)),
                Is.EqualTo(
                    YokkoDisplaySettings.ReferenceLayoutSize
                    / 0.9f
                    / SettingsScreen.ReferenceLayoutScale));
        });
    }

    [Test]
    public void ResultScreenScalesThe1920By1080CanvasProportionally()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                GameplayResultOverlay.CalculateResponsiveStageScale(
                    new osuTK.Vector2(1777.7778f, 1000)),
                Is.EqualTo(0.9259259f).Within(0.0001f));
            Assert.That(
                GameplayResultOverlay.CalculateResponsiveStageScale(
                    new osuTK.Vector2(1024, 576)),
                Is.EqualTo(0.5333333f).Within(0.0001f));
            Assert.That(
                GameplayResultOverlay.CalculateResponsiveStageScale(
                    new osuTK.Vector2(1600, 1000)),
                Is.EqualTo(0.8333333f).Within(0.0001f));
        });
    }

    [Test]
    public void SettingsStageShrinksBelowReferenceInsteadOfClipping()
    {
        Assert.Multiple(() =>
        {
            // 参考布局（1920x1080）下保持 1.25 倍放大。
            var referenceStage = SettingsScreen.CalculateResponsiveStageSize(
                YokkoDisplaySettings.ReferenceLayoutSize);
            Assert.That(
                SettingsScreen.CalculateStageScale(
                    YokkoDisplaySettings.ReferenceLayoutSize,
                    referenceStage),
                Is.EqualTo(SettingsScreen.ReferenceLayoutScale).Within(0.001f));

            // 小于参考布局的窗口不再裁切左右/底部，而是整体缩小到完整可见。
            var smallViewport = new osuTK.Vector2(1280, 720);
            var smallStage = SettingsScreen.CalculateResponsiveStageSize(smallViewport);
            float smallScale = SettingsScreen.CalculateStageScale(smallViewport, smallStage);
            Assert.That(smallScale, Is.EqualTo(1f).Within(0.001f));
            Assert.That(smallStage.X * smallScale, Is.LessThanOrEqualTo(smallViewport.X));
            Assert.That(smallStage.Y * smallScale, Is.LessThanOrEqualTo(smallViewport.Y));

            var tinyViewport = new osuTK.Vector2(1024, 600);
            var tinyStage = SettingsScreen.CalculateResponsiveStageSize(tinyViewport);
            float tinyScale = SettingsScreen.CalculateStageScale(tinyViewport, tinyStage);
            Assert.That(tinyScale, Is.LessThan(1f));
            Assert.That(tinyStage.X * tinyScale, Is.LessThanOrEqualTo(tinyViewport.X + 0.01f));
            Assert.That(tinyStage.Y * tinyScale, Is.LessThanOrEqualTo(tinyViewport.Y + 0.01f));
        });
    }

    [Test]
    public void SettingsContentCentresOnLargeStages()
    {
        // 2560x1440 全屏时舞台放大到 2048x1152，多出的空间要重新分配，
        // 不能把内容留在左上角让右侧空成一片。
        var largeStage = new osuTK.Vector2(2048, 1152);

        Assert.Multiple(() =>
        {
            // 内容列在侧边栏与右边缘之间居中。
            Assert.That(
                SettingsScreen.CalculateContentOffset(largeStage),
                Is.EqualTo(new osuTK.Vector2(384, 216)));

            // 参考布局下一切保持设计稿原样，零偏移。
            var designedStage = new osuTK.Vector2(1280, 720);
            Assert.That(
                SettingsScreen.CalculateContentOffset(designedStage),
                Is.EqualTo(osuTK.Vector2.Zero));
        });
    }

    [Test]
    public void SettingsBackButtonPressKeepsItsLayoutPosition()
    {
        Assert.That(
            SettingsOutlineButton.CalculatePressedY(182),
            Is.EqualTo(184));
    }

    [Test]
    public void ResolutionOnlyUsesWindowedSizeInWindowedMode()
    {
        var windowedResolution = new System.Drawing.Size(2560, 1440);
        var displayResolution = new System.Drawing.Size(3200, 2000);

        Assert.Multiple(() =>
        {
            Assert.That(
                DisplaySettingsPanel.GetDisplayedResolution(
                    WindowMode.Windowed,
                    windowedResolution,
                    displayResolution),
                Is.EqualTo(windowedResolution));
            Assert.That(DisplaySettingsPanel.CanChooseResolution(WindowMode.Windowed), Is.True);

            foreach (WindowMode mode in new[] { WindowMode.Borderless, WindowMode.Fullscreen })
            {
                Assert.That(
                    DisplaySettingsPanel.GetDisplayedResolution(
                        mode,
                        windowedResolution,
                        displayResolution),
                    Is.EqualTo(displayResolution));
                Assert.That(DisplaySettingsPanel.CanChooseResolution(mode), Is.False);
            }
        });
    }

    [Test]
    public void WindowAspectRatioDefaultsToNormalSixteenByNine()
    {
        Assert.That(
            DisplaySettingsPanel.DefaultAspectRatio,
            Is.EqualTo(DisplaySettingsPanel.WindowAspectRatio.Normal16By9));
        Assert.That(
            DisplaySettingsPanel.GetAspectRatio(
                new System.Drawing.Size(1280, 720)),
            Is.EqualTo(DisplaySettingsPanel.WindowAspectRatio.Normal16By9));
    }

    [Test]
    public void WindowAspectRatioRecognisesCommonSizes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                DisplaySettingsPanel.GetAspectRatio(
                    new System.Drawing.Size(1920, 1200)),
                Is.EqualTo(
                    DisplaySettingsPanel.WindowAspectRatio.Ratio16By10));
            Assert.That(
                DisplaySettingsPanel.GetAspectRatio(
                    new System.Drawing.Size(1440, 1080)),
                Is.EqualTo(
                    DisplaySettingsPanel.WindowAspectRatio.Ratio4By3));
            Assert.That(
                DisplaySettingsPanel.GetAspectRatio(
                    new System.Drawing.Size(3440, 1440)),
                Is.EqualTo(
                    DisplaySettingsPanel.WindowAspectRatio
                        .Ultrawide21By9));
        });
    }

    [Test]
    public void SwitchingAspectRatioKeepsNearestResolutionHeight()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                DisplaySettingsPanel.ChooseResolutionForAspect(
                    DisplaySettingsPanel.WindowAspectRatio.Ratio16By10,
                    new System.Drawing.Size(1920, 1080)),
                Is.EqualTo(new System.Drawing.Size(1680, 1050)));
            Assert.That(
                DisplaySettingsPanel.ChooseResolutionForAspect(
                    DisplaySettingsPanel.WindowAspectRatio.Ultrawide21By9,
                    new System.Drawing.Size(1920, 1080)),
                Is.EqualTo(new System.Drawing.Size(2560, 1080)));
        });
    }

    [Test]
    public void UltrawideWindowSizeIsPreservedWhenFittedToDisplay()
    {
        System.Drawing.Size fitted =
            YokkoWindowSizeGuard.CalculateSafeWindowedSize(
                new System.Drawing.Size(3440, 1440),
                new System.Drawing.Size(2560, 1440),
                1);

        Assert.That(
            fitted.Width / (double)fitted.Height,
            Is.EqualTo(3440d / 1440d).Within(0.002));
    }

    [Test]
    public void FrameLimitPersistsAcrossConfigInstances()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "display-settings-config",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var firstSettings = new YokkoDisplaySettings();
            using (var firstConfig = new YokkoConfigManager(new NativeStorage(directory)))
            {
                firstConfig.BindDisplaySettings(firstSettings);
                firstSettings.FrameLimit.Value = YokkoFrameLimit.Limit8x;
                Assert.That(firstConfig.Save(), Is.True);
            }

            var restoredSettings = new YokkoDisplaySettings();
            using (var restoredConfig = new YokkoConfigManager(new NativeStorage(directory)))
            {
                restoredConfig.BindDisplaySettings(restoredSettings);
                Assert.That(
                    restoredSettings.FrameLimit.Value,
                    Is.EqualTo(YokkoFrameLimit.Limit8x));
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Test]
    public void PerformanceReadoutDefaultsOffAndPersists()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "performance-readout-config",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var firstSettings = new YokkoDisplaySettings();
            using (var firstConfig = new YokkoConfigManager(new NativeStorage(directory)))
            {
                firstConfig.BindDisplaySettings(firstSettings);
                Assert.That(firstSettings.ShowPerformanceReadout.Value, Is.False);

                firstSettings.ShowPerformanceReadout.Value = true;
                Assert.That(firstConfig.Save(), Is.True);
            }

            var restoredSettings = new YokkoDisplaySettings();
            using (var restoredConfig = new YokkoConfigManager(new NativeStorage(directory)))
            {
                restoredConfig.BindDisplaySettings(restoredSettings);
                Assert.That(
                    restoredSettings.ShowPerformanceReadout.Value,
                    Is.True);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Test]
    public void DifficultyRatingModeDefaultsToMsdAndPersists()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "difficulty-rating-mode-config",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var firstSettings = new YokkoDisplaySettings();
            using (var firstConfig =
                   new YokkoConfigManager(
                       new NativeStorage(directory)))
            {
                firstConfig.BindDisplaySettings(firstSettings);
                Assert.That(
                    firstSettings.DifficultyRatingMode.Value,
                    Is.EqualTo(
                        ManiaDifficultyRatingMode.EtternaMsd));

                firstSettings.DifficultyRatingMode.Value =
                    ManiaDifficultyRatingMode.RebirthStars;
                Assert.That(firstConfig.Save(), Is.True);
            }

            var restoredSettings = new YokkoDisplaySettings();
            using (var restoredConfig =
                   new YokkoConfigManager(
                       new NativeStorage(directory)))
            {
                restoredConfig.BindDisplaySettings(
                    restoredSettings);
                Assert.That(
                    restoredSettings.DifficultyRatingMode.Value,
                    Is.EqualTo(
                        ManiaDifficultyRatingMode.RebirthStars));
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
