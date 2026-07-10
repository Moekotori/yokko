using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

public partial class SettingsScreen : Screen
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

    private readonly List<SettingsChoiceButton> resolutionButtons = new();
    private readonly List<SettingsChoiceButton> modeButtons = new();
    private readonly List<SettingsChoiceButton> scaleButtons = new();

    private SpriteText currentDisplayText;

    [Resolved]
    private FrameworkConfigManager frameworkConfig { get; set; }

    [Resolved]
    private YokkoDisplaySettings displaySettings { get; set; }

    private Bindable<Size> windowedSize;
    private Bindable<WindowMode> windowMode;

    [BackgroundDependencyLoader]
    private void load()
    {
        windowedSize = frameworkConfig.GetBindable<Size>(FrameworkSetting.WindowedSize);
        windowMode = frameworkConfig.GetBindable<WindowMode>(FrameworkSetting.WindowMode);

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = YokkoPalette.Background,
            },
            new MainBackdrop(),
            new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(1120, 630),
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 18),
                    Children = new Drawable[]
                    {
                        createHeader(),
                        createCurrentDisplayCard(),
                        createSection("WINDOW MODE", "Choose how Yokko uses your display.", createModeButtons()),
                        createSection("WINDOW SIZE", "Applied immediately in windowed mode.", createResolutionButtons()),
                        createSection("INTERFACE SIZE", "Scale every screen consistently.", createScaleButtons()),
                        createFooter(),
                    },
                },
            },
        };

        windowedSize.BindValueChanged(onWindowedSizeChanged, true);
        windowMode.BindValueChanged(onWindowModeChanged, true);
        displaySettings.UiScale.BindValueChanged(onUiScaleChanged, true);
    }

    private Drawable createHeader() => new Container
    {
        RelativeSizeAxes = Axes.X,
        Height = 76,
        Children = new Drawable[]
        {
            new YokkoButton("Back", FontAwesome.Solid.ArrowLeft, this.Exit, 96, 42, YokkoButtonStyle.Quiet),
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                X = 120,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = "Display settings",
                        Font = FontUsage.Default.With(size: 34, weight: "Bold"),
                        Colour = YokkoPalette.Text,
                    },
                    new SpriteText
                    {
                        Text = "Tune the window once; Yokko keeps every screen consistent.",
                        Font = FontUsage.Default.With(size: 16),
                        Colour = YokkoPalette.TextMuted,
                    },
                },
            },
        },
    };

    private Drawable createCurrentDisplayCard() => new Container
    {
        RelativeSizeAxes = Axes.X,
        Height = 68,
        Masking = true,
        CornerRadius = 10,
        BorderThickness = 1,
        BorderColour = new Color4(YokkoPalette.Cyan.R, YokkoPalette.Cyan.G, YokkoPalette.Cyan.B, 0.4f),
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(YokkoPalette.Cyan.R * 0.1f, YokkoPalette.Cyan.G * 0.1f, YokkoPalette.Cyan.B * 0.1f, 0.96f),
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 22,
                Size = new Vector2(23),
                Icon = FontAwesome.Solid.Desktop,
                Colour = YokkoPalette.Cyan,
            },
            currentDisplayText = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 60,
                Font = FontUsage.Default.With(size: 18, weight: "SemiBold"),
                Colour = YokkoPalette.Text,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -22,
                Text = "Alt + Enter switches window mode",
                Font = FontUsage.Default.With(size: 14),
                Colour = YokkoPalette.TextDim,
            },
        },
    };

    private static Drawable createSection(string title, string description, Drawable choices) => new Container
    {
        RelativeSizeAxes = Axes.X,
        Height = 118,
        Masking = true,
        CornerRadius = 10,
        BorderThickness = 1,
        BorderColour = YokkoPalette.Border,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = YokkoPalette.PanelAlt,
            },
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Y,
                Width = 250,
                Padding = new MarginPadding { Left = 22, Top = 21, Right = 16 },
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 7),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = title,
                        Font = FontUsage.Default.With(size: 13, weight: "Bold"),
                        Colour = YokkoPalette.Cyan,
                    },
                    new SpriteText
                    {
                        Text = description,
                        Font = FontUsage.Default.With(size: 14),
                        Colour = YokkoPalette.TextMuted,
                        Width = 205,
                    },
                },
            },
            new Container
            {
                RelativeSizeAxes = Axes.Y,
                X = 268,
                Width = 834,
                Padding = new MarginPadding { Vertical = 20 },
                Child = choices,
            },
        },
    };

    private Drawable createModeButtons()
    {
        var options = new[]
        {
            (WindowMode.Windowed, "Windowed", "Resizable window"),
            (WindowMode.Borderless, "Borderless", "Uses the full desktop"),
            (WindowMode.Fullscreen, "Fullscreen", "Exclusive display mode"),
        };

        foreach ((WindowMode mode, string title, string detail) in options)
        {
            WindowMode capturedMode = mode;
            modeButtons.Add(new SettingsChoiceButton(title, detail, () => frameworkConfig.SetValue(FrameworkSetting.WindowMode, capturedMode), 258)
            {
                Value = mode,
            });
        }

        return createChoiceFlow(modeButtons, 12);
    }

    private Drawable createResolutionButtons()
    {
        foreach (Size resolution in supportedResolutions)
        {
            Size capturedResolution = resolution;
            resolutionButtons.Add(new SettingsChoiceButton($"{resolution.Width} × {resolution.Height}", aspectLabel(resolution),
                () => frameworkConfig.SetValue(FrameworkSetting.WindowedSize, capturedResolution), 124)
            {
                Value = resolution,
            });
        }

        return createChoiceFlow(resolutionButtons, 10);
    }

    private Drawable createScaleButtons()
    {
        var options = new[]
        {
            (YokkoUiScale.Large, "Large", "Best for couch play"),
            (YokkoUiScale.Comfortable, "Comfortable", "Recommended"),
            (YokkoUiScale.Compact, "Compact", "More room on screen"),
        };

        foreach ((YokkoUiScale scale, string title, string detail) in options)
        {
            YokkoUiScale capturedScale = scale;
            scaleButtons.Add(new SettingsChoiceButton(title, detail, () => displaySettings.UiScale.Value = capturedScale, 258)
            {
                Value = scale,
            });
        }

        return createChoiceFlow(scaleButtons, 12);
    }

    private static Drawable createChoiceFlow(IEnumerable<SettingsChoiceButton> choices, float spacing) => new FillFlowContainer
    {
        RelativeSizeAxes = Axes.X,
        AutoSizeAxes = Axes.Y,
        Direction = FillDirection.Horizontal,
        Spacing = new Vector2(spacing, 10),
        Children = choices.Cast<Drawable>().ToArray(),
    };

    private static Drawable createFooter() => new Container
    {
        RelativeSizeAxes = Axes.X,
        Height = 34,
        Child = new SpriteText
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Text = "Yokko uses osu!framework's native window configuration and resolution-aware scaling.",
            Font = FontUsage.Default.With(size: 14),
            Colour = YokkoPalette.TextDim,
        },
    };

    private void onWindowedSizeChanged(ValueChangedEvent<Size> _) => refreshSelection();

    private void onWindowModeChanged(ValueChangedEvent<WindowMode> _) => refreshSelection();

    private void onUiScaleChanged(ValueChangedEvent<YokkoUiScale> _) => refreshSelection();

    private void refreshSelection()
    {
        if (currentDisplayText == null)
            return;

        currentDisplayText.Text = $"{windowedSize.Value.Width} × {windowedSize.Value.Height}  •  {windowMode.Value}  •  {scaleLabel(displaySettings.UiScale.Value)} UI";

        foreach (SettingsChoiceButton button in resolutionButtons)
            button.SetSelected(button.Value is Size size && size == windowedSize.Value);

        foreach (SettingsChoiceButton button in modeButtons)
            button.SetSelected(button.Value is WindowMode mode && mode == windowMode.Value);

        foreach (SettingsChoiceButton button in scaleButtons)
            button.SetSelected(button.Value is YokkoUiScale scale && scale == displaySettings.UiScale.Value);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key != Key.Escape)
            return base.OnKeyDown(e);

        this.Exit();
        return true;
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            if (windowedSize != null)
                windowedSize.ValueChanged -= onWindowedSizeChanged;

            if (windowMode != null)
                windowMode.ValueChanged -= onWindowModeChanged;

            if (displaySettings != null)
                displaySettings.UiScale.ValueChanged -= onUiScaleChanged;
        }

        base.Dispose(isDisposing);
    }

    private static string aspectLabel(Size size)
    {
        int divisor = greatestCommonDivisor(size.Width, size.Height);
        return $"{size.Width / divisor}:{size.Height / divisor}";
    }

    private static int greatestCommonDivisor(int a, int b)
    {
        while (b != 0)
            (a, b) = (b, a % b);

        return a;
    }

    private static string scaleLabel(YokkoUiScale scale) => scale switch
    {
        YokkoUiScale.Large => "Large",
        YokkoUiScale.Compact => "Compact",
        _ => "Comfortable",
    };
}

public partial class SettingsChoiceButton : ClickableContainer
{
    private readonly Box background;
    private readonly Box accent;
    private readonly Color4 idleColour = YokkoPalette.SurfaceElevated;
    private bool selected;

    public object Value { get; init; }

    public SettingsChoiceButton(string title, string detail, Action action, float width)
    {
        Action = action;
        Size = new Vector2(width, 72);
        Masking = true;
        CornerRadius = 8;
        BorderThickness = 1;
        BorderColour = YokkoPalette.Border;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = idleColour,
            },
            accent = new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 4,
                Colour = YokkoPalette.Cyan,
                Alpha = 0,
            },
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding { Left = 15, Top = 12, Right = 10 },
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = title,
                        Font = FontUsage.Default.With(size: 15, weight: "SemiBold"),
                        Colour = YokkoPalette.Text,
                    },
                    new SpriteText
                    {
                        Text = detail,
                        Font = FontUsage.Default.With(size: 12),
                        Colour = YokkoPalette.TextDim,
                    },
                },
            },
        };
    }

    public void SetSelected(bool isSelected)
    {
        selected = isSelected;
        accent.FadeTo(selected ? 1 : 0, 120);
        background.FadeColour(selected
            ? new Color4(YokkoPalette.Cyan.R * 0.16f, YokkoPalette.Cyan.G * 0.16f, YokkoPalette.Cyan.B * 0.16f, 1f)
            : idleColour, 120);
        BorderColour = selected
            ? new Color4(YokkoPalette.Cyan.R, YokkoPalette.Cyan.G, YokkoPalette.Cyan.B, 0.7f)
            : YokkoPalette.Border;
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!selected)
            background.FadeColour(YokkoPalette.SurfaceHover, 120, Easing.OutQuint);

        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        if (!selected)
            background.FadeColour(idleColour, 140, Easing.OutQuint);
    }
}
