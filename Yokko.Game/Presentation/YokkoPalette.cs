using osuTK.Graphics;

namespace Yokko.Game.Presentation;

public static class YokkoPalette
{
    private static YokkoDarkColourTokens colours =>
        YokkoUiTheme.Default.Colours.Dark;

    public static Color4 Background => colours.Background;
    public static Color4 Surface => colours.Surface;
    public static Color4 SurfaceElevated => colours.SurfaceElevated;
    public static Color4 SurfaceHover => colours.SurfaceHover;
    public static Color4 Panel => colours.Panel;
    public static Color4 PanelAlt => colours.PanelAlt;
    public static Color4 Chip => colours.Chip;
    public static Color4 Border => colours.Border;
    public static Color4 Text => colours.Text;
    public static Color4 TextMuted => colours.TextMuted;
    public static Color4 TextDim => colours.TextDim;
    public static Color4 Cyan => colours.Cyan;
    public static Color4 Rose => colours.Rose;
    public static Color4 Lime => colours.Lime;
    public static Color4 Violet => colours.Violet;
}
