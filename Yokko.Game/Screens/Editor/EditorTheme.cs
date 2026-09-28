using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Editor;

/// <summary>
/// Editor colours forward the shared Song Select and brand tokens so the
/// editor stays on the same lazer-inspired navy/ivory language as Song Select.
/// </summary>
internal static class EditorTheme
{
    private static YokkoSongSelectColourTokens songSelect =>
        YokkoUiTheme.Default.Colours.SongSelect;

    private static YokkoBrandColourTokens brand =>
        YokkoUiTheme.Default.Colours.Brand;

    public static Color4 Ivory => songSelect.Ivory;
    public static Color4 Navy => songSelect.Navy;
    public static Color4 DeepNavy => songSelect.DeepNavy;
    public static Color4 Surface => songSelect.Surface;
    public static Color4 SurfaceRaised => songSelect.SurfaceRaised;
    public static Color4 Cyan => songSelect.Cyan;
    public static Color4 PaleCyan => songSelect.PaleCyan;
    public static Color4 Yellow => songSelect.Yellow;
    public static Color4 Pink => songSelect.Pink;
    public static Color4 Muted => songSelect.Muted;
    public static Color4 Ink => brand.Ink;

    public const float CardRadius = 10;

    public static Color4 NavyText(float alpha = 1) => new(
        Navy.R,
        Navy.G,
        Navy.B,
        alpha);

    public static Color4 Border(float alpha = 0.18f) => NavyText(alpha);

    /// <summary>
    /// The ivory card surface used by the toolbar, transport, and inspector.
    /// Mirrors <c>SongSelectSurface.CreateCard</c> (navy border, white top
    /// hairline) without coupling the editor to Song Select internals.
    /// </summary>
    public static Container CreateIvoryCard(
        float cornerRadius = CardRadius,
        float borderThickness = 1.25f) => new()
    {
        RelativeSizeAxes = Axes.Both,
        Masking = true,
        CornerRadius = cornerRadius,
        BorderThickness = borderThickness,
        BorderColour = Border(),
        Children =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Ivory,
            },
            new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Colour = new Color4(1f, 1f, 1f, 0.82f),
            },
        ],
    };

    /// <summary>
    /// The soft drop shadow placed beneath ivory cards.
    /// </summary>
    public static Drawable CreateCardShadow(
        float cornerRadius = CardRadius,
        float opacity = 0.18f,
        float yOffset = 3) => new Container
    {
        RelativeSizeAxes = Axes.Both,
        Position = new Vector2(0, yOffset),
        Masking = true,
        CornerRadius = cornerRadius,
        Child = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = new Color4(
                DeepNavy.R,
                DeepNavy.G,
                DeepNavy.B,
                opacity),
        },
    };
}
