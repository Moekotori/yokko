using System;

namespace Yokko.Game.Presentation;

internal static class YokkoPlatformCapabilities
{
    internal static bool SupportsWindowManagement =>
        !OperatingSystem.IsIOS()
        && !OperatingSystem.IsAndroid()
        && (OperatingSystem.IsWindows()
            || OperatingSystem.IsMacOS()
            || OperatingSystem.IsLinux());
}
