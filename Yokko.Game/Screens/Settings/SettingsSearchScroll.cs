using osu.Framework.Graphics.Containers;

namespace Yokko.Game.Screens.Settings;

/// <summary>
/// Scroll helpers for settings search. Scroll Y values are owned by
/// <see cref="SettingsSearchCatalog"/> so panels do not duplicate coordinates.
/// </summary>
internal static class SettingsSearchScroll
{
    internal static float? GetScrollY(SettingsPageKind page, string itemId)
    {
        foreach (SettingsSearchMatch match in SettingsSearchCatalog.All)
        {
            if (match.Page == page
                && string.Equals(
                    match.ItemId,
                    itemId,
                    System.StringComparison.Ordinal))
            {
                return match.ScrollY;
            }
        }

        return null;
    }

    internal static bool TryFocus(
        SettingsPageKind page,
        string itemId,
        SettingsContentScrollContainer scroll = null,
        Container contentRoot = null)
    {
        float? y = GetScrollY(page, itemId);
        if (!y.HasValue)
            return false;

        if (scroll != null)
        {
            scroll.ScrollTo(y.Value, true);
            contentRoot ??= scroll.Child as Container;
        }

        if (contentRoot != null)
            SettingsSearchHighlight.PulseRow(contentRoot, y.Value);

        return true;
    }
}
