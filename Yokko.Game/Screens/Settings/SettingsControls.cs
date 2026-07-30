using System;
using System.Collections.Generic;
using System.Drawing;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

internal partial class SettingsPanelFooter : CompositeDrawable
{
    private readonly Container escChip;
    private readonly Box escChipBackground;

    public SettingsPanelFooter()
        : this(YokkoStrings.Get("settings.changes_apply_instantly"))
    {
    }

    public SettingsPanelFooter(LocalisableString message)
    {
        Anchor = Anchor.BottomLeft;
        Origin = Anchor.BottomLeft;
        Position = new Vector2(372, -27);
        Size = new Vector2(840, 42);

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Colour = SettingsTheme.Divider,
            },
            new FillFlowContainer
            {
                Position = new Vector2(2, 14),
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(12, 0),
                Children = new Drawable[]
                {
                    new SpriteIcon
                    {
                        Y = 1,
                        Size = new Vector2(22),
                        Icon = FontAwesome.Solid.CheckSquare,
                        Colour = HomeControlColours.Pink,
                    },
                    new SpriteText
                    {
                        Y = 1,
                        Text = message,
                        Font = HomeTypography.Body(17),
                        Colour = HomeControlColours.Navy,
                    },
                    new Box
                    {
                        Width = 1,
                        Height = 22,
                        Margin = new MarginPadding { Horizontal = 6 },
                        Colour = SettingsTheme.Divider,
                    },
                    escChip = new Container
                    {
                        Size = new Vector2(30, 24),
                        Masking = true,
                        CornerRadius = 4,
                        Children = new Drawable[]
                        {
                            escChipBackground = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = HomeControlColours.Navy,
                            },
                            new SpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Text = "Esc",
                                Font = HomeTypography.Body(14),
                                Colour = Color4.White,
                            },
                        },
                    },
                    new SpriteText
                    {
                        Y = 1,
                        Text = YokkoStrings.Get("settings.esc_to_return"),
                        Font = HomeTypography.Body(17),
                        Colour = HomeControlColours.Navy,
                    },
                },
            },
        };
    }

    /// <summary>
    /// 按下 Esc 时键帽下压闪粉，提示这个键真的能用。
    /// </summary>
    public void FlashEsc()
    {
        escChip.ScaleTo(0.85f, 70, Easing.OutQuint)
               .Then().ScaleTo(1f, 200, Easing.OutBack);
        escChipBackground.FlashColour(HomeControlColours.Pink, 320, Easing.OutQuint);
    }
}

internal partial class SettingsSegmentedChoiceButton : ClickableContainer
{
    private readonly Box background;
    private readonly Box focusLine;
    private readonly Box hoverUnderline;
    private readonly Container content;
    private readonly SpriteIcon icon;
    private readonly SpriteText text;
    private readonly SpriteIcon check;
    private bool selected;

    public object Value { get; init; }
    public override bool AcceptsFocus => true;

    public SettingsSegmentedChoiceButton(LocalisableString label, IconUsage itemIcon, Action action, float width)
    {
        Action = action;
        Size = new Vector2(width, 54);

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new Box
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Width = 1,
                RelativeSizeAxes = Axes.Y,
                Colour = SettingsTheme.Divider,
            },
            content = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    icon = new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 28,
                        Size = new Vector2(16),
                        Icon = itemIcon,
                        Colour = HomeControlColours.Navy,
                    },
                    text = new SpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 59,
                        Text = label,
                        Font = HomeTypography.Body(18),
                        Colour = HomeControlColours.Navy,
                    },
                    check = new SpriteIcon
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -22,
                        Size = new Vector2(17),
                        Icon = FontAwesome.Solid.Check,
                        Colour = HomeControlColours.Yellow,
                        Alpha = 0,
                    },
                },
            },
            hoverUnderline = new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                X = 14,
                Width = 0,
                Height = 2,
                Colour = HomeControlColours.Cyan,
                Alpha = 0,
            },
            focusLine = new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                RelativeSizeAxes = Axes.X,
                Height = 3,
                Colour = HomeControlColours.Pink,
                Alpha = 0,
            },
        };
    }

    public void SetSelected(bool isSelected)
    {
        bool becameSelected = isSelected && !selected;
        selected = isSelected;
        background.FadeColour(selected ? HomeControlColours.Navy : Color4.White, 120, Easing.OutQuint);
        icon.FadeColour(selected ? Color4.White : HomeControlColours.Navy, 120, Easing.OutQuint);
        text.FadeColour(selected ? Color4.White : HomeControlColours.Navy, 120, Easing.OutQuint);
        check.FadeTo(selected ? 1 : 0, 120, Easing.OutQuint);

        if (becameSelected)
        {
            check.ScaleTo(0.4f).ScaleTo(1f, 220, Easing.OutBack);
            content.MoveToX(4).MoveToX(0, 200, Easing.OutQuint);
        }
    }

    protected override bool OnHover(HoverEvent e)
    {
        hoverUnderline.FadeIn(120, Easing.OutQuint)
                      .ResizeWidthTo(90, 160, Easing.OutQuint);

        if (!selected)
        {
            background.FadeColour(SettingsTheme.PaleCyan, 120, Easing.OutQuint);
            icon.MoveToX(32, 120, Easing.OutQuint)
                .RotateTo(-10, 140, Easing.OutQuint);
            text.MoveToX(63, 140, Easing.OutQuint);
        }

        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        hoverUnderline.FadeOut(140, Easing.OutQuint)
                      .ResizeWidthTo(0, 140, Easing.OutQuint);

        if (!selected)
        {
            background.FadeColour(Color4.White, 140, Easing.OutQuint);
        }

        icon.MoveToX(28, 150, Easing.OutQuint)
            .RotateTo(0, 180, Easing.OutQuint);
        text.MoveToX(59, 160, Easing.OutQuint);
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        content.ScaleTo(0.94f, 400, Easing.OutQuint);
        return base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseUpEvent e)
    {
        content.ScaleTo(1f, 220, Easing.OutQuint);
        base.OnMouseUp(e);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            Action?.Invoke();
            return true;
        }

        return base.OnKeyDown(e);
    }

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        focusLine.FadeIn(100, Easing.OutQuint);
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        focusLine.FadeOut(100, Easing.OutQuint);
    }
}

internal partial class SettingsFrameLimitChoiceButton : ClickableContainer
{
    private readonly Box background;
    private readonly Box focusLine;
    private readonly Box hoverUnderline;
    private readonly Container content;
    private readonly SpriteText modeText;
    private readonly SpriteText rateText;
    private bool selected;

    public YokkoFrameLimit Value { get; }
    public override bool AcceptsFocus => true;

    public SettingsFrameLimitChoiceButton(YokkoFrameLimit value, Action action, float width)
    {
        Value = value;
        Action = action;
        Size = new Vector2(width, 54);

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new Box
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Width = 1,
                RelativeSizeAxes = Axes.Y,
                Colour = SettingsTheme.Divider,
            },
            content = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    modeText = new SpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Y = 8,
                        Font = HomeTypography.Display(13)
                            .With(fixedWidth: true),
                        Colour = HomeControlColours.Navy,
                    },
                    rateText = new SpriteText
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Y = -8,
                        Font = HomeTypography.Body(11)
                            .With(fixedWidth: true),
                        Colour = HomeControlColours.Navy,
                    },
                },
            },
            hoverUnderline = new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                X = 12,
                Width = 0,
                Height = 2,
                Colour = HomeControlColours.Cyan,
                Alpha = 0,
            },
            focusLine = new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                RelativeSizeAxes = Axes.X,
                Height = 3,
                Colour = HomeControlColours.Pink,
                Alpha = 0,
            },
        };
    }

    public void SetLabels(string mode, string rate)
    {
        modeText.Text = mode;
        rateText.Text = rate;
    }

    public void SetSelected(bool isSelected)
    {
        bool becameSelected = isSelected && !selected;
        selected = isSelected;
        background.FadeColour(selected ? HomeControlColours.Navy : Color4.White, 120, Easing.OutQuint);
        modeText.FadeColour(selected ? Color4.White : HomeControlColours.Navy, 120, Easing.OutQuint);
        rateText.FadeColour(selected ? HomeControlColours.Cyan : SettingsTheme.MutedNavy, 120, Easing.OutQuint);

        if (becameSelected)
            content.ScaleTo(0.9f).ScaleTo(1f, 220, Easing.OutBack);
    }

    protected override bool OnHover(HoverEvent e)
    {
        hoverUnderline.FadeIn(120, Easing.OutQuint)
                      .ResizeWidthTo(60, 160, Easing.OutQuint);

        if (!selected)
        {
            background.FadeColour(SettingsTheme.PaleCyan, 120, Easing.OutQuint);
            content.MoveToY(-2, 130, Easing.OutQuint);
        }

        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        hoverUnderline.FadeOut(140, Easing.OutQuint)
                      .ResizeWidthTo(0, 140, Easing.OutQuint);

        if (!selected)
            background.FadeColour(Color4.White, 140, Easing.OutQuint);

        content.MoveToY(0, 160, Easing.OutQuint);
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        content.ScaleTo(0.92f, 400, Easing.OutQuint);
        return base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseUpEvent e)
    {
        content.ScaleTo(1f, 220, Easing.OutQuint);
        base.OnMouseUp(e);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            Action?.Invoke();
            return true;
        }

        return base.OnKeyDown(e);
    }

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        focusLine.FadeIn(100, Easing.OutQuint);
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        focusLine.FadeOut(100, Easing.OutQuint);
    }
}

/// <summary>
/// Resolution selector with an explicit option list. It deliberately does not
/// cycle values so the interaction remains predictable as the list grows.
/// </summary>
internal partial class SettingsResolutionDropdown : CompositeDrawable
{
    private readonly IReadOnlyList<Size> options;
    private readonly Action<Size> onSelected;
    private readonly Box headerBackground;
    private readonly SpriteText valueText;
    private readonly SpriteIcon chevron;
    private readonly Container menu;
    private readonly List<SettingsResolutionOption> optionRows = new();
    private bool open;
    private bool enabled = true;

    internal bool IsOpen => open;
    internal bool IsEnabled => enabled;
    internal Size SelectedSize { get; private set; }

    public SettingsResolutionDropdown(IReadOnlyList<Size> options, Action<Size> onSelected)
    {
        this.options = options;
        this.onSelected = onSelected;
        Size = new Vector2(598, 54);

        Box focusLine;
        var header = new SettingsDropdownHeader(
            () => open,
            Toggle,
            () => enabled)
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                headerBackground = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
                valueText = new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 18,
                    Font = HomeTypography.Body(19),
                    Colour = HomeControlColours.Navy,
                },
                chevron = new SpriteIcon
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -20,
                    Size = new Vector2(15),
                    Icon = FontAwesome.Solid.ChevronDown,
                    Colour = HomeControlColours.Pink,
                },
                focusLine = new Box
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.X,
                    Height = 3,
                    Colour = HomeControlColours.Pink,
                    Alpha = 0,
                },
            },
        };

        header.Background = headerBackground;
        header.FocusLine = focusLine;
        header.ValueText = valueText;

        var headerCard = new SettingsStickerCard(new Vector2(598, 54), 8);
        headerCard.SetContent(header);

        var flow = new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
        };

        foreach (Size option in options)
        {
            Size captured = option;
            var row = new SettingsResolutionOption(option, () => select(captured));
            optionRows.Add(row);
            flow.Add(row);
        }

        var menuCard = new SettingsStickerCard(
            new Vector2(598, options.Count * SettingsResolutionOption.RowHeight),
            8);
        menuCard.SetContent(flow);

        menu = new Container
        {
            Y = 59,
            AutoSizeAxes = Axes.Both,
            Alpha = 0,
            Scale = new Vector2(1, 0.96f),
            Depth = -20,
            Child = menuCard,
        };

        InternalChildren = new Drawable[] { headerCard, menu };
    }

    public void SetSelected(Size size)
    {
        SelectedSize = size;
        valueText.Text = $"{size.Width} × {size.Height}";

        foreach (SettingsResolutionOption option in optionRows)
            option.SetSelected(option.Value == size);
    }

    public void SetEnabled(bool isEnabled)
    {
        enabled = isEnabled;

        if (!enabled && open)
            setOpen(false);

        this.FadeTo(enabled ? 1 : 0.58f, 100, Easing.OutQuint);
        chevron.FadeTo(enabled ? 1 : 0, 100, Easing.OutQuint);
    }

    internal void Toggle()
    {
        if (!enabled)
            return;

        setOpen(!open);
    }

    public bool Dismiss()
    {
        if (!open)
            return false;

        setOpen(false);
        return true;
    }

    private void setOpen(bool shouldOpen)
    {
        open = shouldOpen;
        headerBackground.FadeColour(open ? SettingsTheme.PaleCyan : Color4.White, 120, Easing.OutQuint);
        chevron.RotateTo(open ? 180 : 0, 160, Easing.OutQuint);

        if (open)
        {
            menu.Show();
            menu.FadeTo(1, 140, Easing.OutQuint);
            menu.ScaleTo(1, 140, Easing.OutQuint);
        }
        else
        {
            menu.FadeOut(100, Easing.OutQuint);
            menu.ScaleTo(new Vector2(1, 0.96f), 100, Easing.OutQuint);
        }
    }

    private void select(Size size)
    {
        onSelected(size);

        if (open)
            Toggle();
    }
}

internal partial class SettingsDropdownHeader : ClickableContainer
{
    private readonly Func<bool> isOpen;
    private readonly Func<bool> isEnabled;

    public Box Background { private get; set; }
    public Box FocusLine { private get; set; }
    public SpriteText ValueText { private get; set; }
    public override bool AcceptsFocus => isEnabled();

    public SettingsDropdownHeader(
        Func<bool> isOpen,
        Action action,
        Func<bool> isEnabled = null)
    {
        this.isOpen = isOpen;
        this.isEnabled = isEnabled ?? (() => true);
        Action = action;
    }

    protected override bool OnHover(HoverEvent e)
    {
        Background.FadeColour(SettingsTheme.PaleCyan, 120, Easing.OutQuint);
        ValueText?.MoveToX(22, 130, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        if (!isOpen())
            Background.FadeColour(Color4.White, 140, Easing.OutQuint);

        ValueText?.MoveToX(18, 150, Easing.OutQuint);
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        this.ScaleTo(0.985f, 400, Easing.OutQuint);
        return base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseUpEvent e)
    {
        this.ScaleTo(1f, 220, Easing.OutQuint);
        base.OnMouseUp(e);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            Action?.Invoke();
            return true;
        }

        return base.OnKeyDown(e);
    }

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);

        if (FocusLine != null)
            FocusLine.FadeIn(100, Easing.OutQuint);
        else
        {
            BorderColour = HomeControlColours.Pink;
            BorderThickness = 2.4f;
        }
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);

        if (FocusLine != null)
            FocusLine.FadeOut(100, Easing.OutQuint);
        else
        {
            BorderColour = HomeControlColours.Navy;
            BorderThickness = 1.4f;
        }
    }
}

internal partial class SettingsResolutionOption : ClickableContainer
{
    public const float RowHeight = 40;

    private readonly Box background;
    private readonly SpriteIcon check;
    private readonly SpriteText label;
    private readonly Box focusLine;

    public Size Value { get; }
    public override bool AcceptsFocus => true;

    public SettingsResolutionOption(Size value, Action action)
    {
        Value = value;
        Action = action;
        RelativeSizeAxes = Axes.X;
        Height = RowHeight;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            label = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 18,
                Text = $"{value.Width} × {value.Height}",
                Font = HomeTypography.Body(17),
                Colour = HomeControlColours.Navy,
            },
            check = new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -20,
                Size = new Vector2(13),
                Icon = FontAwesome.Solid.Check,
                Colour = HomeControlColours.Pink,
                Alpha = 0,
            },
            focusLine = new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 4,
                Colour = HomeControlColours.Pink,
                Alpha = 0,
            },
            new Box
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Colour = SettingsTheme.Divider,
            },
        };
    }

    public void SetSelected(bool selected) => check.FadeTo(selected ? 1 : 0, 100, Easing.OutQuint);

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(SettingsTheme.PaleCyan, 100, Easing.OutQuint);
        label.MoveToX(24, 110, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(Color4.White, 120, Easing.OutQuint);
        label.MoveToX(18, 130, Easing.OutQuint);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            Action?.Invoke();
            return true;
        }

        return base.OnKeyDown(e);
    }

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        focusLine.FadeIn(100, Easing.OutQuint);
        background.FadeColour(SettingsTheme.PaleCyan, 100, Easing.OutQuint);
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        focusLine.FadeOut(100, Easing.OutQuint);
        background.FadeColour(Color4.White, 100, Easing.OutQuint);
    }
}
