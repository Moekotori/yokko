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
    public static readonly Color4 PaleCyan = new(0.78f, 0.96f, 1f, 1f);
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
    private readonly Box focusLine;

    public HomePrimaryAction(string title, string eyebrow, IconUsage icon, Action action)
    {
        Action = action;
        Size = new Vector2(520, 120);
        Masking = true;
        CornerRadius = 10;
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
                Position = new Vector2(5),
                Size = new Vector2(510, 110),
                Masking = true,
                CornerRadius = 7,
                BorderThickness = 1,
                BorderColour = new Color4(0.56f, 0.88f, 1f, 0.72f),
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                },
            },
            new HomeDotField
            {
                Position = new Vector2(344, 18),
                Size = new Vector2(132, 78),
                Colour = new Color4(0.39f, 0.76f, 1f, 0.34f),
            },
            new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 25,
                Size = new Vector2(64),
                Masking = true,
                CornerRadius = 9,
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
                        Size = new Vector2(28),
                        Icon = icon,
                        Colour = HomeControlColours.Navy,
                    },
                },
            },
            new FillFlowContainer
            {
                X = 118,
                Y = 24,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, -4),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = eyebrow,
                        Font = HomeTypography.Display(13),
                        Spacing = new Vector2(2.4f, 0),
                        Colour = HomeControlColours.Cyan,
                    },
                    new SpriteText
                    {
                        Text = title,
                        Font = HomeTypography.Display(43),
                        Scale = new Vector2(0.9f, 1),
                        Colour = Color4.White,
                    },
                },
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -27,
                Size = new Vector2(25),
                Icon = FontAwesome.Solid.ChevronRight,
                Colour = HomeControlColours.Yellow,
            },
            new Box
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Size = new Vector2(19),
                Rotation = 45,
                Colour = HomeControlColours.Yellow,
            },
            new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                X = 18,
                Width = 111,
                Height = 4,
                Colour = HomeControlColours.Pink,
            },
            focusLine = new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                X = 129,
                Width = 188,
                Height = 2,
                Colour = HomeControlColours.Cyan,
            },
            new Box
            {
                Position = new Vector2(14, 13),
                Size = new Vector2(16, 2),
                Colour = HomeControlColours.Cyan,
            },
            new Box
            {
                Position = new Vector2(14, 13),
                Size = new Vector2(2, 16),
                Colour = HomeControlColours.Cyan,
            },
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(new Color4(0.055f, 0.15f, 0.7f, 1f), 130, Easing.OutQuint);
        focusLine.ResizeWidthTo(340, 160, Easing.OutQuint);
        this.ScaleTo(1.008f, 130, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(HomeControlColours.Navy, 150, Easing.OutQuint);
        focusLine.ResizeWidthTo(188, 150, Easing.OutQuint);
        this.ScaleTo(1f, 150, Easing.OutQuint);
    }
}

public partial class HomeSecondaryAction : ClickableContainer
{
    private readonly Box background;
    private readonly Box underline;

    public HomeSecondaryAction(string title, IconUsage icon, Action action)
    {
        Action = action;
        Size = new Vector2(252, 82);
        Masking = true;
        CornerRadius = 9;
        BorderThickness = 2;
        BorderColour = HomeControlColours.Navy;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 14,
                Size = new Vector2(56),
                Masking = true,
                CornerRadius = 9,
                BorderThickness = 2,
                BorderColour = HomeControlColours.Navy,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = HomeControlColours.PaleCyan,
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(27),
                        Icon = icon,
                        Colour = HomeControlColours.Navy,
                    },
                },
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 85,
                Text = title,
                Font = HomeTypography.Display(28),
                Scale = new Vector2(0.94f, 1),
                Colour = HomeControlColours.Navy,
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
            new FillFlowContainer
            {
                Position = new Vector2(87, 61),
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(5, 0),
                Children = new Drawable[]
                {
                    createDetailDot(),
                    createDetailDot(),
                    createDetailDot(),
                    createDetailDot(),
                    createDetailDot(),
                },
            },
            new Box
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Size = new Vector2(15),
                Rotation = 45,
                Colour = HomeControlColours.Yellow,
            },
            new Container
            {
                Position = new Vector2(4),
                Size = new Vector2(244, 74),
                Masking = true,
                CornerRadius = 6,
                BorderThickness = 1,
                BorderColour = new Color4(HomeControlColours.Cyan.R, HomeControlColours.Cyan.G, HomeControlColours.Cyan.B, 0.44f),
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                },
            },
            underline = new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Width = 0,
                Height = 3,
                Colour = HomeControlColours.Cyan,
            },
        };
    }

    private static Drawable createDetailDot() => new Circle
    {
        Size = new Vector2(3),
        Colour = HomeControlColours.Cyan,
        Alpha = 0.76f,
    };

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(new Color4(0.9f, 0.985f, 1f, 1f), 120, Easing.OutQuint);
        underline.ResizeWidthTo(252, 150, Easing.OutQuint);
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
        Size = new Vector2(width, 58);
        Masking = true;
        CornerRadius = 10;
        BorderThickness = 2;
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
                        Size = new Vector2(24),
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
                Size = new Vector2(14),
                Rotation = 45,
                Colour = HomeControlColours.Yellow,
            },
            new Container
            {
                Position = new Vector2(4),
                Size = new Vector2(width - 8, 50),
                Masking = true,
                CornerRadius = 7,
                BorderThickness = 1,
                BorderColour = new Color4(HomeControlColours.Cyan.R, HomeControlColours.Cyan.G, HomeControlColours.Cyan.B, 0.55f),
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                },
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

        Size = new Vector2(122, 94);

        InternalChildren = new Drawable[]
        {
            new Container
            {
                Position = new Vector2(84, 72),
                Size = new Vector2(18),
                Rotation = 45,
                Masking = true,
                BorderThickness = 2,
                BorderColour = HomeControlColours.Navy,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
            },
            new Container
            {
                Size = new Vector2(122, 84),
                Masking = true,
                CornerRadius = 30,
                BorderThickness = 2,
                BorderColour = HomeControlColours.Navy,
                Children = new Drawable[]
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
                        Spacing = new Vector2(0, -3),
                        Children = new Drawable[]
                        {
                            new SpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Text = words[0],
                                Font = HomeTypography.Display(23),
                                Scale = new Vector2(0.94f, 1),
                                Colour = HomeControlColours.Navy,
                            },
                            new SpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Text = words.Length > 1 ? words[1] : string.Empty,
                                Font = HomeTypography.Display(23),
                                Scale = new Vector2(0.94f, 1),
                                Colour = HomeControlColours.Navy,
                            },
                        },
                    },
                    new Box
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Y = -1,
                        Width = 48,
                        Height = 4,
                        Colour = HomeControlColours.Pink,
                    },
                },
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

public partial class HomeDotField : CompositeDrawable
{
    public HomeDotField()
    {
        for (int row = 0; row < 9; row++)
        {
            for (int column = 0; column < 15; column++)
            {
                AddInternal(new Circle
                {
                    RelativePositionAxes = Axes.Both,
                    Position = new Vector2(column / 14f, row / 8f),
                    Size = new Vector2(2.5f),
                    Colour = Color4.White,
                });
            }
        }
    }
}

public partial class HomeCornerBracket : CompositeDrawable
{
    public HomeCornerBracket()
    {
        Width = 18;
        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 2,
                Colour = HomeControlColours.Navy,
                Alpha = 0.78f,
            },
            new Box
            {
                Width = 18,
                Height = 2,
                Colour = HomeControlColours.Navy,
                Alpha = 0.78f,
            },
            new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Width = 18,
                Height = 2,
                Colour = HomeControlColours.Navy,
                Alpha = 0.78f,
            },
        };
    }
}

public partial class HomeMicroLine : CompositeDrawable
{
    public HomeMicroLine()
    {
        Height = 4;
        InternalChildren = new Drawable[]
        {
            new Box
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Colour = HomeControlColours.Cyan,
                Alpha = 0.7f,
            },
            new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                Size = new Vector2(4),
                Colour = HomeControlColours.Cyan,
            },
        };
    }
}
