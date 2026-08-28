using System;
using System.Linq;
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
using osuTK.Input;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Screens.Settings;

internal partial class SkinSettingsPanel
    : CompositeDrawable, ISettingsSearchTarget
{
    private readonly OsuManiaSkinLibrary library;
    private readonly FillFlowContainer skinList;
    private readonly SettingsContentScrollContainer contentScroll;

    internal int SkinCount { get; private set; }

    public SkinSettingsPanel(OsuManiaSkinLibrary library, YokkoSkinSettings settings)
    {
        this.library = library;
        RelativeSizeAxes = Axes.Both;

        var content = new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Children = new Drawable[]
            {
                SettingsChrome.CreateHeader(
                    YokkoStrings.Get("settings.skins.title"),
                    YokkoStrings.Get("settings.skins.subtitle"),
                    FontAwesome.Solid.PaintBrush,
                    6),
                createDropCard(),
                new SpriteText
                {
                    Position = new Vector2(378, 272),
                    Text = YokkoStrings.Get("settings.skins.section_library"),
                    Font = HomeTypography.Display(24),
                    Colour = HomeControlColours.Navy,
                },
                new BasicScrollContainer
                {
                    Position = new Vector2(378, 310),
                    Size = new Vector2(840, 190),
                    ScrollbarVisible = false,
                    Child = skinList = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 10),
                    },
                },
                new SpriteText
                {
                    Position = new Vector2(378, 512),
                    Text = YokkoStrings.Get("settings.skins.section_gameplay"),
                    Font = HomeTypography.Display(24),
                    Colour = HomeControlColours.Navy,
                },
                new AdditionalLongNoteCutControls(settings)
                {
                    Position = new Vector2(380, 548),
                },
                new GameplayInlineToggle(
                    YokkoStrings.Get("settings.skins.combo_bursts"),
                    YokkoStrings.Get("settings.skins.combo_bursts_note"),
                    settings.ShowComboBursts)
                {
                    Position = new Vector2(380, 610),
                    Size = new Vector2(826, 26),
                },
            },
        };

        InternalChild = contentScroll = new SettingsContentScrollContainer
        {
            RelativeSizeAxes = Axes.Both,
            Child = content,
        };

        library.LibraryChanged += onLibraryChanged;
        refresh();
    }

    internal bool SelectSkin(string id) => library.Select(id);

    internal bool DeleteSkin(string id) => library.Delete(id);

    public bool TryFocusSearchItem(string itemId) =>
        SettingsSearchScroll.TryFocus(
            SettingsPageKind.Skins,
            itemId,
            contentScroll);

    private Drawable createDropCard() => new Container
    {
        Position = new Vector2(378, 156),
        Size = new Vector2(840, 88),
        Masking = true,
        CornerRadius = 9,
        BorderThickness = 1.2f,
        BorderColour = HomeControlColours.Cyan,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SettingsTheme.PaleCyan,
            },
            new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 50,
                Size = new Vector2(56),
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 50,
                Size = new Vector2(25),
                Icon = FontAwesome.Solid.Download,
                Colour = HomeControlColours.Navy,
            },
            new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 94,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = YokkoStrings.Get("settings.skins.section_import"),
                        Font = HomeTypography.Display(22),
                        Colour = HomeControlColours.Navy,
                    },
                    new SpriteText
                    {
                        Text = YokkoStrings.Get("settings.skins.drop_hint"),
                        Font = HomeTypography.Body(17),
                        Colour = SettingsTheme.MutedNavy,
                    },
                },
            },
            new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -28,
                Text = YokkoStrings.Get("settings.skins.accepted_formats"),
                Font = HomeTypography.Display(15),
                Spacing = new Vector2(1),
                Colour = HomeControlColours.Pink,
            },
        },
    };

    private void onLibraryChanged() => Schedule(refresh);

    private void refresh()
    {
        skinList.Clear();
        var entries = library.GetInstalledSkins();
        SkinCount = entries.Count;

        if (entries.Count == 0)
        {
            skinList.Add(new EmptySkinLibraryCard());
            return;
        }

        foreach (OsuManiaSkinEntry entry in entries)
        {
            skinList.Add(new SkinLibraryRow(
                entry,
                library.IsSelected(entry.Id),
                () => library.Select(entry.Id),
                () => library.Delete(entry.Id)));
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            library.LibraryChanged -= onLibraryChanged;

        base.Dispose(isDisposing);
    }
}

internal partial class AdditionalLongNoteCutControls : CompositeDrawable
{
    private readonly BindableBool enabled;
    private readonly AdditionalLongNoteCutSlider slider;

    internal bool IsSliderEnabled => slider.IsEnabled;

    public AdditionalLongNoteCutControls(YokkoSkinSettings settings)
    {
        enabled = settings.LongNoteCutEnabled;
        Size = new Vector2(826, 54);

        InternalChildren = new Drawable[]
        {
            new GameplayInlineToggle(
                YokkoStrings.Get("settings.skins.ln_cut_amount"),
                YokkoStrings.Get("settings.skins.ln_cut_amount_note"),
                enabled)
            {
                Size = new Vector2(826, 26),
            },
            slider = new AdditionalLongNoteCutSlider(
                settings.LongNoteCutAmount,
                YokkoSkinSettings.LongNoteCutAmountStep,
                YokkoSkinSettings.MinimumLongNoteCutAmount,
                YokkoSkinSettings.MaximumLongNoteCutAmount)
            {
                Position = new Vector2(436, 28),
            },
        };

        enabled.BindValueChanged(onEnabledChanged, true);
    }

    private void onEnabledChanged(ValueChangedEvent<bool> change) =>
        slider.SetEnabled(change.NewValue);

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            enabled.ValueChanged -= onEnabledChanged;

        base.Dispose(isDisposing);
    }
}

internal partial class AdditionalLongNoteCutSlider : CompositeDrawable
{
    private const float track_x = 7;
    private const float track_width = 286;
    private readonly Bindable<double> value;
    private readonly double step;
    private readonly double minimum;
    private readonly double maximum;
    private readonly Box track;
    private readonly Box fill;
    private readonly Circle knob;
    private readonly SpriteText valueText;
    private bool isEnabled = true;

    internal bool IsEnabled => isEnabled;

    public override bool AcceptsFocus => isEnabled;

    public AdditionalLongNoteCutSlider(
        Bindable<double> value,
        double step,
        double minimum,
        double maximum)
    {
        this.value = value;
        this.step = step;
        this.minimum = minimum;
        this.maximum = maximum;
        Size = new Vector2(390, 26);

        InternalChildren = new Drawable[]
        {
            track = new Box
            {
                Position = new Vector2(track_x, 11),
                Size = new Vector2(track_width, 5),
                Colour = SettingsTheme.Divider,
            },
            fill = new Box
            {
                Position = new Vector2(track_x, 11),
                Height = 5,
                Colour = HomeControlColours.Pink,
            },
            knob = new Circle
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                Position = new Vector2(track_x, 13.5f),
                Size = new Vector2(14),
                Colour = Color4.White,
                BorderThickness = 2.5f,
                BorderColour = HomeControlColours.Pink,
            },
            valueText = new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -2,
                Font = HomeTypography.Display(14),
                Colour = HomeControlColours.Navy,
            },
        };

        value.BindValueChanged(onValueChanged, true);
    }

    internal static double ValueFromProgress(
        double progress,
        double step,
        double minimum,
        double maximum)
    {
        double raw = minimum
                     + Math.Clamp(progress, 0, 1)
                     * (maximum - minimum);
        return Math.Clamp(
            Math.Round(raw / step) * step,
            minimum,
            maximum);
    }

    internal void SetEnabled(bool enabled)
    {
        isEnabled = enabled;
        this.FadeTo(enabled ? 1 : 0.38f, 120, Easing.OutQuint);
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (!isEnabled || e.Button != MouseButton.Left)
            return false;

        updateFrom(ToLocalSpace(e.ScreenSpaceMousePosition).X);
        return true;
    }

    protected override bool OnDragStart(DragStartEvent e) => isEnabled;

    protected override void OnDrag(DragEvent e)
    {
        if (isEnabled)
            updateFrom(ToLocalSpace(e.ScreenSpaceMousePosition).X);
    }

    protected override bool OnScroll(ScrollEvent e)
    {
        if (!isEnabled || e.ScrollDelta.Y == 0)
            return false;

        value.Value = snap(
            value.Value + Math.Sign(e.ScrollDelta.Y) * step);
        return true;
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (!isEnabled)
            return base.OnKeyDown(e);

        double next = e.Key switch
        {
            Key.Left or Key.Down => value.Value - step,
            Key.Right or Key.Up => value.Value + step,
            Key.Home => minimum,
            Key.End => maximum,
            _ => value.Value,
        };

        if (next == value.Value
            && e.Key is not Key.Home and not Key.End
            and not Key.Left and not Key.Right
            and not Key.Up and not Key.Down)
        {
            return base.OnKeyDown(e);
        }

        value.Value = snap(next);
        return true;
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!isEnabled)
            return false;

        track.FadeColour(SettingsTheme.PaleCyan, 100, Easing.OutQuint);
        knob.ScaleTo(1.18f, 100, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        track.FadeColour(SettingsTheme.Divider, 120, Easing.OutQuint);
        knob.ScaleTo(1, 120, Easing.OutQuint);
    }

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        valueText.FadeColour(HomeControlColours.Pink, 100, Easing.OutQuint);
        knob.BorderColour = HomeControlColours.Cyan;
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        valueText.FadeColour(HomeControlColours.Navy, 100, Easing.OutQuint);
        knob.BorderColour = HomeControlColours.Pink;
    }

    private void updateFrom(float localX) =>
        value.Value = ValueFromProgress(
            (localX - track_x) / track_width,
            step,
            minimum,
            maximum);

    private double snap(double next) =>
        Math.Clamp(
            Math.Round(next / step) * step,
            minimum,
            maximum);

    private void onValueChanged(ValueChangedEvent<double> change)
    {
        float progress = (float)Math.Clamp(
            (change.NewValue - minimum) / (maximum - minimum),
            0,
            1);
        fill.Width = progress * track_width;
        knob.X = track_x + progress * track_width;
        valueText.Text = $"{change.NewValue:0.0} × NOTE";
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            value.ValueChanged -= onValueChanged;

        base.Dispose(isDisposing);
    }
}

internal partial class EmptySkinLibraryCard : CompositeDrawable
{
    public EmptySkinLibraryCard()
    {
        RelativeSizeAxes = Axes.X;
        Height = 114;
        Masking = true;
        CornerRadius = 8;
        BorderThickness = 1.2f;
        BorderColour = SettingsTheme.Divider;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 48,
                Size = new Vector2(28),
                Icon = FontAwesome.Regular.Image,
                Colour = SettingsTheme.MutedNavy,
            },
            new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 88,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = YokkoStrings.Get("settings.skins.empty"),
                        Font = HomeTypography.Display(22),
                        Colour = HomeControlColours.Navy,
                    },
                    new SpriteText
                    {
                        Text = YokkoStrings.Get("settings.skins.empty_note"),
                        Font = HomeTypography.Body(17),
                        Colour = SettingsTheme.MutedNavy,
                    },
                },
            },
        };
    }
}

internal partial class SkinLibraryRow : CompositeDrawable
{
    private readonly SettingsSkinActionButton deleteButton;
    private readonly Action onDelete;
    private bool awaitingDeleteConfirmation;

    public SkinLibraryRow(
        OsuManiaSkinEntry entry,
        bool selected,
        Action onSelect,
        Action onDelete)
    {
        this.onDelete = onDelete;
        RelativeSizeAxes = Axes.X;
        Height = 76;
        Masking = true;
        CornerRadius = 8;
        BorderThickness = selected ? 2 : 1.2f;
        BorderColour = selected ? HomeControlColours.Cyan : SettingsTheme.Divider;

        string details = string.Join(" · ", new[]
        {
            string.IsNullOrWhiteSpace(entry.Author) ? null : entry.Author,
            entry.KeyModes.Count == 0
                ? null
                : string.Join(" / ", entry.KeyModes.Select(keys => $"{keys}K")),
        }.Where(value => value != null));

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = selected ? SettingsTheme.PaleCyan : Color4.White,
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 7,
                Colour = selected ? HomeControlColours.Cyan : HomeControlColours.Yellow,
            },
            new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 26,
                Width = 510,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Width = 510,
                        Truncate = true,
                        Text = entry.Name,
                        Font = HomeTypography.Display(21),
                        Colour = HomeControlColours.Navy,
                    },
                    new SpriteText
                    {
                        Width = 510,
                        Truncate = true,
                        Text = details,
                        Font = HomeTypography.Body(15),
                        Colour = SettingsTheme.MutedNavy,
                    },
                },
            },
            new SettingsSkinActionButton(
                selected
                    ? YokkoStrings.Get("settings.skins.active")
                    : YokkoStrings.Get("settings.skins.use"),
                selected ? FontAwesome.Solid.Check : FontAwesome.Solid.Play,
                onSelect,
                selected)
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -130,
            },
            deleteButton = new SettingsSkinActionButton(
                YokkoStrings.Get("settings.skins.delete"),
                FontAwesome.Solid.Trash,
                requestDelete,
                false,
                destructive: true)
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -16,
            },
        };
    }

    private void requestDelete()
    {
        if (awaitingDeleteConfirmation)
        {
            onDelete();
            return;
        }

        awaitingDeleteConfirmation = true;
        deleteButton.SetLabel(YokkoStrings.Get("settings.skins.confirm_delete"));
        Scheduler.AddDelayed(() =>
        {
            awaitingDeleteConfirmation = false;
            deleteButton.SetLabel(YokkoStrings.Get("settings.skins.delete"));
        }, 2500);
    }
}

internal partial class SettingsSkinActionButton : ClickableContainer
{
    private readonly Box background;
    private readonly SpriteText label;
    private readonly bool selected;
    private readonly bool destructive;

    public override bool AcceptsFocus => true;

    public SettingsSkinActionButton(
        LocalisableString text,
        IconUsage icon,
        Action action,
        bool selected,
        bool destructive = false)
    {
        Action = action;
        this.selected = selected;
        this.destructive = destructive;
        Size = new Vector2(104, 38);
        Masking = true;
        CornerRadius = 6;
        BorderThickness = 1.2f;
        BorderColour = destructive ? HomeControlColours.Pink : HomeControlColours.Navy;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = selected ? HomeControlColours.Navy : Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 18,
                Size = new Vector2(14),
                Icon = icon,
                Colour = selected
                    ? Color4.White
                    : destructive ? HomeControlColours.Pink : HomeControlColours.Navy,
            },
            label = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                X = 9,
                Text = text,
                Font = HomeTypography.Control(14),
                Colour = selected
                    ? Color4.White
                    : destructive ? HomeControlColours.Pink : HomeControlColours.Navy,
            },
        };
    }

    public void SetLabel(LocalisableString text) => label.Text = text;

    protected override bool OnHover(osu.Framework.Input.Events.HoverEvent e)
    {
        if (!selected)
            background.FadeColour(destructive ? HomeControlColours.Pink : SettingsTheme.PaleCyan, 100);

        if (destructive)
            label.FadeColour(Color4.White, 100);

        return true;
    }

    protected override void OnHoverLost(osu.Framework.Input.Events.HoverLostEvent e)
    {
        background.FadeColour(selected ? HomeControlColours.Navy : Color4.White, 120);
        label.FadeColour(
            selected ? Color4.White : destructive ? HomeControlColours.Pink : HomeControlColours.Navy,
            120);
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
        BorderColour = HomeControlColours.Pink;
        BorderThickness = 2.4f;
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        BorderColour = destructive
            ? HomeControlColours.Pink
            : HomeControlColours.Navy;
        BorderThickness = 1.2f;
    }
}
