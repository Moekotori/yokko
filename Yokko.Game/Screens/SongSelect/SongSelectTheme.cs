using osuTK.Graphics;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.SongSelect;

internal static class SongSelectTheme
{
    private static YokkoSongSelectColourTokens colours =>
        YokkoUiTheme.Default.Colours.SongSelect;

    public static Color4 Ivory => colours.Ivory;
    public static Color4 Navy => colours.Navy;
    public static Color4 DeepNavy => colours.DeepNavy;
    public static Color4 Surface => colours.Surface;
    public static Color4 SurfaceRaised => colours.SurfaceRaised;
    public static Color4 Cyan => colours.Cyan;
    public static Color4 PaleCyan => colours.PaleCyan;
    public static Color4 Yellow => colours.Yellow;
    public static Color4 Pink => colours.Pink;
    public static Color4 Muted => colours.Muted;
}
