using System;
using System.Drawing;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Game.Audio;
using Yokko.Game.Configuration;
using Yokko.Game.Gameplay;
using Yokko.Game.Importing;
using Yokko.Game.Presentation;
using Yokko.Game.Resources;
using Yokko.Game.Screens.Main;
using Yokko.Game.Skinning.OsuMania;
using RectangleF = osu.Framework.Graphics.Primitives.RectangleF;

namespace Yokko.Game.Screens.Settings;

/// <summary>
/// Owns navigation into and out of settings. Individual settings areas own their
/// own layout and interactions so future categories do not inflate this screen.
/// </summary>
public partial class SettingsScreen : Screen
{
    private const float designedWidth = 1280;
    private const float designedHeight = 720;
    internal const float ReferenceLayoutScale = 1.25f;

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
    private YokkoResourceStorage resourceStorage { get; set; }

    private Bindable<Size> windowedSize;
    private Bindable<WindowMode> windowMode;
    private IBindable<DisplayMode> currentDisplayMode;
    private Bindable<string> locale;
    private Texture mascotTexture;
    private SettingsSidebar sidebar;
    private Container stage;
    private Container contentHost;
    private Drawable activePanel;
    private Vector2 lastResponsiveStageSize;
    private Container decorationLayer;
    private Container mascotLayer;
    private HomeMascotBubble bubble;
    private Sprite mascotPeek;
    private SpriteText watermark;
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
        mascotTexture = textures.Get("yokko")
                                .Crop(new RectangleF(80, 1840, 1200, 1360));
        Texture bubbleStickerTexture = textures.Get("Home/home-mascot-bubble-sticker");
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
                    mascotLayer = createMascotLayer(bubbleStickerTexture),
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
        mascotLayer.MoveToX(36).MoveToX(0, 620, Easing.OutQuint)
                   .FadeInFromZero(420);
        watermark.FadeInFromZero(700);
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        // 吉祥物探头呼吸式起伏，保持页面“活着”。
        mascotPeek.MoveToY(-26).MoveToY(-18, 1800, Easing.InOutSine)
                  .Then().MoveToY(-26, 1800, Easing.InOutSine)
                  .Loop();

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
                plus(1246, 664, 14, HomeControlColours.Pink),
                plus(640, 700, 9, HomeControlColours.Cyan),
                plus(1150, 44, 9, HomeControlColours.Yellow),
                new HomeDotField
                {
                    Position = new Vector2(1128, 596),
                    Size = new Vector2(96, 56),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.13f),
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
                    Position = new Vector2(1150, 692),
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
                    Position = new Vector2(1246, 590),
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
                    Position = new Vector2(1268, 420),
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
                    Position = new Vector2(1096, 688),
                },
                new HomeMicroLine
                {
                    Position = new Vector2(392, 30),
                    Width = 150,
                },
            },
        };
    }

    /// <summary>
    /// 右上角的吉祥物与台词气泡：切页时气泡报出当前分类，点气泡 mascot 会弹一下。
    /// </summary>
    private Container createMascotLayer(Texture bubbleStickerTexture)
    {
        var mascotSprite = new Sprite
        {
            Position = new Vector2(0, -22),
            Size = new Vector2(294, 333),
            Texture = mascotTexture,
        };

        var layer = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new Container
                {
                    Position = new Vector2(992, -16),
                    Size = new Vector2(296, 232),
                    Masking = true,
                    Child = mascotSprite,
                },
                bubble = new HomeMascotBubble(
                    SettingsPages.Get(CurrentPage).Title,
                    HomeMascotBubbleStyle.PopSignalSticker,
                    bubbleStickerTexture,
                    onMascotTapped)
                {
                    Position = new Vector2(688, 28),
                },
            },
        };

        mascotPeek = mascotSprite;
        return layer;
    }

    private void onMascotTapped()
    {
        mascotLayer.ScaleTo(1.04f, 90, Easing.Out)
                   .Then().ScaleTo(1f, 380, Easing.OutBack);
        bubble.SetText(SettingsPages.Get(CurrentPage).Title);
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
        }

        updateParallax();
    }

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
        mascotLayer.Position = parallaxCurrent * new Vector2(14, 9);
        watermark.Position = new Vector2(614, 548)
                             + parallaxCurrent * new Vector2(-8, -5);
    }

    internal static Vector2 CalculateResponsiveStageSize(Vector2 viewport) =>
        new(
            MathF.Max(viewport.X / ReferenceLayoutScale, designedWidth),
            MathF.Max(viewport.Y / ReferenceLayoutScale, designedHeight));

    /// <summary>
    /// The stage never shrinks below the designed 1280x720 layout, so windows
    /// smaller than the 1600x900 reference used to clip the right and bottom
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
        bubble?.SetText(SettingsPages.Get(page).Title);

        sidebar.SetSelected(page);
        activePanel = page switch
        {
            SettingsPageKind.General => new GeneralSettingsPanel(
                locale,
                gameplaySettings),
            SettingsPageKind.Display => new DisplaySettingsPanel(
                windowedSize,
                windowMode,
                displaySettings.UiScale,
                displaySettings.FrameLimit,
                displaySettings.ShowPerformanceReadout,
                currentDisplayMode,
                size => frameworkConfig.SetValue(FrameworkSetting.WindowedSize, size),
                mode => frameworkConfig.SetValue(FrameworkSetting.WindowMode, mode)),
            SettingsPageKind.Audio => new AudioSettingsPanel(
                audioSettings,
                gameplaySettings,
                host.Storage.GetFullPath("audio-tests", true)),
            SettingsPageKind.Gameplay => new GameplaySettingsPanel(
                gameplaySettings,
                audioSettings,
                host.Storage.GetFullPath("audio-tests", true),
                clipboard),
            SettingsPageKind.Shortcuts => new ShortcutSettingsPanel(
                gameplaySettings),
            SettingsPageKind.Skins => new SkinSettingsPanel(skinLibrary),
            SettingsPageKind.Import => new ImportSettingsPanel(
                importSettings,
                resourceStorage,
                yokkoConfig),
            _ => new SettingsPlaceholderPanel(SettingsPages.Get(page)),
        };

        activePanel.Alpha = 0;
        activePanel.X = 10;
        contentHost.Child = activePanel;
        activePanel.FadeIn(180, Easing.OutQuint);
        activePanel.MoveToX(0, 180, Easing.OutQuint);
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

    private static SettingsPageKind parseRememberedPage(string page) =>
        Enum.TryParse(page, out SettingsPageKind remembered)
            ? remembered
            : SettingsPageKind.Display;
}
