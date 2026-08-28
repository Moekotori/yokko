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
        SettingsContentScrollContainer scroll)
    {
        if (scroll == null)
            return GetScrollY(page, itemId).HasValue;

        float? y = GetScrollY(page, itemId);
        if (!y.HasValue)
            return false;

        scroll.ScrollTo(y.Value, true);
        return true;
    }
}
