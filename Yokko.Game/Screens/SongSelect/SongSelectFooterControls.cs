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

internal partial class SongSelectFooterBackButton : ClickableContainer
{
    private readonly Box background;
    private readonly Box underline;
    private readonly SpriteIcon chevron;

    public SongSelectFooterBackButton(Action action)
    {
        Action = action;
        Size = new Vector2(174, 74);

        InternalChildren = new Drawable[]
        {
            new Container
            {
                Position = new Vector2(0, 4),
                Size = new Vector2(174, 70),
                Masking = true,
                CornerRadius = 7,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.34f),
                },
            },
            new Container
            {
                Position = new Vector2(-2, -2),
                Size = new Vector2(178, 72),
                Masking = true,
                CornerRadius = 9,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        HomeControlColours.PaleCyan.R,
                        HomeControlColours.PaleCyan.G,
                        HomeControlColours.PaleCyan.B,
                        0.7f),
                },
            },
            new Container
            {
                Size = new Vector2(174, 70),
                Masking = true,
                CornerRadius = 7,
                BorderThickness = 1.5f,
                BorderColour = HomeControlColours.Navy,
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                    },
                    new Container
                    {
                        Position = new Vector2(4),
                        Size = new Vector2(166, 62),
                        Masking = true,
                        CornerRadius = 4,
                        BorderThickness = 1,
                        BorderColour = new Color4(
                            HomeControlColours.Cyan.R,
                            HomeControlColours.Cyan.G,
                            HomeControlColours.Cyan.B,
                            0.42f),
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Alpha = 0,
                        },
                    },
                },
            },
            new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 14,
                Y = -2,
                Size = new Vector2(44),
                Masking = true,
                CornerRadius = 6,
                BorderThickness = 1.5f,
                BorderColour = HomeControlColours.Navy,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = HomeControlColours.Ivory,
                    },
                    new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = "ESC",
                        Font = HomeTypography.Display(11),
                        Colour = HomeControlColours.Navy,
                    },
                },
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 68,
                Y = -2,
                Text = "BACK",
                Font = HomeTypography.Display(20),
                Colour = HomeControlColours.Navy,
            },
            chevron = new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -13,
                Y = -2,
                Size = new Vector2(12),
                Icon = FontAwesome.Solid.ChevronRight,
                Colour = HomeControlColours.Pink,
            },
            new Box
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Size = new Vector2(17),
                Rotation = 45,
                Colour = HomeControlColours.Yellow,
            },
            underline = new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                X = 68,
                Y = -4,
                Width = 0,
                Height = 2,
                Colour = HomeControlColours.Cyan,
            },
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(HomeControlColours.PaleCyan, 120, Easing.OutQuint);
        underline.ResizeWidthTo(58, 150, Easing.OutQuint);
        chevron.MoveToX(-9, 150, Easing.OutQuint);
        this.ScaleTo(1.018f, 120, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(Color4.White, 140, Easing.OutQuint);
        underline.ResizeWidthTo(0, 130, Easing.OutQuint);
        chevron.MoveToX(-13, 130, Easing.OutQuint);
        this.ScaleTo(1, 140, Easing.OutQuint);
    }
}
