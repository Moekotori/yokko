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
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Editor;
using Yokko.Game.Screens.Settings;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Screens.Main;

public partial class MainScreen : Screen
{
    private const float designedWidth = 1280;
    private const float designedHeight = 720;
    private const double exitHoldDuration = 2000;

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
    private HomeMascotBubble bubble;
    private HomeMusicPlayer musicPlayer;
    private HomeExitHoldIndicator exitIndicator;
    private readonly Box[] stageLines = new Box[2];
    private readonly List<SpriteIcon> decorationIcons = new();
    private readonly List<Drawable> floaters = new();
    private readonly Dictionary<Key, HomeKeycap> keycapByKey = new();
    private readonly Action requestGameExit;

    private static readonly LocalisableString[] bubbleLines =
    {
        YokkoStrings.Get("main.lets_play"),
        YokkoStrings.Get("main.bubble_again"),
        YokkoStrings.Get("main.bubble_pick_song"),
        YokkoStrings.Get("main.bubble_keys"),
    };

    private int bubbleLineIndex;
    private float sparkleAngleOffset;

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
        Texture logoTexture = textures.Get("home-logo-hd");

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
                            new SpriteText
                            {
                                Origin = Anchor.Centre,
                                Position = new Vector2(22, 380),
                                Rotation = -90,
                                Text = "RHYTHM CHART STUDIO · VOL.01",
                                Font = HomeTypography.Display(13),
                                Spacing = new Vector2(3, 0),
                                Colour = new Color4(navy.R, navy.G, navy.B, 0.32f),
                            },
                            new HomeBarcode("NO.004-KEY")
                            {
                                Position = new Vector2(612, 664),
                            },
                            new SpriteText
                            {
                                Position = new Vector2(714, 680),
                                Text = "EST. 2025 · 4K MANIA",
                                Font = HomeTypography.Display(10),
                                Spacing = new Vector2(1.8f, 0),
                                Colour = new Color4(navy.R, navy.G, navy.B, 0.5f),
                            },
                            new HomeDotField
                            {
                                Position = new Vector2(36, 545),
                                Size = new Vector2(84, 52),
                                Colour = new Color4(navy.R, navy.G, navy.B, 0.13f),
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
                    exitIndicator = new HomeExitHoldIndicator(YokkoStrings.Get("main.exit_hold"))
                    {
                        Position = new Vector2(60, 606),
                    },
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
        musicPlayer.Activate();

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
        musicPlayer.Activate();
        this.FadeIn(200, Easing.OutQuint);
    }

    public override void OnSuspending(ScreenTransitionEvent e)
    {
        base.OnSuspending(e);
        cancelExitHold();
        musicPlayer.Deactivate();
        this.FadeTo(0.4f, 200, Easing.OutQuint);
    }

    public override bool OnExiting(ScreenExitEvent e)
    {
        cancelExitHold();
        musicPlayer.Deactivate();
        this.FadeOut(200, Easing.OutQuint);
        return base.OnExiting(e);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key != Key.Escape)
        {
            // 播放器键盘控制：P 播放/暂停，← → 切歌。
            switch (e.Key)
            {
                case Key.P:
                    musicPlayer.TogglePlayPause();
                    return true;

                case Key.Right:
                    musicPlayer.NextTrack();
                    return true;

                case Key.Left:
                    musicPlayer.PreviousTrack();
                    return true;
            }

            // 页脚键帽与真实键盘联动。
            if (!e.Repeat && keycapByKey.TryGetValue(e.Key, out HomeKeycap keycap))
                keycap.SetPressed(true);

            return base.OnKeyDown(e);
        }

        if (!isEscapeHeld)
        {
            isEscapeHeld = true;
            escapeHoldStartedAt = Time.Current;
            exitIndicator.Reveal();
        }

        return true;
    }

    protected override void OnKeyUp(KeyUpEvent e)
    {
        if (e.Key == Key.Escape)
            cancelExitHold();
        else if (keycapByKey.TryGetValue(e.Key, out HomeKeycap keycap))
            keycap.SetPressed(false);

        base.OnKeyUp(e);
    }

    protected override void Update()
    {
        base.Update();

        if (isEscapeHeld)
        {
            double heldFor = Time.Current - escapeHoldStartedAt;
            exitIndicator.SetProgress((float)(heldFor / exitHoldDuration));

            if (heldFor >= exitHoldDuration)
            {
                cancelExitHold();
                requestGameExit?.Invoke();
            }
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
        exitIndicator?.Conceal();
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

    private void onMascotTapped()
    {
        // 压扁回弹，不影响既有的浮动/旋转循环（它们只动 Y 与 Rotation）。
        mascot.ScaleTo(new Vector2(1.05f, 0.93f), 90, Easing.Out)
              .Then().ScaleTo(Vector2.One, 700, Easing.OutElastic);

        bubbleLineIndex = (bubbleLineIndex + 1) % bubbleLines.Length;
        bubble.SetText(bubbleLines[bubbleLineIndex]);

        spawnSparkles();
    }

    private void spawnSparkles()
    {
        for (int i = 0; i < 4; i++)
        {
            float angle = sparkleAngleOffset + i * MathF.PI / 2;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

            var star = new SpriteIcon
            {
                Origin = Anchor.Centre,
                Position = new Vector2(937.5f, 330),
                Size = new Vector2(14),
                Icon = FontAwesome.Solid.Star,
                Colour = i % 2 == 0 ? yellow : Color4.White,
            };

            rightParallax.Add(star);
            star.MoveToOffset(direction * 72, 620, Easing.OutQuint);
            star.RotateTo(140, 620);
            star.FadeOut(620, Easing.InQuart).Expire();
        }

        sparkleAngleOffset += MathF.PI / 4;
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
                // 点击 mascot：弹跳、换台词、迸发星星。热区避开底部的播放器。
                new ClickableContainer
                {
                    Origin = Anchor.Centre,
                    Position = new Vector2(937.5f, 355),
                    Size = new Vector2(420, 470),
                    Action = onMascotTapped,
                },
                bubble = new HomeMascotBubble(bubbleLines[0])
                {
                    X = 632,
                    Y = 365,
                },
                new HomeTwinkle(14, 1700)
                {
                    Position = new Vector2(706, 300),
                },
                new HomeTwinkle(11, 2300)
                {
                    Position = new Vector2(1238, 252),
                },
                new HomeTwinkle(10, 2000)
                {
                    Position = new Vector2(1010, 118),
                    Colour = yellow,
                },
                new HomeTwinkle(12, 2600)
                {
                    Position = new Vector2(858, 578),
                    Colour = pink,
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
                musicPlayer = new HomeMusicPlayer
                {
                    Position = new Vector2(788, 624),
                },
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
                () => this.Push(new SongSelectScreen()))
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
        Size = new Vector2(240, 150),
        Children = new Drawable[]
        {
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                Height = 72,
                // 悬停 tooltip 向下弹出时要盖过下方的时钟卡片。
                Depth = -1,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(12, 0),
                Children = new Drawable[]
                {
                    new HomeUtilityButton(string.Empty, FontAwesome.Solid.PowerOff,
                        exitGame, 72, tooltipText: YokkoStrings.Get("main.utility_exit")),
                    new HomeUtilityButton(string.Empty, FontAwesome.Solid.Cog,
                        () => this.Push(new SettingsScreen()), 72, tooltipText: YokkoStrings.Get("main.settings")),
                    new HomeUtilityButton(string.Empty, FontAwesome.Solid.FolderOpen,
                        () => this.Push(new EditorScreen(true)), 72, FontAwesome.Solid.ArrowRight,
                        YokkoStrings.Get("main.utility_folder")),
                },
            },
            new HomeClock
            {
                Y = 84,
            },
            new HomeHazardStripes(240, new Color4(1f, 1f, 1f, 0.5f))
            {
                Y = 142,
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

    private Drawable createKeycapCluster()
    {
        keycapByKey.Clear();
        var keycaps = new FillFlowContainer
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(5, 0),
        };

        foreach ((string label, Key key) in new[] { ("D", Key.D), ("F", Key.F), ("J", Key.J), ("K", Key.K) })
        {
            var keycap = new HomeKeycap(label);
            keycapByKey[key] = keycap;
            keycaps.Add(keycap);
        }

        return new FillFlowContainer
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
                keycaps,
            },
        };
    }

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
