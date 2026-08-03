using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Importing;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.ChartLibrary;

internal enum ChartLibrarySourceFilter
{
    All,
    Managed,
    ExternalOsu,
}

internal partial class ChartLibraryActionButton : ClickableContainer
{
    private readonly Container hoverContent;
    private readonly Box background;
    private readonly SpriteIcon icon;
    private readonly SpriteText label;
    private readonly Color4 accent;
    private readonly bool primary;
    private bool enabled = true;

    public ChartLibraryActionButton(
        LocalisableString text,
        IconUsage iconUsage,
        Action action,
        float width,
        bool primary = false,
        Color4? accent = null)
    {
        Action = action;
        this.primary = primary;
        this.accent = accent ?? HomeControlColours.Cyan;
        Size = new Vector2(width, 48);

        InternalChild = hoverContent = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new Container
                {
                    Position = new Vector2(0, 4),
                    Size = new Vector2(width, 44),
                    Masking = true,
                    CornerRadius = 8,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(0.015f, 0.045f, 0.28f, 0.22f),
                    },
                },
                new Container
                {
                    Size = new Vector2(width, 44),
                    Masking = true,
                    CornerRadius = 8,
                    BorderThickness = 1.5f,
                    BorderColour = HomeControlColours.Navy,
                    Children = new Drawable[]
                    {
                        background = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = primary ? HomeControlColours.Navy : Color4.White,
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 3,
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Colour = this.accent,
                            Alpha = primary ? 1 : 0.72f,
                        },
                    },
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Y = -2,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(9, 0),
                    Children = new Drawable[]
                    {
                        icon = new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Size = new Vector2(17),
                            Icon = iconUsage,
                            Colour = primary ? Color4.White : HomeControlColours.Navy,
                        },
                        label = new SpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = text,
                            Font = HomeTypography.Control(16),
                            Colour = primary ? Color4.White : HomeControlColours.Navy,
                        },
                    },
                },
                new Box
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.Centre,
                    Size = new Vector2(10),
                    Rotation = 45,
                    Colour = HomeControlColours.Yellow,
                },
            },
        };
    }

    internal void SetEnabled(bool value)
    {
        enabled = value;
        Alpha = value ? 1 : 0.46f;
    }

    internal void SetText(LocalisableString text) => label.Text = text;

    protected override bool OnClick(ClickEvent e)
    {
        if (!enabled)
            return true;

        return base.OnClick(e);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (enabled)
        {
            background.FadeColour(
                primary ? new Color4(0.055f, 0.14f, 0.66f, 1) : HomeControlColours.PaleCyan,
                120,
                Easing.OutQuint);
            icon.ScaleTo(1.1f, 120, Easing.OutQuint);
            hoverContent.MoveToY(-2, 120, Easing.OutQuint);
        }

        return base.OnHover(e);
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(primary ? HomeControlColours.Navy : Color4.White, 150, Easing.OutQuint);
        icon.ScaleTo(1, 150, Easing.OutQuint);
        hoverContent.MoveToY(0, 150, Easing.OutQuint);
        base.OnHoverLost(e);
    }
}

internal partial class ChartLibraryFilterChip : ClickableContainer
{
    private readonly Box background;
    private readonly SpriteText label;
    private bool selected;

    public ChartLibraryFilterChip(LocalisableString text, Action action, float width)
    {
        Action = action;
        Size = new Vector2(width, 34);
        Masking = true;
        CornerRadius = 8;
        BorderThickness = 1.2f;
        BorderColour = new Color4(
            HomeControlColours.Navy.R,
            HomeControlColours.Navy.G,
            HomeControlColours.Navy.B,
            0.48f);
        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            label = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = text,
                Font = HomeTypography.Display(12),
                Colour = HomeControlColours.Navy,
            },
        };
    }

    internal void SetSelected(bool value)
    {
        selected = value;
        background.Colour = value ? HomeControlColours.Navy : Color4.White;
        label.Colour = value ? Color4.White : HomeControlColours.Navy;
        BorderColour = value ? HomeControlColours.Cyan : new Color4(
            HomeControlColours.Navy.R,
            HomeControlColours.Navy.G,
            HomeControlColours.Navy.B,
            0.48f);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!selected)
            background.FadeColour(HomeControlColours.PaleCyan, 100, Easing.OutQuint);

        return base.OnHover(e);
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        if (!selected)
            background.FadeColour(Color4.White, 120, Easing.OutQuint);

        base.OnHoverLost(e);
    }
}

internal partial class ChartLibrarySearchBox : BasicTextBox
{
    private readonly Action<string> queryChanged;

    protected override float LeftRightPadding => 48;

    public ChartLibrarySearchBox(Action<string> queryChanged)
    {
        this.queryChanged = queryChanged;
        Size = new Vector2(318, 40);
        Masking = true;
        CornerRadius = 8;
        BorderThickness = 1.4f;
        BorderColour = new Color4(
            HomeControlColours.Cyan.R,
            HomeControlColours.Cyan.G,
            HomeControlColours.Cyan.B,
            0.72f);
        BackgroundUnfocused = Color4.White;
        BackgroundFocused = HomeControlColours.PaleCyan;
        FontSize = 15;
        PlaceholderText = YokkoStrings.Get("chart_library.search");

        AddInternal(new Container
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            X = 7,
            Size = new Vector2(28),
            Depth = -2,
            Masking = true,
            CornerRadius = 5,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        0.18f),
                },
                new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(14),
                    Icon = FontAwesome.Solid.Search,
                    Colour = HomeControlColours.Navy,
                },
            },
        });

        Current.ValueChanged += onValueChanged;
    }

    private void onValueChanged(ValueChangedEvent<string> change) =>
        queryChanged(change.NewValue);

    protected override Drawable GetDrawableCharacter(char c) => new SpriteText
    {
        Text = c.ToString(),
        Font = HomeTypography.Body(15),
        Colour = HomeControlColours.Navy,
    };

    protected override SpriteText CreatePlaceholder() => new()
    {
        Font = HomeTypography.Body(15),
        Colour = new Color4(
            HomeControlColours.Navy.R,
            HomeControlColours.Navy.G,
            HomeControlColours.Navy.B,
            0.5f),
    };
}

internal partial class ChartLibraryStatCard : CompositeDrawable
{
    private readonly SpriteText valueText;

    public ChartLibraryStatCard(
        LocalisableString label,
        IconUsage icon,
        Color4 accent,
        float width)
    {
        Size = new Vector2(width, 78);
        Masking = true;
        CornerRadius = 9;
        BorderThickness = 1.4f;
        BorderColour = HomeControlColours.Navy;
        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new Box
            {
                Width = 7,
                RelativeSizeAxes = Axes.Y,
                Colour = accent,
            },
            new SpriteIcon
            {
                Position = new Vector2(21, 19),
                Size = new Vector2(20),
                Icon = icon,
                Colour = HomeControlColours.Navy,
            },
            valueText = new SpriteText
            {
                Position = new Vector2(54, 12),
                Text = "0",
                Font = HomeTypography.Display(28),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(54, 48),
                Text = label,
                Font = HomeTypography.Body(12),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.72f),
            },
        };
    }

    internal void SetValue(int value) => valueText.Text = value.ToString("N0");
}

internal partial class ChartLibraryChartRow : CompositeDrawable
{
    private readonly ChartLibraryActionButton removeButton;
    private readonly Action removeAction;
    private bool removalArmed;
    private int removalGeneration;

    public ChartLibraryChartRow(ImportedChart chart, Action removeAction)
    {
        this.removeAction = removeAction;
        RelativeSizeAxes = Axes.X;
        Height = 76;
        Masking = true;
        CornerRadius = 8;
        BorderThickness = 1.2f;
        BorderColour = new Color4(
            HomeControlColours.Navy.R,
            HomeControlColours.Navy.G,
            HomeControlColours.Navy.B,
            0.23f);

        Color4 sourceAccent = chart.SourceKind == ImportedChartSourceKind.ExternalOsu
            ? HomeControlColours.Pink
            : HomeControlColours.Cyan;
        LocalisableString title = string.IsNullOrWhiteSpace(chart.Result.Beatmap.Title)
            ? YokkoStrings.Get("chart_library.untitled")
            : chart.Result.Beatmap.Title;
        string subtitle = $"{chart.Result.Beatmap.Artist}  //  {chart.Result.Beatmap.DifficultyName}";
        string detail = $"{(int)chart.Result.Beatmap.KeyMode}K";

        if (chart.StarRating.Value.HasValue)
            detail += $"   SR {chart.StarRating.Value.Value:0.00}";

        if (chart.Bpm.HasValue)
            detail += $"   {chart.Bpm.Value:0} BPM";

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new Box
            {
                Width = 5,
                RelativeSizeAxes = Axes.Y,
                Colour = sourceAccent,
            },
            new Container
            {
                Position = new Vector2(16, 12),
                Size = new Vector2(52),
                Masking = true,
                CornerRadius = 8,
                BorderThickness = 1.3f,
                BorderColour = HomeControlColours.Navy,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(sourceAccent.R, sourceAccent.G, sourceAccent.B, 0.2f),
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(22),
                        Icon = chart.IsPackage ? FontAwesome.Solid.LayerGroup : FontAwesome.Solid.Music,
                        Colour = HomeControlColours.Navy,
                    },
                },
            },
            new SpriteText
            {
                Position = new Vector2(82, 11),
                Text = title,
                Font = HomeTypography.Display(18),
                Colour = HomeControlColours.Navy,
                MaxWidth = 420,
                Truncate = true,
            },
            new SpriteText
            {
                Position = new Vector2(82, 37),
                Text = subtitle,
                Font = HomeTypography.Body(13),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.68f),
                MaxWidth = 440,
                Truncate = true,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -150,
                Y = -9,
                Text = detail,
                Font = HomeTypography.Display(12),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -150,
                Y = 13,
                Text = chart.SourceKind == ImportedChartSourceKind.ExternalOsu
                    ? YokkoStrings.Get("chart_library.source_external")
                    : chart.IsPackage
                        ? YokkoStrings.Get("chart_library.source_package")
                        : YokkoStrings.Get("chart_library.source_managed"),
                Font = HomeTypography.Body(11),
                Colour = sourceAccent,
            },
            removeButton = new ChartLibraryActionButton(
                chart.IsReadOnly
                    ? YokkoStrings.Get("chart_library.read_only")
                    : YokkoStrings.Get("chart_library.remove"),
                chart.IsReadOnly ? FontAwesome.Solid.Lock : FontAwesome.Solid.Trash,
                beginRemove,
                116,
                accent: HomeControlColours.Pink)
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -14,
                Scale = new Vector2(0.78f),
            },
        };

        removeButton.SetEnabled(!chart.IsReadOnly);
    }

    private void beginRemove()
    {
        if (!removalArmed)
        {
            removalArmed = true;
            int generation = ++removalGeneration;
            removeButton.SetText(YokkoStrings.Get("chart_library.remove_confirm"));
            Scheduler.AddDelayed(() =>
            {
                if (generation != removalGeneration)
                    return;

                removalArmed = false;
                removeButton.SetText(YokkoStrings.Get("chart_library.remove"));
            }, 3500);
            return;
        }

        removalGeneration++;
        removalArmed = false;
        removeButton.SetEnabled(false);
        removeAction();
    }
}

internal partial class ChartLibraryLoadMoreButton : ClickableContainer
{
    private readonly Box background;

    public ChartLibraryLoadMoreButton(LocalisableString text, Action action)
    {
        Action = action;
        RelativeSizeAxes = Axes.X;
        Height = 44;
        Masking = true;
        CornerRadius = 8;
        BorderThickness = 1.2f;
        BorderColour = HomeControlColours.Cyan;
        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(
                    HomeControlColours.PaleCyan.R,
                    HomeControlColours.PaleCyan.G,
                    HomeControlColours.PaleCyan.B,
                    0.38f),
            },
            new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = text,
                Font = HomeTypography.Control(14),
                Colour = HomeControlColours.Navy,
            },
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(HomeControlColours.PaleCyan, 100, Easing.OutQuint);
        return base.OnHover(e);
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(new Color4(
            HomeControlColours.PaleCyan.R,
            HomeControlColours.PaleCyan.G,
            HomeControlColours.PaleCyan.B,
            0.38f), 120, Easing.OutQuint);
        base.OnHoverLost(e);
    }
}

internal partial class ChartLibraryScrollContainer : ScrollContainer<Drawable>
{
    public ChartLibraryScrollContainer()
        : base(Direction.Vertical)
    {
        ScrollbarOverlapsContent = true;
        ClampExtension = 0;
    }

    protected override ScrollbarContainer CreateScrollbar(Direction direction) =>
        new ChartLibraryScrollbar(direction);

    private partial class ChartLibraryScrollbar : ScrollbarContainer
    {
        private const float thickness = 5;

        public ChartLibraryScrollbar(Direction direction)
            : base(direction)
        {
            Alpha = 0.62f;
            Colour = HomeControlColours.Cyan;
            CornerRadius = thickness / 2;
            Masking = true;
            Margin = new MarginPadding(3);
            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
            };
            ResizeTo(1);
        }

        public override void ResizeTo(
            float value,
            int duration = 0,
            Easing easing = Easing.None)
        {
            var size = new Vector2(thickness)
            {
                [(int)ScrollDirection] = value,
            };
            this.ResizeTo(size, duration, easing);
        }

        protected override bool OnHover(HoverEvent e)
        {
            this.FadeTo(1, 100, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e) =>
            this.FadeTo(0.62f, 120, Easing.OutQuint);
    }
}
