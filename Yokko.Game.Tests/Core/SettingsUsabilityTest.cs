using NUnit.Framework;
using Yokko.Game.Screens.Settings;

namespace Yokko.Game.Tests.Core;

[TestFixture]
internal class SettingsUsabilityTest
{
    [TestCase("-200", -200)]
    [TestCase("+200", 200)]
    [TestCase("  -24  ", -24)]
    [TestCase("0", 0)]
    public void OffsetEntryAcceptsSignedMilliseconds(string text, double expected)
    {
        Assert.That(SettingsOffsetStepper.TryParseOffset(text, out double value), Is.True);
        Assert.That(value, Is.EqualTo(expected));
    }

    [TestCase("201")]
    [TestCase("-201")]
    [TestCase("NaN")]
    [TestCase("Infinity")]
    [TestCase("1e2")]
    [TestCase("-2.5")]
    [TestCase("")]
    [TestCase("--2")]
    public void OffsetEntryRejectsInvalidValuesInsteadOfClampingThem(string text) =>
        Assert.That(SettingsOffsetStepper.TryParseOffset(text, out _), Is.False);

    [TestCase("声音不同步", SettingsQuickAction.Calibration)]
    [TestCase("调延迟", SettingsQuickAction.Calibration)]
    [TestCase("Timing offset", SettingsQuickAction.Calibration)]
    [TestCase("配置预设", SettingsQuickAction.Presets)]
    [TestCase("音频设备", SettingsQuickAction.None)]
    [TestCase("", SettingsQuickAction.None)]
    public void QuickSearchOnlyInterceptsExplicitTaskAliases(string query, SettingsQuickAction expected) =>
        Assert.That(SettingsQuickActions.Find(query), Is.EqualTo(expected));
}
