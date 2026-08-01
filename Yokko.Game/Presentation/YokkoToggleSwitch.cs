using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;

namespace Yokko.Game.Presentation;

public enum YokkoToggleSwitchStyle
{
    Brand,
    Dark,
}

/// <summary>
/// Shared visual switch. The owning row or card keeps click, focus, text, and
/// business behaviour while this component presents a bound boolean value.
/// </summary>
public partial class YokkoToggleSwitch : CompositeDrawable
{
    private readonly Box track;
    private readonly Circle thumb;
    private readonly YokkoToggleSwitchStyle style;
    private IBindable<bool> currentValue;
    private IBindable<YokkoUiTheme> currentTheme;
    private YokkoUiTheme appliedTheme = YokkoUiTheme.Default;
    private bool loaded;

    public bool Value => currentValue?.Value ?? false;
    public Color4 CurrentTrackColour => track.Colour;

    public YokkoToggleSwitch(
        IBindable<bool> value,
        YokkoToggleSwitchStyle style = YokkoToggleSwitchStyle.Brand,
        float width = 48,
        float height = 24,
        float thumbInset = 3)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!float.IsFinite(width) || width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (!float.IsFinite(height) || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        if (!float.IsFinite(thumbInset) || thumbInset < 0)
            throw new ArgumentOutOfRangeException(nameof(thumbInset));
        if (width <= height || height <= thumbInset * 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "Switch width must exceed its height and leave room for the thumb.");
        }

        this.style = style;
        Size = new Vector2(width, height);
        Masking = true;
        CornerRadius = height / 2;

        InternalChildren = new Drawable[]
        {
            track = new Box
            {
                RelativeSizeAxes = Axes.Both,
            },
            thumb = new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                Size = new Vector2(height - thumbInset * 2),
                Colour = Color4.White,
            },
        };

        currentValue = value.GetBoundCopy();
        currentValue.BindValueChanged(onValueChanged, true);
    }

    [BackgroundDependencyLoader]
    private void load(YokkoUiThemeStore themeStore)
    {
        currentTheme = themeStore.Current.GetBoundCopy();
        currentTheme.BindValueChanged(
            change =>
            {
                appliedTheme = change.NewValue;
                refresh(false);
            },
            true);
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        loaded = true;
        refresh(false);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            currentValue?.UnbindAll();
            currentTheme?.UnbindAll();
        }

        base.Dispose(isDisposing);
    }

    private void onValueChanged(ValueChangedEvent<bool> change) =>
        refresh(loaded);

    private void refresh(bool animated)
    {
        bool value = currentValue?.Value ?? false;
        Color4 active = style == YokkoToggleSwitchStyle.Brand
            ? appliedTheme.Colours.Brand.Ink
            : appliedTheme.Colours.Dark.Cyan;
        Color4 inactive = style == YokkoToggleSwitchStyle.Brand
            ? appliedTheme.Colours.Settings.Divider
            : appliedTheme.Colours.Dark.Border;
        float targetX = value
            ? Width - Height / 2
            : Height / 2;

        track.ClearTransforms(targetMember: nameof(Colour));
        thumb.ClearTransforms(targetMember: nameof(X));

        if (animated)
        {
            track.FadeColour(
                value ? active : inactive,
                appliedTheme.Motion.StateChangeDuration,
                appliedTheme.Motion.HoverEasing);
            thumb.MoveToX(
                targetX,
                appliedTheme.Motion.StateChangeDuration,
                appliedTheme.Motion.HoverEasing);
        }
        else
        {
            track.Colour = value ? active : inactive;
            thumb.X = targetX;
        }
    }
}
