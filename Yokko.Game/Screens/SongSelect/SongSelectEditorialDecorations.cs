using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

/// <summary>
/// A quiet editorial anchor for the open portion of Song Select. It borrows
/// the poster hierarchy from the home screen without competing with the
/// interactive chart cards.
/// </summary>
internal partial class SongSelectPosterBlock : CompositeDrawable
{
    internal Vector2 RestingPosition { get; }

    internal SongSelectPosterBlock(Vector2 position)
    {
        RestingPosition = position;
        Position = position;
        Size = new Vector2(600, 220);
        Alpha = 0;

        InternalChildren =
        [
            new Container
            {
                Position = new Vector2(7, 7),
                Size = new Vector2(560, 190),
                Rotation = -0.8f,
                Children =
                [
                    SongSelectSurface.CreateShadow(7, 0.07f, 3),
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 7,
                        BorderThickness = 1,
                        BorderColour = SongSelectSurface.Border(0.16f),
                        Children =
                        [
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = SongSelectSurface.Ivory(0.66f),
                            },
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = ColourInfo.GradientHorizontal(
                                    new Color4(
                                        SongSelectTheme.Cyan.R,
                                        SongSelectTheme.Cyan.G,
                                        SongSelectTheme.Cyan.B,
                                        0.065f),
                                    new Color4(
                                        SongSelectTheme.Pink.R,
                                        SongSelectTheme.Pink.G,
                                        SongSelectTheme.Pink.B,
                                        0.025f)),
                            },
                            new Box
                            {
                                Position = new Vector2(0, 0),
                                Size = new Vector2(96, 3),
                                Colour = SongSelectTheme.Cyan,
                            },
                            new Box
                            {
                                Position = new Vector2(96, 0),
                                Size = new Vector2(34, 3),
                                Colour = SongSelectTheme.Pink,
                            },
                            new SpriteText
                            {
                                Position = new Vector2(24, 18),
                                Text = "YOKKO RHYTHM INDEX // VOL.07",
                                Font = HomeTypography.Display(10),
                                Spacing = new Vector2(0.8f, 0),
                                Colour = new Color4(
                                    SongSelectTheme.Cyan.R,
                                    SongSelectTheme.Cyan.G,
                                    SongSelectTheme.Cyan.B,
                                    0.88f),
                            },
                            new Box
                            {
                                Position = new Vector2(24, 43),
                                Size = new Vector2(244, 1),
                                Colour = new Color4(
                                    SongSelectTheme.Cyan.R,
                                    SongSelectTheme.Cyan.G,
                                    SongSelectTheme.Cyan.B,
                                    0.58f),
                            },
                            new Circle
                            {
                                Position = new Vector2(270, 40),
                                Size = new Vector2(7),
                                Colour = SongSelectTheme.Pink,
                            },
                            new SpriteText
                            {
                                Position = new Vector2(24, 54),
                                Text = "FIND YOUR",
                                Font = HomeTypography.Display(36),
                                Colour = new Color4(
                                    SongSelectTheme.Navy.R,
                                    SongSelectTheme.Navy.G,
                                    SongSelectTheme.Navy.B,
                                    0.82f),
                            },
                            new Box
                            {
                                Position = new Vector2(18, 111),
                                Size = new Vector2(255, 30),
                                Rotation = -1.2f,
                                Colour = new Color4(
                                    SongSelectTheme.Yellow.R,
                                    SongSelectTheme.Yellow.G,
                                    SongSelectTheme.Yellow.B,
                                    0.68f),
                            },
                            new SpriteText
                            {
                                Position = new Vector2(24, 94),
                                Text = "NEXT BEAT",
                                Font = HomeTypography.Display(44),
                                Colour = SongSelectTheme.Navy,
                            },
                            new SpriteText
                            {
                                Position = new Vector2(25, 158),
                                Text = "CHART LAB / FEEL THE BEAT / LIBRARY READY",
                                Font = HomeTypography.Display(9),
                                Spacing = new Vector2(1.1f, 0),
                                Colour = new Color4(
                                    SongSelectTheme.Navy.R,
                                    SongSelectTheme.Navy.G,
                                    SongSelectTheme.Navy.B,
                                    0.50f),
                            },
                            new HomeSignalWave(new Color4(
                                SongSelectTheme.Cyan.R,
                                SongSelectTheme.Cyan.G,
                                SongSelectTheme.Cyan.B,
                                0.64f))
                            {
                                Position = new Vector2(405, 86),
                                Scale = new Vector2(0.78f),
                            },
                            new SpriteText
                            {
                                Position = new Vector2(405, 121),
                                Text = "SELECT SIGNAL // 04",
                                Font = HomeTypography.Display(8),
                                Spacing = new Vector2(0.8f, 0),
                                Colour = new Color4(
                                    SongSelectTheme.Navy.R,
                                    SongSelectTheme.Navy.G,
                                    SongSelectTheme.Navy.B,
                                    0.52f),
                            },
                            new HomeBeatPips(
                                new Color4(
                                    SongSelectTheme.Cyan.R,
                                    SongSelectTheme.Cyan.G,
                                    SongSelectTheme.Cyan.B,
                                    0.50f),
                                SongSelectTheme.Pink)
                            {
                                Position = new Vector2(405, 153),
                                Scale = new Vector2(0.82f),
                            },
                        ],
                    },
                ],
            },
        ];
    }

    internal void Play(double delay)
    {
        ClearTransforms();
        Position = RestingPosition + new Vector2(-14, 7);
        Scale = new Vector2(0.985f);
        Alpha = 0;
        this.Delay(delay)
            .FadeIn(320, Easing.OutQuint);
        this.Delay(delay)
            .MoveTo(RestingPosition, 460, Easing.OutQuint);
        this.Delay(delay)
            .ScaleTo(1, 500, Easing.OutBack);
    }
}

/// <summary>
/// A compact live-signal strip that closes the empty space between chart
/// identity and its facts without inventing another control.
/// </summary>
internal partial class SongSelectPreviewSignalStrip : CompositeDrawable
{
    private readonly Circle statusPulse;

    internal SongSelectPreviewSignalStrip()
    {
        Size = new Vector2(522, 40);
        Masking = true;
        CornerRadius = 9;
        BorderThickness = 1;
        BorderColour = new Color4(
            SongSelectTheme.Cyan.R,
            SongSelectTheme.Cyan.G,
            SongSelectTheme.Cyan.B,
            0.24f);

        InternalChildren =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(
                    SongSelectTheme.PaleCyan.R,
                    SongSelectTheme.PaleCyan.G,
                    SongSelectTheme.PaleCyan.B,
                    0.22f),
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 4,
                Colour = SongSelectTheme.Pink,
            },
            new Container
            {
                Position = new Vector2(12, 7),
                Size = new Vector2(26),
                Masking = true,
                CornerRadius = 13,
                BorderThickness = 1,
                BorderColour = new Color4(
                    SongSelectTheme.Cyan.R,
                    SongSelectTheme.Cyan.G,
                    SongSelectTheme.Cyan.B,
                    0.40f),
                Children =
                [
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = SongSelectSurface.Ivory(0.86f),
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(12),
                        Icon = FontAwesome.Solid.Headphones,
                        Colour = SongSelectTheme.Cyan,
                    },
                ],
            },
            new SpriteText
            {
                Position = new Vector2(48, 5),
                Text = "PREVIEW SIGNAL",
                Font = HomeTypography.Display(13),
                Spacing = new Vector2(0.7f, 0),
                Colour = new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.82f),
            },
            new SpriteText
            {
                Position = new Vector2(48, 19),
                Text = "ACTIVE / READY",
                Font = HomeTypography.Display(15),
                Colour = SongSelectTheme.Navy,
            },
            new HomeSignalWave(new Color4(
                SongSelectTheme.Cyan.R,
                SongSelectTheme.Cyan.G,
                SongSelectTheme.Cyan.B,
                0.72f))
            {
                Position = new Vector2(342, 5),
                Scale = new Vector2(0.72f),
            },
            new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -24,
                Text = "SYNC / 04",
                Font = HomeTypography.Display(13),
                Colour = new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.82f),
            },
            statusPulse = new Circle
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -10,
                Size = new Vector2(6),
                Colour = SongSelectTheme.Pink,
            },
        ];
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        statusPulse.FadeTo(0.32f, 620, Easing.InOutSine)
                   .Then()
                   .FadeTo(1, 620, Easing.InOutSine)
                   .Loop();
    }
}
