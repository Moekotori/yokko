using System;
using System.Collections.Generic;
using System.Linq;
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
using Yokko.Core.Gameplay;
using Yokko.Game.Configuration;
using Yokko.Game.Gameplay;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

internal partial class EditorSettingsPanel
    : CompositeDrawable, ISettingsSearchTarget
{
    private static readonly IReadOnlyDictionary<string, float> search_scroll_targets =
        new Dictionary<string, float>
        {
            ["grid"] = 236,
            ["autosave"] = 354,
        };

    private readonly YokkoEditorSettings settings;
    private readonly SettingsContentScrollContainer contentScroll;
    private readonly List<SettingsSegmentedChoiceButton> keyModeButtons = new();
    private readonly SettingsIntegerStepper autosaveIntervalStepper;

    public EditorSettingsPanel(YokkoEditorSettings settings)
    {
        this.settings = settings
            ?? throw new ArgumentNullException(nameof(settings));
        RelativeSizeAxes = Axes.Both;

        autosaveIntervalStepper = new SettingsIntegerStepper(
            settings.AutosaveIntervalSeconds,
            15,
            15,
            600,
            seconds => YokkoStrings.Get(
                "settings.editor.autosave_interval_value",
                seconds));

        var content = new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Children = new Drawable[]
            {
                SettingsChrome.CreateHeader(
                    YokkoStrings.Get("settings.editor.title"),
                    YokkoStrings.Get("settings.editor.subtitle"),
                    FontAwesome.Solid.Pen,
                    (int)SettingsPageKind.Editor + 1),
                SettingsChrome.CreateDivider(228),
                SettingsChrome.CreateSettingRow(
                    180,
                    YokkoStrings.Get("settings.editor.default_key_mode"),
                    createKeyModeControl()),
                SettingsChrome.CreateDivider(236),
                SettingsChrome.CreateSettingRow(
                    236,
                    YokkoStrings.Get("settings.editor.snap_divisor"),
                    new SettingsIntegerStepper(
                        settings.SnapDivisor,
                        1,
                        1,
                        16,
                        divisor => YokkoStrings.Get(
                            "settings.editor.snap_divisor_value",
                            divisor))),
                SettingsChrome.CreateDivider(298),
                SettingsChrome.CreateSettingRow(
                    298,
                    YokkoStrings.Get("settings.editor.visible_rows"),
                    new SettingsIntegerStepper(
                        settings.VisibleRows,
                        1,
                        12,
                        64,
                        rows => YokkoStrings.Get(
                            "settings.editor.visible_rows_value",
                            rows))),
                SettingsChrome.CreateDivider(354),
                SettingsChrome.CreateSettingRow(
                    354,
                    YokkoStrings.Get("settings.editor.autosave"),
                    new SettingsBooleanToggle(
                        settings.AutosaveEnabled,
                        "settings.editor.autosave_on",
                        "settings.editor.autosave_off")),
                SettingsChrome.CreateDivider(416),
                SettingsChrome.CreateSettingRow(
                    416,
                    YokkoStrings.Get("settings.editor.autosave_interval"),
                    autosaveIntervalStepper),
            },
        };

        InternalChild = contentScroll = new SettingsContentScrollContainer
        {
            RelativeSizeAxes = Axes.Both,
            Child = content,
        };

        settings.DefaultKeyMode.BindValueChanged(onKeyModeChanged, true);
        settings.AutosaveEnabled.BindValueChanged(onAutosaveChanged, true);
        refreshKeyModeSelection();
        refreshAutosaveLayout();
    }

    public bool TryFocusSearchItem(string itemId)
    {
        if (!search_scroll_targets.TryGetValue(itemId, out float y))
            return false;

        contentScroll.ScrollTo(y, true);
        return true;
    }

    private Drawable createKeyModeControl()
    {
        var options = new[]
        {
            (KeyMode.FourKey, YokkoStrings.Get("settings.editor.key_mode_4k"), FontAwesome.Solid.Th),
            (KeyMode.SevenKey, YokkoStrings.Get("settings.editor.key_mode_7k"), FontAwesome.Solid.ThList),
        };

        float width = SettingsChrome.ControlWidth / options.Length;
        foreach ((KeyMode mode, LocalisableString label, IconUsage icon) in options)
        {
            KeyMode captured = mode;
            keyModeButtons.Add(new SettingsSegmentedChoiceButton(
                label,
                icon,
                () => settings.DefaultKeyMode.Value = captured,
                width)
            {
                Value = mode,
            });
        }

        return SettingsChrome.CreateSegmentedControl(keyModeButtons.Cast<Drawable>());
    }

    private void refreshKeyModeSelection()
    {
        foreach (SettingsSegmentedChoiceButton button in keyModeButtons)
        {
            button.SetSelected(button.Value is KeyMode mode
                && mode == settings.DefaultKeyMode.Value);
        }
    }

    private void refreshAutosaveLayout() =>
        autosaveIntervalStepper.SetEnabled(settings.AutosaveEnabled.Value);

    private void onKeyModeChanged(ValueChangedEvent<KeyMode> _) =>
        refreshKeyModeSelection();

    private void onAutosaveChanged(ValueChangedEvent<bool> _) =>
        refreshAutosaveLayout();

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            settings.DefaultKeyMode.ValueChanged -= onKeyModeChanged;
            settings.AutosaveEnabled.ValueChanged -= onAutosaveChanged;
        }

        base.Dispose(isDisposing);
    }
}

internal partial class SettingsIntegerStepper : CompositeDrawable
{
    private readonly Bindable<int> value;
    private readonly int step;
    private readonly int minimum;
    private readonly int maximum;
    private readonly Func<int, LocalisableString> formatter;
    private readonly SpriteText valueText;
    private bool isEnabled = true;

    public override bool AcceptsFocus => isEnabled;

    public SettingsIntegerStepper(
        Bindable<int> value,
        int step,
        int minimum,
        int maximum,
        Func<int, LocalisableString> formatter)
    {
        this.value = value;
        this.step = step;
        this.minimum = minimum;
        this.maximum = maximum;
        this.formatter = formatter;
        Size = new Vector2(SettingsChrome.ControlWidth, 54);

        InternalChildren = new Drawable[]
        {
            new Container
            {
                Position = new Vector2(0, 4),
                Size = new Vector2(SettingsChrome.ControlWidth, 50),
                Masking = true,
                CornerRadius = 8,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.015f, 0.045f, 0.28f, 0.2f),
                },
            },
            new Container
            {
                Position = new Vector2(-1.5f, -1.5f),
                Size = new Vector2(SettingsChrome.ControlWidth + 3, 57),
                Masking = true,
                CornerRadius = 8,
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
            new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 7,
                BorderThickness = 1.6f,
                BorderColour = HomeControlColours.Navy,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
            },
            createButton(FontAwesome.Solid.Minus, Anchor.CentreLeft, -step),
            valueText = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Font = HomeTypography.Display(20),
                Colour = HomeControlColours.Navy,
            },
            createButton(FontAwesome.Solid.Plus, Anchor.CentreRight, step),
        };

        value.BindValueChanged(onValueChanged, true);
    }

    internal void SetEnabled(bool enabled)
    {
        isEnabled = enabled;
        this.FadeTo(enabled ? 1 : 0.58f, 100, Easing.OutQuint);
    }

    private ClickableContainer createButton(
        IconUsage icon,
        Anchor anchor,
        int delta) => new ClickableContainer
    {
        Anchor = anchor,
        Origin = anchor,
        Width = 72,
        RelativeSizeAxes = Axes.Y,
        Action = () =>
        {
            if (!isEnabled)
                return;

            value.Value = Math.Clamp(value.Value + delta, minimum, maximum);
        },
        Child = new SpriteIcon
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Size = new Vector2(16),
            Icon = icon,
            Colour = HomeControlColours.Pink,
        },
    };

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (!isEnabled)
            return base.OnKeyDown(e);

        int next = e.Key switch
        {
            Key.Left or Key.Down => value.Value - step,
            Key.Right or Key.Up => value.Value + step,
            Key.Home => minimum,
            Key.End => maximum,
            _ => int.MinValue,
        };

        if (next == int.MinValue)
            return base.OnKeyDown(e);

        value.Value = Math.Clamp(next, minimum, maximum);
        return true;
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
        BorderThickness = 0;
    }

    private void onValueChanged(ValueChangedEvent<int> change) =>
        valueText.Text = formatter(Math.Clamp(change.NewValue, minimum, maximum));

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            value.ValueChanged -= onValueChanged;

        base.Dispose(isDisposing);
    }
}
