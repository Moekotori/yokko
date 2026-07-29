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
    private static readonly Size[] supportedResolutions =
    {
        new(1024, 768),
        new(1280, 720),
        new(1366, 768),
        new(1600, 900),
        new(1920, 1080),
        new(2560, 1440),
    };

    private static readonly YokkoFrameLimit[] supportedFrameLimits =
    {
        YokkoFrameLimit.RefreshRate,
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
    private readonly List<SettingsFrameLimitChoiceButton> frameLimitButtons = new();

    private readonly SpriteText currentDisplayMetadata;
    private readonly SettingsResolutionDropdown resolutionDropdown;

    internal bool IsResolutionMenuOpen => resolutionDropdown.IsOpen;
    internal bool IsResolutionSelectionEnabled => resolutionDropdown.IsEnabled;
    internal Size DisplayedResolution => resolutionDropdown.SelectedSize;
    internal YokkoUiScale CurrentUiScale => uiScale.Value;

    public DisplaySettingsPanel(
        Texture mascotTexture,
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

        InternalChildren = new Drawable[]
        {
            new SpriteText
            {
                Position = new Vector2(378, 42),
                Text = YokkoStrings.Get("settings.display.title"),
                Font = HomeTypography.Display(58),
                Spacing = new Vector2(0.45f, 0),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(378, 105),
                Text = YokkoStrings.Get("settings.display.subtitle"),
                Font = HomeTypography.Body(20),
                Spacing = new Vector2(0.2f, 0),
                Colour = SettingsTheme.MutedNavy,
            },
            createMascotCrop(mascotTexture),
            createDisplayStatus(out currentDisplayMetadata),
            createDivider(270),
            createSettingRow(276, YokkoStrings.Get("settings.display.window_mode"), createModeControl()),
            createDivider(340),
            createSettingRow(
                346,
                YokkoStrings.Get("settings.display.resolution"),
                resolutionDropdown = new SettingsResolutionDropdown(supportedResolutions, setWindowedSize),
                -10),
            createDivider(410),
            createSettingRow(416, YokkoStrings.Get("settings.display.frame_limit"), createFrameLimitControl()),
            createDivider(480),
            createSettingRow(486, YokkoStrings.Get("settings.display.interface_scale"), createScaleControl()),
            createDivider(550),
            createSettingRow(
                556,
                YokkoStrings.Get("settings.display.performance_readout"),
                new DisplayPerformanceReadoutToggle(showPerformanceReadout)),
            new SettingsPanelFooter(),
        };

        windowedSize.BindValueChanged(onWindowedSizeChanged, true);
        windowMode.BindValueChanged(onWindowModeChanged, true);
        uiScale.BindValueChanged(onUiScaleChanged, true);
        frameLimit.BindValueChanged(onFrameLimitChanged, true);
        currentDisplayMode.BindValueChanged(onCurrentDisplayModeChanged, true);
    }

    private static Drawable createMascotCrop(Texture mascotTexture) => new Container
    {
        Position = new Vector2(944, -8),
        Size = new Vector2(252, 182),
        Masking = true,
        Child = new Sprite
        {
            Position = new Vector2(0, -8),
            Size = new Vector2(250, 284),
            Texture = mascotTexture,
        },
    };

    private static Drawable createDisplayStatus(out SpriteText metadata)
    {
        var result = new Container
        {
            Position = new Vector2(378, 174),
            Size = new Vector2(840, 86),
            Masking = true,
            CornerRadius = 8,
        };

        result.Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SettingsTheme.StatusCyan,
            },
            new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 48,
                Size = new Vector2(56),
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 48,
                Size = new Vector2(26),
                Icon = FontAwesome.Solid.Desktop,
                Colour = HomeControlColours.Navy,
            },
            new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 105,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = YokkoStrings.Get("settings.display.current_display"),
                        Font = HomeTypography.Display(22),
                        Colour = HomeControlColours.Navy,
                    },
                    metadata = new SpriteText
                    {
                        Font = HomeTypography.Body(18),
                        Colour = HomeControlColours.Navy,
                    },
                },
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -34,
                Size = new Vector2(44),
                Icon = FontAwesome.Solid.Heartbeat,
                Colour = Color4.White,
            },
        };

        return result;
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

        return createSegmentedControl(modeButtons);
    }

    private Drawable createFrameLimitControl()
    {
        const float buttonWidth = 598f / 5;

        foreach (YokkoFrameLimit limit in supportedFrameLimits)
        {
            YokkoFrameLimit capturedLimit = limit;
            frameLimitButtons.Add(new SettingsFrameLimitChoiceButton(
                limit,
                () => frameLimit.Value = capturedLimit,
                buttonWidth));
        }

        return createSegmentedControl(frameLimitButtons);
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

        return createSegmentedControl(scaleButtons);
    }

    private static Drawable createSegmentedControl(IEnumerable<Drawable> buttons) => new Container
    {
        Size = new Vector2(598, 54),
        Masking = true,
        CornerRadius = 7,
        BorderThickness = 1.4f,
        BorderColour = HomeControlColours.Navy,
        Child = new FillFlowContainer
        {
            RelativeSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Children = buttons.ToArray(),
        },
    };

    private static Drawable createSettingRow(float y, LocalisableString title, Drawable control, float depth = 0) => new Container
    {
        Position = new Vector2(378, y),
        Size = new Vector2(840, 60),
        Depth = depth,
        Children = new Drawable[]
        {
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = title,
                Font = HomeTypography.Display(25),
                Colour = HomeControlColours.Navy,
            },
            new Container
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Size = new Vector2(598, 54),
                Child = control,
            },
        },
    };

    private static Drawable createDivider(float y) => new Box
    {
        Position = new Vector2(378, y),
        Width = 840,
        Height = 1,
        Colour = SettingsTheme.Divider,
    };

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
        resolutionDropdown.SetSelected(GetDisplayedResolution(
            windowMode.Value,
            windowedSize.Value,
            displaySize));

        foreach (SettingsSegmentedChoiceButton button in modeButtons)
            button.SetSelected(button.Value is WindowMode mode && mode == windowMode.Value);

        foreach (SettingsSegmentedChoiceButton button in scaleButtons)
            button.SetSelected(button.Value is YokkoUiScale scale && scale == uiScale.Value);

        foreach (SettingsFrameLimitChoiceButton button in frameLimitButtons)
        {
            button.SetLabel(FormatFrameLimit(button.Value, refreshRate));
            button.SetSelected(button.Value == frameLimit.Value);
        }

    }

    internal static string FormatFrameLimit(YokkoFrameLimit limit, float refreshRate)
    {
        if (limit == YokkoFrameLimit.Unlimited)
            return "∞";

        int multiplier = limit switch
        {
            YokkoFrameLimit.Limit2x => 2,
            YokkoFrameLimit.Limit4x => 4,
            YokkoFrameLimit.Limit8x => 8,
            _ => 1,
        };

        return $"{MathF.Round(MathF.Max(refreshRate, 1) * multiplier):0} FPS";
    }

    internal static string FormatRefreshRate(float refreshRate) =>
        $"{MathF.Round(MathF.Max(refreshRate, 1)):0}";

    internal static bool CanChooseResolution(WindowMode mode) =>
        mode == WindowMode.Windowed;

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

internal partial class DisplayPerformanceReadoutToggle : ClickableContainer
{
    private readonly BindableBool value;
    private readonly Box background;
    private readonly Box switchTrack;
    private readonly Circle switchThumb;
    private readonly SpriteText stateText;

    public DisplayPerformanceReadoutToggle(BindableBool value)
    {
        this.value = value;
        Action = () => value.Value = !value.Value;
        Size = new Vector2(598, 54);
        Masking = true;
        CornerRadius = 7;
        BorderThickness = 1.4f;
        BorderColour = HomeControlColours.Navy;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
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
            120,
            Easing.OutQuint);
        switchThumb.MoveToX(
            change.NewValue ? 36 : 12,
            120,
            Easing.OutQuint);
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
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        background.FadeColour(Color4.White, 140, Easing.OutQuint);

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            value.ValueChanged -= onValueChanged;

        base.Dispose(isDisposing);
    }
}
