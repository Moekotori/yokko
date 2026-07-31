using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Configuration;
using osu.Framework.Platform;
using Yokko.Core.Difficulty;
using Yokko.Game.Configuration;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.Main;
using Yokko.Game.Screens.Settings;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class DisplaySettingsTest
{
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
            Assert.That(large.X / large.Y, Is.EqualTo(16f / 9f).Within(0.001f));
            Assert.That(comfortable.X / comfortable.Y, Is.EqualTo(16f / 9f).Within(0.001f));
            Assert.That(compact.X / compact.Y, Is.EqualTo(16f / 9f).Within(0.001f));
            Assert.That(large.X, Is.LessThan(comfortable.X));
            Assert.That(comfortable.X, Is.LessThan(compact.X));
            Assert.That(YokkoDisplaySettings.GetScalePercentage(YokkoUiScale.Large), Is.EqualTo(100));
            Assert.That(YokkoDisplaySettings.GetScalePercentage(YokkoUiScale.Comfortable), Is.EqualTo(90));
            Assert.That(YokkoDisplaySettings.GetScalePercentage(YokkoUiScale.Compact), Is.EqualTo(80));
        });
    }

    [TestCase(YokkoUiScale.Large, 1f)]
    [TestCase(YokkoUiScale.Comfortable, 0.9f)]
    [TestCase(YokkoUiScale.Compact, 0.8f)]
    public void InterfaceScaleChangesAtAuthoredResolution(
        YokkoUiScale scale,
        float expected)
    {
        Assert.That(
            YokkoDisplaySettings.CalculateContentScale(
                new osuTK.Vector2(1600, 900),
                scale),
            Is.EqualTo(expected).Within(0.001f));
    }

    [TestCase(YokkoUiScale.Large, 2f)]
    [TestCase(YokkoUiScale.Comfortable, 1.8f)]
    [TestCase(YokkoUiScale.Compact, 1.6f)]
    public void InterfaceScaleFollowsDesktopResolution(
        YokkoUiScale scale,
        float expected)
    {
        Assert.That(
            YokkoDisplaySettings.CalculateContentScale(
                new osuTK.Vector2(3200, 1955),
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
            Is.EqualTo(2.4f).Within(0.001f));
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
                Is.EqualTo(new osuTK.Vector2(1280, 720)));
            Assert.That(
                SettingsScreen.CalculateResponsiveStageSize(
                    YokkoDisplaySettings.GetTargetDrawSize(
                        YokkoUiScale.Comfortable)),
                Is.EqualTo(new osuTK.Vector2(
                    1280f / 0.9f,
                    800)));
            Assert.That(
                SettingsScreen.CalculateResponsiveStageSize(
                    YokkoDisplaySettings.GetTargetDrawSize(
                        YokkoUiScale.Compact)),
                Is.EqualTo(new osuTK.Vector2(1600, 900)));
        });
    }

    [Test]
    public void ResultScreenScalesReferenceCanvasProportionally()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                GameplayResultOverlay.CalculateResponsiveStageScale(
                    new osuTK.Vector2(1777.7778f, 1000)),
                Is.EqualTo(1.1111111f).Within(0.0001f));
            Assert.That(
                GameplayResultOverlay.CalculateResponsiveStageScale(
                    new osuTK.Vector2(1024, 576)),
                Is.EqualTo(0.64f).Within(0.0001f));
            Assert.That(
                GameplayResultOverlay.CalculateResponsiveStageScale(
                    new osuTK.Vector2(1600, 1000)),
                Is.EqualTo(1f).Within(0.0001f));
        });
    }

    [Test]
    public void SettingsStageShrinksBelowReferenceInsteadOfClipping()
    {
        Assert.Multiple(() =>
        {
            // 参考布局（1600x900）下保持 1.25 倍放大。
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
