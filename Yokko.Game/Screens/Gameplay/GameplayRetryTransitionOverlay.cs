using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

/// <summary>
/// A deliberately quiet retry handoff using the same dark surface as gameplay.
/// It exists only to hide the drawable replacement without adding a separate
/// visual motif to the play experience.
/// </summary>
internal partial class GameplayRetryTransitionOverlay : CompositeDrawable
{
    private const double coverDurationMilliseconds = 55;
    private const double revealDurationMilliseconds = 75;

    private readonly Box veil;

    internal bool CoverComplete =>
        Alpha == 1
        && veil.Alpha >= 0.999f;

    internal static double RevealDurationMilliseconds =>
        revealDurationMilliseconds;

    internal GameplayRetryTransitionOverlay()
    {
        RelativeSizeAxes = Axes.Both;
        Depth = -1000;
        Alpha = 0;

        InternalChildren =
        [
            veil = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = YokkoPalette.Background,
            },
        ];
    }

    internal void BeginCover()
    {
        ClearTransforms(true);
        Alpha = 1;
        veil.Alpha = 0;
        veil.FadeTo(1, coverDurationMilliseconds, Easing.OutQuint);
    }

    internal void BeginReveal()
    {
        ClearTransforms(true);
        Alpha = 1;

        veil.Alpha = 1;
        veil.FadeOut(revealDurationMilliseconds, Easing.OutQuint);
        this.Delay(revealDurationMilliseconds).FadeOut();
    }

    internal void ResetInstant()
    {
        ClearTransforms(true);
        Alpha = 0;
    }
}
