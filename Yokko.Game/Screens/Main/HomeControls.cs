using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;

namespace Yokko.Game.Screens.Main;

internal static class HomeControlColours
{
    public static readonly Color4 Navy = new(0.035f, 0.085f, 0.54f, 1f);
    public static readonly Color4 Cyan = new(0.18f, 0.78f, 0.94f, 1f);
    public static readonly Color4 Yellow = new(1f, 0.91f, 0.42f, 1f);
    public static readonly Color4 Pink = new(1f, 0.22f, 0.65f, 1f);
    public static readonly Color4 Ivory = new(0.986f, 0.982f, 0.956f, 1f);
}

public partial class HomePrimaryAction : ClickableContainer
{
    private readonly Box background;

    public HomePrimaryAction(string title, string detail, IconUsage icon, Action action)
    {
        Action = action;
        Size = new Vector2(510, 122);
        Masking = true;
        CornerRadius = 9;
        BorderThickness = 2;
        BorderColour = HomeControlColours.Navy;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = HomeControlColours.Navy,
            },
            new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 24,
                Size = new Vector2(52),
                Masking = true,
                CornerRadius = 8,
                BorderThickness = 2,
                BorderColour = Color4.White,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(24),
                        Icon = icon,
                        Colour = HomeControlColours.Navy,
                    },
                },
            },
            new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 98,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 3),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = title,
                        Font = FontUsage.Default.With(size: 29, weight: "Bold"),
                        Colour = Color4.White,
                    },
                    new SpriteText
                    {
                        Text = detail,
                        Font = FontUsage.Default.With(size: 16),
                        Colour = new Color4(0.78f, 0.89f, 1f, 1f),
                    },
                },
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -24,
                Size = new Vector2(23),
                Icon = FontAwesome.Solid.ChevronRight,
                Colour = HomeControlColours.Yellow,
            },
            new Box
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Size = new Vector2(24, 5),
                Colour = HomeControlColours.Yellow,
            },
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(new Color4(0.055f, 0.15f, 0.7f, 1f), 130, Easing.OutQuint);
        this.ScaleTo(1.012f, 130, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(HomeControlColours.Navy, 150, Easing.OutQuint);
        this.ScaleTo(1f, 150, Easing.OutQuint);
    }
}

public partial class HomeDemoAction : ClickableContainer
{
    private readonly Box background;
    private readonly Box underline;

    public HomeDemoAction(string title, string keyHint, Action action)
    {
        Action = action;
        Size = new Vector2(250, 76);
        Masking = true;
        CornerRadius = 7;
        BorderThickness = 1.5f;
        BorderColour = HomeControlColours.Navy;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 18,
                Size = new Vector2(24),
                Icon = FontAwesome.Solid.Keyboard,
                Colour = HomeControlColours.Cyan,
            },
            new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 55,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = title,
                        Font = FontUsage.Default.With(size: 17, weight: "Bold"),
                        Colour = HomeControlColours.Navy,
                    },
                    new SpriteText
                    {
                        Text = keyHint,
                        Font = FontUsage.Default.With(size: 9, weight: "SemiBold"),
                        Colour = new Color4(0.25f, 0.45f, 0.68f, 1f),
                    },
                },
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -16,
                Size = new Vector2(15),
                Icon = FontAwesome.Solid.ChevronRight,
                Colour = HomeControlColours.Pink,
            },
            underline = new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Width = 0,
                Height = 4,
                Colour = HomeControlColours.Cyan,
            },
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(new Color4(0.9f, 0.985f, 1f, 1f), 120, Easing.OutQuint);
        underline.ResizeWidthTo(250, 150, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(Color4.White, 140, Easing.OutQuint);
        underline.ResizeWidthTo(0, 140, Easing.OutQuint);
    }
}

public partial class HomeUtilityButton : ClickableContainer
{
    private readonly Box background;

    public HomeUtilityButton(string text, IconUsage icon, Action action, float width)
    {
        Action = action;
        Size = new Vector2(width, 48);
        Masking = true;
        CornerRadius = 8;
        BorderThickness = 1.5f;
        BorderColour = HomeControlColours.Navy;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(1f, 1f, 1f, 0.94f),
            },
            new FillFlowContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(9, 0),
                Children = new Drawable[]
                {
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(18),
                        Icon = icon,
                        Colour = HomeControlColours.Navy,
                    },
                    new SpriteText
                    {
                        Text = text,
                        Font = FontUsage.Default.With(size: 15, weight: "SemiBold"),
                        Colour = HomeControlColours.Navy,
                        Alpha = string.IsNullOrEmpty(text) ? 0 : 1,
                    },
                },
            },
            new Box
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Size = new Vector2(18, 4),
                Colour = HomeControlColours.Yellow,
                Alpha = string.IsNullOrEmpty(text) ? 0 : 1,
            },
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(new Color4(0.9f, 0.985f, 1f, 1f), 120, Easing.OutQuint);
        this.ScaleTo(1.025f, 120, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(new Color4(1f, 1f, 1f, 0.94f), 140, Easing.OutQuint);
        this.ScaleTo(1f, 140, Easing.OutQuint);
    }
}

public partial class HomeMascotBubble : CompositeDrawable
{
    public HomeMascotBubble(string text)
    {
        Size = new Vector2(126, 72);
        Masking = true;
        CornerRadius = 28;
        BorderThickness = 2;
        BorderColour = HomeControlColours.Navy;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = text,
                Font = FontUsage.Default.With(size: 18, weight: "Bold"),
                Colour = HomeControlColours.Navy,
            },
            new Box
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Y = -1,
                Width = 54,
                Height = 4,
                Colour = HomeControlColours.Pink,
            },
        };
    }
}
