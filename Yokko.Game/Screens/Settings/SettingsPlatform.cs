using System;

namespace Yokko.Game.Screens.Settings;

internal static class SettingsPlatform
{
    internal static bool SupportsDesktopSettings =>
        OperatingSystem.IsWindows()
        || OperatingSystem.IsMacOS()
        || OperatingSystem.IsLinux();
}
