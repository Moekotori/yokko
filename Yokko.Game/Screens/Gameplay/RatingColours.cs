using osu.Framework.Graphics.Colour;
using osuTK.Graphics;
using Yokko.Core.Scoring;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

public static class RatingColours
{
    private static readonly Color4 stableRainbowLeft =
        new(0.20f, 0.84f, 1f, 1f);
    private static readonly Color4 stableRainbowRight =
        new(1f, 0.32f, 0.72f, 1f);

    public static Color4 For(JudgementRating rating) => rating switch
    {
        JudgementRating.Perfect => YokkoPalette.Cyan,
        JudgementRating.Great => YokkoPalette.Lime,
        JudgementRating.Good => new Color4(0.95f, 0.82f, 0.34f, 1f),
        JudgementRating.Ok => new Color4(0.35f, 0.72f, 0.95f, 1f),
        JudgementRating.Meh => YokkoPalette.Rose,
        JudgementRating.Miss => new Color4(0.7f, 0.72f, 0.78f, 1f),
        JudgementRating.ComboBreak => YokkoPalette.Rose,
        _ => YokkoPalette.Text,
    };

    public static ColourInfo ForDisplay(
        JudgementRating rating,
        JudgementConfiguration configuration)
    {
        if (configuration.Mode != JudgementMode.OsuStable)
            return For(rating);

        return rating == JudgementRating.Perfect
            ? ColourInfo.GradientHorizontal(
                stableRainbowLeft,
                stableRainbowRight)
            : StableSolid(rating);
    }

    public static Color4 StableSolid(JudgementRating rating) => rating switch
    {
        JudgementRating.Perfect => stableRainbowRight,
        JudgementRating.Great => new Color4(1f, 0.82f, 0.16f, 1f),
        JudgementRating.Good => new Color4(0.30f, 0.82f, 0.34f, 1f),
        JudgementRating.Ok => new Color4(0.20f, 0.56f, 1f, 1f),
        JudgementRating.Meh => new Color4(0.68f, 0.38f, 0.92f, 1f),
        JudgementRating.Miss => new Color4(1f, 0.28f, 0.38f, 1f),
        _ => YokkoPalette.Text,
    };
}
