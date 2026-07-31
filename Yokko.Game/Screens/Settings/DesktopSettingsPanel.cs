using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Audio;
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

internal partial class DesktopSettingsPanel : CompositeDrawable
{
    private readonly YokkoDisplaySettings displaySettings;
    private readonly YokkoAudioSettings audioSettings;
    private readonly IWindow window;
    private readonly List<SettingsSegmentedChoiceButton> frameRateButtons = new();
    private readonly List<SettingsSegmentedChoiceButton> audioButtons = new();
    private readonly List<SettingsSegmentedChoiceButton> displayButtons = new();
    private readonly List<SettingsSegmentedChoiceButton> refreshButtons = new();
    private readonly Container displayControlHost;
    private readonly Container refreshControlHost;
    private readonly SpriteText statusMetadata;

    internal bool FastAltTabEnabled => displaySettings.FastAltTab.Value;
    internal YokkoBackgroundFrameRate BackgroundFrameRate =>
        displaySettings.BackgroundFrameRate.Value;
    internal BackgroundAudioMode BackgroundAudio =>
        audioSettings.BackgroundAudio.Value;

    internal DesktopSettingsPanel(
        YokkoDisplaySettings displaySettings,
        YokkoAudioSettings audioSettings,
        GameHost host)
    {
        this.displaySettings = displaySettings;
        this.audioSettings = audioSettings;
        window = host.Window;
        RelativeSizeAxes = Axes.Both;

        displayControlHost = new Container
        {
            Size = new Vector2(SettingsChrome.ControlWidth, SettingsChrome.ControlHeight),
        };
        refreshControlHost = new Container
        {
            Size = new Vector2(SettingsChrome.ControlWidth, SettingsChrome.ControlHeight),
        };

        InternalChildren = new Drawable[]
        {
            SettingsChrome.CreateHeader(
                YokkoStrings.Get("settings.desktop.title"),
                YokkoStrings.Get("settings.desktop.subtitle"),
                FontAwesome.Solid.Laptop,
                9),
            SettingsChrome.CreateStatusCard(
                174,
                FontAwesome.Solid.Desktop,
                YokkoStrings.Get("settings.desktop.current_output"),
                FontAwesome.Solid.Bolt,
                out statusMetadata),
            SettingsChrome.CreateDivider(270),
            SettingsChrome.CreateSettingRow(
                274,
                YokkoStrings.Get("settings.desktop.fast_alt_tab"),
                new SettingsBooleanToggle(displaySettings.FastAltTab)),
            SettingsChrome.CreateDivider(334),
            SettingsChrome.CreateSettingRow(
                338,
                YokkoStrings.Get("settings.desktop.background_fps"),
                createBackgroundFrameRateControl()),
            SettingsChrome.CreateDivider(398),
            SettingsChrome.CreateSettingRow(
                402,
                YokkoStrings.Get("settings.desktop.background_audio"),
                createBackgroundAudioControl()),
            SettingsChrome.CreateDivider(462),
            SettingsChrome.CreateSettingRow(
                466,
                YokkoStrings.Get("settings.desktop.fullscreen_display"),
                displayControlHost),
            SettingsChrome.CreateDivider(526),
            SettingsChrome.CreateSettingRow(
                530,
                YokkoStrings.Get("settings.desktop.refresh_rate"),
                refreshControlHost),
            SettingsChrome.CreateDivider(590),
            SettingsChrome.CreateSettingRow(
                594,
                YokkoStrings.Get("settings.desktop.boss_key"),
                new DesktopShortcutHint("F10", FontAwesome.Solid.WindowMinimize)),
            new SettingsPanelFooter(),
        };

        rebuildDisplayControls();
        refreshSelection();

        displaySettings.FastAltTab.BindValueChanged(onPreferenceChanged);
        displaySettings.BackgroundFrameRate.BindValueChanged(onPreferenceChanged);
        displaySettings.FullscreenRefreshRate.BindValueChanged(onPreferenceChanged);
        audioSettings.BackgroundAudio.BindValueChanged(onPreferenceChanged);
        window?.CurrentDisplayBindable.BindValueChanged(onDisplayChanged);
        window?.CurrentDisplayMode.BindValueChanged(onDisplayModeChanged);
        if (window != null)
            window.DisplaysChanged += onDisplaysChanged;
    }

    private Drawable createBackgroundFrameRateControl()
    {
        var options = new[]
        {
            (YokkoBackgroundFrameRate.Fps30, "30 FPS"),
            (YokkoBackgroundFrameRate.Fps60, "60 FPS"),
            (YokkoBackgroundFrameRate.Unlimited, "MAX"),
        };

        foreach ((YokkoBackgroundFrameRate value, string label) in options)
        {
            YokkoBackgroundFrameRate captured = value;
            frameRateButtons.Add(new SettingsSegmentedChoiceButton(
                label,
                value == YokkoBackgroundFrameRate.Unlimited
                    ? FontAwesome.Solid.Infinity
                    : FontAwesome.Solid.TachometerAlt,
                () => displaySettings.BackgroundFrameRate.Value = captured,
                SettingsChrome.ControlWidth / options.Length)
            {
                Value = value,
            });
        }

        return SettingsChrome.CreateSegmentedControl(frameRateButtons);
    }

    private Drawable createBackgroundAudioControl()
    {
        var options = new[]
        {
            (BackgroundAudioMode.KeepPlaying, "settings.desktop.audio_keep", FontAwesome.Solid.VolumeUp),
            (BackgroundAudioMode.Dim, "settings.desktop.audio_dim", FontAwesome.Solid.VolumeDown),
            (BackgroundAudioMode.Mute, "settings.desktop.audio_mute", FontAwesome.Solid.VolumeMute),
        };

        foreach ((BackgroundAudioMode value, string key, IconUsage icon) in options)
        {
            BackgroundAudioMode captured = value;
            audioButtons.Add(new SettingsSegmentedChoiceButton(
                YokkoStrings.Get(key),
                icon,
                () => audioSettings.BackgroundAudio.Value = captured,
                SettingsChrome.ControlWidth / options.Length)
            {
                Value = value,
            });
        }

        return SettingsChrome.CreateSegmentedControl(audioButtons);
    }

    private void rebuildDisplayControls()
    {
        displayButtons.Clear();
        Display[] displays = window?.Displays.ToArray() ?? [];
        if (displays.Length == 0)
        {
            displayControlHost.Child = new DesktopShortcutHint(
                "—",
                FontAwesome.Solid.Desktop);
            rebuildRefreshControls();
            return;
        }

        float width = SettingsChrome.ControlWidth / displays.Length;
        foreach (Display display in displays)
        {
            Display captured = display;
            string label = string.IsNullOrWhiteSpace(display.Name)
                ? $"DISPLAY {display.Index + 1}"
                : $"{display.Index + 1} · {display.Name}";
            displayButtons.Add(new SettingsSegmentedChoiceButton(
                label,
                FontAwesome.Solid.Desktop,
                () => selectDisplay(captured),
                width)
            {
                Value = display.Index,
            });
        }

        displayControlHost.Child =
            SettingsChrome.CreateSegmentedControl(displayButtons);
        rebuildRefreshControls();
    }

    private void rebuildRefreshControls()
    {
        refreshButtons.Clear();
        Display display = window?.CurrentDisplayBindable.Value;
        int[] rates = GetNativeRefreshRates(display);

        if (rates.Length == 0)
        {
            refreshControlHost.Child = new DesktopShortcutHint(
                "AUTO",
                FontAwesome.Solid.SyncAlt);
            return;
        }

        float width = SettingsChrome.ControlWidth / rates.Length;
        foreach (int rate in rates)
        {
            int captured = rate;
            refreshButtons.Add(new SettingsSegmentedChoiceButton(
                rate.ToString(),
                FontAwesome.Solid.SyncAlt,
                () => displaySettings.FullscreenRefreshRate.Value = captured,
                width)
            {
                Value = rate,
            });
        }

        refreshControlHost.Child =
            SettingsChrome.CreateSegmentedControl(refreshButtons);
    }

    internal static int[] GetNativeRefreshRates(Display display) =>
        display?.DisplayModes
            .Where(mode =>
                mode.Size == display.Bounds.Size
                && mode.RefreshRate > 0)
            .Select(mode => (int)MathF.Round(mode.RefreshRate))
            .Distinct()
            .OrderByDescending(rate => rate)
            .Take(6)
            .OrderBy(rate => rate)
            .ToArray() ?? [];

    private void selectDisplay(Display display)
    {
        if (window == null)
            return;

        window.CurrentDisplayBindable.Value = display;
        int[] rates = GetNativeRefreshRates(display);
        if (rates.Length > 0
            && !rates.Contains(displaySettings.FullscreenRefreshRate.Value))
        {
            displaySettings.FullscreenRefreshRate.Value = rates[^1];
        }

        rebuildRefreshControls();
        refreshSelection();
    }

    private void onPreferenceChanged<T>(ValueChangedEvent<T> _) =>
        refreshSelection();

    private void onDisplayChanged(ValueChangedEvent<Display> _)
    {
        rebuildRefreshControls();
        refreshSelection();
    }

    private void onDisplayModeChanged(ValueChangedEvent<DisplayMode> _) =>
        refreshSelection();

    private void onDisplaysChanged(IEnumerable<Display> _) =>
        Schedule(rebuildDisplayControls);

    private void refreshSelection()
    {
        Display display = window?.CurrentDisplayBindable.Value;
        DisplayMode mode = window?.CurrentDisplayMode.Value ?? default;
        statusMetadata.Text = display == null
            ? "—"
            : $"Display {display.Index + 1}  ·  {mode.Size.Width} × {mode.Size.Height}  ·  {mode.RefreshRate:0} Hz";

        foreach (SettingsSegmentedChoiceButton button in frameRateButtons)
            button.SetSelected(button.Value is YokkoBackgroundFrameRate value
                               && value == displaySettings.BackgroundFrameRate.Value);

        foreach (SettingsSegmentedChoiceButton button in audioButtons)
            button.SetSelected(button.Value is BackgroundAudioMode value
                               && value == audioSettings.BackgroundAudio.Value);

        foreach (SettingsSegmentedChoiceButton button in displayButtons)
            button.SetSelected(button.Value is int index
                               && index == display?.Index);

        int selectedRate = displaySettings.FullscreenRefreshRate.Value > 0
            ? displaySettings.FullscreenRefreshRate.Value
            : (int)MathF.Round(mode.RefreshRate);
        foreach (SettingsSegmentedChoiceButton button in refreshButtons)
            button.SetSelected(button.Value is int rate && rate == selectedRate);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            displaySettings.FastAltTab.ValueChanged -= onPreferenceChanged;
            displaySettings.BackgroundFrameRate.ValueChanged -= onPreferenceChanged;
            displaySettings.FullscreenRefreshRate.ValueChanged -= onPreferenceChanged;
            audioSettings.BackgroundAudio.ValueChanged -= onPreferenceChanged;
            if (window != null)
            {
                window.CurrentDisplayBindable.ValueChanged -= onDisplayChanged;
                window.CurrentDisplayMode.ValueChanged -= onDisplayModeChanged;
                window.DisplaysChanged -= onDisplaysChanged;
            }
        }

        base.Dispose(isDisposing);
    }
}

internal partial class DesktopShortcutHint : CompositeDrawable
{
    internal DesktopShortcutHint(string text, IconUsage icon)
    {
        Size = new Vector2(SettingsChrome.ControlWidth, SettingsChrome.ControlHeight);
        Masking = true;
        CornerRadius = 8;
        BorderThickness = 1.5f;
        BorderColour = HomeControlColours.Navy;
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
                X = 24,
                Size = new Vector2(20),
                Icon = icon,
                Colour = HomeControlColours.Pink,
            },
            new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = text,
                Font = HomeTypography.Display(19),
                Colour = HomeControlColours.Navy,
            },
        };
    }
}
