using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

/// <summary>
/// Shared visual building blocks for settings panels. The layered "sticker"
/// treatment (offset cyan underlay, navy outline, inner hairline) mirrors the
/// home screen cards in Yokko.Game/Screens/Main/HomeControls.cs so settings no
/// longer looks like a flat wireframe next to the main menu.
/// </summary>
internal static partial class SettingsChrome
{
    public const float ContentX = 378;
    public const float ContentWidth = 840;
    public const float ControlWidth = 598;
    public const float ControlHeight = 54;

    /// <summary>
    /// Panel header: eyebrow index, cyan tick, display title, muted subtitle,
    /// pink underline with detail dots, and a gently rocking page-icon tile,
    /// echoing the home hero typography.
    /// </summary>
    public static Drawable CreateHeader(
        LocalisableString title,
        LocalisableString subtitle,
        IconUsage icon,
        int index)
    {
        var textColumn = new Container
        {
            AutoSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new SpriteText
                {
                    Position = new Vector2(2, -20),
                    Text = $"{index:00} // SETTINGS",
                    Font = HomeTypography.Display(14),
                    Spacing = new Vector2(2.2f, 0),
                    Colour = HomeControlColours.Cyan,
                },
                new Box
                {
                    Position = new Vector2(-12, 6),
                    Size = new Vector2(5, 54),
                    Colour = HomeControlColours.Cyan,
                },
                new SpriteText
                {
                    Text = title,
                    Font = HomeTypography.Display(58),
                    Spacing = new Vector2(0.45f, 0),
                    Colour = HomeControlColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(0, 63),
                    Text = subtitle,
                    Font = HomeTypography.Body(20),
                    Spacing = new Vector2(0.2f, 0),
                    Colour = SettingsTheme.MutedNavy,
                },
                new Box
                {
                    Position = new Vector2(2, 93),
                    Size = new Vector2(46, 3),
                    Colour = HomeControlColours.Pink,
                },
                new FillFlowContainer
                {
                    Position = new Vector2(58, 94),
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(5, 0),
                    Children = new[]
                    {
                        createDetailDot(),
                        createDetailDot(),
                        createDetailDot(),
                    },
                },
            },
        };

        return new FillFlowContainer
        {
            Position = new Vector2(ContentX, 42),
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(30, 0),
            Children = new Drawable[]
            {
                textColumn,
                new HeaderIconTile(icon)
                {
                    Margin = new MarginPadding { Top = 6 },
                },
            },
        };
    }

    /// <summary>
    /// Grouped choice buttons wrapped in a sticker card so the control reads
    /// as one physical object, like the home action tiles.
    /// </summary>
    public static Container CreateSegmentedControl(IEnumerable<Drawable> buttons)
    {
        var card = new SettingsStickerCard(new Vector2(ControlWidth, ControlHeight), 8);
        card.SetContent(new FillFlowContainer
        {
            RelativeSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Children = buttons.ToArray(),
        });
        return card;
    }

    /// <summary>
    /// Highlight banner summarising the current state of a settings area
    /// (e.g. active display, active language).
    /// </summary>
    public static Container CreateStatusCard(
        float y,
        IconUsage icon,
        LocalisableString title,
        IconUsage trailingIcon,
        out SpriteText metadata)
    {
        var trailing = new SpriteIcon
        {
            Anchor = Anchor.CentreRight,
            Origin = Anchor.CentreRight,
            X = -34,
            Size = new Vector2(44),
            Icon = trailingIcon,
            Colour = Color4.White,
        };

        var card = new SettingsStickerCard(new Vector2(ContentWidth, 86), 10, SettingsTheme.StatusCyan)
        {
            Position = new Vector2(ContentX, y),
        };

        SpriteText metadataText;
        card.SetContent(
            new HomeDotField
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Position = new Vector2(-108, 0),
                Size = new Vector2(130, 66),
                Colour = new Color4(1f, 1f, 1f, 0.35f),
            },
            new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 48,
                Size = new Vector2(56),
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 48,
                Size = new Vector2(26),
                Icon = icon,
                Colour = HomeControlColours.Navy,
            },
            new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 122,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = title,
                        Font = HomeTypography.Display(22),
                        Colour = HomeControlColours.Navy,
                    },
                    metadataText = new SpriteText
                    {
                        Font = HomeTypography.Body(18),
                        Colour = HomeControlColours.Navy,
                    },
                },
            },
            trailing,
            new Box
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Size = new Vector2(14),
                Rotation = 45,
                Colour = HomeControlColours.Yellow,
            });

        metadata = metadataText;
        return new StatusCardPulse(card, trailing);
    }

    /// <summary>
    /// Wraps a flat status/summary card in the sticker frame (offset shadow,
    /// cyan underlay, navy outline) so legacy cards match the new chrome
    /// without rewriting their contents.
    /// </summary>
    public static Container CreateStickerFrame(Container flatCard)
    {
        var frame = new SettingsStickerCard(flatCard.Size, 9, SettingsTheme.StatusCyan)
        {
            Position = flatCard.Position,
        };
        flatCard.Position = Vector2.Zero;
        frame.SetContent(flatCard);
        return frame;
    }

    public static Container CreateSettingRow(float y, LocalisableString title, Drawable control, float depth = 0) => new Container
    {
        Position = new Vector2(ContentX, y),
        Size = new Vector2(ContentWidth, 60),
        Depth = depth,
        Children = new Drawable[]
        {
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = title,
                Font = HomeTypography.Display(25),
                Colour = HomeControlColours.Navy,
            },
            new Container
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Size = new Vector2(ControlWidth, ControlHeight),
                Child = control,
            },
        },
    };

    public static Drawable CreateDivider(float y) => new Box
    {
        Position = new Vector2(ContentX, y),
        Width = ContentWidth,
        Height = 1,
        Colour = SettingsTheme.Divider,
    };

    private static Drawable createDetailDot() => new Circle
    {
        Size = new Vector2(2.5f),
        Colour = HomeControlColours.Cyan,
        Alpha = 0.85f,
    };

    /// <summary>
    /// Keeps the trailing status icon gently breathing so the banner feels alive.
    /// </summary>
    private sealed partial class StatusCardPulse : Container
    {
        private readonly SpriteIcon trailing;

        public StatusCardPulse(Container card, SpriteIcon trailing)
        {
            this.trailing = trailing;
            AutoSizeAxes = Axes.Both;
            Position = card.Position;
            card.Position = Vector2.Zero;
            InternalChild = card;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            trailing.ScaleTo(1.12f, 1100, Easing.InOutSine)
                    .Then().ScaleTo(1f, 1100, Easing.InOutSine)
                    .Loop();
        }
    }

    /// <summary>
    /// 页头图标砖：贴纸卡片里放页面图标，缓慢摇摆并带黄色角标。
    /// </summary>
    private sealed partial class HeaderIconTile : CompositeDrawable
    {
        private readonly Container tile;

        public HeaderIconTile(IconUsage icon)
        {
            Size = new Vector2(60);

            InternalChildren = new Drawable[]
            {
                new Container
                {
                    Position = new Vector2(0, 4),
                    Size = new Vector2(56),
                    Masking = true,
                    CornerRadius = 11,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(0.015f, 0.045f, 0.28f, 0.2f),
                    },
                },
                new Container
                {
                    Position = new Vector2(-1.5f, -1.5f),
                    Size = new Vector2(59),
                    Masking = true,
                    CornerRadius = 11,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(
                            HomeControlColours.Cyan.R,
                            HomeControlColours.Cyan.G,
                            HomeControlColours.Cyan.B,
                            0.45f),
                    },
                },
                tile = new Container
                {
                    Origin = Anchor.Centre,
                    Position = new Vector2(28, 28),
                    Size = new Vector2(56),
                    Masking = true,
                    CornerRadius = 10,
                    BorderThickness = 1.6f,
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
                            Size = new Vector2(24),
                            Icon = icon,
                            Colour = HomeControlColours.Navy,
                        },
                    },
                },
                new Box
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.Centre,
                    Size = new Vector2(13),
                    Rotation = 45,
                    Colour = HomeControlColours.Yellow,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            tile.RotateTo(-4).RotateTo(4, 1900, Easing.InOutSine)
                .Then().RotateTo(-4, 1900, Easing.InOutSine)
                .Loop();
        }
    }
}

/// <summary>
/// Layered card matching the home screen sticker treatment: an offset navy
/// shadow, a cyan underlay peeking from behind, a navy outline and an inner
/// hairline. Content added as children lands inside the bordered body.
/// </summary>
internal partial class SettingsStickerCard : Container
{
    private readonly Container body;
    private readonly Box background;

    public Box Background => background;

    public SettingsStickerCard(
        Vector2 size,
        float cornerRadius,
        Color4? bodyColour = null,
        float borderThickness = 1.6f)
    {
        Size = size;

        InternalChildren = new Drawable[]
        {
            new Container
            {
                Position = new Vector2(0, 4),
                Size = size,
                Masking = true,
                CornerRadius = cornerRadius + 1,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.015f, 0.045f, 0.28f, 0.2f),
                },
            },
            new Container
            {
                Position = new Vector2(-1.5f, -1.5f),
                Size = size + new Vector2(3),
                Masking = true,
                CornerRadius = cornerRadius + 1,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        0.4f),
                },
            },
            body = new Container
            {
                Size = size,
                Masking = true,
                CornerRadius = cornerRadius,
                BorderThickness = borderThickness,
                BorderColour = HomeControlColours.Navy,
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = bodyColour ?? Color4.White,
                    },
                    new Container
                    {
                        Position = new Vector2(4),
                        Size = size - new Vector2(8),
                        Masking = true,
                        CornerRadius = cornerRadius - 2,
                        BorderThickness = 1,
                        BorderColour = new Color4(
                            HomeControlColours.Cyan.R,
                            HomeControlColours.Cyan.G,
                            HomeControlColours.Cyan.B,
                            0.3f),
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            // 子节点 Alpha 为 0 会被剔除并连带描边消失，给趋近 0 的值保住描边。
                            Alpha = 0.01f,
                        },
                    },
                },
            },
        };
    }

    /// <summary>
    /// Replaces the content inside the bordered body. The sticker layers
    /// themselves live in <see cref="CompositeDrawable.InternalChildren"/>.
    /// </summary>
    public void SetContent(params Drawable[] content) => body.Children = content;

    public void SetBorderColour(Color4 colour, double duration = 0, Easing easing = Easing.None)
    {
        if (duration <= 0)
            body.BorderColour = colour;
        else
        {
            body.TransformTo(
                nameof(body.BorderColour),
                colour,
                duration,
                easing);
        }
    }

    public void SetBorderThickness(float thickness) => body.BorderThickness = thickness;
}
