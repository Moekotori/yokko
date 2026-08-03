using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace Yokko.Game.Presentation;

public enum YokkoButtonStyle
{
    Quiet,
    Secondary,
    Primary,
    Accent,
}

public enum YokkoAccentRole
{
    Accent,
    Positive,
    Warning,
    Danger,
    Violet,
}

/// <summary>
/// Shared Yokko action button. Its presentation follows the active
/// <see cref="YokkoUiTheme"/> while its action remains owned by the page.
/// </summary>
public partial class YokkoButton : ClickableContainer
{
    private readonly Box background;
    private readonly Box accentBar;
    private readonly Box focusLine;
    private readonly FillFlowContainer content;
    private readonly SpriteIcon iconDrawable;
    private readonly SpriteText label;
    private readonly YokkoButtonStyle style;
    private readonly Color4? accentOverride;
    private readonly YokkoAccentRole accentRole;
    private readonly float labelSize;
    private readonly string labelWeight;

    private IBindable<YokkoUiTheme> currentTheme;
    private YokkoUiTheme appliedTheme = YokkoUiTheme.Default;
    private Color4 idleColour;
    private Color4 hoverColour;
    private bool hovered;
    private bool focused;

    public override bool AcceptsFocus => Enabled.Value;

    public bool IsEnabled
    {
        get => Enabled.Value;
        set => Enabled.Value = value;
    }

    public LocalisableString Text
    {
        get => label.Text;
        set => label.Text = value;
    }

    public Color4 CurrentBackgroundColour => background.Colour;
    public float CurrentFocusAlpha => focusLine.Alpha;

    public YokkoButton(
        string text,
        IconUsage icon,
        Action action,
        float width = 112,
        float height = 42,
        YokkoButtonStyle style = YokkoButtonStyle.Secondary,
        Color4? accent = null,
        YokkoAccentRole accentRole = YokkoAccentRole.Accent)
        : this(
            (LocalisableString)text,
            icon,
            action,
            width,
            height,
            style,
            accent,
            accentRole)
    {
    }

    public YokkoButton(
        LocalisableString text,
        IconUsage icon,
        Action action,
        float width = 112,
        float height = 42,
        YokkoButtonStyle style = YokkoButtonStyle.Secondary,
        Color4? accent = null,
        YokkoAccentRole accentRole = YokkoAccentRole.Accent)
        : this(
            text,
            icon,
            action,
            width,
            height,
            style,
            accent,
            12,
            "SemiBold",
            accentRole)
    {
    }

    public YokkoButton(
        LocalisableString text,
        Action action,
        float width = 112,
        float height = 42,
        YokkoButtonStyle style = YokkoButtonStyle.Secondary,
        Color4? accent = null,
        float labelSize = 12,
        string labelWeight = "SemiBold",
        YokkoAccentRole accentRole = YokkoAccentRole.Accent)
        : this(
            text,
            null,
            action,
            width,
            height,
            style,
            accent,
            labelSize,
            labelWeight,
            accentRole)
    {
    }

    private YokkoButton(
        LocalisableString text,
        IconUsage? icon,
        Action action,
        float width,
        float height,
        YokkoButtonStyle style,
        Color4? accent,
        float labelSize,
        string labelWeight,
        YokkoAccentRole accentRole)
    {
        if (!float.IsFinite(width) || width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (!float.IsFinite(height) || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        if (!float.IsFinite(labelSize) || labelSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(labelSize));

        this.style = style;
        accentOverride = accent;
        this.accentRole = accentRole;
        this.labelSize = labelSize;
        this.labelWeight = labelWeight;

        Action = action;
        Size = new Vector2(width, height);
        Masking = true;
        CornerRadius = YokkoUiTheme.Default.Metrics.ControlCornerRadius;

        var contentChildren = new List<Drawable>();
        if (icon.HasValue)
        {
            contentChildren.Add(iconDrawable = new SpriteIcon
            {
                Size = new Vector2(15),
                Icon = icon.Value,
            });
        }

        contentChildren.Add(label = new SpriteText
        {
            Text = text,
        });

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
            },
            accentBar = new Box
            {
                RelativeSizeAxes = Axes.Y,
                Alpha = style == YokkoButtonStyle.Accent ? 1 : 0,
            },
            content = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(
                    YokkoUiTheme.Default.Metrics.InlineSpacing,
                    0),
                Children = contentChildren,
            },
            focusLine = new Box
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                RelativeSizeAxes = Axes.X,
                Width = 0.72f,
                Alpha = 0,
            },
        };

        applyTheme(YokkoUiTheme.Default);
        Enabled.BindValueChanged(onEnabledChanged, true);
    }

    [BackgroundDependencyLoader]
    private void load(YokkoUiThemeStore themeStore)
    {
        currentTheme = themeStore.Current.GetBoundCopy();
        currentTheme.BindValueChanged(
            change => applyTheme(change.NewValue),
            true);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!Enabled.Value)
            return base.OnHover(e);

        hovered = true;
        background.FadeColour(
            hoverColour,
            appliedTheme.Motion.HoverInDuration,
            appliedTheme.Motion.HoverEasing);
        this.ScaleTo(
            style == YokkoButtonStyle.Accent
                ? 1f
                : appliedTheme.Metrics.HoverScale,
            appliedTheme.Motion.HoverInDuration,
            appliedTheme.Motion.HoverEasing);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        hovered = false;
        background.FadeColour(
            idleColour,
            appliedTheme.Motion.HoverOutDuration,
            appliedTheme.Motion.HoverEasing);
        this.ScaleTo(
            1f,
            appliedTheme.Motion.HoverOutDuration,
            appliedTheme.Motion.HoverEasing);
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (!Enabled.Value)
            return true;

        this.ScaleTo(
            appliedTheme.Metrics.PressedScale,
            appliedTheme.Motion.HoverInDuration,
            appliedTheme.Motion.HoverEasing);
        return base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseUpEvent e)
    {
        if (Enabled.Value)
        {
            this.ScaleTo(
                hovered && style != YokkoButtonStyle.Accent
                    ? appliedTheme.Metrics.HoverScale
                    : 1f,
                appliedTheme.Motion.HoverOutDuration,
                appliedTheme.Motion.HoverEasing);
        }

        base.OnMouseUp(e);
    }

    protected override bool OnClick(ClickEvent e)
    {
        if (!Enabled.Value)
            return true;

        return base.OnClick(e);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (Enabled.Value && (e.Key is Key.Enter or Key.Space))
        {
            TriggerClick();
            return true;
        }

        return base.OnKeyDown(e);
    }

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        focused = true;
        focusLine.FadeIn(
            appliedTheme.Motion.FocusDuration,
            appliedTheme.Motion.HoverEasing);
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        focused = false;
        focusLine.FadeOut(
            appliedTheme.Motion.FocusDuration,
            appliedTheme.Motion.HoverEasing);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            currentTheme?.UnbindAll();
            Enabled.ValueChanged -= onEnabledChanged;
        }

        base.Dispose(isDisposing);
    }

    private void applyTheme(YokkoUiTheme theme)
    {
        background.ClearTransforms(targetMember: nameof(Colour));
        this.ClearTransforms(targetMember: nameof(Scale));
        this.ClearTransforms(targetMember: nameof(Alpha));
        focusLine.ClearTransforms(targetMember: nameof(Alpha));

        appliedTheme = theme;
        YokkoDarkColourTokens colours = theme.Colours.Dark;
        Color4 accent = accentOverride ?? resolveAccent(theme, accentRole);

        CornerRadius = theme.Metrics.ControlCornerRadius;
        BorderThickness = style == YokkoButtonStyle.Accent ? 0 : 1;
        BorderColour = style == YokkoButtonStyle.Primary
            ? withAlpha(accent, 0.72f)
            : colours.Border;

        idleColour = style switch
        {
            YokkoButtonStyle.Primary => multiplyRgb(accent, 0.24f),
            YokkoButtonStyle.Quiet => withAlpha(colours.PanelAlt, 0.72f),
            YokkoButtonStyle.Accent => colours.AccentControl,
            _ => colours.SurfaceElevated,
        };
        hoverColour = style == YokkoButtonStyle.Primary
            ? multiplyRgb(accent, 0.34f)
            : colours.SurfaceHover;

        background.Colour = hovered ? hoverColour : idleColour;
        accentBar.Width = theme.Metrics.AccentBarWidth;
        accentBar.Colour = accent;
        content.Spacing = new Vector2(theme.Metrics.InlineSpacing, 0);
        label.Font = theme.Typography.Control(labelSize, labelWeight);
        label.Colour = colours.Text;
        focusLine.Height = theme.Metrics.FocusLineHeight;
        focusLine.Colour = accent;
        focusLine.Alpha = focused && Enabled.Value ? 1 : 0;
        Scale = new Vector2(
            hovered && Enabled.Value && style != YokkoButtonStyle.Accent
                ? theme.Metrics.HoverScale
                : 1f);

        if (iconDrawable != null)
        {
            iconDrawable.Colour = style == YokkoButtonStyle.Primary
                ? accent
                : colours.TextMuted;
        }

        refreshEnabledState(false);
    }

    private void refreshEnabledState(bool animated)
    {
        float alpha = Enabled.Value
            ? 1
            : appliedTheme.Metrics.DisabledAlpha;

        if (animated && IsLoaded)
        {
            this.FadeTo(
                alpha,
                appliedTheme.Motion.StateChangeDuration,
                appliedTheme.Motion.HoverEasing);
        }
        else
            Alpha = alpha;

        if (!Enabled.Value)
        {
            background.ClearTransforms(targetMember: nameof(Colour));
            background.Colour = idleColour;
            focusLine.Alpha = 0;
            this.ScaleTo(1f);
        }
        else
            focusLine.Alpha = focused ? 1 : 0;
    }

    private void onEnabledChanged(ValueChangedEvent<bool> change)
    {
        if (!change.NewValue)
            hovered = false;

        refreshEnabledState(true);
    }

    private static Color4 resolveAccent(
        YokkoUiTheme theme,
        YokkoAccentRole role) => role switch
        {
            YokkoAccentRole.Positive => theme.Colours.Dark.Lime,
            YokkoAccentRole.Warning => theme.Colours.Brand.Yellow,
            YokkoAccentRole.Danger => theme.Colours.Dark.Rose,
            YokkoAccentRole.Violet => theme.Colours.Dark.Violet,
            _ => theme.Colours.Dark.Cyan,
        };

    private static Color4 withAlpha(Color4 colour, float alpha) =>
        new(colour.R, colour.G, colour.B, alpha);

    private static Color4 multiplyRgb(Color4 colour, float amount) =>
        new(
            colour.R * amount,
            colour.G * amount,
            colour.B * amount,
            1f);
}
