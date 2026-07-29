using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Core.Mods;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class GameplayModsCategoryButton : ClickableContainer
{
    private readonly Color4 accentColour;
    private readonly Box background;
    private readonly Box accent;
    private readonly SpriteIcon icon;
    private bool selected;

    public GameplayModsCategoryButton(
        string label,
        IconUsage iconUsage,
        Color4 accentColour,
        Action action)
    {
        this.accentColour = accentColour;
        Action = action;
        Size = new Vector2(207, 50);
        Masking = true;
        CornerRadius = 3;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
            },
            accent = new Box
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-3, 0),
                Size = new Vector2(14),
                Rotation = 45,
                Colour = HomeControlColours.Yellow,
            },
            new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 12,
                Size = new Vector2(27),
                Colour = accentColour,
            },
            icon = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 19,
                Size = new Vector2(13),
                Icon = iconUsage,
                Colour = Color4.White,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 53,
                Text = label.ToUpperInvariant(),
                Font = HomeTypography.Display(12),
                Colour = HomeControlColours.Navy,
            },
        };

        SetSelected(false);
    }

    public void SetSelected(bool value)
    {
        selected = value;
        background.Colour = selected
            ? HomeControlColours.PaleCyan
            : Color4.White;
        accent.Alpha = selected ? 1 : 0;
        icon.Scale = selected ? new Vector2(1.04f) : Vector2.One;
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(HomeControlColours.PaleCyan, 100);
        this.ScaleTo(1.018f, 100, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        this.ScaleTo(1, 120, Easing.OutQuint);
        SetSelected(selected);
    }
}

internal partial class GameplayModListItem : ClickableContainer
{
    private readonly ManiaModDefinition definition;
    private readonly Color4 accentColour;
    private readonly bool selectable;
    private readonly Action<ManiaModId, bool> hoverChanged;
    private readonly Box focusBackground;
    private readonly Container acronymBadge;
    private readonly Box acronymBackground;
    private readonly SpriteText acronym;
    private readonly SpriteText name;
    private readonly SpriteText description;
    private bool selected;
    private bool focused;

    public GameplayModListItem(
        ManiaModDefinition definition,
        Color4 accentColour,
        bool selectable,
        Action action,
        Action<ManiaModId, bool> hoverChanged)
    {
        this.definition = definition;
        this.accentColour = accentColour;
        this.selectable = selectable;
        this.hoverChanged = hoverChanged;
        Action = action;
        Size = new Vector2(266, 42);
        Alpha = selectable ? 1 : 0.42f;

        InternalChildren = new Drawable[]
        {
            focusBackground = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(
                    HomeControlColours.PaleCyan.R,
                    HomeControlColours.PaleCyan.G,
                    HomeControlColours.PaleCyan.B,
                    0.38f),
                Alpha = 0,
            },
            acronymBadge = new Container
            {
                Size = new Vector2(40),
                Masking = true,
                CornerRadius = 4,
                BorderThickness = 1.3f,
                BorderColour = accentColour,
                Child = acronymBackground = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
            },
            new Container
            {
                Size = new Vector2(40),
                Child = acronym = new SpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = 9,
                    Text = definition.Acronym,
                    Font = HomeTypography.Display(16),
                    Colour = accentColour,
                },
            },
            name = new SpriteText
            {
                Position = new Vector2(52, 2),
                Text = definition.Name,
                Font = HomeTypography.Display(13),
                Colour = HomeControlColours.Navy,
            },
            description = new SpriteText
            {
                Position = new Vector2(52, 21),
                Text = selectable
                    ? shorten(definition.Description, 34)
                    : "Requires an osu!standard chart.",
                Font = HomeTypography.Body(9.5f),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.58f),
            },
            new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Position = new Vector2(52, -1),
                Size = new Vector2(214, 1),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.1f),
            },
        };

        SetSelected(false);
    }

    public void SetSelected(bool value)
    {
        selected = value && selectable;
        acronymBackground.Colour = selected
            ? accentColour
            : Color4.White;
        acronym.Colour = selected
            ? HomeControlColours.Navy
            : accentColour;
        name.Colour = selected
            ? accentColour
            : HomeControlColours.Navy;
    }

    public void SetFocused(bool value)
    {
        if (focused == value)
            return;

        focused = value;
        focusBackground.FadeTo(value ? 1 : 0, 90, Easing.OutQuint);
        acronymBadge.BorderThickness = value ? 2.3f : 1.3f;
        acronymBadge.BorderColour = value
            ? HomeControlColours.Navy
            : accentColour;
    }

    protected override bool OnHover(HoverEvent e)
    {
        hoverChanged?.Invoke(definition.Id, true);
        acronymBackground.FadeColour(
            selected
                ? accentColour
                : new Color4(
                    HomeControlColours.PaleCyan.R,
                    HomeControlColours.PaleCyan.G,
                    HomeControlColours.PaleCyan.B,
                    0.9f),
            90);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        hoverChanged?.Invoke(definition.Id, false);
        SetSelected(selected);
    }

    private static string shorten(string text, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(text)
            || text.Length <= maximumLength)
        {
            return text;
        }

        return text[..(maximumLength - 1)].TrimEnd() + "…";
    }
}

internal partial class GameplayActiveModRow : ClickableContainer
{
    private readonly Box background;
    private readonly SpriteIcon removeIcon;

    public GameplayActiveModRow(
        ManiaModDefinition definition,
        string value,
        Action remove)
    {
        Action = remove;
        Size = new Vector2(283, 37);
        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new Container
            {
                Size = new Vector2(40, 37),
                Masking = true,
                CornerRadius = 4,
                BorderThickness = 1.2f,
                BorderColour = HomeControlColours.Cyan,
                Children = new Drawable[]
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
                        Text = definition.Acronym,
                        Font = HomeTypography.Display(13),
                        Colour = HomeControlColours.Cyan,
                    },
                },
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 54,
                Text = definition.Name,
                Font = HomeTypography.Display(10),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -31,
                Text = value,
                Font = HomeTypography.Display(9),
                Colour = HomeControlColours.Navy,
            },
            removeIcon = new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -7,
                Size = new Vector2(9),
                Icon = FontAwesome.Solid.Times,
                Colour = HomeControlColours.Pink,
            },
            new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                X = 50,
                Size = new Vector2(233, 1),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.1f),
            },
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(HomeControlColours.PaleCyan, 90);
        removeIcon.ScaleTo(1.2f, 90, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(Color4.White, 110);
        removeIcon.ScaleTo(1, 110, Easing.OutQuint);
    }
}

internal partial class GameplayModsRateSlider : ClickableContainer
{
    private const float track_width = 284;

    private readonly Action<double> changed;
    private readonly Action interactionCompleted;
    private readonly Box fill;
    private readonly Circle marker;
    private double minimum;
    private double maximum;
    private double value;
    private bool enabled;

    internal GameplayModsRateSlider(
        Action<double> changed,
        Action interactionCompleted)
    {
        this.changed = changed;
        this.interactionCompleted = interactionCompleted;
        Size = new Vector2(track_width, 20);
        InternalChildren =
        [
            new Box
            {
                Y = 7,
                Size = new Vector2(track_width, 6),
                Colour = new Color4(0.78f, 0.81f, 0.88f, 1f),
            },
            fill = new Box
            {
                Y = 7,
                Height = 6,
                Colour = HomeControlColours.Cyan,
            },
            marker = new Circle
            {
                Origin = Anchor.Centre,
                Y = 10,
                Size = new Vector2(16),
                BorderThickness = 3,
                BorderColour = HomeControlColours.Cyan,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
            },
        ];
    }

    internal void SetState(
        bool isEnabled,
        double minimum,
        double maximum,
        double value)
    {
        bool enabledChanged = enabled != isEnabled;
        enabled = isEnabled;
        this.minimum = minimum;
        this.maximum = maximum;
        updateVisualValue(value);
        if (enabledChanged)
            this.FadeTo(isEnabled ? 1 : 0.48f, 100);
        else
            Alpha = isEnabled ? 1 : 0.48f;
    }

    private void updateVisualValue(double newValue)
    {
        value = Math.Clamp(newValue, minimum, maximum);
        double progress = Math.Clamp(
            (value - minimum) / (maximum - minimum),
            0,
            1);
        float x = (float)(progress * track_width);
        fill.Width = x;
        marker.X = x;
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (!enabled || e.Button != MouseButton.Left)
            return false;

        updateFrom(e.ScreenSpaceMousePosition);
        return true;
    }

    protected override bool OnDragStart(DragStartEvent e) => enabled;

    protected override void OnDrag(DragEvent e) =>
        updateFrom(e.ScreenSpaceMousePosition);

    protected override void OnMouseUp(MouseUpEvent e)
    {
        if (enabled && e.Button == MouseButton.Left)
            interactionCompleted?.Invoke();

        base.OnMouseUp(e);
    }

    protected override bool OnScroll(ScrollEvent e)
    {
        if (!enabled || e.ScrollDelta.Y == 0)
            return false;

        double nextValue = Math.Clamp(
            Math.Round(value + Math.Sign(e.ScrollDelta.Y) * 0.01, 2),
            minimum,
            maximum);
        if (Math.Abs(nextValue - value) < 0.0001)
            return true;

        updateVisualValue(nextValue);
        changed(nextValue);
        interactionCompleted?.Invoke();
        return true;
    }

    private void updateFrom(Vector2 screenPosition)
    {
        if (!enabled)
            return;

        double progress = Math.Clamp(
            ToLocalSpace(screenPosition).X / track_width,
            0,
            1);
        double nextValue = Math.Round(
            minimum + progress * (maximum - minimum),
            2);
        if (Math.Abs(nextValue - value) < 0.0001)
            return;

        updateVisualValue(nextValue);
        changed(nextValue);
    }
}

internal partial class GameplayModsPitchButton : ClickableContainer
{
    private readonly SpriteText label;
    private bool enabled;
    private bool selected;

    internal GameplayModsPitchButton(Action action)
    {
        Action = () =>
        {
            if (enabled)
                action();
        };
        Size = new Vector2(284, 22);
        Child = label = new SpriteText
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Font = HomeTypography.Display(9),
        };
    }

    internal void SetState(
        bool isEnabled,
        bool supported,
        bool selected)
    {
        enabled = isEnabled && supported;
        this.selected = selected;
        label.Text = supported
            ? $"MUSIC PITCH · {(selected ? "ON" : "OFF")}  (P)"
            : "MUSIC FREQUENCY LOCKED BY THIS MOD";
        label.Colour = selected
            ? HomeControlColours.Pink
            : new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                enabled ? 0.62f : 0.42f);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (enabled)
            label.FadeColour(HomeControlColours.Cyan, 90);
        return enabled;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        label.FadeColour(
            selected
                ? HomeControlColours.Pink
                : new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    enabled ? 0.62f : 0.42f),
            100);
    }
}

internal partial class GameplayModsResetButton : ClickableContainer
{
    private readonly Box background;
    private readonly SpriteIcon icon;
    private bool enabled = true;

    internal bool IsEnabled => enabled;

    public GameplayModsResetButton(Action action)
    {
        Action = () =>
        {
            if (enabled)
                action();
        };
        Size = new Vector2(138, 64);
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
            icon = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 19,
                Size = new Vector2(22),
                Icon = FontAwesome.Solid.Undo,
                Colour = HomeControlColours.Cyan,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 55,
                Text = "RESET",
                Font = HomeTypography.Display(15),
                Colour = HomeControlColours.Navy,
            },
            new Box
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Size = new Vector2(16),
                Rotation = 45,
                Colour = HomeControlColours.Yellow,
            },
        };
    }

    public void SetEnabled(bool value)
    {
        if (enabled == value)
            return;

        enabled = value;
        this.FadeTo(value ? 1 : 0.46f, 120, Easing.OutQuint);
        BorderColour = value
            ? HomeControlColours.Navy
            : new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.38f);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!enabled)
            return false;

        background.FadeColour(HomeControlColours.PaleCyan, 100);
        icon.RotateTo(-30, 150, Easing.OutQuint);
        this.ScaleTo(1.02f, 100, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(Color4.White, 120);
        icon.RotateTo(0, 120, Easing.OutQuint);
        this.ScaleTo(1, 120, Easing.OutQuint);
    }
}

internal partial class GameplayModsDoneButton : ClickableContainer
{
    private readonly Box background;
    private readonly SpriteIcon chevron;

    public GameplayModsDoneButton(Action action)
    {
        Action = action;
        Size = new Vector2(354, 78);
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
                Position = new Vector2(8),
                Size = new Vector2(62),
                Masking = true,
                CornerRadius = 6,
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
                        Size = new Vector2(26),
                        Icon = FontAwesome.Solid.Play,
                        Colour = HomeControlColours.Navy,
                    },
                },
            },
            new SpriteText
            {
                Position = new Vector2(88, 23),
                Text = "DONE",
                Font = HomeTypography.Display(27),
                Colour = Color4.White,
            },
            chevron = new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -18,
                Size = new Vector2(15),
                Icon = FontAwesome.Solid.ChevronRight,
                Colour = HomeControlColours.Yellow,
            },
            new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                X = 88,
                Width = 60,
                Height = 3,
                Colour = HomeControlColours.Pink,
            },
            new Box
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Size = new Vector2(17),
                Rotation = 45,
                Colour = HomeControlColours.Yellow,
            },
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(
            new Color4(0.02f, 0.04f, 0.42f, 1f),
            100);
        chevron.MoveToX(-11, 120, Easing.OutQuint);
        this.ScaleTo(1.012f, 100, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(HomeControlColours.Navy, 120);
        chevron.MoveToX(-18, 120, Easing.OutQuint);
        this.ScaleTo(1, 120, Easing.OutQuint);
    }
}
