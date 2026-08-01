using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Audio;
using Yokko.Game.Importing;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Editor;
using Yokko.Game.Screens.Settings;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Screens.Main;

public partial class MainScreen : Screen
{
    // Legacy authored coordinate floor. The responsive stage expands to the
    // shared 1920x1080 viewport; these values are not a full-screen reference.
    private const float designedWidth = 1280;
    private const float designedHeight = 720;
    private const float compactPlayerCardY = 638;
    private const float fullPlayerCardY = 700;
    private const float fullStatusBarY = 864;
    private const float musicPlayerHeight = 72;
    // 播放器底部为贴底波形带让位（带高约 64，并留 4px 呼吸缝）；带内立柱经卡片下方时自动收低。
    private const float musicPlayerBottomMargin = 68;
    private const double exitHoldDuration = 2000;
    private const double bubbleIdleLineInterval = 8000;
    private static readonly Vector2 mascotCentre = new(785, 500);
    private static readonly Vector2 mascotSize = new(1070, 1210);

    private static readonly Color4 ivory = new(0.992f, 0.992f, 0.988f, 1f);
    private static readonly Color4 cyan = new(0.29f, 0.81f, 0.94f, 1f);
    private static readonly Color4 navy = new(0.035f, 0.085f, 0.54f, 1f);
    private static readonly Color4 yellow = new(1f, 0.91f, 0.42f, 1f);
    private static readonly Color4 pink = new(1f, 0.22f, 0.65f, 1f);
    private static readonly Color4 mutedNavy = new(0.18f, 0.28f, 0.58f, 1f);
    private static readonly Color4 paleCyan = new(0.54f, 0.91f, 0.98f, 1f);

    private Container content;
    private Container leftStageLayout;
    private Container rightStageLayout;
    private Container decorationStageLayout;
    private Container utilityAreaLayout;
    private Container leftStage;
    private Container decorationLayer;
    private Container rightParallax;
    private Box ivoryBase;
    private Box ivorySlant;
    private Container slantStripe;
    private Drawable rightStage;
    private Drawable brandLockup;
    private Drawable commandArea;
    private Drawable utilityArea;
    private HomePrimaryAction primaryAction;
    private FillFlowContainer secondaryActionRow;
    private HomeMultiplayerAction multiplayerAction;
    private Sprite mascot;
    private SpriteText watermark;
    private Box heroHighlight;
    private HomeMascotBubble bubble;
    private HomeMusicPlayer musicPlayer;
    private HomeWaveformVisualiser waveform;
    private HomeExitHoldIndicator exitIndicator;
    private HomeKeyTestPad keyTestPad;
    private HomeSignalSnake signalSnake;
    private HomeMarqueeTicker ticker;
    private Drawable statusBar;
    private HomePlayerProgressCard playerProgressCard;
    private Circle readyDot;
    private readonly Box[] stageLines = new Box[2];
    private readonly List<SpriteIcon> decorationIcons = new();
    private readonly List<Drawable> floaters = new();
    private readonly Action requestGameExit;
    private readonly CancellationTokenSource songSelectPreloadCancellation =
        new();
    private readonly SongSelectSelectionMemory songSelectSelectionMemory = new();
    private SongSelectScreen preloadedSongSelect;
    private bool songSelectPreloadInProgress;
    private bool preloadResourcesDisposed;
    private bool songSelectOpenRequested;
    private int songSelectPreloadGeneration;

    private static readonly LocalisableString[] bubbleLines =
    {
        YokkoStrings.Get("main.lets_play"),
        YokkoStrings.Get("main.bubble_again"),
        YokkoStrings.Get("main.bubble_pick_song"),
        YokkoStrings.Get("main.bubble_keys"),
    };

    private int bubbleLineIndex;
    private double lastBubbleInteraction;
    private float sparkleAngleOffset;

    internal int BubbleLineIndex => bubbleLineIndex;

    internal int BubbleLineCount => bubbleLines.Length;

    internal int PreparedSongSelectEntryCount =>
        preloadedSongSelect?.VisibleEntryCount ?? -1;

    internal bool IsPreparedSongSelectCurrent =>
        preloadedSongSelect != null
        && preloadedSongSelect.LibraryRevision
        == importedChartLibrary.Revision;

    private Vector2 parallaxCurrent;
    private double escapeHoldStartedAt;
    private bool isEscapeHeld;

    [Resolved]
    private GameHost host { get; set; }
    [Resolved]
    private ImportedChartLibrary importedChartLibrary { get; set; }

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

        Texture yokkoTexture = textures.Get("yokko");
        Texture mascotTexture = yokkoTexture
                                        .Crop(new RectangleF(80, 1840, 1200, 1360));
        Texture logoTexture = textures.Get("home-logo-light");
        Texture bubbleStickerTexture = textures.Get("Home/home-mascot-bubble-sticker");

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
                    // 底部框线：与顶部字幕带呼应，给构图收口。
                    new Container
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = 8,
                        Child = new HomeHazardStripes(
                            4000,
                            new Color4(navy.R, navy.G, navy.B, 0.3f)),
                    },
                    rightStageLayout = new Container
                    {
                        Size = new Vector2(designedWidth, designedHeight),
                        Child = rightStage = createRightStage(mascotTexture, bubbleStickerTexture),
                    },
                    // 贴底整曲波形带：横贯舞台底边，立柱压在底部框线上生长，
                    // 盖在右舞台之上、左侧卡片之下，与顶部字幕带一起给构图收口。
                    // Separate from the mascot stage so this background toy
                    // cannot move or modify the character artwork.
                    signalSnake = new HomeSignalSnake(),
                    waveform = new HomeWaveformVisualiser
                    {
                        Alpha = 0,
                    },
                    // 点击涟漪：盖在右舞台之上、左侧卡片与工具区之下。
                    new HomeTapRippleLayer(),
                    decorationStageLayout = new Container
                    {
                        Size = new Vector2(designedWidth, designedHeight),
                        Child = decorationLayer = new Container
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
                                new HomeDotField
                                {
                                    Position = new Vector2(520, 468),
                                    Size = new Vector2(68, 38),
                                    Colour = new Color4(navy.R, navy.G, navy.B, 0.1f),
                                },
                                new HomeRing(20, 2.5f, cyan)
                                {
                                    Position = new Vector2(566, 146),
                                },
                                new HomeTwinkle(10, 2200)
                                {
                                    Position = new Vector2(526, 116),
                                    Colour = pink,
                                },
                                registerFloater(new osu.Framework.Graphics.Shapes.Triangle
                                {
                                    Position = new Vector2(548, 528),
                                    Size = new Vector2(12, 11),
                                    Rotation = 18,
                                    Colour = new Color4(navy.R, navy.G, navy.B, 0.28f),
                                }),
                                new SpriteText
                                {
                                    Position = new Vector2(508, 516),
                                    Text = "04 // INPUT",
                                    Font = HomeTypography.Display(9),
                                    Spacing = new Vector2(1.5f, 0),
                                    Colour = new Color4(navy.R, navy.G, navy.B, 0.38f),
                                },
                            },
                        },
                    },
                    leftStageLayout = new Container
                    {
                        Size = new Vector2(designedWidth, designedHeight),
                        Child = leftStage = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                brandLockup = new HomeBrandLockup(logoTexture, navy, yellow),
                                commandArea = createCommandArea(),
                                playerProgressCard = new HomePlayerProgressCard(
                                    mascotTexture,
                                    new HomePlayerSummary(
                                        "YOKKO_PLAYER",
                                        7,
                                        24,
                                        72,
                                        1284,
                                        36))
                                {
                                    Position = new Vector2(72, fullPlayerCardY),
                                },
                                statusBar = createStatusBar(audioStatus),
                            },
                        },
                    },
                    utilityAreaLayout = new Container
                    {
                        Size = new Vector2(designedWidth, designedHeight),
                        Child = utilityArea = createUtilityArea(),
                    },
                    exitIndicator = new HomeExitHoldIndicator(YokkoStrings.Get("main.exit_hold"))
                    {
                        Position = new Vector2(60, 670),
                    },
                    ticker = new HomeMarqueeTicker(),
                },
            },
        };

        // 入场初始状态，OnEntering 中归位。
        keyTestPad.LanePressed += signalSnake.HandleLane;
        musicPlayer.AttachWaveform(waveform);
        brandLockup.X -= 26;
        brandLockup.Alpha = 0;
        commandArea.Y += 24;
        commandArea.Alpha = 0;
        playerProgressCard.Y += 24;
        playerProgressCard.Alpha = 0;
        statusBar.Y += 24;
        statusBar.Alpha = 0;
        rightStage.X += 44;
        rightStage.Alpha = 0;
        utilityArea.Y -= 20;
        utilityArea.Alpha = 0;
        ticker.Alpha = 0;

        // MainScreen itself is loaded off the update thread. Wait for the one
        // startup library scan before constructing SongSelect, otherwise an
        // empty preloaded screen queues its full row rebuild until the click
        // that first puts it in the drawable tree.
        try
        {
            importedChartLibrary.StartupLoadTask.GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                "Could not finish the startup beatmap scan before preloading song select.",
                LoggingTarget.Runtime);
        }

        // Finish the first page load before exposing Play. Later visits are
        // prepared asynchronously when MainScreen resumes.
        importedChartLibrary.LibraryChanged += onChartLibraryChanged;
        preloadedSongSelect = new SongSelectScreen(
            previewAudioEngine: null,
            // MainScreen starts the next preload when it resumes. Building a
            // second full browser while this one is visible causes an entry hitch.
            requestNextPreload: null,
            selectionMemory: songSelectSelectionMemory,
            previewHost: musicPlayer);
        LoadComponent(preloadedSongSelect);
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
        playerProgressCard.Delay(250).FadeIn(420);
        statusBar.Delay(320).FadeIn(420);
        utilityArea.Delay(340).FadeIn(360).MoveToY(24, 460, Easing.OutQuint);
        ticker.Delay(430).FadeIn(520);
        waveform.Delay(500).FadeIn(560);
        keyTestPad.Delay(560).FadeIn(420);
    }

    public override void OnResuming(ScreenTransitionEvent e)
    {
        base.OnResuming(e);
        beginSongSelectPreload();
        musicPlayer.Activate();
        this.FadeIn(200, Easing.OutQuint);
    }

    public override void OnSuspending(ScreenTransitionEvent e)
    {
        base.OnSuspending(e);
        cancelExitHold();
        musicPlayer.Deactivate(pause: !KeepsMusicPlaying(e.Next));
        this.FadeTo(0.4f, 200, Easing.OutQuint);
    }

    public override bool OnExiting(ScreenExitEvent e)
    {
        cancelExitHold();
        musicPlayer.Deactivate();
        this.FadeOut(200, Easing.OutQuint);
        return base.OnExiting(e);
    }

    private void openSongSelect()
    {
        if (songSelectOpenRequested)
            return;

        if (preloadedSongSelect != null
            && preloadedSongSelect.LibraryRevision
            != importedChartLibrary.Revision)
        {
            Logger.Log(
                "Discarding a stale song select preload before navigation.",
                LoggingTarget.Runtime,
                LogLevel.Important);
            invalidateSongSelectPreload();
        }

        if (preloadedSongSelect == null)
        {
            Logger.Log(
                "Song select navigation is waiting for its background preload.",
                LoggingTarget.Runtime,
                LogLevel.Important);
            songSelectOpenRequested = true;
            beginSongSelectPreload();
            return;
        }

        Logger.Log(
            "Song select navigation used a prepared screen.",
            LoggingTarget.Runtime,
            LogLevel.Important);
        pushPreloadedSongSelect();
    }

    private void beginSongSelectPreload()
    {
        if (preloadedSongSelect != null
            || songSelectPreloadInProgress
            || songSelectPreloadCancellation.IsCancellationRequested)
        {
            return;
        }

        songSelectPreloadInProgress = true;
        int generation = songSelectPreloadGeneration;
        _ = LoadComponentAsync(
            new SongSelectScreen(
                previewAudioEngine: null,
                requestNextPreload: null,
                selectionMemory: songSelectSelectionMemory,
                previewHost: musicPlayer),
            screen =>
            {
                songSelectPreloadInProgress = false;

                if (generation != songSelectPreloadGeneration
                    || screen.LibraryRevision
                    != importedChartLibrary.Revision)
                {
                    screen.Dispose();
                    beginSongSelectPreload();
                    return;
                }

                preloadedSongSelect = screen;
                if (songSelectOpenRequested)
                    pushPreloadedSongSelect();
            },
            songSelectPreloadCancellation.Token);
    }

    private void pushPreloadedSongSelect()
    {
        SongSelectScreen screen = preloadedSongSelect;
        if (screen == null)
            return;

        preloadedSongSelect = null;
        songSelectOpenRequested = false;
        this.Push(screen);
    }

    private void onChartLibraryChanged() => Scheduler.Add(() =>
    {
        if (preloadResourcesDisposed
            || songSelectPreloadCancellation.IsCancellationRequested
            || IsPreparedSongSelectCurrent)
        {
            return;
        }

        invalidateSongSelectPreload();
    });

    private void invalidateSongSelectPreload()
    {
        songSelectPreloadGeneration++;
        preloadedSongSelect?.Dispose();
        preloadedSongSelect = null;
        beginSongSelectPreload();
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing && !preloadResourcesDisposed)
        {
            preloadResourcesDisposed = true;
            if (keyTestPad != null && signalSnake != null)
                keyTestPad.LanePressed -= signalSnake.HandleLane;
            if (importedChartLibrary != null)
                importedChartLibrary.LibraryChanged -= onChartLibraryChanged;
            songSelectPreloadCancellation.Cancel();
            preloadedSongSelect?.Dispose();
            songSelectPreloadCancellation.Dispose();
        }

        base.Dispose(isDisposing);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key != Key.Escape)
        {
            // Bare arrows steer the visible background toy. When the toy is
            // hidden at compact sizes, the existing left/right track shortcuts
            // continue to work.
            if (signalSnake.TryHandleArrowKey(e.Key, e.Repeat))
                return true;

            // 播放器键盘控制：P 播放/暂停；小游戏隐藏的紧凑布局下，← → 切歌。
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

            // 4K 键位试玩盘：D F J K 点亮对应键帽。
            if (!e.Repeat && keyTestPad.TryHandleKey(e.Key, true))
                return true;

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
        else
            keyTestPad.TryHandleKey(e.Key, false);

        base.OnKeyUp(e);
    }

    protected override void Update()
    {
        base.Update();
        updateResponsiveLayout();

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

        // 待机一段时间后吉祥物自己换台词，让主页保持“活着”。
        if (Time.Current - lastBubbleInteraction > bubbleIdleLineInterval)
        {
            lastBubbleInteraction = Time.Current;
            advanceBubbleLine();
        }

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

    private void updateResponsiveLayout()
    {
        if (content == null || DrawWidth <= 0 || DrawHeight <= 0)
            return;

        Vector2 stageSize = CalculateResponsiveStageSize(new Vector2(
            DrawWidth,
            DrawHeight));
        Vector2 extra = stageSize - new Vector2(designedWidth, designedHeight);
        Vector2 rightStageOffset = CalculateRightStageOffset(stageSize);

        content.Size = stageSize;
        leftStageLayout.Y = extra.Y * 0.18f;
        decorationStageLayout.Y = extra.Y * 0.18f;
        rightStageLayout.Position = rightStageOffset;
        musicPlayer.Y = CalculateMusicPlayerY(stageSize);
        waveform.Width = CalculateWaveformWidth(stageSize);
        utilityAreaLayout.X = extra.X;
        exitIndicator.Y = stageSize.Y - 50;
        signalSnake.Position = new Vector2(
            620 + MathF.Max(extra.X, 0) * 0.15f,
            220 + MathF.Max(extra.Y, 0) * 0.12f);
        signalSnake.SetAvailable(stageSize.X >= 1500 && stageSize.Y >= 820);

        updatePlayerCardLayout(stageSize.Y < 900);
        updateWaveformObstacles(stageSize, rightStageOffset);

        float stageLeft = MathF.Max((DrawWidth - stageSize.X) / 2, 0);
        float ivoryWidth = stageLeft + 510;
        ivoryBase.Width = ivoryWidth;
        ivorySlant.X = ivoryWidth;
        slantStripe.X = ivoryWidth + 220;
    }

    private void updatePlayerCardLayout(bool compact)
    {
        if (playerProgressCard == null)
            return;

        playerProgressCard.SetCompact(compact);

        if (compact)
        {
            brandLockup.Position = new Vector2(56, 28);
            brandLockup.Size = new Vector2(450, 152);
            commandArea.Position = new Vector2(72, 180);
            primaryAction.Y = 150;
            secondaryActionRow.Y = 278;
            multiplayerAction.Y = 366;
            playerProgressCard.Position = new Vector2(72, compactPlayerCardY);
            // 键位试玩盘上移到播放器左侧，给贴底波形带让位。
            keyTestPad.Position = new Vector2(620, 588);
            statusBar.Alpha = 0;
            return;
        }

        brandLockup.Position = new Vector2(56, 46);
        brandLockup.Size = new Vector2(500, 169);
        commandArea.Position = new Vector2(72, 208);
        primaryAction.Y = 164;
        secondaryActionRow.Y = 300;
        multiplayerAction.Y = 398;
        playerProgressCard.Position = new Vector2(72, fullPlayerCardY);
        keyTestPad.Position = new Vector2(440, 676);
        statusBar.Position = new Vector2(72, fullStatusBarY);
        statusBar.Alpha = 1;
    }

    internal static Vector2 CalculateResponsiveStageSize(Vector2 viewport) =>
        new(
            MathF.Max(viewport.X, designedWidth),
            MathF.Max(viewport.Y, designedHeight));

    internal static bool PlayerCardLayoutsHaveBreathingRoom =>
        180 + 366 + 82 + 10 <= compactPlayerCardY
        && compactPlayerCardY + HomePlayerProgressCard.CompactHeight <= designedHeight
        && 208 + 398 + 82 + 12 <= fullPlayerCardY
        && fullPlayerCardY + HomePlayerProgressCard.FullHeight + 16 <= fullStatusBarY;

    internal static Vector2 CalculateRightStageOffset(Vector2 stageSize)
    {
        Vector2 extra = stageSize - new Vector2(designedWidth, designedHeight);
        return new Vector2(
            MathF.Max(extra.X, 0),
            MathF.Max(extra.Y, 0) * 0.5f);
    }

    internal static float CalculateMusicPlayerY(Vector2 stageSize) =>
        stageSize.Y
        - CalculateRightStageOffset(stageSize).Y
        - musicPlayerHeight
        - musicPlayerBottomMargin;

    internal static float CalculateWaveformWidth(Vector2 stageSize) =>
        MathF.Max(0, stageSize.X);

    /// <summary>
    /// 把悬浮在波形带上的卡片（播放器、键位试玩盘）登记为收低区间，
    /// 立柱经过它们下方时自动变矮，看起来像从卡片背后穿过。
    /// </summary>
    private void updateWaveformObstacles(Vector2 stageSize, Vector2 rightStageOffset)
    {
        float bandBottom = stageSize.Y;
        float playerLeft = 788 + rightStageOffset.X;
        // 播放器卡片高 72，含 4px 投影。
        float playerBottom = rightStageOffset.Y + musicPlayer.Y + 76;
        // 键位试玩盘本体高 74，含四周各约 8px 的投影与角标。
        float keypadLeft = keyTestPad.Position.X + rightStageOffset.X - 8;
        float keypadBottom = keyTestPad.Position.Y + rightStageOffset.Y + 82;

        waveform.SetObstacles(
            (playerLeft, playerLeft + 456, bandBottom - playerBottom - 6),
            (keypadLeft, keypadLeft + 166, bandBottom - keypadBottom - 6));
    }

    private void cancelExitHold()
    {
        isEscapeHeld = false;
        escapeHoldStartedAt = 0;
        exitIndicator?.Conceal();
    }

    internal static bool KeepsMusicPlaying(IScreen next) =>
        next is SettingsScreen or SongSelectScreen;

    private void startAmbientMotion()
    {
        mascot.MoveToY(mascotCentre.Y + 7, 1900, Easing.InOutSine)
              .Then().MoveToY(mascotCentre.Y - 7, 1900, Easing.InOutSine)
              .Loop();
        mascot.RotateTo(1.1f, 2400, Easing.InOutSine)
              .Then().RotateTo(-1.1f, 2400, Easing.InOutSine)
              .Loop();

        watermark.FadeTo(0.22f, 2600, Easing.InOutSine)
                 .Then().FadeTo(0.1f, 2600, Easing.InOutSine)
                 .Loop();

        for (int i = 0; i < stageLines.Length; i++)
        {
            float restWidth = stageLines[i].Width;
            double duration = 1800 + i * 420;

            stageLines[i].FadeTo(0.55f, duration, Easing.InOutSine)
                         .Then().FadeTo(1f, duration, Easing.InOutSine)
                         .Loop();
            stageLines[i].ResizeWidthTo(restWidth * 1.16f, duration, Easing.InOutSine)
                         .Then().ResizeWidthTo(restWidth, duration, Easing.InOutSine)
                         .Loop();
        }

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

        advanceBubbleLine();
        spawnSparkles();
        lastBubbleInteraction = Time.Current;
    }

    /// <summary>
    /// 切到下一句气泡台词；点击 mascot 或待机超时都会走到这里。
    /// </summary>
    internal void advanceBubbleLine()
    {
        bubbleLineIndex = (bubbleLineIndex + 1) % bubbleLines.Length;
        bubble.SetText(bubbleLines[bubbleLineIndex]);
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
                Position = new Vector2(mascotCentre.X, mascotCentre.Y - 62),
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

    private Drawable createIvoryStage() => new Container
    {
        RelativeSizeAxes = Axes.Both,
        Children = new Drawable[]
        {
            ivoryBase = new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 510,
                Colour = ivory,
            },
            ivorySlant = new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 220,
                X = 510,
                Y = -20,
                Height = 1.14f,
                Rotation = 11,
                Colour = ivory,
            },
            slantStripe = createSlantStripe(),
        },
    };

    /// <summary>
    /// 沿象牙面板斜边的描边条：细亮线 + 刻度短杠，强化主对角线。
    /// 与 ivorySlant 共用同一旋转基准，右缘始终贴合。
    /// </summary>
    private static Container createSlantStripe()
    {
        var stripe = new Container
        {
            RelativeSizeAxes = Axes.Y,
            Height = 1.14f,
            Width = 30,
            X = 510 + 220,
            Y = -20,
            Rotation = 11,
        };

        stripe.Add(new Box
        {
            RelativeSizeAxes = Axes.Y,
            Width = 3,
            Colour = new Color4(paleCyan.R, paleCyan.G, paleCyan.B, 0.9f),
        });

        for (int i = 1; i <= 12; i++)
        {
            bool major = i % 4 == 0;
            stripe.Add(new Box
            {
                RelativePositionAxes = Axes.Y,
                Y = i / 13f,
                X = 7,
                Size = new Vector2(major ? 15 : 9, 2),
                Colour = major
                    ? new Color4(yellow.R, yellow.G, yellow.B, 0.95f)
                    : i % 4 == 2
                        ? new Color4(pink.R, pink.G, pink.B, 0.9f)
                        : new Color4(navy.R, navy.G, navy.B, 0.32f),
            });
        }

        return stripe;
    }

    private Drawable createRightStage(Texture mascotTexture, Texture bubbleStickerTexture) => new Container
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
                    Position = new Vector2(96, 300),
                    Size = new Vector2(152, 78),
                    Colour = new Color4(1f, 1f, 1f, 0.14f),
                },
                new HomeCrosshairMark
                {
                    Position = new Vector2(292, 316),
                },
                new HomeBeatPips(
                    new Color4(1f, 1f, 1f, 0.62f),
                    pink)
                {
                    Position = new Vector2(132, 346),
                },
                new SpriteText
                {
                    Position = new Vector2(132, 365),
                    Text = "CHART BUS // ONLINE",
                    Font = HomeTypography.Display(9),
                    Spacing = new Vector2(1.4f, 0),
                    Colour = new Color4(navy.R, navy.G, navy.B, 0.5f),
                },
                new HomeSignalWave(
                    new Color4(navy.R, navy.G, navy.B, 0.46f))
                {
                    Position = new Vector2(132, 402),
                },
                new HomePulseBeacon(
                    54,
                    new Color4(1f, 1f, 1f, 0.6f),
                    yellow)
                {
                    Position = new Vector2(330, 428),
                },
                new HomeTelemetryRail(
                    212,
                    "INPUT STREAM // 04",
                    new Color4(1f, 1f, 1f, 0.72f),
                    pink)
                {
                    Position = new Vector2(118, 458),
                    Alpha = 0.82f,
                },
                new HomeTwinkle(11, 2150)
                {
                    Position = new Vector2(280, 382),
                    Colour = yellow,
                },
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
                new HomeDotField
                {
                    Position = new Vector2(420, 82),
                    Size = new Vector2(76, 42),
                    Colour = new Color4(navy.R, navy.G, navy.B, 0.16f),
                },
                new HomeBeatPips(
                    new Color4(navy.R, navy.G, navy.B, 0.48f),
                    pink)
                {
                    Position = new Vector2(428, 150),
                },
                new SpriteText
                {
                    Position = new Vector2(428, 170),
                    Text = "BEAT GRID // 08",
                    Font = HomeTypography.Display(9),
                    Spacing = new Vector2(1.4f, 0),
                    Colour = new Color4(navy.R, navy.G, navy.B, 0.42f),
                },
                new HomeCrosshairMark
                {
                    Position = new Vector2(536, 104),
                },
                new HomeRing(22, 2.5f, yellow)
                {
                    Position = new Vector2(522, 214),
                },
                new HomeTwinkle(9, 2350)
                {
                    Position = new Vector2(472, 210),
                    Colour = pink,
                },
                createDecorationIcon(
                    FontAwesome.Solid.Plus,
                    446,
                    62,
                    10,
                    pink),
                createDecorationIcon(
                    FontAwesome.Solid.Plus,
                    506,
                    354,
                    9,
                    yellow),
                new HomeRing(
                    18,
                    2,
                    new Color4(navy.R, navy.G, navy.B, 0.46f))
                {
                    Position = new Vector2(456, 352),
                },
                new SpriteText
                {
                    Position = new Vector2(424, 382),
                    Text = "CAL // 1.00",
                    Font = HomeTypography.Display(9),
                    Spacing = new Vector2(1.5f, 0),
                    Colour = new Color4(navy.R, navy.G, navy.B, 0.42f),
                },
                registerFloater(new Circle
                {
                    Position = new Vector2(490, 426),
                    Size = new Vector2(6),
                    Colour = pink,
                    Alpha = 0.82f,
                }),
                new HomeTickRuler(460)
                {
                    Position = new Vector2(768, 26),
                },
                new HomeRing(900, 1.5f, new Color4(1f, 1f, 1f, 0.14f))
                {
                    Position = mascotCentre,
                },
                new HomeDashedRing(420)
                {
                    Position = mascotCentre,
                    Colour = new Color4(1f, 1f, 1f, 0.3f),
                },
                new HomeOrbitNodes(
                    330,
                    new Color4(1f, 1f, 1f, 0.68f),
                    yellow,
                    6)
                {
                    Position = mascotCentre,
                    Alpha = 0.78f,
                },
                new HomePulseBeacon(
                    78,
                    new Color4(1f, 1f, 1f, 0.72f),
                    pink)
                {
                    Position = new Vector2(458, 252),
                },
                new HomeSignalWave(new Color4(1f, 1f, 1f, 0.78f))
                {
                    Position = new Vector2(470, 240),
                },
                new SpriteText
                {
                    Position = new Vector2(470, 274),
                    Text = "SYNC // 04.00",
                    Font = HomeTypography.Display(10),
                    Spacing = new Vector2(1.7f, 0),
                    Colour = new Color4(1f, 1f, 1f, 0.68f),
                },
                new HomeDotField
                {
                    Position = new Vector2(480, 184),
                    Size = new Vector2(58, 34),
                    Colour = new Color4(1f, 1f, 1f, 0.22f),
                },
                registerFloater(new osu.Framework.Graphics.Shapes.Triangle
                {
                    Position = new Vector2(760, 120),
                    Size = new Vector2(19, 17),
                    Rotation = 90,
                    Colour = new Color4(1f, 1f, 1f, 0.28f),
                }),
                registerFloater(new osu.Framework.Graphics.Shapes.Triangle
                {
                    Position = new Vector2(558, 286),
                    Size = new Vector2(11, 10),
                    Rotation = 225,
                    Colour = new Color4(navy.R, navy.G, navy.B, 0.3f),
                }),
                new HomeTelemetryRail(
                    220,
                    "LIVE SIGNAL // 128",
                    new Color4(1f, 1f, 1f, 0.78f),
                    yellow)
                {
                    Position = new Vector2(438, 584),
                    Alpha = 0.86f,
                },
                mascot = new Sprite
                {
                    Origin = Anchor.Centre,
                    Position = mascotCentre,
                    Size = mascotSize,
                    Texture = mascotTexture,
                },
                // 点击 mascot：弹跳、换台词、迸发星星。热区避开底部的播放器。
                new ClickableContainer
                {
                    Origin = Anchor.Centre,
                    Position = mascotCentre + new Vector2(0, -150),
                    Size = new Vector2(600, 540),
                    Action = onMascotTapped,
                },
                bubble = new HomeMascotBubble(
                    bubbleLines[0],
                    HomeMascotBubbleStyle.PopSignalSticker,
                    bubbleStickerTexture,
                    onMascotTapped)
                {
                    X = 350,
                    Y = 348,
                    Scale = new Vector2(1.35f),
                },
                new SpriteText
                {
                    Origin = Anchor.Centre,
                    Position = new Vector2(1266, 380),
                    Rotation = -90,
                    Text = "MANIA CHART LAB · 4K",
                    Font = HomeTypography.Display(12),
                    Spacing = new Vector2(2.6f, 0),
                    Colour = new Color4(1f, 1f, 1f, 0.3f),
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
                new HomeTwinkle(9, 2100)
                {
                    Position = new Vector2(752, 248),
                    Colour = yellow,
                },
                new HomeTwinkle(8, 2450)
                {
                    Position = new Vector2(1155, 468),
                },
                new HomeRing(26, 3.5f, yellow)
                {
                    Position = new Vector2(1248, 420),
                },
                new HomeRing(18, 2.5f, new Color4(1f, 1f, 1f, 0.7f))
                {
                    Position = new Vector2(742, 532),
                },
                registerFloater(new osu.Framework.Graphics.Shapes.Triangle
                {
                    Position = new Vector2(1168, 372),
                    Size = new Vector2(13, 12),
                    Rotation = 42,
                    Colour = new Color4(1f, 1f, 1f, 0.38f),
                }),
                registerFloater(new Circle
                {
                    Position = new Vector2(712, 648),
                    Size = new Vector2(8),
                    Colour = pink,
                    Alpha = 0.9f,
                }),
                new HomeSignalWave(new Color4(navy.R, navy.G, navy.B, 0.5f))
                {
                    Position = new Vector2(452, 640),
                },
                new SpriteText
                {
                    Position = new Vector2(452, 672),
                    Text = "GROOVE // LINK",
                    Font = HomeTypography.Display(9),
                    Spacing = new Vector2(1.5f, 0),
                    Colour = new Color4(navy.R, navy.G, navy.B, 0.55f),
                },
                new HomeDottedRail(340, new Color4(navy.R, navy.G, navy.B, 0.3f))
                {
                    Position = new Vector2(88, 292),
                },
                new HomeBeatPips(
                    new Color4(navy.R, navy.G, navy.B, 0.5f),
                    pink)
                {
                    Position = new Vector2(100, 638),
                },
                new SpriteText
                {
                    Position = new Vector2(100, 658),
                    Text = "READY // GO",
                    Font = HomeTypography.Display(9),
                    Spacing = new Vector2(1.5f, 0),
                    Colour = new Color4(navy.R, navy.G, navy.B, 0.48f),
                },
                new HomeDotField
                {
                    Position = new Vector2(60, 700),
                    Size = new Vector2(110, 48),
                    Colour = new Color4(navy.R, navy.G, navy.B, 0.22f),
                },
                new HomeRing(24, 2.5f, yellow)
                {
                    Position = new Vector2(-60, 640),
                },
                new HomeRing(22, 2.5f, pink)
                {
                    Position = new Vector2(232, 652),
                },
                registerFloater(new Circle
                {
                    Position = new Vector2(90, 780),
                    Size = new Vector2(7),
                    Colour = pink,
                    Alpha = 0.85f,
                }),
                new HomeSignalWave(new Color4(navy.R, navy.G, navy.B, 0.45f))
                {
                    Position = new Vector2(-200, 700),
                },
                new SpriteText
                {
                    Position = new Vector2(-200, 730),
                    Text = "BEAT // STREAM",
                    Font = HomeTypography.Display(9),
                    Spacing = new Vector2(1.5f, 0),
                    Colour = new Color4(navy.R, navy.G, navy.B, 0.45f),
                },
                new HomeDotField
                {
                    Position = new Vector2(-180, 770),
                    Size = new Vector2(120, 52),
                    Colour = new Color4(navy.R, navy.G, navy.B, 0.22f),
                },
                new HomeRing(24, 2.5f, yellow)
                {
                    Position = new Vector2(-60, 850),
                },
                new HomeTwinkle(9, 2050)
                {
                    Position = new Vector2(-180, 850),
                    Colour = pink,
                },
                registerFloater(new osu.Framework.Graphics.Shapes.Triangle
                {
                    Position = new Vector2(-20, 760),
                    Size = new Vector2(10, 9),
                    Rotation = 70,
                    Colour = new Color4(navy.R, navy.G, navy.B, 0.3f),
                }),
                new HomeCrosshairMark
                {
                    Position = new Vector2(-220, 800),
                },
                registerFloater(new Circle
                {
                    Position = new Vector2(-140, 900),
                    Size = new Vector2(6),
                    Colour = pink,
                    Alpha = 0.8f,
                }),
                new HomeRing(20, 2.5f, yellow)
                {
                    Position = new Vector2(452, 704),
                },
                new HomeTwinkle(10, 1900)
                {
                    Position = new Vector2(700, 700),
                    Colour = pink,
                },
                new HomeCrosshairMark
                {
                    Position = new Vector2(556, 706),
                },
                registerFloater(new osu.Framework.Graphics.Shapes.Triangle
                {
                    Position = new Vector2(352, 576),
                    Size = new Vector2(11, 10),
                    Rotation = 42,
                    Colour = new Color4(navy.R, navy.G, navy.B, 0.3f),
                }),
                createDecorationIcon(FontAwesome.Solid.Plus, 240, 590, 9, yellow),
                keyTestPad = new HomeKeyTestPad
                {
                    Position = new Vector2(440, 676),
                    Alpha = 0,
                },
                musicPlayer = new HomeMusicPlayer
                {
                    Position = new Vector2(788, 636),
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
        var spark = new HomeSparkIcon(icon, size, colour)
        {
            Position = new Vector2(x + size / 2f, y + size / 2f),
        };

        decorationIcons.Add(spark.Icon);
        return spark;
    }

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
            primaryAction = new HomePrimaryAction(
                YokkoStrings.Get("main.play"),
                YokkoStrings.Get("main.song_select"),
                FontAwesome.Solid.Play,
                openSongSelect)
            {
                Y = 164,
            },
            new HomeConnectorPlus
            {
                Position = new Vector2(546, 219),
            },
            secondaryActionRow = new FillFlowContainer
            {
                Y = 300,
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
            multiplayerAction = new HomeMultiplayerAction(
                YokkoStrings.Get("main.multiplayer"),
                default,
                onMultiplayerSelected)
            {
                Y = 398,
            },
        },
    };

    /// <summary>
    /// 左下角状态栏：细分隔线 + 心跳 + 音频后端状态。
    /// </summary>
    private Drawable createStatusBar(LocalisableString audioStatus) => new Container
    {
        Position = new Vector2(72, fullStatusBarY),
        Size = new Vector2(520, 36),
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
                Width = 160,
                Height = 1,
                X = 110,
                Colour = new Color4(paleCyan.R, paleCyan.G, paleCyan.B, 0.72f),
            },
            new FillFlowContainer
            {
                Y = 10,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(18, 0),
                Children = new Drawable[]
                {
                    createAudioStatusItem(audioStatus),
                },
            },
        },
    };

    private Drawable createAudioStatusItem(LocalisableString text) => new FillFlowContainer
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

    private static string backendDisplayName(AudioBackendCapabilities backend) => backend.Kind switch
    {
        AudioBackendKind.SharedWasapi => "WASAPI Shared",
        AudioBackendKind.WasapiExclusive => "WASAPI Exclusive",
        AudioBackendKind.Asio => "ASIO",
        _ => backend.Kind.ToString(),
    };

    private Drawable createUtilityArea() => new Container
    {        Position = new Vector2(1016, 24),
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

    private void onMultiplayerSelected()
    {
        bubble.SetText(YokkoStrings.Get("settings.coming_soon"));
        spawnSparkles();
        lastBubbleInteraction = Time.Current;
    }

}
