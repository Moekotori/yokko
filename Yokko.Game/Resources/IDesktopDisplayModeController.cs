using osu.Framework.Platform;

namespace Yokko.Game.Resources;

public interface IDesktopDisplayModeController
{
    bool IsAvailable { get; }

    void EnsureWindowFrameVisible(IWindow window);
}

internal sealed class UnavailableDesktopDisplayModeController
    : IDesktopDisplayModeController
{
    public bool IsAvailable => false;

    public void EnsureWindowFrameVisible(IWindow window)
    {
    }
}
