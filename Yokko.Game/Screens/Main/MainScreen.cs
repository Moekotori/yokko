using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Audio;
using Yokko.Core.Beatmaps;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Editor;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.Settings;

namespace Yokko.Game.Screens.Main;

public partial class MainScreen : Screen
{
    private const float designedWidth = 1280;
    private const float designedHeight = 720;
    private const double exitHoldDuration = 5000;

    private static readonly Color4 ivory = new(0.992f, 0.992f, 0.988f, 1f);
    private static readonly Color4 cyan = new(0.29f, 0.81f, 0.94f, 1f);
    private static readonly Color4 navy = new(0.035f, 0.085f, 0.54f, 1f);
    private static readonly Color4 yellow = new(1f, 0.91f, 0.42f, 1f);
    private static readonly Color4 pink = new(1f, 0.22f, 0.65f, 1f);
    private static readonly Color4 mutedNavy = new(0.18f, 0.28f, 0.58f, 1f);
    private static readonly Color4 paleCyan = new(0.54f, 0.91f, 0.98f, 1f);

    private Container content;
    private Container leftStage;
    private Container decorationLayer;
    private Container rightParallax;
    private Drawable rightStage;
    private Drawable brandLockup;
    private Drawable commandArea;
    private Drawable utilityArea;
    private Drawable footer;
    private Sprite mascot;
    private SpriteText watermark;
    private SpriteIcon heartbeatIcon;
    private Box heroHighlight;
    private Circle readyDot;
    private readonly Box[] stageLines = new Box[2];
    private readonly List<SpriteIcon> decorationIcons = new();
    private readonly List<Drawable> floaters = new();
    private readonly Action requestGameExit;

    private Vector2 parallaxCurrent;
    private double escapeHoldStartedAt;
    private bool isEscapeHeld;

    [Resolved]
    private GameHost host { get; set; }

    public MainScreen(Action requestGameExit = null)
    {
        this.requestGameExit = requestGameExit;
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        string availableBackend = AudioEngineFactory.AvailableBackends
                                                    .Where(backend => backend.IsAvailable)
                                                    .Select(backendDisplayName)
                                                    .FirstOrDefault();
        LocalisableString audioStatus = availableBackend ?? YokkoStrings.Get("main.audio_unavailable");

        Texture mascotTexture = textures.Get("yokko")
                                        .Crop(new RectangleF(80, 1840, 1200, 1360));
        Texture logoTexture = textures.Get("home-logo");

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = cyan,
            },
            createIvoryStage(),
            content = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(designedWidth, designedHeight),
                Children = new Drawable[]
                {
                    rightStage = createRightStage(mascotTexture),
                    decorationLayer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                        Children = new Drawable[]
                        {
                            createDecorationIcon(FontAwesome.Solid.Plus, 55, 36, 12, pink),
                            createDecorationIcon(FontAwesome.Solid.Plus, 116, 29, 10, cyan),
                            createDecorationIcon(FontAwesome.Solid.Plus, 42, 67, 9, yellow),
                            new HomeCornerBracket
                            {
                                Position = new Vector2(30, 218),
                                Height = 232,
                            },
                            new HomeConnectorPlus
                            {
                                Position = new Vector2(38, 448),
                            },
                            new HomeMicroLine
                            {
                                Position = new Vector2(525, 31),
                                Width = 142,
                            },
                            new HomeDotCross
                            {
                                Position = new Vector2(-12, 606),
                                Alpha = 0.62f,
                            },
                            new HomeCrosshairMark
                            {
                                Position = new Vector2(606, 244),
                            },
                            new HomeCrosshairMark
                            {
                                Position = new Vector2(648, 612),
                            },
                            new Circle
                            {
                                Position = new Vector2(624, 556),
                                Size = new Vector2(7),
                                Colour = pink,
                                Alpha = 0.85f,
                            },
                        },
                    },
                    leftStage = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            brandLockup = createBrandLockup(logoTexture),
                            commandArea = createCommandArea(),
                            footer = createFooter(audioStatus),
                        },
                    },
                    utilityArea = createUtilityArea(),
                },
            },
        };

        // 入场初始状态，OnEntering 中归位。
        brandLockup.X -= 26;
        brandLockup.Alpha = 0;
        commandArea.Y += 24;
        commandArea.Alpha = 0;
        footer.Y += 20;
        footer.Alpha = 0;
        rightStage.X += 44;
        rightStage.Alpha = 0;
        utilityArea.Y -= 20;
        utilityArea.Alpha = 0;
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        startAmbientMotion();
    }

    public override void OnEntering(ScreenTransitionEvent e)
    {
        base.OnEntering(e);
        cancelExitHold();

        content.FadeInFromZero(240);
        rightStage.Delay(80).FadeIn(520).MoveToX(0, 640, Easing.OutQuint);
        decorationLayer.Delay(240).FadeIn(600);
        brandLockup.Delay(60).FadeIn(420).MoveToX(56, 540, Easing.OutQuint);
        commandArea.Delay(150).FadeIn(420).MoveToY(208, 540, Easing.OutQuint);
        footer.Delay(300).FadeIn(420).MoveToY(655, 540, Easing.OutQuint);
        utilityArea.Delay(340).FadeIn(360).MoveToY(24, 460, Easing.OutQuint);
    }

    public override void OnResuming(ScreenTransitionEvent e)
    {
        base.OnResuming(e);
        this.FadeIn(200, Easing.OutQuint);
    }

    public override void OnSuspending(ScreenTransitionEvent e)
    {
        base.OnSuspending(e);
        cancelExitHold();
        this.FadeTo(0.4f, 200, Easing.OutQuint);
    }

    public override bool OnExiting(ScreenExitEvent e)
    {
        cancelExitHold();
        this.FadeOut(200, Easing.OutQuint);
        return base.OnExiting(e);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key != Key.Escape)
            return base.OnKeyDown(e);

        if (!isEscapeHeld)
        {
            isEscapeHeld = true;
            escapeHoldStartedAt = Time.Current;
        }

        return true;
    }

    protected override void OnKeyUp(KeyUpEvent e)
    {
        if (e.Key == Key.Escape)
            cancelExitHold();

        base.OnKeyUp(e);
    }

    protected override void Update()
    {
        base.Update();

        if (isEscapeHeld && Time.Current - escapeHoldStartedAt >= exitHoldDuration)
        {
            cancelExitHold();
            requestGameExit?.Invoke();
        }

        var inputManager = GetContainingInputManager();
        if (inputManager == null)
            return;

        Vector2 local = ToLocalSpace(inputManager.CurrentState.Mouse.Position);
        Vector2 target = new Vector2(
            Math.Clamp(local.X / DrawWidth - 0.5f, -0.65f, 0.65f),
            Math.Clamp(local.Y / DrawHeight - 0.5f, -0.65f, 0.65f));

        float blend = 1f - MathF.Exp((float)(-Clock.ElapsedFrameTime / 110));
        parallaxCurrent = Vector2.Lerp(parallaxCurrent, target, blend);

        rightParallax.Position = parallaxCurrent * new Vector2(16, 11);
        decorationLayer.Position = parallaxCurrent * new Vector2(24, 15);
        leftStage.Position = parallaxCurrent * new Vector2(-5, -3);
    }

    private void cancelExitHold()
    {
        isEscapeHeld = false;
        escapeHoldStartedAt = 0;
    }

    private void startAmbientMotion()
    {
        mascot.MoveToY(396.5f + 7, 1900, Easing.InOutSine)
              .Then().MoveToY(396.5f - 7, 1900, Easing.InOutSine)
              .Loop();
        mascot.RotateTo(1.1f, 2400, Easing.InOutSine)
              .Then().RotateTo(-1.1f, 2400, Easing.InOutSine)
              .Loop();

        watermark.FadeTo(0.22f, 2600, Easing.InOutSine)
                 .Then().FadeTo(0.1f, 2600, Easing.InOutSine)
                 .Loop();

        for (int i = 0; i < stageLines.Length; i++)
        {
            stageLines[i].FadeTo(0.55f, 1800 + i * 420, Easing.InOutSine)
                         .Then().FadeTo(1f, 1800 + i * 420, Easing.InOutSine)
                         .Loop();
        }

        // 双跳模拟心拍。
        heartbeatIcon.ScaleTo(1.28f, 110, Easing.Out)
                     .Then().ScaleTo(1f, 150, Easing.Out)
                     .Then().ScaleTo(1.16f, 100, Easing.Out)
                     .Then().ScaleTo(1f, 640, Easing.OutQuint)
                     .Loop();

        // 每个装饰按不同周期呼吸、轻摆，避免整齐划一。
        for (int i = 0; i < decorationIcons.Count; i++)
        {
            float duration = 1500 + i % 5 * 170;
            decorationIcons[i].ScaleTo(1.16f, duration, Easing.InOutSine)
                              .Then().ScaleTo(1f, duration, Easing.InOutSine)
                              .Loop();
            decorationIcons[i].RotateTo(7, duration * 1.6f, Easing.InOutSine)
                              .Then().RotateTo(-7, duration * 1.6f, Easing.InOutSine)
                              .Loop();
        }

        // 标题高亮标记在入场后刷出。
        heroHighlight.Delay(650).ScaleTo(Vector2.One, 380, Easing.OutQuint);

        // 就绪指示灯呼吸。
        readyDot.FadeTo(0.25f, 700, Easing.InOutSine)
                .Then().FadeTo(1f, 700, Easing.InOutSine)
                .Loop();

        // 小形状缓慢浮沉。
        for (int i = 0; i < floaters.Count; i++)
        {
            float y = floaters[i].Y;
            float duration = 2100 + i * 350;
            floaters[i].MoveToY(y - 5, duration, Easing.InOutSine)
                       .Then().MoveToY(y + 5, duration, Easing.InOutSine)
                       .Loop();
        }
    }

    private Drawable registerFloater(Drawable drawable)
    {
        floaters.Add(drawable);
        return drawable;
    }

    private static Drawable createIvoryStage() => new Container
    {
        RelativeSizeAxes = Axes.Both,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 510,
                Colour = ivory,
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 220,
                X = 510,
                Y = -20,
                Height = 1.14f,
                Rotation = 11,
                Colour = ivory,
            },
        },
    };

    private Drawable createRightStage(Texture mascotTexture) => new Container
    {
        RelativeSizeAxes = Axes.Both,
        Child = rightParallax = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                watermark = new SpriteText
                {
                    X = 690,
                    Y = 20,
                    Text = "YOKKO",
                    Font = HomeTypography.Brand(102),
                    Colour = new Color4(1f, 1f, 1f, 0.14f),
                },
                createDecorationIcon(FontAwesome.Solid.Plus, 730, 166, 18, Color4.White),
                createDecorationIcon(FontAwesome.Solid.Plus, 1220, 552, 19, Color4.White),
                createDecorationIcon(FontAwesome.Solid.Plus, 1190, 151, 14, yellow),
                createDecorationIcon(FontAwesome.Solid.Plus, 1225, 666, 10, pink),
                stageLines[0] = (Box)createStageLine(1128, 235, 180, -28),
                stageLines[1] = (Box)createStageLine(1060, 532, 250, -28),
                new HomeDotField
                {
                    Position = new Vector2(1186, 16),
                    Size = new Vector2(72, 44),
                    Colour = new Color4(1f, 1f, 1f, 0.24f),
                },
                new HomeDotField
                {
                    Position = new Vector2(1184, 610),
                    Size = new Vector2(72, 58),
                    Colour = new Color4(1f, 1f, 1f, 0.22f),
                },
                new HomeTickRuler(460)
                {
                    Position = new Vector2(768, 10),
                },
                new HomeEcgStrip(260, 38)
                {
                    Position = new Vector2(940, 674),
                    Colour = new Color4(1f, 1f, 1f, 0.45f),
                },
                new HomeDashedRing(295)
                {
                    Position = new Vector2(937.5f, 396.5f),
                    Colour = new Color4(1f, 1f, 1f, 0.3f),
                },
                registerFloater(new osu.Framework.Graphics.Shapes.Triangle
                {
                    Position = new Vector2(760, 120),
                    Size = new Vector2(19, 17),
                    Rotation = 90,
                    Colour = new Color4(1f, 1f, 1f, 0.28f),
                }),
                mascot = new Sprite
                {
                    Origin = Anchor.Centre,
                    Position = new Vector2(937.5f, 396.5f),
                    Size = new Vector2(675, 765),
                    Texture = mascotTexture,
                },
                new HomeMascotBubble(YokkoStrings.Get("main.lets_play"))
                {
                    X = 632,
                    Y = 365,
                },
                new HomeRing(26, 3.5f, yellow)
                {
                    Position = new Vector2(1248, 420),
                },
                registerFloater(new Circle
                {
                    Position = new Vector2(712, 648),
                    Size = new Vector2(8),
                    Colour = pink,
                    Alpha = 0.9f,
                }),
            },
        },
    };

    private static Drawable createStageLine(float x, float y, float width, float rotation) => new Box
    {
        Position = new Vector2(x, y),
        Width = width,
        Height = 2,
        Rotation = rotation,
        Colour = new Color4(1f, 1f, 1f, 0.22f),
    };

    private Drawable createDecorationIcon(IconUsage icon, float x, float y, float size, Color4 colour)
    {
        var sprite = new SpriteIcon
        {
            Origin = Anchor.Centre,
            Position = new Vector2(x + size / 2f, y + size / 2f),
            Size = new Vector2(size),
            Icon = icon,
            Colour = colour,
            Alpha = 0.9f,
        };

        decorationIcons.Add(sprite);
        return sprite;
    }

    private static Drawable createBrandLockup(Texture logoTexture) => new Sprite
    {
        Position = new Vector2(56, 46),
        Size = new Vector2(500, 169),
        Texture = logoTexture,
    };

    private Drawable createCommandArea() => new Container
    {
        Position = new Vector2(72, 208),
        Size = new Vector2(520, 394),
        Children = new Drawable[]
        {
            new FillFlowContainer
            {
                X = 34,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, -11),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = YokkoStrings.Get("main.hero_line_1"),
                        Font = HomeTypography.Hero(72),
                        Scale = new Vector2(1.08f, 1),
                        Colour = navy,
                    },
                    new Container
                    {
                        AutoSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            heroHighlight = new Box
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                RelativeSizeAxes = Axes.X,
                                Width = 1.04f,
                                Height = 26,
                                Y = 2,
                                Rotation = -1.2f,
                                Colour = new Color4(yellow.R, yellow.G, yellow.B, 0.5f),
                                Scale = new Vector2(0, 1),
                            },
                            new SpriteText
                            {
                                Text = YokkoStrings.Get("main.hero_line_2"),
                                Font = HomeTypography.Hero(72),
                                Scale = new Vector2(1.08f, 1),
                                Colour = navy,
                            },
                        },
                    },
                },
            },
            new HomeDotCross
            {
                Position = new Vector2(480, 18),
            },
            createDecorationIcon(FontAwesome.Solid.Plus, 508, 67, 15, pink),
            new HomePrimaryAction(
                YokkoStrings.Get("main.play"),
                YokkoStrings.Get("main.song_select"),
                FontAwesome.Solid.Play,
                () => this.Push(new GameplayScreen(DemoBeatmaps.CreateFourKeyDemo())))
            {
                Y = 162,
            },
            new HomeConnectorPlus
            {
                Position = new Vector2(546, 219),
            },
            new FillFlowContainer
            {
                Y = 302,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(16, 0),
                Children = new Drawable[]
                {
                    new HomeSecondaryAction(YokkoStrings.Get("main.editor"), FontAwesome.Solid.WindowMaximize,
                        () => this.Push(new EditorScreen()), FontAwesome.Solid.Pen),
                    new HomeSecondaryAction(YokkoStrings.Get("main.settings"), FontAwesome.Solid.Cog,
                        () => this.Push(new SettingsScreen())),
                },
            },
        },
    };

    private Drawable createUtilityArea() => new Container
    {
        Position = new Vector2(1016, 24),
        Size = new Vector2(240, 72),
        Child = new FillFlowContainer
        {
            RelativeSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(12, 0),
            Children = new Drawable[]
            {
                new HomeUtilityButton(string.Empty, FontAwesome.Solid.PowerOff,
                    exitGame, 72),
                new HomeUtilityButton(string.Empty, FontAwesome.Solid.Cog,
                    () => this.Push(new SettingsScreen()), 72),
                new HomeUtilityButton(string.Empty, FontAwesome.Solid.FolderOpen,
                    () => this.Push(new EditorScreen(true)), 72, FontAwesome.Solid.ArrowRight),
            },
        },
    };

    private void exitGame()
    {
        if (requestGameExit != null)
            requestGameExit();
        else
            host.Exit();
    }

    private Drawable createFooter(LocalisableString audioStatus) => new Container
    {
        Position = new Vector2(60, 655),
        Size = new Vector2(530, 48),
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Colour = new Color4(navy.R, navy.G, navy.B, 0.24f),
            },
            new Box
            {
                Width = 165,
                Height = 1,
                X = 116,
                Colour = new Color4(paleCyan.R, paleCyan.G, paleCyan.B, 0.72f),
            },
            heartbeatIcon = new SpriteIcon
            {
                Origin = Anchor.Centre,
                Position = new Vector2(271.5f, 1.5f),
                Size = new Vector2(23),
                Icon = FontAwesome.Solid.Heartbeat,
                Colour = navy,
            },
            new FillFlowContainer
            {
                Y = 17,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(25, 0),
                Children = new Drawable[]
                {
                    createKeycapCluster(),
                    createFooterItem(FontAwesome.Solid.SignOutAlt, YokkoStrings.Get("main.hold_esc_exit")),
                    createAudioFooterItem(audioStatus),
                },
            },
        },
    };

    private static Drawable createKeycapCluster() => new FillFlowContainer
    {
        AutoSizeAxes = Axes.Both,
        Direction = FillDirection.Horizontal,
        Spacing = new Vector2(8, 0),
        Children = new Drawable[]
        {
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Size = new Vector2(14),
                Icon = FontAwesome.Solid.Keyboard,
                Colour = navy,
            },
            new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(5, 0),
                Children = new Drawable[]
                {
                    new HomeKeycap("D"),
                    new HomeKeycap("F"),
                    new HomeKeycap("J"),
                    new HomeKeycap("K"),
                },
            },
        },
    };

    private Drawable createAudioFooterItem(LocalisableString text) => new FillFlowContainer
    {
        AutoSizeAxes = Axes.Both,
        Direction = FillDirection.Horizontal,
        Spacing = new Vector2(8, 0),
        Children = new Drawable[]
        {
            readyDot = new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Size = new Vector2(7),
                Colour = pink,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Size = new Vector2(14),
                Icon = FontAwesome.Solid.VolumeUp,
                Colour = navy,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = text,
                Font = HomeTypography.Display(14),
                Colour = mutedNavy,
            },
        },
    };

    private static Drawable createFooterItem(IconUsage icon, LocalisableString text) => new FillFlowContainer
    {
        AutoSizeAxes = Axes.Both,
        Direction = FillDirection.Horizontal,
        Spacing = new Vector2(8, 0),
        Children = new Drawable[]
        {
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Size = new Vector2(14),
                Icon = icon,
                Colour = navy,
            },
            new SpriteText
            {
                Text = text,
                Font = HomeTypography.Display(14),
                Colour = mutedNavy,
            },
        },
    };

    private static string backendDisplayName(AudioBackendCapabilities backend) => backend.Kind switch
    {
        AudioBackendKind.SharedWasapi => "WASAPI Shared",
        AudioBackendKind.WasapiExclusive => "WASAPI Exclusive",
        AudioBackendKind.Asio => "ASIO",
        _ => backend.Kind.ToString(),
    };
}
