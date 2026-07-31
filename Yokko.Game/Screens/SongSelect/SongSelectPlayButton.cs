using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectPlayButton : ClickableContainer
{
    private readonly Box background;
    private readonly SpriteIcon chevron;

    public SongSelectPlayButton(Action action)
    {
        Action = action;
        Size = new Vector2(390, 82);

        InternalChildren =
        [
            new Container
            {
                Position = new Vector2(3, 5),
                Size = new Vector2(387, 77),
                Masking = true,
                CornerRadius = 10,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        SongSelectTheme.DeepNavy.R,
                        SongSelectTheme.DeepNavy.G,
                        SongSelectTheme.DeepNavy.B,
                        0.34f),
                },
            },
            new Container
            {
                Size = new Vector2(387, 77),
                Masking = true,
                CornerRadius = 10,
                BorderThickness = 1.5f,
                BorderColour = SongSelectTheme.Navy,
                Children =
                [
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = SongSelectTheme.Yellow,
                    },
                    new Container
                    {
                        Position = new Vector2(13, 7),
                        Size = new Vector2(64),
                        Masking = true,
                        CornerRadius = 8,
                        Children =
                        [
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = SongSelectTheme.Navy,
                            },
                            new SpriteIcon
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Size = new Vector2(28),
                                Icon = FontAwesome.Solid.Play,
                                Colour = Color4.White,
                            },
                        ],
                    },
                    new SpriteText
                    {
                        Position = new Vector2(100, 14),
                        Text = "SONG SELECT",
                        Font = HomeTypography.Display(10),
                        Spacing = new Vector2(1.8f, 0),
                        Colour = SongSelectTheme.Cyan,
                    },
                    new SpriteText
                    {
                        Position = new Vector2(99, 28),
                        Text = "PLAY",
                        Font = HomeTypography.Display(34),
                        Colour = SongSelectTheme.Navy,
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.BottomRight,
                        Origin = Anchor.BottomRight,
                        Position = new Vector2(-44, -8),
                        Size = new Vector2(22),
                        Icon = FontAwesome.Regular.Heart,
                        Colour = new Color4(
                            SongSelectTheme.Navy.R,
                            SongSelectTheme.Navy.G,
                            SongSelectTheme.Navy.B,
                            0.52f),
                    },
                    chevron = new SpriteIcon
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -15,
                        Size = new Vector2(14),
                        Icon = FontAwesome.Solid.ChevronRight,
                        Colour = SongSelectTheme.Navy,
                    },
                    new SpriteIcon
                    {
                        Position = new Vector2(89, 16),
                        Size = new Vector2(10),
                        Icon = FontAwesome.Solid.Plus,
                        Colour = SongSelectTheme.Pink,
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Position = new Vector2(-42, 12),
                        Size = new Vector2(10),
                        Icon = FontAwesome.Solid.Plus,
                        Colour = SongSelectTheme.Cyan,
                    },
                ],
            },
            new Box
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-25, 1),
                Size = new Vector2(42, 13),
                Rotation = 8,
                Colour = new Color4(
                    SongSelectTheme.Pink.R,
                    SongSelectTheme.Pink.G,
                    SongSelectTheme.Pink.B,
                    0.76f),
            },
        ];
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(
            new Color4(1f, 0.95f, 0.42f, 1f),
            110,
            Easing.OutQuint);
        chevron.MoveToX(-10, 130, Easing.OutQuint);
        this.ScaleTo(1.018f, 110, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(SongSelectTheme.Yellow, 130, Easing.OutQuint);
        chevron.MoveToX(-15, 130, Easing.OutQuint);
        this.ScaleTo(1, 130, Easing.OutQuint);
    }
}
