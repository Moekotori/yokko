using osu.Framework.Configuration;
using osu.Framework.Platform;

namespace Yokko.Game.Resources;

public interface IDesktopDisplayModeController
{
    bool IsAvailable { get; }

    void EnsureWindowFrameVisible(IWindow window);

    bool TryApply(
        IWindow window,
        FrameworkConfigManager frameworkConfig,
        DisplayMode mode);
}

internal sealed class UnavailableDesktopDisplayModeController
    : IDesktopDisplayModeController
{
    public bool IsAvailable => false;

    public void EnsureWindowFrameVisible(IWindow window)
    {
    }

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
