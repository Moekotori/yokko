using NUnit.Framework;
using osu.Framework.Configuration;
using Yokko.Game.Screens.Settings;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class DisplayFrameRateTest
{
    [TestCase(FrameSync.VSync, 144, "144 FPS")]
    [TestCase(FrameSync.Limit2x, 144, "288 FPS")]
    [TestCase(FrameSync.Limit4x, 60, "240 FPS")]
    [TestCase(FrameSync.Limit8x, 60, "480 FPS")]
    [TestCase(FrameSync.Unlimited, 60, "∞")]
    public void FrameLimitUsesCurrentDisplayRefreshRate(
        FrameSync mode,
        float refreshRate,
        string expected)
    {
        Assert.That(
            DisplaySettingsPanel.FormatFrameLimit(mode, refreshRate),
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
