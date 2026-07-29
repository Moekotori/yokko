using NUnit.Framework;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Settings;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class DisplayFrameRateTest
{
    [TestCase(YokkoFrameLimit.RefreshRate, 144, "144 FPS")]
    [TestCase(YokkoFrameLimit.Limit2x, 144, "288 FPS")]
    [TestCase(YokkoFrameLimit.Limit4x, 60, "240 FPS")]
    [TestCase(YokkoFrameLimit.Limit8x, 60, "480 FPS")]
    [TestCase(YokkoFrameLimit.Limit8x, 165, "1000 FPS")]
    [TestCase(YokkoFrameLimit.Unlimited, 60, "∞")]
    public void FrameLimitUsesCurrentDisplayRefreshRate(
        YokkoFrameLimit limit,
        float refreshRate,
        string expected)
    {
        Assert.That(
            DisplaySettingsPanel.FormatFrameLimit(limit, refreshRate),
            Is.EqualTo(expected));
    }

    [TestCase(YokkoFrameLimit.RefreshRate, 165, 165, 330)]
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
