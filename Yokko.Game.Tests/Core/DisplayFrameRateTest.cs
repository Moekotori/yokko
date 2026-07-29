using System;
using NUnit.Framework;
using osu.Framework.Configuration;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Settings;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class DisplayFrameRateTest
{
    [Test]
    public void DefaultPolicyReachesLowLatencyCeilingAt165Hz()
    {
        var settings = new YokkoDisplaySettings();

        Assert.That(
            settings.FrameLimit.Value,
            Is.EqualTo(YokkoFrameRateLimits.LowLatencyDefault));
        Assert.That(
            YokkoFrameRateLimits.LowLatencyDefault,
            Is.EqualTo(YokkoFrameLimit.Limit4x));

        YokkoFrameRates rates = YokkoFrameRateLimits.Calculate(
            YokkoFrameRateLimits.LowLatencyDefault,
            165);

        Assert.That(rates.MaximumDrawHz, Is.EqualTo(660));
        Assert.That(
            rates.MaximumUpdateHz,
            Is.EqualTo(YokkoFrameRateLimits.MaximumSaneRate));
        Assert.That(
            YokkoFrameRateLimits.ToFrameworkFrameSync(
                YokkoFrameRateLimits.LowLatencyDefault),
            Is.EqualTo(FrameSync.Limit4x));
    }

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
}
