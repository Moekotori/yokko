using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
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
    private readonly List<SettingsSegmentedChoiceButton> displayButtons = new();
    private readonly Container displayControlHost;
    private readonly SpriteText statusMetadata;
    private readonly Container backgroundFrameRateRow;
    private readonly Drawable postFrameRateDivider;
    private readonly Container backgroundAudioRow;
    private readonly Drawable postAudioDivider;
    private readonly Container fullscreenDisplayRow;
    private readonly SettingsBooleanToggle dynamicFrameRateToggle;

    internal bool FastAltTabEnabled => displaySettings.FastAltTab.Value;
    internal double BackgroundFrameRate =>
        displaySettings.BackgroundFrameRate.Value;
    internal double BackgroundAudioVolume =>
        audioSettings.BackgroundVolume.Value;
    internal bool DynamicBackgroundFrameRateEnabled =>
        displaySettings.DynamicBackgroundFrameRate.Value;
    internal bool IsBackgroundFrameRateVisible =>
        backgroundFrameRateRow.Alpha > 0.99f;
    internal SettingsBooleanToggle DynamicFrameRateToggle =>
        dynamicFrameRateToggle;

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
        backgroundFrameRateRow = SettingsChrome.CreateSettingRow(
            402,
            YokkoStrings.Get("settings.desktop.background_fps"),
            new DesktopSettingsSlider(
                displaySettings.BackgroundFrameRate,
                DesktopSettingsSliderKind.FrameRate));
        postFrameRateDivider = SettingsChrome.CreateDivider(462);
        backgroundAudioRow = SettingsChrome.CreateSettingRow(
            466,
            YokkoStrings.Get("settings.desktop.background_audio"),
            new DesktopSettingsSlider(
                audioSettings.BackgroundVolume,
                DesktopSettingsSliderKind.Percentage));
        postAudioDivider = SettingsChrome.CreateDivider(526);
        fullscreenDisplayRow = SettingsChrome.CreateSettingRow(
            530,
            YokkoStrings.Get("settings.desktop.fullscreen_display"),
            displayControlHost);
        dynamicFrameRateToggle = new SettingsBooleanToggle(
            displaySettings.DynamicBackgroundFrameRate,
            "settings.desktop.enabled",
            "settings.desktop.disabled");
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
                new SettingsBooleanToggle(
                    displaySettings.FastAltTab,
                    "settings.desktop.enabled",
                    "settings.desktop.disabled")),
            SettingsChrome.CreateDivider(334),
            SettingsChrome.CreateSettingRow(
                338,
                YokkoStrings.Get("settings.desktop.dynamic_fps"),
                dynamicFrameRateToggle),
            SettingsChrome.CreateDivider(398),
            backgroundFrameRateRow,
            postFrameRateDivider,
            backgroundAudioRow,
            postAudioDivider,
            fullscreenDisplayRow,
        };

        rebuildDisplayControls();
        refreshSelection();
        refreshDynamicFrameRateLayout(false);

        displaySettings.FastAltTab.BindValueChanged(onPreferenceChanged);
        displaySettings.DynamicBackgroundFrameRate.BindValueChanged(
            onDynamicBackgroundFrameRateChanged);
        displaySettings.BackgroundFrameRate.BindValueChanged(onPreferenceChanged);
        audioSettings.BackgroundVolume.BindValueChanged(onPreferenceChanged);
        window?.CurrentDisplayBindable.BindValueChanged(onDisplayChanged);
        window?.CurrentDisplayMode.BindValueChanged(onDisplayModeChanged);
        if (window != null)
            window.DisplaysChanged += onDisplaysChanged;
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
    }

    private void selectDisplay(Display display)
    {
        if (window == null)
            return;

        window.CurrentDisplayBindable.Value = display;
        refreshSelection();
    }

    private void onPreferenceChanged<T>(ValueChangedEvent<T> _) =>
        refreshSelection();

    private void onDynamicBackgroundFrameRateChanged(
        ValueChangedEvent<bool> _) =>
        refreshDynamicFrameRateLayout(true);

    private void refreshDynamicFrameRateLayout(bool animate)
    {
        bool enabled = displaySettings.DynamicBackgroundFrameRate.Value;
        float audioY = enabled ? 466 : 402;
        float audioDividerY = enabled ? 526 : 462;
        float displayY = enabled ? 530 : 466;

        backgroundFrameRateRow.ClearTransforms();
        postFrameRateDivider.ClearTransforms();
        backgroundAudioRow.ClearTransforms();
        postAudioDivider.ClearTransforms();
        fullscreenDisplayRow.ClearTransforms();

        if (!animate)
        {
            backgroundFrameRateRow.Alpha = enabled ? 1 : 0;
            backgroundFrameRateRow.Y = 402;
            postFrameRateDivider.Alpha = enabled ? 1 : 0;
            backgroundAudioRow.Y = audioY;
            postAudioDivider.Y = audioDividerY;
            fullscreenDisplayRow.Y = displayY;
            return;
        }

        if (enabled)
        {
            backgroundFrameRateRow.Y = 392;
            backgroundFrameRateRow.Alpha = 0;
            backgroundFrameRateRow
                .FadeIn(150, Easing.OutQuint)
                .MoveToY(402, 190, Easing.OutQuint);
            postFrameRateDivider.FadeIn(150, Easing.OutQuint);
        }
        else
        {
            backgroundFrameRateRow.FadeOut(110, Easing.OutQuint);
            postFrameRateDivider.FadeOut(110, Easing.OutQuint);
        }

        backgroundAudioRow.MoveToY(audioY, 190, Easing.OutQuint);
        postAudioDivider.MoveToY(audioDividerY, 190, Easing.OutQuint);
        fullscreenDisplayRow.MoveToY(displayY, 190, Easing.OutQuint);
    }

    private void onDisplayChanged(ValueChangedEvent<Display> _) =>
        refreshSelection();

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

        foreach (SettingsSegmentedChoiceButton button in displayButtons)
            button.SetSelected(button.Value is int index
                               && index == display?.Index);

    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            displaySettings.FastAltTab.ValueChanged -= onPreferenceChanged;
            displaySettings.DynamicBackgroundFrameRate.ValueChanged -=
                onDynamicBackgroundFrameRateChanged;
            displaySettings.BackgroundFrameRate.ValueChanged -= onPreferenceChanged;
            audioSettings.BackgroundVolume.ValueChanged -= onPreferenceChanged;
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

internal enum DesktopSettingsSliderKind
{
    FrameRate,
    Percentage,
}

internal partial class DesktopSettingsSlider : CompositeDrawable
{
    private const float track_x = 18;
    private const float track_y = 38;
    private const float track_width = SettingsChrome.ControlWidth - track_x * 2;
    private const int frame_rate_regular_step_count =
        (int)((YokkoDisplaySettings.MaximumBackgroundFrameRate
               - YokkoDisplaySettings.MinimumBackgroundFrameRate)
              / YokkoDisplaySettings.BackgroundFrameRateStep);
    private const int frame_rate_unlimited_index =
        frame_rate_regular_step_count + 1;

    private readonly Bindable<double> value;
    private readonly DesktopSettingsSliderKind kind;
    private readonly Box track;
    private readonly Box fill;
    private readonly Circle knob;
    private readonly SpriteText valueText;

    public override bool AcceptsFocus => true;

    internal DesktopSettingsSlider(
        Bindable<double> value,
        DesktopSettingsSliderKind kind)
    {
        this.value = value;
        this.kind = kind;
        Size = new Vector2(SettingsChrome.ControlWidth, 54);

        InternalChildren = new Drawable[]
        {
            valueText = new SpriteText
            {
                Position = new Vector2(track_x, 5),
                Font = HomeTypography.Display(18),
                Colour = HomeControlColours.Navy,
            },
            track = new Box
            {
                Position = new Vector2(track_x, track_y),
                Size = new Vector2(track_width, 5),
                Colour = SettingsTheme.Divider,
            },
            fill = new Box
            {
                Position = new Vector2(track_x, track_y),
                Height = 5,
                Colour = HomeControlColours.Pink,
            },
            knob = new Circle
            {
                Origin = Anchor.Centre,
                Position = new Vector2(track_x, track_y + 2.5f),
                Size = new Vector2(15),
                Colour = Color4.White,
                BorderThickness = 2.5f,
                BorderColour = HomeControlColours.Pink,
            },
        };

        value.BindValueChanged(onValueChanged, true);
    }

    internal static double FrameRateFromProgress(double progress)
    {
        int index = (int)Math.Round(
            Math.Clamp(progress, 0, 1) * frame_rate_unlimited_index);
        return index >= frame_rate_unlimited_index
            ? YokkoDisplaySettings.UnlimitedBackgroundFrameRate
            : YokkoDisplaySettings.MinimumBackgroundFrameRate
              + index * YokkoDisplaySettings.BackgroundFrameRateStep;
    }

    internal static double FrameRateProgress(double frameRate)
    {
        if (frameRate <= YokkoDisplaySettings.UnlimitedBackgroundFrameRate)
            return 1;

        double snapped = snapFrameRate(frameRate);
        double index = (snapped
                        - YokkoDisplaySettings.MinimumBackgroundFrameRate)
                       / YokkoDisplaySettings.BackgroundFrameRateStep;
        return index / frame_rate_unlimited_index;
    }

    internal static double PercentageFromProgress(double progress) =>
        Math.Round(Math.Clamp(progress, 0, 1) * 100) / 100;

    internal static double AdjustFrameRate(double frameRate, int direction)
    {
        int index = frameRate <= YokkoDisplaySettings.UnlimitedBackgroundFrameRate
            ? frame_rate_unlimited_index
            : (int)Math.Round(
                (snapFrameRate(frameRate)
                 - YokkoDisplaySettings.MinimumBackgroundFrameRate)
                / YokkoDisplaySettings.BackgroundFrameRateStep);
        index = Math.Clamp(index + Math.Sign(direction), 0, frame_rate_unlimited_index);
        return index == frame_rate_unlimited_index
            ? YokkoDisplaySettings.UnlimitedBackgroundFrameRate
            : YokkoDisplaySettings.MinimumBackgroundFrameRate
              + index * YokkoDisplaySettings.BackgroundFrameRateStep;
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button != MouseButton.Left)
            return false;

        updateFrom(ToLocalSpace(e.ScreenSpaceMousePosition).X);
        return true;
    }

    protected override bool OnDragStart(DragStartEvent e) => true;

    protected override void OnDrag(DragEvent e) =>
        updateFrom(ToLocalSpace(e.ScreenSpaceMousePosition).X);

    protected override bool OnScroll(ScrollEvent e)
    {
        if (e.ScrollDelta.Y == 0)
            return false;

        value.Value = kind == DesktopSettingsSliderKind.FrameRate
            ? AdjustFrameRate(value.Value, Math.Sign(e.ScrollDelta.Y))
            : PercentageFromProgress(
                value.Value + Math.Sign(e.ScrollDelta.Y) * 0.05);
        return true;
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        int direction = e.Key switch
        {
            Key.Left or Key.Down => -1,
            Key.Right or Key.Up => 1,
            _ => 0,
        };

        if (direction != 0)
        {
            value.Value = kind == DesktopSettingsSliderKind.FrameRate
                ? AdjustFrameRate(value.Value, direction)
                : PercentageFromProgress(
                    value.Value + direction * (e.ShiftPressed ? 0.05 : 0.01));
            return true;
        }

        if (e.Key is not Key.Home and not Key.End)
            return base.OnKeyDown(e);

        value.Value = e.Key == Key.Home
            ? kind == DesktopSettingsSliderKind.FrameRate
                ? YokkoDisplaySettings.MinimumBackgroundFrameRate
                : 0
            : kind == DesktopSettingsSliderKind.FrameRate
                ? YokkoDisplaySettings.UnlimitedBackgroundFrameRate
                : 1;
        return true;
    }

    protected override bool OnHover(HoverEvent e)
    {
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
        knob.ScaleTo(1.18f, 100, Easing.OutQuint);
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        valueText.FadeColour(HomeControlColours.Navy, 100, Easing.OutQuint);
        knob.BorderColour = HomeControlColours.Pink;
        knob.ScaleTo(1, 100, Easing.OutQuint);
    }

    private void updateFrom(float localX)
    {
        double progress = (localX - track_x) / track_width;
        value.Value = kind == DesktopSettingsSliderKind.FrameRate
            ? FrameRateFromProgress(progress)
            : PercentageFromProgress(progress);
    }

    private void onValueChanged(ValueChangedEvent<double> change)
    {
        double snapped = kind == DesktopSettingsSliderKind.FrameRate
            ? change.NewValue <= YokkoDisplaySettings.UnlimitedBackgroundFrameRate
                ? YokkoDisplaySettings.UnlimitedBackgroundFrameRate
                : snapFrameRate(change.NewValue)
            : PercentageFromProgress(change.NewValue);
        if (snapped != change.NewValue)
        {
            value.Value = snapped;
            return;
        }

        float progress = (float)(kind == DesktopSettingsSliderKind.FrameRate
            ? FrameRateProgress(snapped)
            : snapped);
        fill.Width = progress * track_width;
        knob.X = track_x + progress * track_width;
        valueText.Text = kind == DesktopSettingsSliderKind.FrameRate
            ? snapped <= YokkoDisplaySettings.UnlimitedBackgroundFrameRate
                ? "MAX"
                : $"{snapped:0} FPS"
            : $"{snapped * 100:0}%";
    }

    private static double snapFrameRate(double frameRate) =>
        Math.Clamp(
            Math.Round(
                (frameRate - YokkoDisplaySettings.MinimumBackgroundFrameRate)
                / YokkoDisplaySettings.BackgroundFrameRateStep)
            * YokkoDisplaySettings.BackgroundFrameRateStep
            + YokkoDisplaySettings.MinimumBackgroundFrameRate,
            YokkoDisplaySettings.MinimumBackgroundFrameRate,
            YokkoDisplaySettings.MaximumBackgroundFrameRate);

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            value.ValueChanged -= onValueChanged;

        base.Dispose(isDisposing);
    }
}

internal partial class DesktopShortcutHint : CompositeDrawable
{
    internal DesktopShortcutHint(
        string text,
        IconUsage icon,
        float width = SettingsChrome.ControlWidth)
    {
        Size = new Vector2(width, SettingsChrome.ControlHeight);
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
