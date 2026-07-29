using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Platform;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Settings;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class DisplayFrameRateTest
{
    [Test]
    public void DefaultPolicyAdaptsToRefreshRateAt165Hz()
    {
        var settings = new YokkoDisplaySettings();

        Assert.That(
            settings.FrameLimit.Value,
            Is.EqualTo(YokkoFrameRateLimits.LowLatencyDefault));
        Assert.That(
            YokkoFrameRateLimits.LowLatencyDefault,
            Is.EqualTo(YokkoFrameLimit.Auto));

        YokkoFrameRates rates = YokkoFrameRateLimits.Calculate(
            YokkoFrameRateLimits.LowLatencyDefault,
            165);

        Assert.That(
            rates.MaximumDrawHz,
            Is.EqualTo(660));
        Assert.That(
            rates.MaximumUpdateHz,
            Is.EqualTo(YokkoFrameRateLimits.MaximumSaneRate));
        Assert.That(
            YokkoFrameRateLimits.ToFrameworkFrameSync(
                YokkoFrameRateLimits.LowLatencyDefault,
                165),
            Is.EqualTo(FrameSync.Limit4x));
        Assert.That(
            YokkoLatencyThreadPolicy.InputPriority,
            Is.EqualTo(ThreadPriority.AboveNormal));
        Assert.That(
            YokkoLatencyThreadPolicy.UpdatePriority,
            Is.EqualTo(ThreadPriority.AboveNormal));
        Assert.That(
            YokkoLatencyThreadPolicy.DrawPriority,
            Is.EqualTo(ThreadPriority.Normal));
    }

    [TestCase(YokkoFrameLimit.Auto, 60, "480 FPS")]
    [TestCase(YokkoFrameLimit.Auto, 165, "660 FPS")]
    [TestCase(YokkoFrameLimit.VSync, 144, "144 Hz")]
    [TestCase(YokkoFrameLimit.Limit2x, 144, "288 FPS")]
    [TestCase(YokkoFrameLimit.Limit4x, 60, "240 FPS")]
    [TestCase(YokkoFrameLimit.Limit8x, 60, "480 FPS")]
    [TestCase(YokkoFrameLimit.Limit8x, 165, "1000 FPS")]
    [TestCase(YokkoFrameLimit.Unlimited, 60, "1000 FPS")]
    public void FrameLimitUsesCurrentDisplayRefreshRate(
        YokkoFrameLimit limit,
        float refreshRate,
        string expected)
    {
        Assert.That(
            DisplaySettingsPanel.FormatFrameLimit(limit, refreshRate),
            Is.EqualTo(expected));
    }

    [TestCase(YokkoFrameLimit.VSync, 165, 165, 660)]
    [TestCase(YokkoFrameLimit.Auto, 60, 480, 960)]
    [TestCase(YokkoFrameLimit.Auto, 120, 960, 1000)]
    [TestCase(YokkoFrameLimit.Auto, 144, 576, 1000)]
    [TestCase(YokkoFrameLimit.Auto, 240, 960, 1000)]
    [TestCase(YokkoFrameLimit.Limit2x, 165, 330, 660)]
    [TestCase(YokkoFrameLimit.Limit4x, 165, 660, 1000)]
    [TestCase(YokkoFrameLimit.Limit8x, 165, 1000, 1000)]
    [TestCase(YokkoFrameLimit.Unlimited, 165, 1000, 1000)]
    [TestCase(YokkoFrameLimit.Limit8x, 60, 480, 960)]
    [TestCase(YokkoFrameLimit.Limit8x, 120, 960, 1000)]
    public void FrameLimitCalculatesRealHostRates(
        YokkoFrameLimit limit,
        float refreshRate,
        double expectedDrawRate,
        double expectedUpdateRate)
    {
        YokkoFrameRates rates = YokkoFrameRateLimits.Calculate(
            limit,
            refreshRate);

        Assert.That(rates.MaximumDrawHz, Is.EqualTo(expectedDrawRate));
        Assert.That(rates.MaximumUpdateHz, Is.EqualTo(expectedUpdateRate));
    }

    [TestCase(YokkoFrameLimit.VSync, FrameSync.VSync)]
    [TestCase(YokkoFrameLimit.Limit2x, FrameSync.Limit2x)]
    [TestCase(YokkoFrameLimit.Limit4x, FrameSync.Limit4x)]
    [TestCase(YokkoFrameLimit.Limit8x, FrameSync.Limit8x)]
    [TestCase(YokkoFrameLimit.Unlimited, FrameSync.Unlimited)]
    public void YokkoModeMapsToFrameworkSingleSourceOfTruth(
        YokkoFrameLimit limit,
        FrameSync expected)
    {
        Assert.That(
            YokkoFrameRateLimits.ToFrameworkFrameSync(limit),
            Is.EqualTo(expected));
        Assert.That(
            YokkoFrameRateLimits.FromFrameworkFrameSync(expected),
            Is.EqualTo(limit));
    }

    [TestCase(60, YokkoFrameLimit.Limit8x, FrameSync.Limit8x)]
    [TestCase(120, YokkoFrameLimit.Limit8x, FrameSync.Limit8x)]
    [TestCase(121, YokkoFrameLimit.Limit4x, FrameSync.Limit4x)]
    [TestCase(165, YokkoFrameLimit.Limit4x, FrameSync.Limit4x)]
    [TestCase(240, YokkoFrameLimit.Limit4x, FrameSync.Limit4x)]
    public void AutoResolvesWithoutForcingMaximumDrawRate(
        float refreshRate,
        YokkoFrameLimit expectedLimit,
        FrameSync expectedFrameSync)
    {
        Assert.That(
            YokkoFrameRateLimits.Resolve(
                YokkoFrameLimit.Auto,
                refreshRate),
            Is.EqualTo(expectedLimit));
        Assert.That(
            YokkoFrameRateLimits.ToFrameworkFrameSync(
                YokkoFrameLimit.Auto,
                refreshRate),
            Is.EqualTo(expectedFrameSync));
    }

    [Test]
    public void ControllerKeepsAutoSelectedAcrossDisplayChanges()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "adaptive-frame-config",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            using var config =
                new FrameworkConfigManager(new NativeStorage(directory));
            var frameLimit =
                new Bindable<YokkoFrameLimit>(YokkoFrameLimit.Auto);
            var displayMode =
                new Bindable<DisplayMode>(createDisplayMode(60));
            var adaptation = new YokkoFrameRateAdaptation();
            using var controller = new YokkoFrameRateController(
                config,
                frameLimit,
                displayMode,
                adaptation);

            Assert.Multiple(() =>
            {
                Assert.That(frameLimit.Value, Is.EqualTo(YokkoFrameLimit.Auto));
                Assert.That(
                    config.Get<FrameSync>(FrameworkSetting.FrameSync),
                    Is.EqualTo(FrameSync.Limit8x));
            });

            adaptation.BeginSession();
            for (int index = 0;
                 index
                 < YokkoFrameRateAdaptation.RequiredCriticalObservations;
                 index++)
            {
                adaptation.Observe(FramePacingHealth.Critical);
            }

            Assert.Multiple(() =>
            {
                Assert.That(
                    adaptation.PendingLimit,
                    Is.EqualTo(YokkoFrameLimit.Limit4x));
                Assert.That(
                    config.Get<FrameSync>(FrameworkSetting.FrameSync),
                    Is.EqualTo(FrameSync.Limit8x));
                Assert.That(frameLimit.Value, Is.EqualTo(YokkoFrameLimit.Auto));
            });

            adaptation.EndSession();
            Assert.Multiple(() =>
            {
                Assert.That(
                    adaptation.EffectiveLimit,
                    Is.EqualTo(YokkoFrameLimit.Limit4x));
                Assert.That(
                    config.Get<FrameSync>(FrameworkSetting.FrameSync),
                    Is.EqualTo(FrameSync.Limit4x));
                Assert.That(frameLimit.Value, Is.EqualTo(YokkoFrameLimit.Auto));
            });

            displayMode.Value = createDisplayMode(165);

            Assert.Multiple(() =>
            {
                Assert.That(frameLimit.Value, Is.EqualTo(YokkoFrameLimit.Auto));
                Assert.That(
                    config.Get<FrameSync>(FrameworkSetting.FrameSync),
                    Is.EqualTo(FrameSync.Limit4x));
            });

            config.SetValue(
                FrameworkSetting.FrameSync,
                FrameSync.Unlimited);
            Assert.Multiple(() =>
            {
                Assert.That(
                    frameLimit.Value,
                    Is.EqualTo(YokkoFrameLimit.Unlimited));
                Assert.That(adaptation.IsEnabled, Is.False);
                Assert.That(
                    config.Get<FrameSync>(
                        FrameworkSetting.FrameSync),
                    Is.EqualTo(FrameSync.Unlimited));
            });
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Test]
    public void AutoRequiresConsecutiveCriticalObservations()
    {
        var adaptation = new YokkoFrameRateAdaptation();
        adaptation.Enable(YokkoFrameLimit.Limit8x, reset: true);

        for (int index = 0;
             index
             < YokkoFrameRateAdaptation.RequiredCriticalObservations - 1;
             index++)
        {
            adaptation.Observe(FramePacingHealth.Critical);
        }
        adaptation.Observe(FramePacingHealth.Stable);
        adaptation.Observe(FramePacingHealth.Critical);

        Assert.Multiple(() =>
        {
            Assert.That(
                adaptation.EffectiveLimit,
                Is.EqualTo(YokkoFrameLimit.Limit8x));
            Assert.That(adaptation.PendingLimit, Is.Null);
        });
    }

    [Test]
    public void AutoOnlyDowngradesOnceUntilReset()
    {
        var adaptation = new YokkoFrameRateAdaptation();
        adaptation.Enable(YokkoFrameLimit.Limit4x, reset: true);

        observeCriticalWindow(adaptation);
        observeCriticalWindow(adaptation);

        Assert.That(
            adaptation.EffectiveLimit,
            Is.EqualTo(YokkoFrameLimit.Limit2x));

        adaptation.Enable(YokkoFrameLimit.Limit4x, reset: true);

        Assert.That(
            adaptation.EffectiveLimit,
            Is.EqualTo(YokkoFrameLimit.Limit4x));
    }

    [Test]
    public void DisplayResetWaitsUntilGameplaySessionEnds()
    {
        var adaptation = new YokkoFrameRateAdaptation();
        adaptation.Enable(YokkoFrameLimit.Limit8x, reset: true);
        adaptation.BeginSession();

        adaptation.Enable(
            YokkoFrameLimit.Limit4x,
            reset: true,
            deferResetWhileSession: true);

        Assert.That(
            adaptation.EffectiveLimit,
            Is.EqualTo(YokkoFrameLimit.Limit8x));

        adaptation.EndSession();

        Assert.Multiple(() =>
        {
            Assert.That(
                adaptation.BaseLimit,
                Is.EqualTo(YokkoFrameLimit.Limit4x));
            Assert.That(
                adaptation.EffectiveLimit,
                Is.EqualTo(YokkoFrameLimit.Limit4x));
        });
    }

    [Test]
    public void MaximumStaysAtFullCeilingWithoutAdaptation()
    {
        YokkoFrameRates rates = YokkoFrameRateLimits.Calculate(
            YokkoFrameLimit.Unlimited,
            60);
        var adaptation = new YokkoFrameRateAdaptation();

        adaptation.Enable(YokkoFrameLimit.Limit8x, reset: true);
        adaptation.Disable();
        observeCriticalWindow(adaptation);

        Assert.Multiple(() =>
        {
            Assert.That(
                YokkoFrameRateLimits.ToFrameworkFrameSync(
                    YokkoFrameLimit.Unlimited),
                Is.EqualTo(FrameSync.Unlimited));
            Assert.That(
                rates,
                Is.EqualTo(new YokkoFrameRates(1000, 1000)));
            Assert.That(adaptation.IsEnabled, Is.False);
            Assert.That(adaptation.HasAdapted, Is.False);
            Assert.That(adaptation.PendingLimit, Is.Null);
        });
    }

    [Test]
    public void LegacyRefreshRateNameMigratesToVSync()
    {
        Assert.That(
            Enum.TryParse(
                "RefreshRate",
                out YokkoFrameLimit parsed),
            Is.True);
        Assert.That(parsed, Is.EqualTo(YokkoFrameLimit.VSync));
    }

    [TestCase(YokkoFrameLimit.VSync, "V-SYNC")]
    [TestCase(YokkoFrameLimit.Auto, "AUTO")]
    [TestCase(YokkoFrameLimit.Limit2x, "2×")]
    [TestCase(YokkoFrameLimit.Limit4x, "4×")]
    [TestCase(YokkoFrameLimit.Limit8x, "8×")]
    [TestCase(YokkoFrameLimit.Unlimited, "MAX")]
    public void FrameModesHaveStableCompactLabels(
        YokkoFrameLimit limit,
        string expected)
    {
        Assert.That(
            DisplaySettingsPanel.FormatFrameLimitMode(limit),
            Is.EqualTo(expected));
    }

    [TestCase(59.94f, "60")]
    [TestCase(120, "120")]
    [TestCase(144, "144")]
    [TestCase(165, "165")]
    [TestCase(240, "240")]
    public void RefreshRateFormattingSupportsCommonDisplays(
        float refreshRate,
        string expected)
    {
        Assert.That(
            DisplaySettingsPanel.FormatRefreshRate(refreshRate),
            Is.EqualTo(expected));
    }

    private static DisplayMode createDisplayMode(float refreshRate) =>
        new(
            null,
            new System.Drawing.Size(1920, 1080),
            0,
            refreshRate,
            0);

    private static void observeCriticalWindow(
        YokkoFrameRateAdaptation adaptation)
    {
        for (int index = 0;
             index
             < YokkoFrameRateAdaptation.RequiredCriticalObservations;
             index++)
        {
            adaptation.Observe(FramePacingHealth.Critical);
        }
    }
}
