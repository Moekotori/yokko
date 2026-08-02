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
    private readonly Box veil;

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
        veil.Alpha = 1;
    }

    internal void BeginReveal()
    {
        ClearTransforms(true);
        Alpha = 1;

        veil.Alpha = 1;
        veil.FadeOut(45, Easing.OutQuint);
        this.Delay(45).FadeOut();
    }

    internal void ResetInstant()
    {
        ClearTransforms(true);
        Alpha = 0;
    }
}
