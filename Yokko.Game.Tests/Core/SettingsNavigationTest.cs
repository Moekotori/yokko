using NUnit.Framework;
using Yokko.Core.Difficulty;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Settings;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class SettingsNavigationTest
{
    [Test]
    public void HiddenPagesAreNotVisibleInNavigation()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                SettingsNavigation.IsVisible(SettingsPageKind.Editor),
                Is.False);
            Assert.That(
                SettingsNavigation.IsVisible(SettingsPageKind.Accessibility),
                Is.False);
            Assert.That(
                SettingsNavigation.VisiblePages,
                Does.Not.Contain(SettingsPageKind.Editor));
            Assert.That(
                SettingsNavigation.VisiblePages,
                Does.Not.Contain(SettingsPageKind.Accessibility));
            Assert.That(
                SettingsNavigation.VisiblePages,
                Does.Contain(SettingsPageKind.General));
        });
    }

    [Test]
    public void DesktopPageVisibilityFollowsPlatformCapabilities()
    {
        bool supportsDesktop = SettingsPlatform.SupportsDesktopSettings;

        Assert.That(
            SettingsNavigation.IsVisible(SettingsPageKind.Desktop),
            Is.EqualTo(supportsDesktop));

        if (supportsDesktop)
        {
            Assert.That(
                SettingsNavigation.VisiblePages,
                Does.Contain(SettingsPageKind.Desktop));
        }
        else
        {
            Assert.That(
                SettingsNavigation.VisiblePages,
                Does.Not.Contain(SettingsPageKind.Desktop));
        }
    }

    [Test]
    public void WindowManagementMatchesDesktopSettingsGate()
    {
        Assert.That(
            SettingsPlatform.SupportsWindowManagement,
            Is.EqualTo(YokkoPlatformCapabilities.SupportsWindowManagement));
        Assert.That(
            SettingsPlatform.SupportsDesktopSettings,
            Is.EqualTo(SettingsPlatform.SupportsWindowManagement));
    }
}
