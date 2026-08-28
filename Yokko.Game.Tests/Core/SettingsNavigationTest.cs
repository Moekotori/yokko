using NUnit.Framework;
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
}
