namespace Yokko.Game.Diagnostics;

/// <summary>
/// Platform-owned debug console surface. Desktop implementations may present
/// diagnostics outside the game window while other platforms keep the in-game
/// overlay.
/// </summary>
public interface IDebugConsoleWindow
{
    void SetVisible(bool visible);
}
