using osuTK.Graphics;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Settings;

internal static class SettingsTheme
{
    private static YokkoSettingsColourTokens colours =>
        YokkoUiTheme.Default.Colours.Settings;

    public static Color4 MutedNavy => colours.MutedInk;
    public static Color4 Divider => colours.Divider;
    public static Color4 StatusCyan => colours.StatusCyan;
    public static Color4 PaleCyan => colours.PaleCyan;
    public static Color4 HoverNavy => colours.HoverInk;
    public static Color4 SoftShadow => colours.SoftShadow;
}
