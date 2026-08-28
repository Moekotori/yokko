using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Settings;

internal static class SettingsPlatform
{
    internal static bool SupportsDesktopSettings =>
        YokkoPlatformCapabilities.SupportsWindowManagement;

    internal static bool SupportsWindowManagement =>
        YokkoPlatformCapabilities.SupportsWindowManagement;
}
