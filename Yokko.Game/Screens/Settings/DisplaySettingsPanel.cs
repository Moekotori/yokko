using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

/// <summary>
/// Display-specific presentation and state binding. This panel can be replaced by
/// another category without changing the settings shell.
/// </summary>
internal partial class DisplaySettingsPanel : CompositeDrawable, ISettingsTransientUi
{
    internal enum WindowAspectRatio
    {
        Normal16By9,
        Ratio16By10,
        Ratio4By3,
        Ultrawide21By9,
    }

    internal const WindowAspectRatio DefaultAspectRatio =
        WindowAspectRatio.Normal16By9;

    // User-selectable physical window sizes, independent from the shared
    // logical 1920x1080 layout reference.
    private static readonly IReadOnlyDictionary<WindowAspectRatio, Size[]>
        supportedResolutions =
            new Dictionary<WindowAspectRatio, Size[]>
            {
                [WindowAspectRatio.Normal16By9] =
                [
                    new(1280, 720),
                    new(1366, 768),
                    new(1600, 900),
                    new(1920, 1080),
                    new(2560, 1440),
                ],
                [WindowAspectRatio.Ratio16By10] =
                [
                    new(1280, 800),
                    new(1440, 900),
                    new(1680, 1050),
                    new(1920, 1200),
                    new(2560, 1600),
                ],
                [WindowAspectRatio.Ratio4By3] =
                [
                    new(1024, 768),
                    new(1280, 960),
                    new(1440, 1080),
                    new(1600, 1200),
                    new(1920, 1440),
                ],
                [WindowAspectRatio.Ultrawide21By9] =
                [
                    new(1280, 540),
                    new(1680, 720),
                    new(2560, 1080),
                    new(3440, 1440),
                    new(5120, 2160),
                ],
            };

    private static readonly YokkoFrameLimit[] supportedFrameLimits =
    {
        YokkoFrameLimit.Auto,
        YokkoFrameLimit.VSync,
        YokkoFrameLimit.Limit2x,
        YokkoFrameLimit.Limit4x,
        YokkoFrameLimit.Limit8x,
        YokkoFrameLimit.Unlimited,
    };

    private readonly Bindable<Size> windowedSize;
    private readonly Bindable<WindowMode> windowMode;
    private readonly Bindable<YokkoUiScale> uiScale;
    private readonly Bindable<YokkoFrameLimit> frameLimit;
    private readonly IBindable<DisplayMode> currentDisplayMode;
    private readonly Action<Size> setWindowedSize;
    private readonly Action<WindowMode> setWindowMode;
    private readonly List<SettingsSegmentedChoiceButton> modeButtons = new();
    private readonly List<SettingsSegmentedChoiceButton> scaleButtons = new();
    private readonly List<SettingsAspectRatioChoiceButton>
        aspectRatioButtons = new();
    private readonly List<SettingsFrameLimitChoiceButton> frameLimitButtons = new();

    private readonly SpriteText currentDisplayMetadata;
    private readonly SettingsResolutionDropdown resolutionDropdown;

    internal bool IsResolutionMenuOpen => resolutionDropdown.IsOpen;
    internal bool IsResolutionSelectionEnabled => resolutionDropdown.IsEnabled;
    internal Size DisplayedResolution => resolutionDropdown.SelectedSize;
    internal YokkoUiScale CurrentUiScale => uiScale.Value;

    internal static bool IsFrameLimitSelectable(YokkoFrameLimit limit) =>
        supportedFrameLimits.Contains(limit);

    public DisplaySettingsPanel(
        Bindable<Size> windowedSize,
        Bindable<WindowMode> windowMode,
        Bindable<YokkoUiScale> uiScale,
        Bindable<YokkoFrameLimit> frameLimit,
        BindableBool showPerformanceReadout,
        IBindable<DisplayMode> currentDisplayMode,
        Action<Size> setWindowedSize,
        Action<WindowMode> setWindowMode)
    {
        this.windowedSize = windowedSize;
        this.windowMode = windowMode;
        this.uiScale = uiScale;
        this.frameLimit = frameLimit;
        this.currentDisplayMode = currentDisplayMode;
        this.setWindowedSize = setWindowedSize;
        this.setWindowMode = setWindowMode;
        RelativeSizeAxes = Axes.Both;

        WindowAspectRatio initialAspect = GetAspectRatio(windowedSize.Value);
        resolutionDropdown = new SettingsResolutionDropdown(
            GetSupportedResolutions(initialAspect),
            setWindowedSize,
            286);

        InternalChildren = new Drawable[]
        {
            SettingsChrome.CreateHeader(
                YokkoStrings.Get("settings.display.title"),
                YokkoStrings.Get("settings.display.subtitle"),
                FontAwesome.Solid.Desktop,
                2),
            SettingsChrome.CreateStatusCard(
                174,
                FontAwesome.Solid.Desktop,
                YokkoStrings.Get("settings.display.current_display"),
                FontAwesome.Solid.Heartbeat,
                out currentDisplayMetadata),
            SettingsChrome.CreateDivider(270),
            SettingsChrome.CreateSettingRow(276, YokkoStrings.Get("settings.display.window_mode"), createModeControl()),
            SettingsChrome.CreateDivider(337),
            SettingsChrome.CreateSettingRow(
                338,
                YokkoStrings.Get("settings.display.ratio_resolution"),
                createGeometryControl(),
                -10),
            SettingsChrome.CreateDivider(399),
            SettingsChrome.CreateSettingRow(400, YokkoStrings.Get("settings.display.frame_limit"), createFrameLimitControl()),
            SettingsChrome.CreateDivider(461),
            SettingsChrome.CreateSettingRow(462, YokkoStrings.Get("settings.display.interface_scale"), createScaleControl()),
            SettingsChrome.CreateDivider(523),
            SettingsChrome.CreateSettingRow(
                524,
                YokkoStrings.Get("settings.display.performance_readout"),
                new SettingsBooleanToggle(showPerformanceReadout)),
        };

        windowedSize.BindValueChanged(onWindowedSizeChanged, true);
        windowMode.BindValueChanged(onWindowModeChanged, true);
        uiScale.BindValueChanged(onUiScaleChanged, true);
        frameLimit.BindValueChanged(onFrameLimitChanged, true);
        currentDisplayMode.BindValueChanged(onCurrentDisplayModeChanged, true);
    }

    private Drawable createModeControl()
    {
        var options = new[]
        {
            (WindowMode.Windowed, YokkoStrings.Get("settings.display.windowed"), FontAwesome.Solid.WindowMaximize),
            (WindowMode.Borderless, YokkoStrings.Get("settings.display.borderless"), FontAwesome.Solid.Expand),
            (WindowMode.Fullscreen, YokkoStrings.Get("settings.display.fullscreen"), FontAwesome.Solid.ExpandArrowsAlt),
        };

        foreach ((WindowMode mode, LocalisableString label, IconUsage icon) in options)
        {
            WindowMode capturedMode = mode;
            modeButtons.Add(new SettingsSegmentedChoiceButton(label, icon, () => setWindowMode(capturedMode), 199)
            {
                Value = mode,
            });
        }

        return SettingsChrome.CreateSegmentedControl(modeButtons);
    }

    private Drawable createFrameLimitControl()
    {
        float buttonWidth = SettingsChrome.ControlWidth
                            / supportedFrameLimits.Length;

        foreach (YokkoFrameLimit limit in supportedFrameLimits)
        {
            YokkoFrameLimit capturedLimit = limit;
            frameLimitButtons.Add(new SettingsFrameLimitChoiceButton(
                limit,
                () => frameLimit.Value = capturedLimit,
                buttonWidth));
        }

        return SettingsChrome.CreateSegmentedControl(frameLimitButtons);
    }

    private Drawable createGeometryControl()
    {
        const float ratio_width = 302;
        const float gap = 10;
        float buttonWidth = ratio_width / 4;

        var options = new[]
        {
            (WindowAspectRatio.Normal16By9, "16:9"),
            (WindowAspectRatio.Ratio16By10, "16:10"),
            (WindowAspectRatio.Ratio4By3, "4:3"),
            (WindowAspectRatio.Ultrawide21By9, "21:9"),
        };

        foreach ((WindowAspectRatio ratio, string label) in options)
        {
            WindowAspectRatio capturedRatio = ratio;
            aspectRatioButtons.Add(new SettingsAspectRatioChoiceButton(
                label,
                () => SelectAspectRatio(capturedRatio),
                buttonWidth)
            {
                Value = ratio,
            });
        }

        var ratioCard = new SettingsStickerCard(
            new Vector2(ratio_width, SettingsChrome.ControlHeight),
            8);
        ratioCard.SetContent(new FillFlowContainer
        {
            RelativeSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Children = aspectRatioButtons.ToArray(),
        });

        return new Container
        {
            Size = new Vector2(SettingsChrome.ControlWidth, SettingsChrome.ControlHeight),
            Children = new Drawable[]
            {
                ratioCard,
                new Container
                {
                    X = ratio_width + gap,
                    Size = new Vector2(286, SettingsChrome.ControlHeight),
                    Child = resolutionDropdown,
                },
            },
        };
    }

    private Drawable createScaleControl()
    {
        var options = new[]
        {
            (YokkoUiScale.Compact, FontAwesome.Solid.List),
            (YokkoUiScale.Comfortable, FontAwesome.Solid.Bars),
            (YokkoUiScale.Large, FontAwesome.Solid.ThList),
        };

        foreach ((YokkoUiScale scale, IconUsage icon) in options)
        {
            YokkoUiScale capturedScale = scale;
            scaleButtons.Add(new SettingsSegmentedChoiceButton(
                $"{YokkoDisplaySettings.GetScalePercentage(scale)}%",
                icon,
                () => uiScale.Value = capturedScale,
                199)
            {
                Value = scale,
            });
        }

        return SettingsChrome.CreateSegmentedControl(scaleButtons);
    }

    private void onWindowedSizeChanged(ValueChangedEvent<Size> _) => refreshSelection();

    private void onWindowModeChanged(ValueChangedEvent<WindowMode> _) => refreshSelection();

    private void onUiScaleChanged(ValueChangedEvent<YokkoUiScale> _) => refreshSelection();

    private void onFrameLimitChanged(ValueChangedEvent<YokkoFrameLimit> _) => refreshSelection();

    private void onCurrentDisplayModeChanged(ValueChangedEvent<DisplayMode> _) => refreshSelection();

    internal void ToggleResolutionMenu() => resolutionDropdown.Toggle();

    public bool DismissTransientUi() => resolutionDropdown.Dismiss();

    private void refreshSelection()
    {
        DisplayMode displayMode = currentDisplayMode.Value;
        Size displaySize = displayMode.Size.Width > 0 && displayMode.Size.Height > 0
            ? displayMode.Size
            : windowedSize.Value;
        float refreshRate = displayMode.RefreshRate > 0 ? displayMode.RefreshRate : 60;

        currentDisplayMetadata.Text = YokkoStrings.Get(
            "settings.display.metadata",
            displayMode.DisplayIndex + 1,
            displaySize.Width,
            displaySize.Height,
            FormatRefreshRate(refreshRate));

        bool canChooseResolution = CanChooseResolution(windowMode.Value);
        resolutionDropdown.SetEnabled(canChooseResolution);
        Size displayedResolution = GetDisplayedResolution(
            windowMode.Value,
            windowedSize.Value,
            displaySize);
        WindowAspectRatio selectedAspect = GetAspectRatio(
            windowedSize.Value);
        resolutionDropdown.SetOptions(
            GetSupportedResolutions(selectedAspect));
        resolutionDropdown.SetSelected(displayedResolution);

        foreach (SettingsAspectRatioChoiceButton button
                 in aspectRatioButtons)
        {
            button.SetSelected(button.Value == selectedAspect);
            button.SetEnabled(canChooseResolution);
        }

        foreach (SettingsSegmentedChoiceButton button in modeButtons)
            button.SetSelected(button.Value is WindowMode mode && mode == windowMode.Value);

        foreach (SettingsSegmentedChoiceButton button in scaleButtons)
            button.SetSelected(button.Value is YokkoUiScale scale && scale == uiScale.Value);

        foreach (SettingsFrameLimitChoiceButton button in frameLimitButtons)
        {
            button.SetLabels(
                FormatFrameLimitMode(button.Value),
                FormatFrameLimit(button.Value, refreshRate));
            button.SetSelected(button.Value == frameLimit.Value);
        }

    }

    internal static string FormatFrameLimit(YokkoFrameLimit limit, float refreshRate)
    {
        YokkoFrameRates rates = YokkoFrameRateLimits.Calculate(
            limit,
            refreshRate);

        if (limit == YokkoFrameLimit.VSync)
            return $"{Math.Round(rates.MaximumDrawHz):0} Hz";

        return $"{Math.Round(rates.MaximumDrawHz):0} FPS";
    }

    internal static string FormatFrameLimitMode(YokkoFrameLimit limit) =>
        limit switch
        {
            YokkoFrameLimit.VSync => "V-SYNC",
            YokkoFrameLimit.Limit2x => "2×",
            YokkoFrameLimit.Limit4x => "4×",
            YokkoFrameLimit.Limit8x => "8×",
            YokkoFrameLimit.Unlimited => "MAX",
            YokkoFrameLimit.Auto => "AUTO",
            _ => throw new ArgumentOutOfRangeException(nameof(limit)),
        };

    internal static string FormatRefreshRate(float refreshRate) =>
        $"{MathF.Round(MathF.Max(refreshRate, 1)):0}";

    internal static bool CanChooseResolution(WindowMode mode) =>
        mode == WindowMode.Windowed;

    internal static IReadOnlyList<Size> GetSupportedResolutions(
        WindowAspectRatio ratio) =>
        supportedResolutions[ratio];

    internal static WindowAspectRatio GetAspectRatio(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
            return DefaultAspectRatio;

        double aspect = size.Width / (double)size.Height;
        var candidates = new[]
        {
            (WindowAspectRatio.Normal16By9, 16d / 9d),
            (WindowAspectRatio.Ratio16By10, 16d / 10d),
            (WindowAspectRatio.Ratio4By3, 4d / 3d),
            (WindowAspectRatio.Ultrawide21By9, 21d / 9d),
        };

        return candidates
            .OrderBy(candidate => Math.Abs(aspect - candidate.Item2))
            .First().Item1;
    }

    internal static Size ChooseResolutionForAspect(
        WindowAspectRatio ratio,
        Size currentSize)
    {
        IReadOnlyList<Size> options = GetSupportedResolutions(ratio);
        int targetHeight = currentSize.Height > 0
            ? currentSize.Height
            : 720;
        return options
            .OrderBy(option => Math.Abs(option.Height - targetHeight))
            .ThenBy(option => Math.Abs(option.Width - currentSize.Width))
            .First();
    }

    internal void SelectAspectRatio(WindowAspectRatio ratio) =>
        setWindowedSize(ChooseResolutionForAspect(ratio, windowedSize.Value));

    internal static Size GetDisplayedResolution(
        WindowMode mode,
        Size windowedResolution,
        Size displayResolution) =>
        CanChooseResolution(mode)
            ? windowedResolution
            : displayResolution;

    internal void SelectUiScale(YokkoUiScale scale) => uiScale.Value = scale;

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            windowedSize.ValueChanged -= onWindowedSizeChanged;
            windowMode.ValueChanged -= onWindowModeChanged;
            uiScale.ValueChanged -= onUiScaleChanged;
            frameLimit.ValueChanged -= onFrameLimitChanged;
            currentDisplayMode.ValueChanged -= onCurrentDisplayModeChanged;
        }

        base.Dispose(isDisposing);
    }
}

internal partial class SettingsBooleanToggle : ClickableContainer
{
    private readonly BindableBool value;
    private readonly Container cardBody;
    private readonly Box background;
    private readonly Box switchTrack;
    private readonly Circle switchThumb;
    private readonly SpriteText stateText;

    public override bool AcceptsFocus => true;

    public SettingsBooleanToggle(BindableBool value)
    {
        this.value = value;
        Action = () => value.Value = !value.Value;
        Size = new Vector2(598, 54);

        InternalChildren = new Drawable[]
        {
            new Container
            {
                Position = new Vector2(0, 4),
                Size = new Vector2(598, 50),
                Masking = true,
                CornerRadius = 9,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.015f, 0.045f, 0.28f, 0.2f),
                },
            },
            new Container
            {
                Position = new Vector2(-1.5f, -1.5f),
                Size = new Vector2(601, 57),
                Masking = true,
                CornerRadius = 9,
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
            cardBody = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 8,
                BorderThickness = 1.6f,
                BorderColour = HomeControlColours.Navy,
                Child = background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
            },
            stateText = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 18,
                Font = HomeTypography.Display(17),
                Colour = HomeControlColours.Navy,
            },
            new Container
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -16,
                Size = new Vector2(48, 24),
                Masking = true,
                CornerRadius = 12,
                BorderThickness = 1.5f,
                BorderColour = HomeControlColours.Navy,
                Children = new Drawable[]
                {
                    switchTrack = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = SettingsTheme.Divider,
                    },
                    switchThumb = new Circle
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.Centre,
                        X = 12,
                        Size = new Vector2(18),
                        Colour = Color4.White,
                    },
                },
            },
        };

        value.BindValueChanged(onValueChanged, true);
    }

    private void onValueChanged(ValueChangedEvent<bool> change)
    {
        switchTrack.FadeColour(
            change.NewValue
                ? HomeControlColours.Navy
                : SettingsTheme.Divider,
            160,
            Easing.OutQuint);
        switchThumb.MoveToX(
            change.NewValue ? 36 : 12,
            280,
            Easing.OutBack);
        stateText.Text = YokkoStrings.Get(
            change.NewValue
                ? "settings.display.enabled"
                : "settings.display.disabled");
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(
            SettingsTheme.PaleCyan,
            120,
            Easing.OutQuint);
        stateText.MoveToX(22, 130, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(Color4.White, 140, Easing.OutQuint);
        stateText.MoveToX(18, 150, Easing.OutQuint);
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        this.MoveToY(2, 300, Easing.OutQuint);
        return base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseUpEvent e)
    {
        this.MoveToY(0, 200, Easing.OutQuint);
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
        cardBody.BorderColour = HomeControlColours.Pink;
        cardBody.BorderThickness = 2.4f;
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        cardBody.BorderColour = HomeControlColours.Navy;
        cardBody.BorderThickness = 1.6f;
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            value.ValueChanged -= onValueChanged;

        base.Dispose(isDisposing);
    }
}
