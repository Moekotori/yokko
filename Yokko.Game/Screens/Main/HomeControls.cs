using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
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
    public static FontUsage Display(float size) => new("Roboto", readableSize(size), "Bold");

    public static FontUsage Hero(float size) => new("Roboto", readableSize(size), "Bold");

    public static FontUsage Body(float size) => new("Roboto", readableSize(size));

    public static FontUsage Brand(float size) => new("Roboto", readableSize(size), "Bold");

    // At high-DPI desktop resolutions the framework renders in physical pixels.
    // Give compact labels a meaningful readability floor without inflating hero text.
    private static float readableSize(float size) => size <= 22 ? size + 3 : size;
}

public partial class HomePrimaryAction : ClickableContainer
{
    private const float hover_scale = 1.004f;

    private readonly Box background;
    private readonly Box focusLine;
    private readonly SpriteIcon chevron;
    private readonly Box shine;

    public HomePrimaryAction(
        LocalisableString title,
        LocalisableString eyebrow,
        IconUsage icon,
        Action action,
        float iconTileSize = 72,
        float iconTileX = 30,
        float iconSize = 31,
        float iconTileY = 0,
        float contentX = 133)
    {
        Action = action;
        Size = new Vector2(520, 120);

        InternalChildren = new Drawable[]
        {
            new Container
            {
                Position = new Vector2(0, 5),
                Size = new Vector2(520, 115),
                Masking = true,
                CornerRadius = 10,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.015f, 0.045f, 0.28f, 0.4f),
                },
            },
            new Container
            {
                Position = new Vector2(-2, -2),
                Size = new Vector2(524, 118),
                Masking = true,
                CornerRadius = 11,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(HomeControlColours.Cyan.R, HomeControlColours.Cyan.G, HomeControlColours.Cyan.B, 0.64f),
                },
            },
            new Container
            {
                Size = new Vector2(520, 114),
                Masking = true,
                CornerRadius = 10,
                BorderThickness = 2,
                BorderColour = HomeControlColours.Navy,
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = HomeControlColours.Navy,
                    },
                    shine = new Box
                    {
                        Position = new Vector2(-140, -28),
                        Origin = Anchor.CentreLeft,
                        Width = 70,
                        Height = 170,
                        Rotation = 22,
                        Colour = Color4.White,
                        Alpha = 0.13f,
                    },
                    new HomeDotField
                    {
                        Position = new Vector2(358, 18),
                        Size = new Vector2(145, 78),
                        Colour = new Color4(0.39f, 0.76f, 1f, 0.24f),
                    },
                    new Container
                    {
                        Position = new Vector2(5),
                        Size = new Vector2(510, 104),
                        Masking = true,
                        CornerRadius = 7,
                        BorderThickness = 1,
                        BorderColour = new Color4(0.56f, 0.88f, 1f, 0.5f),
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
                        X = iconTileX + 3,
                        Y = iconTileY + 4,
                        Size = new Vector2(iconTileSize),
                        Masking = true,
                        CornerRadius = 9,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(HomeControlColours.Cyan.R, HomeControlColours.Cyan.G, HomeControlColours.Cyan.B, 0.54f),
                        },
                    },
                    new Container
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = iconTileX,
                        Y = iconTileY,
                        Size = new Vector2(iconTileSize),
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
                                Size = new Vector2(iconSize - 2),
                                Icon = icon,
                                Colour = HomeControlColours.Navy,
                            },
                        },
                    },
                    new FillFlowContainer
                    {
                        X = contentX,
                        Y = 24,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 0),
                        Children = new Drawable[]
                        {
                            new SpriteText
                            {
                                Text = eyebrow,
                                Font = HomeTypography.Display(12),
                                Spacing = new Vector2(2.4f, 0),
                                Colour = HomeControlColours.Cyan,
                            },
                            new SpriteText
                            {
                                Text = title,
                                Font = HomeTypography.Display(48),
                                Colour = Color4.White,
                            },
                        },
                    },
                    chevron = new SpriteIcon
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -27,
                        Size = new Vector2(23),
                        Icon = FontAwesome.Solid.ChevronRight,
                        Colour = HomeControlColours.Yellow,
                    },
                    new Box
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.Centre,
                        Size = new Vector2(16),
                        Rotation = 45,
                        Colour = HomeControlColours.Yellow,
                    },
                    new Box
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        X = 18,
                        Width = 86,
                        Height = 3,
                        Colour = HomeControlColours.Pink,
                    },
                    focusLine = new Box
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        X = 104,
                        Width = 213,
                        Height = 1.5f,
                        Colour = HomeControlColours.Cyan,
                    },
                },
            },
            new Box
            {
                Position = new Vector2(-3, 15),
                Size = new Vector2(3, 83),
                Colour = HomeControlColours.Cyan,
            },
            new Box
            {
                Position = new Vector2(8, -3),
                Size = new Vector2(494, 3),
                Colour = HomeControlColours.Cyan,
                Alpha = 0.75f,
            },
            new Box
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                X = 3,
                Y = -6,
                Size = new Vector2(3, 82),
                Colour = HomeControlColours.Cyan,
                Alpha = 0.72f,
            },
            new Box
            {
                Position = new Vector2(12, 11),
                Size = new Vector2(16, 3),
                Colour = HomeControlColours.Cyan,
            },
            new Box
            {
                Position = new Vector2(12, 11),
                Size = new Vector2(3, 16),
                Colour = HomeControlColours.Cyan,
            },
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(new Color4(0.055f, 0.15f, 0.7f, 1f), 130, Easing.OutQuint);
        focusLine.ResizeWidthTo(340, 160, Easing.OutQuint);
        chevron.MoveToX(-19, 170, Easing.OutQuint);
        this.ScaleTo(hover_scale, 130, Easing.OutQuint);
        return true;
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        // 周期性光泽扫过，增加精致感。
        shine.MoveToX(-140).MoveToX(660, 800, Easing.InOutQuart).Loop(3200);
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(HomeControlColours.Navy, 150, Easing.OutQuint);
        focusLine.ResizeWidthTo(188, 150, Easing.OutQuint);
        chevron.MoveToX(-27, 180, Easing.OutQuint);
        this.ScaleTo(1f, 150, Easing.OutQuint);
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        this.ScaleTo(0.985f, 500, Easing.OutQuint);
        return base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseUpEvent e)
    {
        this.ScaleTo(IsHovered ? hover_scale : 1f, 260, Easing.OutQuint);
        base.OnMouseUp(e);
    }
}

public partial class HomeSecondaryAction : ClickableContainer
{
    private readonly Box background;
    private readonly Box underline;
    private readonly SpriteIcon chevron;
    private readonly Container iconTile;

    public HomeSecondaryAction(LocalisableString title, IconUsage icon, Action action, IconUsage? overlayIcon = null)
    {
        Action = action;
        Size = new Vector2(252, 82);

        InternalChildren = new Drawable[]
        {
            new Container
            {
                Position = new Vector2(0, 4),
                Size = new Vector2(252, 78),
                Masking = true,
                CornerRadius = 9,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.015f, 0.045f, 0.28f, 0.22f),
                },
            },
            new Container
            {
                Position = new Vector2(-1.5f, -1.5f),
                Size = new Vector2(255, 81),
                Masking = true,
                CornerRadius = 10,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(HomeControlColours.Cyan.R, HomeControlColours.Cyan.G, HomeControlColours.Cyan.B, 0.34f),
                },
            },
            new Container
            {
                Size = new Vector2(252, 78),
                Masking = true,
                CornerRadius = 9,
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
                        Size = new Vector2(244, 70),
                        Masking = true,
                        CornerRadius = 6,
                        BorderThickness = 1,
                        BorderColour = new Color4(HomeControlColours.Cyan.R, HomeControlColours.Cyan.G, HomeControlColours.Cyan.B, 0.28f),
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
                X = 18,
                Y = 3,
                Size = new Vector2(52),
                Masking = true,
                CornerRadius = 8,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.035f, 0.085f, 0.54f, 0.18f),
                },
            },
            iconTile = new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 15,
                Size = new Vector2(52),
                Masking = true,
                CornerRadius = 8,
                BorderThickness = 1.5f,
                BorderColour = HomeControlColours.Navy,
                Children = createIconContents(icon, overlayIcon),
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 80,
                Text = title,
                Font = HomeTypography.Display(24),
                Colour = HomeControlColours.Navy,
            },
            chevron = new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -16,
                Size = new Vector2(13),
                Icon = FontAwesome.Solid.ChevronRight,
                Colour = HomeControlColours.Pink,
            },
            new FillFlowContainer
            {
                Position = new Vector2(82, 59),
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
                Size = new Vector2(12),
                Rotation = 45,
                Colour = HomeControlColours.Yellow,
            },
            underline = new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Y = -4,
                Width = 0,
                Height = 2,
                Colour = HomeControlColours.Cyan,
            },
        };
    }

    private static Drawable[] createIconContents(IconUsage icon, IconUsage? overlayIcon)
    {
        if (overlayIcon == null)
        {
            return new Drawable[]
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
            };
        }

        return new Drawable[]
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
                Position = new Vector2(-2, -2),
                Size = new Vector2(28),
                Icon = icon,
                Colour = HomeControlColours.Navy,
            },
            new Circle
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Position = new Vector2(-4, -4),
                Size = new Vector2(20),
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Position = new Vector2(-7, -7),
                Size = new Vector2(13),
                Icon = overlayIcon.Value,
                Colour = HomeControlColours.Navy,
            },
        };
    }

    private static Drawable createDetailDot() => new Circle
    {
        Size = new Vector2(2.5f),
        Colour = HomeControlColours.Cyan,
        Alpha = 0.76f,
    };

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(new Color4(0.9f, 0.985f, 1f, 1f), 120, Easing.OutQuint);
        underline.ResizeWidthTo(252, 150, Easing.OutQuint);
        chevron.MoveToX(-11, 160, Easing.OutQuint);
        iconTile.RotateTo(-7, 140, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(Color4.White, 140, Easing.OutQuint);
        underline.ResizeWidthTo(0, 140, Easing.OutQuint);
        chevron.MoveToX(-16, 170, Easing.OutQuint);
        iconTile.RotateTo(0, 240, Easing.OutQuint);
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        this.ScaleTo(0.97f, 450, Easing.OutQuint);
        return base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseUpEvent e)
    {
        this.ScaleTo(1f, 240, Easing.OutQuint);
        base.OnMouseUp(e);
    }
}

public partial class HomeMultiplayerAction : ClickableContainer
{
    private readonly Box background;
    private readonly Box underline;
    private readonly SpriteIcon chevron;
    private readonly Container iconTile;

    public HomeMultiplayerAction(
        LocalisableString title,
        LocalisableString friendsOnline,
        Action action,
        IReadOnlyList<Texture> onlineFriendAvatars = null)
    {
        bool hasOnlineFriends = onlineFriendAvatars?.Count > 0;

        Action = action;
        Size = new Vector2(520, 82);

        background = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = Color4.White,
        };
        iconTile = new Container
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            X = 15,
            Size = new Vector2(52),
            Masking = true,
            CornerRadius = 8,
            BorderThickness = 1.5f,
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
                    Icon = FontAwesome.Solid.Users,
                    Colour = HomeControlColours.Navy,
                },
            },
        };
        chevron = new SpriteIcon
        {
            Anchor = Anchor.CentreRight,
            Origin = Anchor.CentreRight,
            X = -16,
            Size = new Vector2(13),
            Icon = FontAwesome.Solid.ChevronRight,
            Colour = HomeControlColours.Pink,
        };
        underline = new Box
        {
            Anchor = Anchor.BottomLeft,
            Origin = Anchor.BottomLeft,
            Y = -4,
            Width = 0,
            Height = 2,
            Colour = HomeControlColours.Cyan,
        };

        var children = new List<Drawable>
        {
            new Container
            {
                Position = new Vector2(0, 4),
                Size = new Vector2(520, 78),
                Masking = true,
                CornerRadius = 9,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.015f, 0.045f, 0.28f, 0.22f),
                },
            },
            new Container
            {
                Position = new Vector2(-1.5f, -1.5f),
                Size = new Vector2(523, 81),
                Masking = true,
                CornerRadius = 10,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        0.34f),
                },
            },
            new Container
            {
                Size = new Vector2(520, 78),
                Masking = true,
                CornerRadius = 9,
                BorderThickness = 1.5f,
                BorderColour = HomeControlColours.Navy,
                Children = new Drawable[]
                {
                    background,
                    new Container
                    {
                        Position = new Vector2(4),
                        Size = new Vector2(512, 70),
                        Masking = true,
                        CornerRadius = 6,
                        BorderThickness = 1,
                        BorderColour = new Color4(
                            HomeControlColours.Cyan.R,
                            HomeControlColours.Cyan.G,
                            HomeControlColours.Cyan.B,
                            0.28f),
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
                X = 18,
                Y = 3,
                Size = new Vector2(52),
                Masking = true,
                CornerRadius = 8,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.035f, 0.085f, 0.54f, 0.18f),
                },
            },
            iconTile,
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 80,
                Y = hasOnlineFriends ? -12 : 0,
                Text = title,
                Font = HomeTypography.Display(24),
                Colour = HomeControlColours.Navy,
            },
            chevron,
            new Box
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Size = new Vector2(12),
                Rotation = 45,
                Colour = HomeControlColours.Yellow,
            },
            underline,
        };

        if (hasOnlineFriends)
        {
            children.Add(new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 86,
                Y = 17,
                Text = friendsOnline,
                Font = HomeTypography.Body(14),
                Colour = HomeControlColours.Navy,
                Alpha = 0.82f,
            });
            children.Add(createAvatarStrip(onlineFriendAvatars));
        }

        InternalChildren = children;
    }

    private static Drawable createAvatarStrip(IReadOnlyList<Texture> avatars)
    {
        var strip = new FillFlowContainer
        {
            Anchor = Anchor.CentreRight,
            Origin = Anchor.CentreRight,
            X = -50,
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(8, 0),
        };

        for (int i = 0; i < Math.Min(avatars.Count, 3); i++)
        {
            strip.Add(new Container
            {
                Size = new Vector2(48),
                Masking = true,
                CornerRadius = 24,
                BorderThickness = 2,
                BorderColour = i % 2 == 0
                    ? HomeControlColours.Cyan
                    : HomeControlColours.Pink,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = HomeControlColours.PaleCyan,
                    },
                    new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        FillMode = FillMode.Fill,
                        Texture = avatars[i],
                    },
                },
            });
        }

        return strip;
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(new Color4(0.9f, 0.985f, 1f, 1f), 120, Easing.OutQuint);
        underline.ResizeWidthTo(520, 150, Easing.OutQuint);
        chevron.MoveToX(-11, 160, Easing.OutQuint);
        iconTile.RotateTo(-7, 140, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(Color4.White, 140, Easing.OutQuint);
        underline.ResizeWidthTo(0, 140, Easing.OutQuint);
        chevron.MoveToX(-16, 170, Easing.OutQuint);
        iconTile.RotateTo(0, 240, Easing.OutQuint);
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        this.ScaleTo(0.985f, 450, Easing.OutQuint);
        return base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseUpEvent e)
    {
        this.ScaleTo(1f, 240, Easing.OutQuint);
        base.OnMouseUp(e);
    }
}

public partial class HomeUtilityButton : ClickableContainer
{
    private const float rest_alpha = 0.96f;
    private const float hover_scale = 1.025f;

    private readonly Box background;
    private readonly Container tooltip;

    public HomeUtilityButton(string text, IconUsage icon, Action action, float width, IconUsage? overlayIcon = null,
        LocalisableString tooltipText = default)
    {
        Action = action;
        Size = new Vector2(width, 72);

        InternalChildren = new Drawable[]
        {
            new Container
            {
                Position = new Vector2(0, 5),
                Size = new Vector2(width, 67),
                Masking = true,
                CornerRadius = 11,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.015f, 0.045f, 0.28f, 0.3f),
                },
            },
            new Container
            {
                Position = new Vector2(-2, -2),
                Size = new Vector2(width + 4, 70),
                Masking = true,
                CornerRadius = 12,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(HomeControlColours.Cyan.R, HomeControlColours.Cyan.G, HomeControlColours.Cyan.B, 0.58f),
                },
            },
            new Container
            {
                Size = new Vector2(width, 68),
                Masking = true,
                CornerRadius = 11,
                BorderThickness = 2,
                BorderColour = HomeControlColours.Navy,
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(1f, 1f, 1f, rest_alpha),
                    },
                    new Container
                    {
                        Position = new Vector2(4),
                        Size = new Vector2(width - 8, 60),
                        Masking = true,
                        CornerRadius = 8,
                        BorderThickness = 1,
                        BorderColour = new Color4(HomeControlColours.Cyan.R, HomeControlColours.Cyan.G, HomeControlColours.Cyan.B, 0.55f),
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Alpha = 0,
                        },
                    },
                },
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
                    new Container
                    {
                        Size = new Vector2(30),
                        Children = new Drawable[]
                        {
                            new SpriteIcon
                            {
                                RelativeSizeAxes = Axes.Both,
                                Icon = icon,
                                Colour = HomeControlColours.Navy,
                            },
                            new SpriteIcon
                            {
                                Anchor = Anchor.BottomRight,
                                Origin = Anchor.BottomRight,
                                Position = new Vector2(-1, -4),
                                Size = new Vector2(13),
                                Icon = overlayIcon ?? icon,
                                Colour = Color4.White,
                                Alpha = overlayIcon == null ? 0 : 1,
                            },
                        },
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
                Size = new Vector2(17),
                Rotation = 45,
                Colour = HomeControlColours.Yellow,
            },
            tooltip = createTooltip(tooltipText),
        };
    }

    private static Container createTooltip(LocalisableString text) => new()
    {
        Anchor = Anchor.TopCentre,
        Origin = Anchor.TopCentre,
        Y = 82,
        AutoSizeAxes = Axes.Both,
        Alpha = 0,
        Children = new Drawable[]
        {
            new Container
            {
                AutoSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 8,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = HomeControlColours.Navy,
                },
            },
            new SpriteText
            {
                Text = text,
                Font = HomeTypography.Display(12),
                Colour = Color4.White,
                Padding = new MarginPadding { Horizontal = 10, Vertical = 5 },
            },
        },
    };

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(new Color4(0.9f, 0.985f, 1f, 1f), 120, Easing.OutQuint);
        tooltip.FadeIn(150, Easing.OutQuint).MoveToY(86, 150, Easing.OutQuint);
        this.ScaleTo(hover_scale, 120, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(new Color4(1f, 1f, 1f, rest_alpha), 140, Easing.OutQuint);
        tooltip.FadeOut(120).MoveToY(82, 120);
        this.ScaleTo(1f, 140, Easing.OutQuint);
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        this.ScaleTo(0.96f, 450, Easing.OutQuint);
        return base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseUpEvent e)
    {
        this.ScaleTo(IsHovered ? hover_scale : 1f, 240, Easing.OutQuint);
        base.OnMouseUp(e);
    }
}

internal enum HomeMascotBubbleStyle
{
    Rounded,
    PopSignalSticker,
}

public partial class HomeMascotBubble : CompositeDrawable
{
    private readonly Box underline;
    private readonly SpriteText label;
    private readonly float underlineRestWidth;
    private readonly float underlinePulseWidth;

    public HomeMascotBubble(LocalisableString text)
        : this(text, HomeMascotBubbleStyle.Rounded)
    {
    }

    internal HomeMascotBubble(
        LocalisableString text,
        HomeMascotBubbleStyle style)
    {
        if (style == HomeMascotBubbleStyle.PopSignalSticker)
        {
            Size = new Vector2(164, 104);
            underlineRestWidth = 30;
            underlinePulseWidth = 40;

            InternalChildren = new Drawable[]
            {
                new Container
                {
                    Position = new Vector2(10, 8),
                    Size = new Vector2(146, 80),
                    Masking = true,
                    CornerRadius = 14,
                    BorderThickness = 2.5f,
                    BorderColour = HomeControlColours.Navy,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = HomeControlColours.Ivory,
                    },
                },
                new Container
                {
                    Position = new Vector2(105, 68),
                    Size = new Vector2(23),
                    Rotation = 45,
                    Masking = true,
                    BorderThickness = 2.5f,
                    BorderColour = HomeControlColours.Navy,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = HomeControlColours.PaleCyan,
                    },
                },
                new Container
                {
                    Position = new Vector2(124, 70),
                    Size = new Vector2(30, 11),
                    Rotation = -18,
                    Masking = true,
                    CornerRadius = 5.5f,
                    Child = underline = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = HomeControlColours.Pink,
                    },
                },
                new Container
                {
                    Position = new Vector2(4, 0),
                    Size = new Vector2(146, 80),
                    Masking = true,
                    CornerRadius = 12,
                    BorderThickness = 2.5f,
                    BorderColour = HomeControlColours.Navy,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = HomeControlColours.PaleCyan,
                        },
                        label = new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            X = 5,
                            Text = text,
                            Font = HomeTypography.Display(20),
                            Scale = new Vector2(0.94f, 1),
                            Colour = HomeControlColours.Navy,
                        },
                    },
                },
                createOutlinedSparkle(new Vector2(-2, 4), 20),
                createOutlinedSparkle(new Vector2(1, 26), 14),
            };

            return;
        }

        Size = new Vector2(112, 94);
        underlineRestWidth = 48;
        underlinePulseWidth = 60;

        InternalChildren = new Drawable[]
        {
            new Container
            {
                Position = new Vector2(78, 72),
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
                Size = new Vector2(112, 84),
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
                    label = new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = text,
                        Font = HomeTypography.Display(20),
                        Scale = new Vector2(0.94f, 1),
                        Colour = HomeControlColours.Navy,
                    },
                    underline = new Box
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Y = -1,
                        Width = underlineRestWidth,
                        Height = 4,
                        Colour = HomeControlColours.Pink,
                    },
                },
            },
        };
    }

    private static Drawable createOutlinedSparkle(
        Vector2 position,
        float size) =>
        new Container
        {
            Position = position,
            Size = new Vector2(size),
            Children = new Drawable[]
            {
                new SpriteIcon
                {
                    RelativeSizeAxes = Axes.Both,
                    Icon = FontAwesome.Solid.Star,
                    Colour = HomeControlColours.Navy,
                },
                new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(size - 5),
                    Icon = FontAwesome.Solid.Star,
                    Colour = HomeControlColours.Yellow,
                },
            },
        };

    /// <summary>
    /// 换一句台词，文字淡入、气泡轻弹。
    /// </summary>
    public void SetText(LocalisableString text)
    {
        label.Text = text;
        label.FadeInFromZero(220);
        this.ScaleTo(1.07f, 90, Easing.Out)
            .Then().ScaleTo(1f, 340, Easing.OutBack);
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        this.MoveToOffset(new Vector2(0, 5), 1500, Easing.InOutSine)
            .Then().MoveToOffset(new Vector2(0, -5), 1500, Easing.InOutSine)
            .Loop();
        underline.ResizeWidthTo(underlinePulseWidth, 1100, Easing.InOutSine)
                 .Then().ResizeWidthTo(underlineRestWidth, 1100, Easing.InOutSine)
                 .Loop();
    }
}

public partial class HomeDotCross : CompositeDrawable
{
    public HomeDotCross()
    {
        Size = new Vector2(70);

        var grid = new Container
        {
            RelativeSizeAxes = Axes.Both,
        };

        for (int row = 0; row < 7; row++)
        {
            for (int column = 0; column < 7; column++)
            {
                if (row is not (2 or 3 or 4) && column is not (2 or 3 or 4))
                    continue;

                grid.Add(new Circle
                {
                    Position = new Vector2(column * 9, row * 9),
                    Size = new Vector2(4),
                    Colour = new Color4(HomeControlColours.Cyan.R, HomeControlColours.Cyan.G, HomeControlColours.Cyan.B, 0.28f),
                });
            }
        }

        InternalChild = grid;
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        InternalChild.FadeTo(0.55f, 2100, Easing.InOutSine)
                     .Then().FadeTo(1f, 2100, Easing.InOutSine)
                     .Loop();
    }
}

public partial class HomeConnectorPlus : CompositeDrawable
{
    public HomeConnectorPlus()
    {
        Size = new Vector2(20);
        Origin = Anchor.Centre;

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

    protected override void LoadComplete()
    {
        base.LoadComplete();

        this.ScaleTo(1.14f, 1800, Easing.InOutSine)
            .Then().ScaleTo(1f, 1800, Easing.InOutSine)
            .Loop();
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

    protected override void LoadComplete()
    {
        base.LoadComplete();

        // 周期各不相同的呼吸，错开后形成细碎的闪烁。
        for (int i = 0; i < InternalChildren.Count; i++)
        {
            float duration = 900 + (i * 17) % 7 * 130;
            InternalChildren[i].FadeTo(0.45f, duration, Easing.InOutSine)
                               .Then().FadeTo(1f, duration, Easing.InOutSine)
                               .Loop();
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
    private readonly Circle dot;

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
            dot = new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                Size = new Vector2(4),
                Colour = HomeControlColours.Cyan,
            },
        };
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        // 扫描点沿线往复，到端点隐去后回到起点。
        dot.MoveToX(DrawWidth, 1700, Easing.InOutSine)
           .Then().FadeOut(140).MoveToX(0).FadeIn(140)
           .Loop();
    }
}

/// <summary>
/// 键盘键帽样式的小标签，用于页脚键位提示。
/// 支持按下态（真实按键联动）与点击触发。
/// </summary>
public partial class HomeKeycap : ClickableContainer
{
    private readonly Container cap;
    private readonly Box background;
    private bool isPressed;

    public HomeKeycap(string label)
    {
        Size = new Vector2(26, 24);
        Action = flash;

        InternalChildren = new Drawable[]
        {
            // 键帽底座，按下时帽体下沉贴合它。
            new Container
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Size = new Vector2(26, 21),
                Masking = true,
                CornerRadius = 5,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.3f),
                },
            },
            cap = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Height = 21,
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 5,
                        BorderThickness = 1.5f,
                        BorderColour = new Color4(HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.55f),
                        Child = background = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.White,
                        },
                    },
                    new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = label,
                        Font = HomeTypography.Display(13),
                        Colour = HomeControlColours.Navy,
                    },
                },
            },
        };
    }

    public void SetPressed(bool pressed)
    {
        if (isPressed == pressed)
            return;

        isPressed = pressed;
        cap.MoveToY(pressed ? 3 : 0, 70, Easing.OutQuint);
        background.FadeColour(pressed ? HomeControlColours.Cyan : Color4.White, 70);
    }

    private void flash()
    {
        SetPressed(true);
        Scheduler.AddDelayed(() => SetPressed(false), 130);
    }
}

/// <summary>
/// 缓慢旋转的虚线圆环，衬在 mascot 背后。
/// </summary>
public partial class HomeDashedRing : CompositeDrawable
{
    public HomeDashedRing(float radius, int dashes = 26)
    {
        Size = new Vector2(radius * 2);
        Origin = Anchor.Centre;

        for (int i = 0; i < dashes; i++)
        {
            float angle = i / (float)dashes * MathF.PI * 2;
            AddInternal(new Box
            {
                Origin = Anchor.Centre,
                Position = new Vector2(radius + MathF.Cos(angle) * radius, radius + MathF.Sin(angle) * radius),
                Size = new Vector2(16, 3),
                Rotation = i / (float)dashes * 360 + 90,
                Colour = Color4.White,
            });
        }
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        this.RotateTo(0).RotateTo(360, 60000).Loop();
    }
}

/// <summary>
/// 青色舞台顶缘的刻度尺。
/// </summary>
public partial class HomeTickRuler : CompositeDrawable
{
    public HomeTickRuler(float width, float spacing = 24)
    {
        Width = width;
        Height = 12;

        int count = (int)(width / spacing);
        for (int i = 0; i <= count; i++)
        {
            bool major = i % 4 == 0;
            AddInternal(new Box
            {
                X = i * spacing,
                Width = 2,
                Height = major ? 11 : 6,
                Colour = Color4.White,
                Alpha = major ? 0.5f : 0.3f,
            });
        }
    }
}

/// <summary>
/// 套准十字标记，带缓慢呼吸。
/// </summary>
public partial class HomeCrosshairMark : CompositeDrawable
{
    private static readonly Color4 markColour = new(HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.42f);

    public HomeCrosshairMark()
    {
        Size = new Vector2(26);

        InternalChild = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(18),
                    Masking = true,
                    CornerRadius = 9,
                    BorderThickness = 1.5f,
                    BorderColour = markColour,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                    },
                },
                new Circle
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(3),
                    Colour = new Color4(HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.5f),
                },
                createTick(Anchor.TopCentre),
                createTick(Anchor.BottomCentre),
                createTick(Anchor.CentreLeft),
                createTick(Anchor.CentreRight),
            },
        };
    }

    private static Drawable createTick(Anchor anchor)
    {
        bool horizontal = anchor is Anchor.CentreLeft or Anchor.CentreRight;
        return new Box
        {
            Anchor = anchor,
            Origin = anchor,
            Position = horizontal ? new Vector2(anchor == Anchor.CentreLeft ? 1 : -1, 0) : new Vector2(0, anchor == Anchor.TopCentre ? 1 : -1),
            Size = horizontal ? new Vector2(5, 1.5f) : new Vector2(1.5f, 5),
            Colour = markColour,
        };
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        InternalChild.FadeTo(0.55f, 1900, Easing.InOutSine)
                     .Then().FadeTo(1f, 1900, Easing.InOutSine)
                     .Loop();
    }
}

/// <summary>
/// 描边圆环装饰，带呼吸缩放。Position 视为圆心。
/// </summary>
public partial class HomeRing : CompositeDrawable
{
    public HomeRing(float size, float thickness, Color4 colour)
    {
        Size = new Vector2(size);
        Origin = Anchor.Centre;

        InternalChild = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Masking = true,
            CornerRadius = size / 2f,
            BorderThickness = thickness,
            BorderColour = colour,
            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
            },
        };
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        this.ScaleTo(1.14f, 1700, Easing.InOutSine)
            .Then().ScaleTo(1f, 1700, Easing.InOutSine)
            .Loop();
    }
}

/// <summary>
/// 四点星光，周期性弹出收回。Position 视为中心。
/// </summary>
public partial class HomeTwinkle : CompositeDrawable
{
    private readonly double loopPause;

    public HomeTwinkle(float size = 14, double loopPause = 1700)
    {
        this.loopPause = loopPause;

        Size = new Vector2(size);
        Origin = Anchor.Centre;
        Scale = Vector2.Zero;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(size, size * 0.22f),
                Colour = Color4.White,
            },
            new Box
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(size * 0.22f, size),
                Colour = Color4.White,
            },
        };
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        this.ScaleTo(1f, 480, Easing.OutBack)
            .Then().ScaleTo(0f, 380, Easing.InBack)
            .Loop(loopPause);
    }
}

/// <summary>
/// 印刷标签风格的条形码与编号。
/// </summary>
public partial class HomeBarcode : CompositeDrawable
{
    private static readonly int[] barWidths = { 2, 1, 3, 1, 1, 2, 1, 4, 1, 2, 2, 1, 3, 1, 2, 1, 1, 3, 2, 1, 2, 2 };

    public HomeBarcode(string label)
    {
        AutoSizeAxes = Axes.Both;

        var bars = new FillFlowContainer
        {
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(2, 0),
        };

        foreach (int width in barWidths)
        {
            bars.Add(new Box
            {
                Width = width,
                Height = 24,
                Colour = new Color4(HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.72f),
            });
        }

        InternalChildren = new Drawable[]
        {
            bars,
            new SpriteText
            {
                Y = 29,
                Text = label,
                Font = HomeTypography.Display(10),
                Spacing = new Vector2(1.6f, 0),
                Colour = new Color4(HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.5f),
            },
        };
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        this.FadeTo(0.7f, 2400, Easing.InOutSine)
            .Then().FadeTo(1f, 2400, Easing.InOutSine)
            .Loop();
    }
}
