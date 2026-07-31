using osu.Framework.Configuration;
using osu.Framework.Platform;

namespace Yokko.Game.Resources;

public interface IDesktopDisplayModeController
{
    bool IsAvailable { get; }

    bool TryApply(
        IWindow window,
        FrameworkConfigManager frameworkConfig,
        DisplayMode mode);
}

internal sealed class UnavailableDesktopDisplayModeController
    : IDesktopDisplayModeController
{
    public bool IsAvailable => false;

    public bool TryApply(
        IWindow window,
        FrameworkConfigManager frameworkConfig,
        DisplayMode mode)
    {
        frameworkConfig.SetValue(
            FrameworkSetting.SizeFullscreen,
            mode.Size);
        return false;
    }
}
