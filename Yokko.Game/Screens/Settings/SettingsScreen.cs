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
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Main;
using RectangleF = osu.Framework.Graphics.Primitives.RectangleF;

namespace Yokko.Game.Screens.Settings;

public partial class SettingsScreen : Screen
{
    private const float designedWidth = 1280;
    private const float designedHeight = 720;

    private static readonly Size[] supportedResolutions =
    {
        new(1024, 768),
        new(1280, 720),
        new(1366, 768),
        new(1600, 900),
        new(1920, 1080),
        new(2560, 1440),
    };

    private readonly List<SettingsSegmentedChoiceButton> modeButtons = new();
    private readonly List<SettingsSegmentedChoiceButton> scaleButtons = new();
    private readonly List<(SettingsNavHeader Header, SettingsNavItem[] Items)> navigationGroups = new();

    private SpriteText currentDisplayMetadata;
    private SettingsResolutionButton resolutionButton;
    private SettingsSearchTextBox searchBox;

    [Resolved]
    private FrameworkConfigManager frameworkConfig { get; set; }

    [Resolved]
    private YokkoDisplaySettings displaySettings { get; set; }

    private Bindable<Size> windowedSize;
    private Bindable<WindowMode> windowMode;

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        windowedSize = frameworkConfig.GetBindable<Size>(FrameworkSetting.WindowedSize);
        windowMode = frameworkConfig.GetBindable<WindowMode>(FrameworkSetting.WindowMode);

        Texture logoTexture = textures.Get("home-logo");
        Texture mascotTexture = textures.Get("yokko")
                                        .Crop(new RectangleF(80, 1840, 1200, 1360));

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = HomeControlColours.Ivory,
            },
            new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(designedWidth, designedHeight),
                Children = new Drawable[]
                {
                    createSidebar(logoTexture),
                    createMainContent(mascotTexture),
                },
            },
        };

        searchBox.Current.BindValueChanged(e => filterNavigation(e.NewValue), true);
        windowedSize.BindValueChanged(onWindowedSizeChanged, true);
        windowMode.BindValueChanged(onWindowModeChanged, true);
        displaySettings.UiScale.BindValueChanged(onUiScaleChanged, true);
    }

    private Drawable createSidebar(Texture logoTexture)
    {
        var coreHeader = new SettingsNavHeader("CORE");
        var general = new SettingsNavItem("General", FontAwesome.Solid.Cog, false);
        var display = new SettingsNavItem("Display", FontAwesome.Solid.Desktop, true);
        var audio = new SettingsNavItem("Audio", FontAwesome.Solid.VolumeUp, false);

        var creationHeader = new SettingsNavHeader("CREATION");
        var gameplay = new SettingsNavItem("Gameplay", FontAwesome.Solid.Gamepad, false);
        var editor = new SettingsNavItem("Editor", FontAwesome.Solid.Pen, false);
        var import = new SettingsNavItem("Import", FontAwesome.Solid.FolderOpen, false);

        var systemHeader = new SettingsNavHeader("SYSTEM");
        var accessibility = new SettingsNavItem("Accessibility", FontAwesome.Solid.UniversalAccess, false);
        var about = new SettingsNavItem("About", FontAwesome.Solid.InfoCircle, false);

        navigationGroups.Add((coreHeader, new[] { general, display, audio }));
        navigationGroups.Add((creationHeader, new[] { gameplay, editor, import }));
        navigationGroups.Add((systemHeader, new[] { accessibility, about }));

        return new Container
        {
            RelativeSizeAxes = Axes.Y,
            Width = 320,
            Children = new Drawable[]
            {
                new Sprite
                {
                    Position = new Vector2(38, 26),
                    Size = new Vector2(244, 83),
                    Texture = logoTexture,
                },
                new SpriteText
                {
                    Position = new Vector2(38, 126),
                    Text = "Settings",
                    Font = HomeTypography.Display(43),
                    Spacing = new Vector2(0.5f, 0),
                    Colour = HomeControlColours.Navy,
                },
                new SettingsOutlineButton("Back", FontAwesome.Solid.ArrowLeft, this.Exit)
                {
                    Position = new Vector2(38, 182),
                },
                searchBox = new SettingsSearchTextBox
                {
                    Position = new Vector2(38, 234),
                },
                new FillFlowContainer
                {
                    Position = new Vector2(30, 292),
                    Width = 252,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 3),
                    Children = new Drawable[]
                    {
                        coreHeader,
                        general,
                        display,
                        audio,
                        creationHeader,
                        gameplay,
                        editor,
                        import,
                        systemHeader,
                        accessibility,
                        about,
                    },
                },
                new Box
                {
                    Position = new Vector2(319, 28),
                    Width = 1,
                    Height = 664,
                    Colour = SettingsTheme.Divider,
                },
            },
        };
    }

    private Drawable createMainContent(Texture mascotTexture) => new Container
    {
        RelativeSizeAxes = Axes.Both,
        Children = new Drawable[]
        {
            new SpriteText
            {
                Position = new Vector2(378, 42),
                Text = "Display",
                Font = HomeTypography.Display(58),
                Spacing = new Vector2(0.45f, 0),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(378, 105),
                Text = "Window, resolution and interface scale",
                Font = HomeTypography.Body(17),
                Spacing = new Vector2(0.2f, 0),
                Colour = SettingsTheme.MutedNavy,
            },
            createMascotCrop(mascotTexture),
            createDisplayStatus(),
            createDivider(378, 292, 840),
            createSettingRow(
                310,
                "Window mode",
                createModeControl()),
            createDivider(378, 388, 840),
            createSettingRow(
                402,
                "Resolution",
                resolutionButton = new SettingsResolutionButton(
                    supportedResolutions,
                    size => frameworkConfig.SetValue(FrameworkSetting.WindowedSize, size))),
            createDivider(378, 478, 840),
            createSettingRow(
                492,
                "Interface scale",
                createScaleControl()),
            createFooter(),
            new HomeDotCross
            {
                Position = new Vector2(1088, 594),
                Scale = new Vector2(1.1f),
            },
            createDecorationIcon(FontAwesome.Solid.Plus, 1172, 601, 16, HomeControlColours.Pink),
            createDecorationIcon(FontAwesome.Solid.Plus, 1200, 637, 12, HomeControlColours.Yellow),
        },
    };

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

    private Drawable createDisplayStatus() => new Container
    {
        Position = new Vector2(378, 174),
        Size = new Vector2(840, 86),
        Masking = true,
        CornerRadius = 8,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SettingsTheme.StatusCyan,
            },
            new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 48,
                Size = new Vector2(56),
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 35,
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
                        Text = "Current display",
                        Font = HomeTypography.Display(19),
                        Colour = HomeControlColours.Navy,
                    },
                    currentDisplayMetadata = new SpriteText
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
        },
    };

    private Drawable createModeControl()
    {
        var options = new[]
        {
            (WindowMode.Windowed, "Windowed", FontAwesome.Solid.WindowMaximize),
            (WindowMode.Borderless, "Borderless", FontAwesome.Solid.Expand),
            (WindowMode.Fullscreen, "Fullscreen", FontAwesome.Solid.ExpandArrowsAlt),
        };

        foreach ((WindowMode mode, string label, IconUsage icon) in options)
        {
            WindowMode capturedMode = mode;
            modeButtons.Add(new SettingsSegmentedChoiceButton(
                label,
                icon,
                () => frameworkConfig.SetValue(FrameworkSetting.WindowMode, capturedMode),
                199)
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
            (YokkoUiScale.Compact, "Compact", FontAwesome.Solid.List),
            (YokkoUiScale.Comfortable, "Comfortable", FontAwesome.Solid.Bars),
            (YokkoUiScale.Large, "Spacious", FontAwesome.Solid.ThList),
        };

        foreach ((YokkoUiScale scale, string label, IconUsage icon) in options)
        {
            YokkoUiScale capturedScale = scale;
            scaleButtons.Add(new SettingsSegmentedChoiceButton(
                label,
                icon,
                () => displaySettings.UiScale.Value = capturedScale,
                199)
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

    private static Drawable createSettingRow(float y, string title, Drawable control) => new Container
    {
        Position = new Vector2(378, y),
        Size = new Vector2(840, 68),
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

    private static Drawable createDivider(float x, float y, float width) => new Box
    {
        Position = new Vector2(x, y),
        Width = width,
        Height = 1,
        Colour = SettingsTheme.Divider,
    };

    private static Drawable createFooter() => new Container
    {
        Position = new Vector2(372, 651),
        Size = new Vector2(650, 42),
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Colour = SettingsTheme.Divider,
            },
            new SpriteIcon
            {
                Position = new Vector2(2, 18),
                Size = new Vector2(22),
                Icon = FontAwesome.Solid.CheckSquare,
                Colour = HomeControlColours.Pink,
            },
            new SpriteText
            {
                Position = new Vector2(36, 17),
                Text = "Changes apply instantly",
                Font = HomeTypography.Body(14),
                Colour = HomeControlColours.Navy,
            },
            new Box
            {
                Position = new Vector2(220, 14),
                Width = 1,
                Height = 22,
                Colour = SettingsTheme.Divider,
            },
            new Container
            {
                Position = new Vector2(252, 14),
                Size = new Vector2(30, 24),
                Masking = true,
                CornerRadius = 4,
                BorderThickness = 1,
                BorderColour = SettingsTheme.MutedNavy,
                Child = new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = "Esc",
                    Font = HomeTypography.Body(11),
                    Colour = HomeControlColours.Navy,
                },
            },
            new SpriteText
            {
                Position = new Vector2(294, 17),
                Text = "Esc to return",
                Font = HomeTypography.Body(14),
                Colour = HomeControlColours.Navy,
            },
        },
    };

    private static Drawable createDecorationIcon(IconUsage icon, float x, float y, float size, Color4 colour) => new SpriteIcon
    {
        Position = new Vector2(x, y),
        Size = new Vector2(size),
        Icon = icon,
        Colour = colour,
    };

    private void filterNavigation(string query)
    {
        string normalized = query?.Trim() ?? string.Empty;

        foreach ((SettingsNavHeader header, SettingsNavItem[] items) in navigationGroups)
        {
            bool anyVisible = false;

            foreach (SettingsNavItem item in items)
            {
                bool visible = normalized.Length == 0 ||
                               item.Label.Contains(normalized, StringComparison.OrdinalIgnoreCase);
                item.SetFiltered(visible);
                anyVisible |= visible;
            }

            header.SetFiltered(anyVisible);
        }
    }

    private void onWindowedSizeChanged(ValueChangedEvent<Size> _) => refreshSelection();

    private void onWindowModeChanged(ValueChangedEvent<WindowMode> _) => refreshSelection();

    private void onUiScaleChanged(ValueChangedEvent<YokkoUiScale> _) => refreshSelection();

    private void refreshSelection()
    {
        if (currentDisplayMetadata == null)
            return;

        currentDisplayMetadata.Text =
            $"Display 1  ·  {windowedSize.Value.Width} × {windowedSize.Value.Height}  ·  60 Hz";
        resolutionButton.SetSelected(windowedSize.Value);

        foreach (SettingsSegmentedChoiceButton button in modeButtons)
            button.SetSelected(button.Value is WindowMode mode && mode == windowMode.Value);

        foreach (SettingsSegmentedChoiceButton button in scaleButtons)
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
}
