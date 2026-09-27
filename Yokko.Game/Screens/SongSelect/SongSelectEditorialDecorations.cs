using Yokko.Game.Localisation;
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
    internal SongSelectPreviewSignalStrip(string keyMode)
    {
        Size = new Vector2(540, 32);
        InternalChildren =
        [
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 2,
                Size = new Vector2(18),
                Icon = FontAwesome.Solid.Headphones,
                Colour = SongSelectTheme.Cyan,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 32,
                Text = YokkoStrings.Get("song_select.preview_caption"),
                Font = HomeTypography.Display(16),
                Colour = SongSelectTheme.Navy,
            },
            new Box
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 188,
                Size = new Vector2(238, 1),
                Colour = new Color4(0.29f, 0.81f, 0.94f, 0.32f),
            },
            new Container
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Size = new Vector2(88, 28),
                Masking = true,
                CornerRadius = 9,
                Children =
                [
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(1f, 0.22f, 0.65f, 0.10f),
                    },
                    new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = keyMode,
                        Font = HomeTypography.Display(16),
                        Colour = SongSelectTheme.Pink,
                    },
                ],
            },
        ];
    }
}
