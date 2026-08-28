using System;
using System.Collections.Generic;
using System.Linq;

namespace Yokko.Game.Screens.Settings;

internal static class SettingsNavigation
{
    internal static IReadOnlyList<SettingsPageKind> VisiblePages { get; } =
        Enum.GetValues<SettingsPageKind>()
            .Where(IsVisible)
            .ToArray();

    internal static bool IsVisible(SettingsPageKind kind) => kind switch
    {
        SettingsPageKind.Desktop => SettingsPlatform.SupportsDesktopSettings,
        _ => true,
    };
}
