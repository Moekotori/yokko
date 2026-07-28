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
    public static readonly Color4 Ivory = new(0.992f, 0.992f, 0.988f, 1f);
}

internal static class HomeTypography
{
    public static FontUsage Display(float size) => new("Roboto", size, "Bold");

    public static FontUsage Hero(float size) => new("Roboto", size, "Bold");

    public static FontUsage Body(float size) => new("Roboto", size);

    public static FontUsage Brand(float size) => new("Roboto", size, "Bold");
}

public partial class HomePrimaryAction : ClickableContainer
{
    private readonly Box background;

    public HomePrimaryAction(string title, string detail, IconUsage icon, Action action)
    {
        Action = action;
        Size = new Vector2(510, 130);
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
                Position = new Vector2(6),
                Size = new Vector2(498, 118),
                Masking = true,
                CornerRadius = 6,
                BorderThickness = 1,
                BorderColour = new Color4(0.55f, 0.78f, 1f, 0.48f),
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                },
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
                        Font = HomeTypography.Display(39),
                        Spacing = new Vector2(0.5f, 0),
                        Colour = Color4.White,
                    },
                    new SpriteText
                    {
                        Text = detail,
                        Font = HomeTypography.Body(19),
                        Spacing = new Vector2(0.35f, 0),
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
                Origin = Anchor.Centre,
                Size = new Vector2(24),
                Rotation = 45,
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
                        Font = HomeTypography.Body(22),
                        Spacing = new Vector2(0.35f, 0),
                        Colour = HomeControlColours.Navy,
                    },
                    new SpriteText
                    {
                        Text = keyHint,
                        Font = HomeTypography.Body(10),
                        Spacing = new Vector2(1.05f, 0),
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
                        Font = HomeTypography.Display(18),
                        Spacing = new Vector2(0.35f, 0),
                        Colour = HomeControlColours.Navy,
                        Alpha = string.IsNullOrEmpty(text) ? 0 : 1,
                    },
                },
            },
            new Box
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Size = new Vector2(16),
                Rotation = 45,
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
        string[] words = text.Split(' ', 2);

        Size = new Vector2(132, 84);
        Masking = true;
        CornerRadius = 32;
        BorderThickness = 2;
        BorderColour = HomeControlColours.Navy;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new FillFlowContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, -2),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Text = words[0],
                        Font = HomeTypography.Display(25),
                        Spacing = new Vector2(0.35f, 0),
                        Colour = HomeControlColours.Navy,
                    },
                    new SpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Text = words.Length > 1 ? words[1] : string.Empty,
                        Font = HomeTypography.Display(25),
                        Spacing = new Vector2(0.35f, 0),
                        Colour = HomeControlColours.Navy,
                    },
                },
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

public partial class HomeDotCross : CompositeDrawable
{
    public HomeDotCross()
    {
        Size = new Vector2(70);

        for (int row = 0; row < 7; row++)
        {
            for (int column = 0; column < 7; column++)
            {
                if (row is not (2 or 3 or 4) && column is not (2 or 3 or 4))
                    continue;

                AddInternal(new Circle
                {
                    Position = new Vector2(column * 9, row * 9),
                    Size = new Vector2(4),
                    Colour = new Color4(HomeControlColours.Cyan.R, HomeControlColours.Cyan.G, HomeControlColours.Cyan.B, 0.28f),
                });
            }
        }
    }
}

public partial class HomeConnectorPlus : CompositeDrawable
{
    public HomeConnectorPlus()
    {
        Size = new Vector2(20);

        InternalChildren = new Drawable[]
        {
            new Circle
            {
                RelativeSizeAxes = Axes.Both,
                Colour = HomeControlColours.Navy,
            },
            new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(9),
                Icon = FontAwesome.Solid.Plus,
                Colour = Color4.White,
            },
        };
    }
}
