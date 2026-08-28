using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using Yokko.Game.Configuration;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

internal partial class AccessibilitySettingsPanel
    : CompositeDrawable, ISettingsSearchTarget
{
    private readonly YokkoAccessibilitySettings settings;
    private readonly SettingsContentScrollContainer contentScroll;
    private readonly Container contentRoot;
    private readonly List<SettingsSegmentedChoiceButton> textScaleButtons = new();

    public AccessibilitySettingsPanel(YokkoAccessibilitySettings settings)
    {
        this.settings = settings
            ?? throw new ArgumentNullException(nameof(settings));
        RelativeSizeAxes = Axes.Both;

        var content = contentRoot = new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Children = new Drawable[]
            {
                SettingsChrome.CreateHeader(
                    YokkoStrings.Get("settings.accessibility.title"),
                    YokkoStrings.Get("settings.accessibility.subtitle"),
                    FontAwesome.Solid.UniversalAccess,
                    (int)SettingsPageKind.Accessibility + 1),
                SettingsChrome.CreateDivider(174),
                SettingsChrome.CreateSettingRow(
                    236,
                    YokkoStrings.Get("settings.accessibility.reduce_motion"),
                    new SettingsBooleanToggle(
                        settings.ReduceMotion,
                        "settings.accessibility.reduce_motion_on",
                        "settings.accessibility.reduce_motion_off")),
                SettingsChrome.CreateDivider(298),
                SettingsChrome.CreateSettingRow(
                    304,
                    YokkoStrings.Get("settings.accessibility.high_contrast"),
                    new SettingsBooleanToggle(
                        settings.HighContrast,
                        "settings.accessibility.high_contrast_on",
                        "settings.accessibility.high_contrast_off")),
                SettingsChrome.CreateDivider(366),
                SettingsChrome.CreateSettingRow(
                    372,
                    YokkoStrings.Get("settings.accessibility.text_scale"),
                    createTextScaleControl()),
            },
        };

        InternalChild = contentScroll = new SettingsContentScrollContainer
        {
            RelativeSizeAxes = Axes.Both,
            Child = content,
        };

        settings.TextScalePercent.BindValueChanged(onTextScaleChanged, true);
    }

    public bool TryFocusSearchItem(string itemId) =>
        SettingsSearchScroll.TryFocus(
            SettingsPageKind.Accessibility,
            itemId,
            contentScroll,
            contentRoot);

    private Drawable createTextScaleControl()
    {
        var options = new[]
        {
            (90, YokkoStrings.Get("settings.accessibility.text_scale_90")),
            (100, YokkoStrings.Get("settings.accessibility.text_scale_100")),
            (110, YokkoStrings.Get("settings.accessibility.text_scale_110")),
        };

        float width = SettingsChrome.ControlWidth / options.Length;
        foreach ((int percent, LocalisableString label) in options)
        {
            int captured = percent;
            textScaleButtons.Add(new SettingsSegmentedChoiceButton(
                label,
                FontAwesome.Solid.TextHeight,
                () => settings.TextScalePercent.Value = captured,
                width)
            {
                Value = percent,
            });
        }

        refreshTextScaleSelection();
        return SettingsChrome.CreateSegmentedControl(
            textScaleButtons.Cast<Drawable>());
    }

    private void refreshTextScaleSelection()
    {
        foreach (SettingsSegmentedChoiceButton button in textScaleButtons)
        {
            button.SetSelected(button.Value is int value
                && value == settings.TextScalePercent.Value);
        }
    }

    private void onTextScaleChanged(ValueChangedEvent<int> _) =>
        refreshTextScaleSelection();

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            settings.TextScalePercent.ValueChanged -= onTextScaleChanged;

        base.Dispose(isDisposing);
    }
}
