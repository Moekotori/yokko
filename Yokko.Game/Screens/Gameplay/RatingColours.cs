using osuTK.Graphics;
using Yokko.Core.Scoring;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

public static class RatingColours
{
    public static Color4 For(JudgementRating rating) => rating switch
    {
        JudgementRating.Perfect => YokkoPalette.Cyan,
        JudgementRating.Great => YokkoPalette.Lime,
        JudgementRating.Good => new Color4(0.95f, 0.82f, 0.34f, 1f),
        JudgementRating.Bad => YokkoPalette.Rose,
        JudgementRating.Miss => new Color4(0.7f, 0.72f, 0.78f, 1f),
        _ => YokkoPalette.Text,
    };
}
