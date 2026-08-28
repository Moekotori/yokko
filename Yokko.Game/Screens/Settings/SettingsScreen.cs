using System;
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
using osu.Framework.Input.Bindings;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Game.Audio;
using Yokko.Game.Configuration;
using Yokko.Game.Diagnostics;
using Yokko.Game.Gameplay;
using Yokko.Game.Importing;
using Yokko.Game.Input;
using Yokko.Game.Presentation;
using Yokko.Game.Resources;
using Yokko.Game.Screens.Main;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Screens.Settings;

/// <summary>
/// Owns navigation into and out of settings. Individual settings areas own their
/// own layout and interactions so future categories do not inflate this screen.
/// </summary>
public partial class SettingsScreen : Screen
{
    // Legacy internal content coordinates. The outer stage and scale adapt
    // them to YokkoDisplaySettings.ReferenceLayoutSize.
    private const float designedWidth = 1280;
    private const float designedHeight = 720;
    internal const float ReferenceLayoutScale = 1.4f;

    private static readonly Vector2 watermarkHomePosition = new(614, 548);

    [Resolved]
    private FrameworkConfigManager frameworkConfig { get; set; }

    [Resolved]
    private YokkoConfigManager yokkoConfig { get; set; }

    [Resolved]
    private YokkoDisplaySettings displaySettings { get; set; }
    [Resolved]
    private YokkoAudioSettings audioSettings { get; set; }
    [Resolved]
    private YokkoImportSettings importSettings { get; set; }
    [Resolved]
    private YokkoGameplaySettings gameplaySettings { get; set; }
    [Resolved]
    private OsuManiaSkinLibrary skinLibrary { get; set; }
    [Resolved]
    private YokkoSkinSettings skinSettings { get; set; }
    [Resolved]
    private YokkoResourceStorage resourceStorage { get; set; }
    [Resolved]
    private YokkoExternalOsuSettings externalOsuSettings { get; set; }
    [Resolved]
    private ImportedChartLibrary importedChartLibrary { get; set; }
    [Resolved]
    private IResourceDirectoryPicker resourceDirectoryPicker { get; set; }
    [Resolved]
    private YokkoDiagnostics diagnostics { get; set; }
    [Resolved]
    private KeyInputTimestampSource keyInputTimestamps { get; set; }

    private Bindable<Size> windowedSize;
    private Bindable<WindowMode> windowMode;
    private IBindable<DisplayMode> currentDisplayMode;
    private Bindable<string> locale;
    private SettingsSidebar sidebar;
    private Container stage;
    private Container contentHost;
    private Drawable activePanel;
    private Vector2 lastResponsiveStageSize;
    private Container decorationLayer;
    private SpriteText watermark;
    private Vector2 watermarkHome = watermarkHomePosition;
    private Vector2[] decorationHomePositions;
    private Vector2 parallaxCurrent;

    [Resolved]
    private GameHost host { get; set; }
    [Resolved]
    private Clipboard clipboard { get; set; }

    internal SettingsPageKind CurrentPage { get; private set; } = SettingsPageKind.Display;
    internal Drawable ActivePanel => activePanel;

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        windowedSize = frameworkConfig.GetBindable<Size>(FrameworkSetting.WindowedSize);
        windowMode = frameworkConfig.GetBindable<WindowMode>(FrameworkSetting.WindowMode);
        currentDisplayMode = host.Window?.CurrentDisplayMode
                             ?? new Bindable<DisplayMode>(new DisplayMode(
                                 null,
                                 windowedSize.Value,
                                 0,
                                 60,
                                 0));
        locale = frameworkConfig.GetBindable<string>(FrameworkSetting.Locale);
        CurrentPage = parseRememberedPage(yokkoConfig.GetLastSettingsPage());

        sidebar = new SettingsSidebar(
            textures.Get("home-logo"),
            this.Exit,
            CurrentPage,
            OpenPage);
        contentHost = new Container { RelativeSizeAxes = Axes.Both };

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = HomeControlColours.Ivory,
            },
            stage = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(designedWidth, designedHeight),
                Scale = new Vector2(ReferenceLayoutScale),
                Children = new Drawable[]
                {
                    watermark = new SpriteText
                    {
                        Position = new Vector2(614, 548),
                        Rotation = -3,
                        Text = "SETTINGS",
                        Font = HomeTypography.Brand(110),
                        Colour = new Color4(
                            HomeControlColours.Navy.R,
                            HomeControlColours.Navy.G,
                            HomeControlColours.Navy.B,
                            0.055f),
                    },
                    decorationLayer = createDecorationLayer(),
                    // 点击涟漪：装饰层之上、侧边栏与面板之下，与主页一致。
                    new HomeTapRippleLayer(),
                    sidebar,
                    contentHost,
                    // 底部警示条纹收边，与主页构图呼应。
                    new Container
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = 8,
                        Child = new HomeHazardStripes(
                            4000,
                            new Color4(
                                HomeControlColours.Navy.R,
                                HomeControlColours.Navy.G,
                                HomeControlColours.Navy.B,
                                0.3f)),
                    },
                },
            },
        };

        OpenPage(CurrentPage);
    }

    public override void OnEntering(ScreenTransitionEvent e)
    {
        base.OnEntering(e);

        sidebar.MoveToX(-28).MoveToX(0, 520, Easing.OutQuint)
               .FadeInFromZero(360);
        decorationLayer.Delay(180).FadeIn(560);
        watermark.FadeInFromZero(700);
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        // 记录装饰在设计稿中的位置，窗口变大时按比例铺到整个舞台，
        // 避免装饰全挤在左上角、右侧空成一片。
        decorationHomePositions = decorationLayer.Children
                                                 .Select(child => child.Position)
                                                 .ToArray();

        watermark.FadeTo(0.075f, 2600, Easing.InOutSine)
                 .Then().FadeTo(0.045f, 2600, Easing.InOutSine)
                 .Loop();
    }

    /// <summary>
    /// 散布在内容区四周的装饰层，随鼠标轻微视差，让设置页像主页一样“活着”。
    /// </summary>
    private Container createDecorationLayer()
    {
        static SpriteIcon plus(float x, float y, float size, Color4 colour) => new()
        {
            Position = new Vector2(x, y),
            Size = new Vector2(size),
            Icon = FontAwesome.Solid.Plus,
            Colour = colour,
        };

        return new Container
        {
            RelativeSizeAxes = Axes.Both,
            Alpha = 0,
            Children = new Drawable[]
            {
                plus(352, 44, 11, HomeControlColours.Pink),
                plus(1248, 36, 12, HomeControlColours.Cyan),
                plus(356, 676, 10, HomeControlColours.Yellow),
                plus(1260, 492, 14, HomeControlColours.Pink),
                plus(640, 700, 9, HomeControlColours.Cyan),
                plus(1150, 44, 9, HomeControlColours.Yellow),
                plus(700, 30, 8, HomeControlColours.Pink),
                plus(1220, 240, 9, HomeControlColours.Cyan),
                new HomeDotField
                {
                    Position = new Vector2(876, 600),
                    Size = new Vector2(96, 56),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.13f),
                },
                new HomeBeatPips(
                    new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.3f),
                    HomeControlColours.Pink)
                {
                    Position = new Vector2(352, 648),
                },
                new HomeOrbitNodes(
                    26,
                    new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.6f),
                    HomeControlColours.Cyan,
                    4)
                {
                    Position = new Vector2(1232, 350),
                },
                new HomePulseBeacon(20, HomeControlColours.Cyan, HomeControlColours.Pink)
                {
                    Position = new Vector2(1252, 232),
                },
                // 吉祥物身后的公转节点光环，让她像站在舞台中央。
                new HomeOrbitNodes(
                    68,
                    new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.5f),
                    HomeControlColours.Pink,
                    5)
                {
                    Position = new Vector2(1185, 618),
                },
                new HomeDotField
                {
                    Position = new Vector2(336, 528),
                    Size = new Vector2(64, 40),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.1f),
                },
                new HomeRing(20, 2.5f, HomeControlColours.Cyan)
                {
                    Position = new Vector2(1240, 306),
                },
                new HomeRing(14, 2f, HomeControlColours.Pink)
                {
                    Position = new Vector2(346, 246),
                },
                new HomeTwinkle(12, 2100)
                {
                    Position = new Vector2(1252, 148),
                    Colour = HomeControlColours.Pink,
                },
                new HomeTwinkle(10, 2500)
                {
                    Position = new Vector2(350, 120),
                    Colour = HomeControlColours.Yellow,
                },
                new HomeTwinkle(9, 1700)
                {
                    Position = new Vector2(1072, 696),
                    Colour = HomeControlColours.Cyan,
                },
                new HomeCrosshairMark
                {
                    Position = new Vector2(1250, 506),
                },
                new HomeCrosshairMark
                {
                    Position = new Vector2(340, 600),
                },
                new Circle
                {
                    Position = new Vector2(352, 330),
                    Size = new Vector2(7),
                    Colour = HomeControlColours.Pink,
                    Alpha = 0.85f,
                },
                new osu.Framework.Graphics.Shapes.Triangle
                {
                    Position = new Vector2(1048, 556),
                    Size = new Vector2(12, 11),
                    Rotation = 18,
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.28f),
                },
                new SpriteText
                {
                    Origin = Anchor.Centre,
                    Position = new Vector2(1256, 420),
                    Rotation = -90,
                    Text = "MAKE IT YOURS · SETTINGS",
                    Font = HomeTypography.Display(14),
                    Spacing = new Vector2(2.4f, 0),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.32f),
                },
                new HomeBarcode("TUNE-128", showLabel: false)
                {
                    Position = new Vector2(856, 690),
                },
                new HomeMicroLine
                {
                    Position = new Vector2(392, 30),
                    Width = 150,
                },
            },
        };
    }

    protected override void Update()
    {
        base.Update();

        if (stage == null || DrawWidth <= 0 || DrawHeight <= 0)
            return;

        var viewport = new Vector2(DrawWidth, DrawHeight);
        Vector2 stageSize = CalculateResponsiveStageSize(viewport);
        float stageScale = CalculateStageScale(viewport, stageSize);

        if ((stageSize - lastResponsiveStageSize).LengthSquared >= 0.01f
            || MathF.Abs(stage.Scale.X - stageScale) >= 0.001f)
        {
            lastResponsiveStageSize = stageSize;
            stage.Size = stageSize;
            stage.Scale = new Vector2(stageScale);
            applyResponsiveLayout(stageSize);
        }

        updateParallax();
    }

    /// <summary>
    /// 窗口比设计稿大时舞台会跟着变大。内容在设计稿里是固定坐标，不重排的
    /// 话会全部堆在左上、右侧空成一片。这里把多出来的空间用起来：内容列
    /// 居中，装饰按比例铺满舞台。
    /// </summary>
    private void applyResponsiveLayout(Vector2 stageSize)
    {
        contentHost.Position = CalculateContentOffset(stageSize);

        var stretch = new Vector2(
            stageSize.X / designedWidth,
            stageSize.Y / designedHeight);
        watermarkHome = watermarkHomePosition * stretch;

        if (decorationHomePositions == null)
            return;

        var decorations = decorationLayer.Children;
        for (int i = 0; i < decorations.Count && i < decorationHomePositions.Length; i++)
            decorations[i].Position = decorationHomePositions[i] * stretch;
    }

    /// <summary>
    /// 内容列在设计稿里固定在 x=378、宽 840。舞台变宽时把多出来的空间
    /// 均分到两侧，让内容在侧边栏与右边缘之间保持居中。
    /// </summary>
    internal static Vector2 CalculateContentOffset(Vector2 stageSize) =>
        new(
            MathF.Max(0, stageSize.X - designedWidth) / 2,
            MathF.Max(0, stageSize.Y - designedHeight) / 2);

    private void updateParallax()
    {
        var inputManager = GetContainingInputManager();
        if (inputManager == null || decorationLayer == null)
            return;

        Vector2 local = ToLocalSpace(inputManager.CurrentState.Mouse.Position);
        Vector2 target = new Vector2(
            Math.Clamp(local.X / DrawWidth - 0.5f, -0.65f, 0.65f),
            Math.Clamp(local.Y / DrawHeight - 0.5f, -0.65f, 0.65f));

        float blend = 1f - MathF.Exp((float)(-Clock.ElapsedFrameTime / 110));
        parallaxCurrent = Vector2.Lerp(parallaxCurrent, target, blend);

        decorationLayer.Position = parallaxCurrent * new Vector2(22, 14);
        watermark.Position = watermarkHome
                             + parallaxCurrent * new Vector2(-8, -5);
    }

    internal static Vector2 CalculateResponsiveStageSize(Vector2 viewport) =>
        new(
            MathF.Max(viewport.X / ReferenceLayoutScale, designedWidth),
            MathF.Max(viewport.Y / ReferenceLayoutScale, designedHeight));

    /// <summary>
    /// The stage never shrinks below the designed 1280x720 layout, so windows
    /// smaller than the 1920x1080 reference used to clip the right and bottom
    /// edges. Shrink the stage scale instead so the whole layout stays visible.
    /// </summary>
    internal static float CalculateStageScale(Vector2 viewport, Vector2 stageSize)
    {
        if (stageSize.X <= 0 || stageSize.Y <= 0)
            return ReferenceLayoutScale;

        float fitScale = MathF.Min(
            viewport.X / stageSize.X,
            viewport.Y / stageSize.Y);
        return MathF.Min(ReferenceLayoutScale, fitScale);
    }

    internal void OpenPage(SettingsPageKind page)
    {
        if (contentHost == null)
        {
            CurrentPage = page;
            return;
        }

        if (CurrentPage == page && activePanel != null)
            return;

        CurrentPage = page;
        yokkoConfig.SetLastSettingsPage(page.ToString());

        sidebar.SetSelected(page);
        activePanel = page switch
        {
            SettingsPageKind.General => new GeneralSettingsPanel(
                locale,
                gameplaySettings,
                diagnostics.ConsoleVisible),
            SettingsPageKind.Display => new DisplaySettingsPanel(
                windowedSize,
                windowMode,
                displaySettings.UiScale,
                displaySettings.FrameLimit,
                displaySettings.ShowPerformanceReadout,
                displaySettings.DifficultyRatingMode,
                currentDisplayMode,
                size => frameworkConfig.SetValue(FrameworkSetting.WindowedSize, size),
                mode => frameworkConfig.SetValue(FrameworkSetting.WindowMode, mode)),
            SettingsPageKind.Desktop => new DesktopSettingsPanel(
                displaySettings,
                audioSettings,
                host),
            SettingsPageKind.Audio => new AudioSettingsPanel(
                audioSettings,
                gameplaySettings,
                host.Storage.GetFullPath("audio-tests", true)),
            SettingsPageKind.Gameplay => new GameplaySettingsPanel(
                gameplaySettings,
                audioSettings,
                host.Storage.GetFullPath("audio-tests", true),
                clipboard,
                keyInputTimestamps),
            SettingsPageKind.Shortcuts => new ShortcutSettingsPanel(
                gameplaySettings),
            SettingsPageKind.Skins => new SkinSettingsPanel(
                skinLibrary,
                skinSettings),
            SettingsPageKind.Import => new ImportSettingsPanel(
                importSettings,
                resourceStorage,
                yokkoConfig,
                resourceDirectoryPicker,
                externalOsuSettings,
                importedChartLibrary),
            SettingsPageKind.Safety => new SafetySettingsPanel(
                host,
                host.Storage.GetFullPath("crash-reports", true),
                yokkoConfig.GetBindable<double>(
                    YokkoSetting.HomeExitHoldDurationMilliseconds)),
            SettingsPageKind.About => new AboutSettingsPanel(),
            _ => new SettingsPlaceholderPanel(SettingsPages.Get(page)),
        };

        activePanel.Alpha = 0;
        activePanel.X = 10;
        contentHost.Child = activePanel;
        activePanel.FadeIn(180, Easing.OutQuint);
        activePanel.MoveToX(0, 180, Easing.OutQuint);
        diagnostics.Trace("SETTINGS", "page-opened", $"page={page}");
    }

    internal bool DismissTransientUi()
    {
        if (activePanel is ISettingsTransientUi transientUi && transientUi.DismissTransientUi())
            return true;

        return sidebar?.DismissTransientUi() == true;
    }

    public override bool OnExiting(ScreenExitEvent e)
    {
        yokkoConfig.Save();
        frameworkConfig.Save();
        return base.OnExiting(e);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (activePanel is GameplaySettingsPanel
            {
                IsCapturingKey: true,
            } gameplayCapture
            && gameplayCapture.HandleKeyDown(e.Key))
        {
            return true;
        }

        if (activePanel is ShortcutSettingsPanel
            {
                IsCapturingShortcut: true,
            } shortcutCapture
            && shortcutCapture.HandleKeyDown(e.Key))
        {
            return true;
        }

        if (HandleNavigationShortcut(e.Key, e.ControlPressed))
            return true;

        if (activePanel is GameplaySettingsPanel gameplayPanel
            && gameplayPanel.HandleKeyDown(e.Key))
        {
            return true;
        }

        if (activePanel is ShortcutSettingsPanel shortcutPanel
            && shortcutPanel.HandleKeyDown(e.Key))
        {
            return true;
        }

        if (e.Key != Key.Escape)
            return base.OnKeyDown(e);

        if (DismissTransientUi())
            return true;

        this.Exit();
        return true;
    }

    internal bool HandleNavigationShortcut(
        Key key,
        bool controlPressed = false)
    {
        if (controlPressed && key == Key.F)
            return sidebar?.FocusSearch() == true;

        if (key == Key.Slash
            && activePanel is not GameplaySettingsPanel
            && activePanel is not ShortcutSettingsPanel
            && sidebar?.SearchHasFocus != true)
        {
            return sidebar?.FocusSearch() == true;
        }

        return false;
    }

    protected override void OnKeyUp(KeyUpEvent e)
    {
        if (activePanel is GameplaySettingsPanel gameplayPanel)
            gameplayPanel.HandleKeyUp(e.Key);

        base.OnKeyUp(e);
    }

    protected override bool OnJoystickPress(JoystickPressEvent e)
    {
        if (activePanel is GameplaySettingsPanel gameplayPanel
            && gameplayPanel.HandleInputDown(
                KeyCombination.FromJoystickButton(e.Button)))
        {
            return true;
        }

        return base.OnJoystickPress(e);
    }

    protected override void OnJoystickRelease(JoystickReleaseEvent e)
    {
        if (activePanel is GameplaySettingsPanel gameplayPanel)
        {
            gameplayPanel.HandleInputUp(
                KeyCombination.FromJoystickButton(e.Button));
        }

        base.OnJoystickRelease(e);
    }

    protected override bool OnMidiDown(MidiDownEvent e)
    {
        if (activePanel is GameplaySettingsPanel gameplayPanel
            && gameplayPanel.HandleInputDown(KeyCombination.FromMidiKey(e.Key)))
        {
            return true;
        }

        return base.OnMidiDown(e);
    }

    protected override void OnMidiUp(MidiUpEvent e)
    {
        if (activePanel is GameplaySettingsPanel gameplayPanel)
            gameplayPanel.HandleInputUp(KeyCombination.FromMidiKey(e.Key));

        base.OnMidiUp(e);
    }

    private static SettingsPageKind parseRememberedPage(string page) =>
        Enum.TryParse(page, out SettingsPageKind remembered)
            ? remembered
            : SettingsPageKind.Display;
}
