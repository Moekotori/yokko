using osu.Framework.Graphics;
using osuTK.Graphics;

namespace Yokko.Game.Skinning.OsuMania;

internal static class LegacyManiaColourCompatibility
{
    public static Color4 DisallowZeroAlpha(Color4 colour)
    {
        if (colour.A == 0)
            colour.A = 1;

        return colour;
    }

    public static T ApplyWithDoubledAlpha<T>(
        T drawable,
        Color4 colour)
        where T : Drawable
    {
        drawable.Alpha = colour.A;
        drawable.Colour = DisallowZeroAlpha(colour);
        return drawable;
    }
}
