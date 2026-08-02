using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

/// <summary>
/// Keeps the gameplay context covered while making the retry state explicit.
/// </summary>
internal partial class GameplayRetryTransitionOverlay : CompositeDrawable
{
    private const double coverDurationMilliseconds = 55;
    private const double revealDurationMilliseconds = 75;

    private readonly Box veil;
    private readonly Container status;
    private readonly SpriteIcon restartIcon;
    private readonly Box activityLine;

    internal bool CoverComplete =>
        Alpha == 1
        && veil.Alpha >= 0.999f;

    internal static double RevealDurationMilliseconds =>
        revealDurationMilliseconds;

    internal bool StatusVisible => status.Alpha > 0.9f;

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
            status = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(360, 92),
                Alpha = 0,
                Children =
                [
                    restartIcon = new SpriteIcon
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.Centre,
                        Position = new Vector2(0, 17),
                        Size = new Vector2(22),
                        Icon = FontAwesome.Solid.Undo,
                        Colour = YokkoPalette.Cyan,
                    },
                    new SpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Y = 39,
                        Text = YokkoStrings.Get(
                            "gameplay.retry.restarting"),
                        Font = YokkoUiTheme.Default.Typography
                                           .Interface(17, "Bold"),
                        Colour = YokkoPalette.Text,
                    },
                    new Box
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Size = new Vector2(164, 2),
                        Colour = new Color4(
                            YokkoPalette.Cyan.R,
                            YokkoPalette.Cyan.G,
                            YokkoPalette.Cyan.B,
                            0.16f),
                    },
                    activityLine = new Box
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Size = new Vector2(42, 2),
                        Colour = YokkoPalette.Cyan,
                    },
                ],
            },
        ];
    }

    internal void BeginCover()
    {
        ClearTransforms(true);
        Alpha = 1;
        veil.Alpha = 0;
        veil.FadeTo(1, coverDurationMilliseconds, Easing.OutQuint);

        status.ClearTransforms(true);
        status.Alpha = 0;
        status.Y = 7;
        status.FadeIn(65, Easing.OutQuint)
              .MoveToY(0, 90, Easing.OutQuint);

        restartIcon.ClearTransforms(true);
        restartIcon.RotateTo(0)
                   .RotateTo(-360, 760, Easing.InOutSine)
                   .Loop();

        activityLine.ClearTransforms(true);
        activityLine.ResizeWidthTo(42)
                    .ResizeWidthTo(154, 520, Easing.InOutSine)
                    .Then()
                    .ResizeWidthTo(42, 520, Easing.InOutSine)
                    .Loop();
    }

    internal void BeginReveal()
    {
        ClearTransforms(true);
        Alpha = 1;

        veil.Alpha = 1;
        veil.FadeOut(revealDurationMilliseconds, Easing.OutQuint);
        status.FadeOut(45, Easing.OutQuint);
        this.Delay(revealDurationMilliseconds).FadeOut();
    }

    internal void ResetInstant()
    {
        ClearTransforms(true);
        status.ClearTransforms(true);
        restartIcon.ClearTransforms(true);
        activityLine.ClearTransforms(true);
        Alpha = 0;
        status.Alpha = 0;
    }
}
