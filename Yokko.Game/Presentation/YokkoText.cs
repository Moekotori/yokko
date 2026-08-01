using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osuTK.Graphics;

namespace Yokko.Game.Presentation;

public enum YokkoTextStyle
{
    Display,
    Heading,
    Body,
    Label,
    Caption,
}

public enum YokkoTextColourRole
{
    Primary,
    Muted,
    Dim,
    Accent,
    Positive,
    Warning,
    Danger,
    BrandInk,
}

/// <summary>
/// Theme-aware text for Yokko-owned interfaces.
/// </summary>
public partial class YokkoText : SpriteText
{
    private IBindable<YokkoUiTheme> currentTheme;
    private YokkoUiTheme appliedTheme = YokkoUiTheme.Default;
    private YokkoTextStyle textStyle;
    private YokkoTextColourRole colourRole;
    private float textSize;
    private Color4? colourOverride;

    public YokkoTextStyle TextStyle
    {
        get => textStyle;
        set
        {
            textStyle = value;
            applyTheme(appliedTheme);
        }
    }

    public YokkoTextColourRole ColourRole
    {
        get => colourRole;
        set
        {
            colourRole = value;
            applyTheme(appliedTheme);
        }
    }

    public float TextSize
    {
        get => textSize;
        set
        {
            if (!float.IsFinite(value) || value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            textSize = value;
            applyTheme(appliedTheme);
        }
    }

    public Color4? ColourOverride
    {
        get => colourOverride;
        set
        {
            colourOverride = value;
            applyTheme(appliedTheme);
        }
    }

    public YokkoText(
        LocalisableString text = default,
        float size = 16,
        YokkoTextStyle style = YokkoTextStyle.Body,
        YokkoTextColourRole colour = YokkoTextColourRole.Primary)
    {
        if (!float.IsFinite(size) || size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));

        Text = text;
        textSize = size;
        textStyle = style;
        colourRole = colour;
        applyTheme(YokkoUiTheme.Default);
    }

    [BackgroundDependencyLoader]
    private void load(YokkoUiThemeStore themeStore)
    {
        currentTheme = themeStore.Current.GetBoundCopy();
        currentTheme.BindValueChanged(
            change => applyTheme(change.NewValue),
            true);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            currentTheme?.UnbindAll();

        base.Dispose(isDisposing);
    }

    private void applyTheme(YokkoUiTheme theme)
    {
        appliedTheme = theme;
        Font = textStyle switch
        {
            YokkoTextStyle.Display => theme.Typography.Display(textSize),
            YokkoTextStyle.Heading =>
                theme.Typography.Interface(textSize, "Bold"),
            YokkoTextStyle.Label =>
                theme.Typography.Interface(textSize, "SemiBold"),
            YokkoTextStyle.Caption =>
                theme.Typography.Interface(textSize),
            _ => theme.Typography.Body(textSize),
        };

        YokkoDarkColourTokens dark = theme.Colours.Dark;
        Colour = colourOverride ?? colourRole switch
        {
            YokkoTextColourRole.Muted => dark.TextMuted,
            YokkoTextColourRole.Dim => dark.TextDim,
            YokkoTextColourRole.Accent => dark.Cyan,
            YokkoTextColourRole.Positive => dark.Lime,
            YokkoTextColourRole.Warning => theme.Colours.Brand.Yellow,
            YokkoTextColourRole.Danger => dark.Rose,
            YokkoTextColourRole.BrandInk => theme.Colours.Brand.Ink,
            _ => dark.Text,
        };
    }
}
