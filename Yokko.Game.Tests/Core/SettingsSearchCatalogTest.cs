using NUnit.Framework;
using Yokko.Game.Screens.Settings;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class SettingsSearchCatalogTest
{
    [Test]
    public void CatalogScrollYMatchesPanelLayout()
    {
        Assert.Multiple(() =>
        {
            assertScrollY(SettingsPageKind.General, "privacy", 836);
            assertScrollY(SettingsPageKind.General, "config", 680);
            assertScrollY(SettingsPageKind.Safety, "diagnostics", 402);
            assertScrollY(SettingsPageKind.Accessibility, "contrast", 304);
            assertScrollY(SettingsPageKind.Editor, "grid", 248);
            assertScrollY(SettingsPageKind.Editor, "autosave", 384);
            assertScrollY(SettingsPageKind.About, "update", 278);
        });
    }

    [Test]
    public void EveryCatalogEntryHasScrollY()
    {
        Assert.That(
            SettingsSearchCatalog.All,
            Has.All.Matches<SettingsSearchMatch>(match => match.ScrollY >= 0));
    }

    private static void assertScrollY(
        SettingsPageKind page,
        string itemId,
        float expectedY) =>
        Assert.That(
            SettingsSearchScroll.GetScrollY(page, itemId),
            Is.EqualTo(expectedY));
}
