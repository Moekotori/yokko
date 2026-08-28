using Yokko.Audio;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Settings;

internal static class SettingsPlatform
{
    internal static bool SupportsDesktopSettings =>
        YokkoPlatformCapabilities.SupportsWindowManagement;

    internal static bool SupportsWindowManagement =>
        YokkoPlatformCapabilities.SupportsWindowManagement;

    internal static bool SupportsNativeAudioConfiguration =>
        YokkoPlatformCapabilities.SupportsNativeAudioConfiguration
        && NativeAudioEngine.IsAvailable;
}
