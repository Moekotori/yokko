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
using osu.Framework.Localisation;
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

    private readonly Bindable<Size> windowedSize;
    private readonly Bindable<WindowMode> windowMode;
    private readonly Bindable<YokkoUiScale> uiScale;
    private readonly Action<Size> setWindowedSize;
    private readonly Action<WindowMode> setWindowMode;
    private readonly List<SettingsSegmentedChoiceButton> modeButtons = new();
    private readonly List<SettingsSegmentedChoiceButton> scaleButtons = new();

    private readonly SpriteText currentDisplayMetadata;
    private readonly SettingsResolutionDropdown resolutionDropdown;

    internal bool IsResolutionMenuOpen => resolutionDropdown.IsOpen;

    public DisplaySettingsPanel(
        Texture mascotTexture,
        Bindable<Size> windowedSize,
        Bindable<WindowMode> windowMode,
        Bindable<YokkoUiScale> uiScale,
        Action<Size> setWindowedSize,
        Action<WindowMode> setWindowMode)
    {
        this.windowedSize = windowedSize;
        this.windowMode = windowMode;
        this.uiScale = uiScale;
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
                Font = HomeTypography.Body(17),
                Spacing = new Vector2(0.2f, 0),
                Colour = SettingsTheme.MutedNavy,
            },
            createMascotCrop(mascotTexture),
            createDisplayStatus(out currentDisplayMetadata),
            createDivider(292),
            createSettingRow(310, YokkoStrings.Get("settings.display.window_mode"), createModeControl()),
            createDivider(388),
            createSettingRow(
                402,
                YokkoStrings.Get("settings.display.resolution"),
                resolutionDropdown = new SettingsResolutionDropdown(supportedResolutions, setWindowedSize),
                -10),
            createDivider(478),
            createSettingRow(492, YokkoStrings.Get("settings.display.interface_scale"), createScaleControl()),
            new SettingsPanelFooter(),
            new HomeDotCross
            {
                Position = new Vector2(1088, 594),
                Scale = new Vector2(1.1f),
            },
            createDecorationIcon(FontAwesome.Solid.Plus, 1172, 601, 16, HomeControlColours.Pink),
            createDecorationIcon(FontAwesome.Solid.Plus, 1200, 637, 12, HomeControlColours.Yellow),
        };

        windowedSize.BindValueChanged(onWindowedSizeChanged, true);
        windowMode.BindValueChanged(onWindowModeChanged, true);
        uiScale.BindValueChanged(onUiScaleChanged, true);
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
                        Font = HomeTypography.Display(19),
                        Colour = HomeControlColours.Navy,
                    },
                    metadata = new SpriteText
                    {
                        Font = HomeTypography.Body(15),
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

    private Drawable createScaleControl()
    {
        var options = new[]
        {
            (YokkoUiScale.Compact, YokkoStrings.Get("settings.display.compact"), FontAwesome.Solid.List),
            (YokkoUiScale.Comfortable, YokkoStrings.Get("settings.display.comfortable"), FontAwesome.Solid.Bars),
            (YokkoUiScale.Large, YokkoStrings.Get("settings.display.spacious"), FontAwesome.Solid.ThList),
        };

        foreach ((YokkoUiScale scale, LocalisableString label, IconUsage icon) in options)
        {
            YokkoUiScale capturedScale = scale;
            scaleButtons.Add(new SettingsSegmentedChoiceButton(label, icon, () => uiScale.Value = capturedScale, 199)
            {
                Value = scale,
            });
        }

        return createSegmentedControl(scaleButtons);
    }

    private static Drawable createSegmentedControl(IEnumerable<SettingsSegmentedChoiceButton> buttons) => new Container
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
            Children = buttons.Cast<Drawable>().ToArray(),
        },
    };

    private static Drawable createSettingRow(float y, LocalisableString title, Drawable control, float depth = 0) => new Container
    {
        Position = new Vector2(378, y),
        Size = new Vector2(840, 68),
        Depth = depth,
        Children = new Drawable[]
        {
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = title,
                Font = HomeTypography.Display(23),
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

    private static Drawable createDecorationIcon(IconUsage icon, float x, float y, float size, Color4 colour) => new SpriteIcon
    {
        Position = new Vector2(x, y),
        Size = new Vector2(size),
        Icon = icon,
        Colour = colour,
    };

    private void onWindowedSizeChanged(ValueChangedEvent<Size> _) => refreshSelection();

    private void onWindowModeChanged(ValueChangedEvent<WindowMode> _) => refreshSelection();

    private void onUiScaleChanged(ValueChangedEvent<YokkoUiScale> _) => refreshSelection();

    internal void ToggleResolutionMenu() => resolutionDropdown.Toggle();

    public bool DismissTransientUi() => resolutionDropdown.Dismiss();

    private void refreshSelection()
    {
        currentDisplayMetadata.Text = YokkoStrings.Get(
            "settings.display.metadata",
            windowedSize.Value.Width,
            windowedSize.Value.Height);
        resolutionDropdown.SetSelected(windowedSize.Value);

        foreach (SettingsSegmentedChoiceButton button in modeButtons)
            button.SetSelected(button.Value is WindowMode mode && mode == windowMode.Value);

        foreach (SettingsSegmentedChoiceButton button in scaleButtons)
            button.SetSelected(button.Value is YokkoUiScale scale && scale == uiScale.Value);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            windowedSize.ValueChanged -= onWindowedSizeChanged;
            windowMode.ValueChanged -= onWindowModeChanged;
            uiScale.ValueChanged -= onUiScaleChanged;
        }

        base.Dispose(isDisposing);
    }
}
