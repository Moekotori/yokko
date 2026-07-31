using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;

namespace Yokko.Game.Screens.SongSelect;

/// <summary>
/// Shared code-drawn surfaces for Song Select.
/// Decorative artwork may sit on top of these surfaces, but never defines
/// their bounds, alignment, or interaction area.
/// </summary>
internal static class SongSelectSurface
{
    public const float CardRadius = 10;

    public static Drawable CreateShadow(
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
                SongSelectTheme.DeepNavy.R,
                SongSelectTheme.DeepNavy.G,
                SongSelectTheme.DeepNavy.B,
                opacity),
        },
    };

    public static Container CreateCard(
        out Box fill,
        Color4 background,
        Color4 border,
        float cornerRadius = CardRadius,
        float borderThickness = 1.25f)
    {
        fill = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = background,
        };

        return new Container
        {
            RelativeSizeAxes = Axes.Both,
            Masking = true,
            CornerRadius = cornerRadius,
            BorderThickness = borderThickness,
            BorderColour = border,
            Children =
            [
                fill,
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 1,
                    Colour = new Color4(1f, 1f, 1f, 0.82f),
                },
            ],
        };
    }

    public static Color4 Ivory(float alpha = 0.97f) => new(
        1f,
        0.995f,
        0.972f,
        alpha);

    public static Color4 Border(float alpha = 0.18f) => new(
        SongSelectTheme.Navy.R,
        SongSelectTheme.Navy.G,
        SongSelectTheme.Navy.B,
        alpha);
}
