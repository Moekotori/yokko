using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Configuration;
using osu.Framework.Platform;
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
                Is.EqualTo(927.5f));
            Assert.That(
                MainScreen.CalculateMusicPlayerY(
                    new osuTK.Vector2(1280, 720)),
                Is.EqualTo(636));
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
    public void ResultScreenUsesFullScaledViewport()
    {
        var stage = GameplayResultOverlay.CalculateResponsiveStageSize(
            new osuTK.Vector2(1777.7778f, 1000));
        var rightOffset =
            GameplayResultOverlay.CalculateRightStageOffset(stage);

        Assert.Multiple(() =>
        {
            Assert.That(
                stage,
                Is.EqualTo(new osuTK.Vector2(1777.7778f, 1000)));
            Assert.That(
                rightOffset.X,
                Is.EqualTo(497.7778f).Within(0.001f));
            Assert.That(rightOffset.Y, Is.EqualTo(140));
            Assert.That(
                GameplayResultOverlay.CalculateResponsiveStageSize(
                    new osuTK.Vector2(1024, 576)),
                Is.EqualTo(new osuTK.Vector2(1280, 720)));
        });
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
}
