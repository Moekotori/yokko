using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Core.Beatmaps;
using Yokko.Core.Scoring;
using Yokko.Game.Gameplay;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Gameplay;

internal partial class GameplayPauseOverlay : CompositeDrawable
{
    // Legacy internal artboard. The stage is fitted to the shared 1920x1080
    // viewport in Update(); new full-screen layouts must use
    // YokkoDisplaySettings.ReferenceLayoutSize instead.
    internal static readonly Vector2 ReferenceSize = new(1600, 900);

    private const float sheetX = 44;
    private const float sheetY = 44;
    private const float sheetWidth = 1512;
    private const float sheetHeight = 812;
    private const float leftContentX = 78;
    private const float leftContentWidth = 440;
    private const float primaryActionWidth = 430;
    private const float primaryActionHeight = 126;
    private const float performanceX = 612;

    private static readonly Color4 backdropNavy =
        new(0.025f, 0.16f, 0.34f, 1f);
    private static readonly Color4 paperShadow =
        new(0.02f, 0.12f, 0.36f, 0.3f);
    private static readonly Color4 ruleColour =
        new(
            HomeControlColours.Navy.R,
            HomeControlColours.Navy.G,
            HomeControlColours.Navy.B,
            0.72f);
    private static readonly Color4 mutedNavy =
        new(
            HomeControlColours.Navy.R,
            HomeControlColours.Navy.G,
            HomeControlColours.Navy.B,
            0.68f);
    private static readonly Color4 softNavy =
        new(
            HomeControlColours.Navy.R,
            HomeControlColours.Navy.G,
            HomeControlColours.Navy.B,
            0.84f);
    private static readonly Color4 faintNavy =
        new(
            HomeControlColours.Navy.R,
            HomeControlColours.Navy.G,
            HomeControlColours.Navy.B,
            0.5f);

    private static readonly string[] bubblePhraseKeys =
    {
        "gameplay.pause.bubble",
        "gameplay.pause.bubble_alt1",
        "gameplay.pause.bubble_alt2",
        "gameplay.pause.bubble_alt3",
    };

    private readonly YokkoBeatmap beatmap;
    private readonly YokkoGameplaySettings gameplaySettings;
    private readonly GameplayPauseSnapshot snapshot;
    private readonly Action resume;
    private readonly Action retry;
    private readonly Action openLayoutEditor;
    private readonly Action openSettings;
    private readonly Action exitGameplay;
    private readonly PauseActionButton[] actions = new PauseActionButton[5];

    private Container stage;
    private Container parallaxBack;
    private Container parallaxFront;
    private HomeMascotBubble bubble;
    private int bubblePhraseIndex;
    private int selectedAction;

    internal int ActionCount => actions.Length;
    internal int SelectedAction => selectedAction;
    internal long DisplayedScore => snapshot.Score;
    internal double DisplayedAccuracy => snapshot.Accuracy;
    internal int DisplayedCombo => snapshot.Combo;
    internal int DisplayedMaxCombo => snapshot.MaxCombo;
    internal string DisplayedRank => snapshot.Rank;
    internal int DisplayedPauseCount => snapshot.PauseCount;

    public GameplayPauseOverlay(
        YokkoBeatmap beatmap,
        YokkoGameplaySettings gameplaySettings,
        GameplayPauseSnapshot snapshot,
        Action resume,
        Action retry,
        Action openSettings,
        Action exitGameplay,
        Action openLayoutEditor = null)
    {
        this.beatmap = beatmap;
        this.gameplaySettings = gameplaySettings;
        this.snapshot = snapshot;
        this.resume = resume;
        this.retry = retry;
        this.openLayoutEditor = openLayoutEditor ?? (() => { });
        this.openSettings = openSettings;
        this.exitGameplay = exitGameplay;

        RelativeSizeAxes = Axes.Both;
        Depth = -1000;
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textures, LocalisationManager localisation)
    {
        string titleText = localisation.GetLocalisedString(
            YokkoStrings.Get("gameplay.pause.title"));
        bool titlePrefersCjkFallback = titleText.Any(c => c > 127);

        InternalChildren = new Drawable[]
        {
            createBackdrop(),
            stage = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = ReferenceSize,
                Alpha = 0,
                Children = new Drawable[]
                {
                    parallaxBack = createGameplayGrid(),
                    createSheetShadow(),
                    createReportSheet(textures, titlePrefersCjkFallback),
                },
            },
        };

        selectAction(0);
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        stage.FadeInFromZero(180, Easing.OutQuint)
             .MoveToX(-18)
             .MoveToX(0, 360, Easing.OutQuint);
    }

    protected override void Update()
    {
        base.Update();

        // 参考版面固定为 1600x900，随可用空间整体缩放，
        // 任何窗口尺寸或 UI 缩放档位下暂停单都保持相同的相对大小。
        if (DrawWidth <= 0 || DrawHeight <= 0 || stage == null)
            return;

        float fit = MathF.Min(
            DrawWidth / ReferenceSize.X,
            DrawHeight / ReferenceSize.Y);
        stage.Scale = new Vector2(MathF.Max(fit, 0.01f));

        updateParallax();
    }

    /// <summary>
    /// 背景轨道线与纸面装饰跟随光标做反向/正向的轻微漂移，
    /// 让暂停单在静态画面里保留一点空间层次。位移很小，
    /// 不会影响任何阅读或点击目标。
    /// </summary>
    private void updateParallax()
    {
        if (parallaxBack == null || parallaxFront == null)
            return;

        var inputManager = GetContainingInputManager();
        if (inputManager == null)
            return;

        Vector2 mouse = ToLocalSpace(inputManager.CurrentState.Mouse.Position);
        var normalized = new Vector2(
            Math.Clamp(mouse.X / DrawWidth - 0.5f, -0.5f, 0.5f) * 2,
            Math.Clamp(mouse.Y / DrawHeight - 0.5f, -0.5f, 0.5f) * 2);

        Vector2 targetBack = normalized * -8;
        Vector2 targetFront = normalized * 10;
        parallaxBack.Position += (targetBack - parallaxBack.Position) * 0.06f;
        parallaxFront.Position += (targetFront - parallaxFront.Position) * 0.06f;
    }

    public bool HandleKey(Key key)
    {
        if (matches(ManiaShortcutAction.PauseOrBack, key))
        {
            resume();
            return true;
        }

        if (matches(ManiaShortcutAction.MenuPrevious, key)
            || matches(
                ManiaShortcutAction.MenuPreviousAlternate,
                key))
        {
            selectAction((selectedAction + actions.Length - 1) % actions.Length);
            return true;
        }

        if (matches(ManiaShortcutAction.MenuNext, key)
            || matches(
                ManiaShortcutAction.MenuNextAlternate,
                key))
        {
            selectAction((selectedAction + 1) % actions.Length);
            return true;
        }

        if (matches(ManiaShortcutAction.Confirm, key)
            || matches(ManiaShortcutAction.ConfirmAlternate, key))
        {
            actions[selectedAction].Trigger();
            return true;
        }

        if (matches(ManiaShortcutAction.Retry, key))
            retry();

        return true;
    }

    internal void SelectNext() =>
        selectAction((selectedAction + 1) % actions.Length);

    internal void TriggerSelected() => actions[selectedAction].Trigger();

    private static Drawable createBackdrop() =>
        new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = backdropNavy,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        0.18f),
                },
            },
        };

    private static Container createGameplayGrid()
    {
        var grid = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Alpha = 0.28f,
        };

        for (int i = 0; i < 4; i++)
        {
            grid.Add(new Box
            {
                Position = new Vector2(835 + i * 126, 0),
                Size = new Vector2(2, 900),
                Colour = Color4.White,
                Alpha = 0.42f,
            });
            grid.Add(new HomeRing(
                118,
                3,
                new Color4(1f, 1f, 1f, 0.38f))
            {
                Position = new Vector2(835 + i * 126, 828),
            });
        }

        grid.Add(new Circle
        {
            Position = new Vector2(1082, 10),
            Size = new Vector2(98),
            Colour = new Color4(1f, 1f, 1f, 0.18f),
        });

        return grid;
    }

    private static Drawable createSheetShadow() =>
        new Container
        {
            Position = new Vector2(sheetX + 10, sheetY + 12),
            Size = new Vector2(sheetWidth, sheetHeight),
            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = paperShadow,
            },
        };

    private Drawable createReportSheet(
        TextureStore textures,
        bool titlePrefersCjkFallback) =>
        new Container
        {
            Position = new Vector2(sheetX, sheetY),
            Size = new Vector2(sheetWidth, sheetHeight),
            Masking = true,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = HomeControlColours.Ivory,
                },
                createAngledDivider(),
                createLeftHeader(
                    textures.Get("Mods/home-logo-transparent")),
                createPauseCopy(titlePrefersCjkFallback),
                createActionColumn(),
                createKeyHintStrip(),
                createLeftFooter(),
                createSongHeader(),
                createPerformanceSummary(),
                createJudgementLedger(),
                createSuspendedAudioStrip(),
                createMascot(textures.Get("yokko")),
                parallaxFront = createSheetDecorations(),
            },
        };

    private static Drawable createLeftHeader(Texture logoTexture) =>
        new Container
        {
            Position = new Vector2(leftContentX, 32),
            Size = new Vector2(330, 112),
            Children = new Drawable[]
            {
                new SpriteIcon
                {
                    Position = new Vector2(4, 2),
                    Size = new Vector2(13),
                    Icon = FontAwesome.Solid.Plus,
                    Colour = HomeControlColours.Pink,
                },
                new SpriteIcon
                {
                    Position = new Vector2(48, 2),
                    Size = new Vector2(11),
                    Icon = FontAwesome.Solid.Plus,
                    Colour = HomeControlColours.Cyan,
                },
                new Sprite
                {
                    Position = new Vector2(-8, 28),
                    Size = new Vector2(292, 100),
                    FillMode = FillMode.Fit,
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Texture = logoTexture,
                },
                new HomeMicroLine
                {
                    Position = new Vector2(2, 126),
                    Width = 218,
                    Colour = HomeControlColours.Navy,
                },
            },
        };

    private static Drawable createPauseCopy(bool titlePrefersCjkFallback) =>
        new Container
        {
            Position = new Vector2(leftContentX, 196),
            Size = new Vector2(leftContentWidth, 198),
            Children = new Drawable[]
            {
                new SpriteText
                {
                    Text = YokkoStrings.Get("gameplay.pause.title"),
                    Font = pauseTitleFont(titlePrefersCjkFallback),
                    Colour = HomeControlColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(2, 86),
                    Text = YokkoStrings.Get("gameplay.pause.subtitle"),
                    Font = PauseTypography.Display(28),
                    Colour = softNavy,
                },
                new Box
                {
                    Position = new Vector2(0, 138),
                    Size = new Vector2(172, 5),
                    Colour = HomeControlColours.Yellow,
                },
                new Box
                {
                    Position = new Vector2(180, 138),
                    Size = new Vector2(26, 5),
                    Colour = HomeControlColours.Pink,
                },
                new PausedStatusRow
                {
                    Position = new Vector2(1, 168),
                },
            },
        };

    /// <summary>
    /// 拉丁标题用 ArchivoBlack 海报体；CJK 标题走 ArchivoBlack 的回退链
    /// 只能拿到 Yokko Regular，改走 Roboto Bold 的 CJK 回退拿到
    /// Yokko-Bold，让「暂停」这类标题保持粗体量感。
    /// </summary>
    private static FontUsage pauseTitleFont(bool prefersCjkFallback) =>
        prefersCjkFallback
            ? PauseTypography.Display(56)
            : PauseTypography.Poster(64);

    private Drawable createActionColumn()
    {
        actions[0] = new PauseActionButton(
            YokkoStrings.Get("gameplay.pause.resume"),
            $"{formatResumeKey()} TO RESUME",
            FontAwesome.Solid.Play,
            true,
            HomeControlColours.Pink,
            resume,
            () => selectAction(0))
        {
            Position = new Vector2(leftContentX - 4, 404),
        };
        actions[1] = new PauseActionButton(
            YokkoStrings.Get("gameplay.pause.retry"),
            string.Empty,
            FontAwesome.Solid.Redo,
            false,
            HomeControlColours.Pink,
            retry,
            () => selectAction(1),
            1,
            4)
        {
            Position = new Vector2(leftContentX, 562),
        };
        actions[2] = new PauseActionButton(
            "HUD 布局",
            string.Empty,
            FontAwesome.Solid.ThLarge,
            false,
            HomeControlColours.Yellow,
            openLayoutEditor,
            () => selectAction(2),
            2,
            4)
        {
            Position = new Vector2(
                leftContentX + leftContentWidth / 4,
                562),
        };
        actions[3] = new PauseActionButton(
            YokkoStrings.Get("gameplay.pause.settings"),
            string.Empty,
            FontAwesome.Solid.Cog,
            false,
            HomeControlColours.Cyan,
            openSettings,
            () => selectAction(3),
            3,
            4)
        {
            Position = new Vector2(
                leftContentX + leftContentWidth / 2,
                562),
        };
        actions[4] = new PauseActionButton(
            YokkoStrings.Get("gameplay.pause.exit"),
            string.Empty,
            FontAwesome.Solid.SignOutAlt,
            false,
            HomeControlColours.Pink,
            exitGameplay,
            () => selectAction(4),
            4,
            4)
        {
            Position = new Vector2(
                leftContentX + leftContentWidth * 3 / 4,
                562),
        };

        return new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new Container
                {
                    Position = new Vector2(leftContentX - 5, 570),
                    Size = new Vector2(leftContentWidth + 5, 76),
                    Masking = true,
                    CornerRadius = 9,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = paperShadow,
                    },
                },
                new Container
                {
                    Position = new Vector2(leftContentX - 5, 562),
                    Size = new Vector2(leftContentWidth + 5, 76),
                    Masking = true,
                    CornerRadius = 9,
                    BorderThickness = 2,
                    BorderColour = HomeControlColours.Navy,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = HomeControlColours.Ivory,
                        },
                        createSecondaryDivider(leftContentWidth / 4 + 5),
                        createSecondaryDivider(
                            leftContentWidth / 2 + 5),
                        createSecondaryDivider(
                            leftContentWidth * 3 / 4 + 5),
                    },
                },
                actions[0],
                actions[1],
                actions[2],
                actions[3],
                actions[4],
            },
        };
    }

    private Drawable createKeyHintStrip()
    {
        LocalisableString selectLabel =
            YokkoStrings.Get("gameplay.pause.hint_select");
        LocalisableString confirmLabel =
            YokkoStrings.Get("gameplay.pause.hint_confirm");
        LocalisableString retryLabel =
            YokkoStrings.Get("gameplay.pause.hint_retry");

        return new FillFlowContainer
        {
            Position = new Vector2(leftContentX - 4, 660),
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(5, 0),
            Children = new Drawable[]
            {
                createKeyChip(formatKey(ManiaShortcutAction.MenuPrevious)),
                createKeyChip(formatKey(ManiaShortcutAction.MenuNext)),
                createHintLabel(selectLabel),
                createKeyChip(
                    formatKey(ManiaShortcutAction.Confirm),
                    marginLeft: 10),
                createHintLabel(confirmLabel),
                createKeyChip(
                    formatKey(ManiaShortcutAction.Retry),
                    marginLeft: 10),
                createHintLabel(retryLabel),
            },
        };
    }

    private static Drawable createKeyChip(
        LocalisableString text,
        float marginLeft = 0) =>
        new Container
        {
            AutoSizeAxes = Axes.Both,
            Margin = new MarginPadding { Left = marginLeft },
            Masking = true,
            CornerRadius = 5,
            BorderThickness = 1.5f,
            BorderColour = new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.5f),
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
                new SpriteText
                {
                    Text = text,
                    Font = PauseTypography.Display(10),
                    Colour = HomeControlColours.Navy,
                    Padding = new MarginPadding
                    {
                        Horizontal = 7,
                        Vertical = 3.5f,
                    },
                },
            },
        };

    private static SpriteText createHintLabel(LocalisableString text) =>
        new()
        {
            Text = text,
            Font = PauseTypography.Display(9.5f),
            Spacing = new Vector2(1.4f, 0),
            Colour = faintNavy,
            Margin = new MarginPadding { Left = 3, Top = 5 },
        };

    private static Drawable createSecondaryDivider(float x) =>
        new Box
        {
            Position = new Vector2(x, 15),
            Size = new Vector2(1.5f, 46),
            Colour = new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.5f),
        };

    private static Drawable createLeftFooter() =>
        new Container
        {
            Position = new Vector2(leftContentX, 700),
            Size = new Vector2(leftContentWidth, 70),
            Children = new Drawable[]
            {
                new HomeBarcode("NO.004-KEY")
                {
                    X = -4,
                    Scale = new Vector2(1.32f),
                },
                new HomeDotField
                {
                    Position = new Vector2(298, -4),
                    Size = new Vector2(86, 52),
                    Colour = new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                            0.65f),
                },
            },
        };

    private static Drawable createAngledDivider() =>
        new Container
        {
            Position = new Vector2(591, -38),
            Size = new Vector2(3, 930),
            Rotation = 7.2f,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = HomeControlColours.Navy,
                },
                new Box
                {
                    Position = new Vector2(-1, 291),
                    Size = new Vector2(17),
                    Rotation = 45,
                    Colour = HomeControlColours.Yellow,
                },
                new Box
                {
                    Position = new Vector2(0, 465),
                    Size = new Vector2(13),
                    Rotation = 45,
                    Colour = HomeControlColours.Pink,
                },
                new Box
                {
                    Position = new Vector2(0, 641),
                    Size = new Vector2(15),
                    Rotation = 45,
                    Colour = HomeControlColours.Cyan,
                },
            },
        };

    private Drawable createSongHeader()
    {
        string displayedMods = string.IsNullOrWhiteSpace(snapshot.DisplayedMods)
            ? "NM"
            : snapshot.DisplayedMods;
        string artist = string.IsNullOrWhiteSpace(beatmap.Artist)
            ? beatmap.Creator
            : beatmap.Artist;

        return new Container
        {
            Position = new Vector2(performanceX + 24, 38),
            Size = new Vector2(800, 124),
            Children = new Drawable[]
            {
                new Container
                {
                    Position = new Vector2(-1, 5),
                    Size = new Vector2(82, 86),
                    Masking = true,
                    CornerRadius = 12,
                    BorderThickness = 2,
                    BorderColour = HomeControlColours.Navy,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = HomeControlColours.Ivory,
                        },
                        new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(35),
                            Icon = FontAwesome.Solid.Music,
                            Colour = HomeControlColours.Cyan,
                        },
                        new Container
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.Centre,
                            Position = new Vector2(4, -4),
                            Size = new Vector2(13),
                            Rotation = 45,
                            Colour = HomeControlColours.Yellow,
                        },
                    },
                },
                new ScrollingSongTitle(beatmap.Title)
                {
                    Position = new Vector2(97, 2),
                    Size = new Vector2(424, 48),
                },
                new SpriteText
                {
                    Position = new Vector2(98, 52),
                    Width = 420,
                    Truncate = true,
                    Text = artist,
                    Font = PauseTypography.Body(21),
                    Colour = mutedNavy,
                },
                new SpriteText
                {
                    Position = new Vector2(560, 12),
                    Text = $"{beatmap.KeysPerStage}K · {displayedMods}",
                    Font = PauseTypography.Display(16),
                    Spacing = new Vector2(2.4f, 0),
                    Colour = HomeControlColours.Cyan,
                },
                new SpriteText
                {
                    Position = new Vector2(560, 42),
                    Text = formatProgress(),
                    Font = PauseTypography.Body(16),
                    Colour = softNavy,
                },
                createProgressRule(),
                new HomeDotField
                {
                    Position = new Vector2(748, 14),
                    Size = new Vector2(52, 40),
                    Colour = new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        0.72f),
                },
                new Box
                {
                    Position = new Vector2(0, 112),
                    Size = new Vector2(792, 1.5f),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.22f),
                },
                new Box
                {
                    Position = new Vector2(796, 112),
                    Size = new Vector2(9),
                    Rotation = 45,
                    Origin = Anchor.Centre,
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.4f),
                },
            },
        };
    }

    private Drawable createProgressRule()
    {
        double denominator = Math.Max(1, snapshot.TotalTimeMilliseconds);
        float progress = (float)Math.Clamp(
            snapshot.GameplayTimeMilliseconds / denominator,
            0,
            1);

        const float ruleWidth = 300;

        var rule = new Container
        {
            Position = new Vector2(559, 76),
            Size = new Vector2(ruleWidth, 10),
            Children = new Drawable[]
            {
                new Box
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    RelativeSizeAxes = Axes.X,
                    Height = 2,
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.68f),
                },
                new Box
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Width = ruleWidth * progress,
                    Height = 3,
                    Colour = HomeControlColours.Cyan,
                },
                new Circle
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.Centre,
                    X = ruleWidth * progress,
                    Size = new Vector2(9),
                    Colour = HomeControlColours.Cyan,
                },
            },
        };

        for (int i = 1; i <= 3; i++)
        {
            rule.Add(new Box
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.BottomCentre,
                X = ruleWidth * i / 4f,
                Y = -3,
                Size = new Vector2(1.5f, 5),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.35f),
            });
        }

        return rule;
    }

    private Drawable createPerformanceSummary() =>
        new Container
        {
            Position = new Vector2(performanceX, 188),
            Size = new Vector2(800, 400),
            Children = new Drawable[]
            {
                createVerticalRule(new Vector2(12, 6), 205),
                createVerticalRule(new Vector2(470, 6), 205),
                new Container
                {
                    Position = new Vector2(52, 24),
                    Size = new Vector2(168, 34),
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = HomeControlColours.Ivory,
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = "ACCURACY",
                            Font = PauseTypography.Display(13),
                            Spacing = new Vector2(3.6f, 0),
                            Colour = HomeControlColours.Cyan,
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 1.5f,
                            Colour = HomeControlColours.Cyan,
                        },
                        new Box
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            RelativeSizeAxes = Axes.X,
                            Height = 1.5f,
                            Colour = HomeControlColours.Cyan,
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = 1.5f,
                            Colour = HomeControlColours.Cyan,
                        },
                        new Box
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            RelativeSizeAxes = Axes.Y,
                            Width = 1.5f,
                            Colour = HomeControlColours.Cyan,
                        },
                    },
                },
                new AccuracyReadout(
                    $"{snapshot.Accuracy * 100:0.00}")
                {
                    Position = new Vector2(34, 66),
                },
                new RankStamp(snapshot.Rank)
                {
                    Origin = Anchor.Centre,
                    Position = new Vector2(624, 127),
                },
                createVerticalRule(new Vector2(12, 248), 112),
                createSummaryMetric(
                    new Vector2(52, 268),
                    "SCORE",
                    $"{snapshot.Score:N0}",
                    205),
                createVerticalRule(new Vector2(242, 248), 112),
                createComboMetric(new Vector2(284, 268), 184),
                createVerticalRule(new Vector2(474, 248), 112),
                createSummaryMetric(
                    new Vector2(520, 268),
                    "PAUSES",
                    snapshot.PauseCount.ToString("00"),
                    128),
            },
        };

    private static Drawable createVerticalRule(Vector2 position, float height) =>
        new Container
        {
            Position = position,
            Size = new Vector2(12, height),
            Children = new Drawable[]
            {
                new Box
                {
                    X = 5,
                    Width = 2,
                    RelativeSizeAxes = Axes.Y,
                    Colour = ruleColour,
                },
                new Container
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(12),
                    Rotation = 45,
                    BorderThickness = 1.5f,
                    BorderColour = HomeControlColours.Navy,
                    Masking = true,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = HomeControlColours.Ivory,
                    },
                },
            },
        };

    /// <summary>
    /// 评级印章。悬停时整枚印章像被重新按下一样倾斜晃动，
    /// 评级字母闪过青色高光。
    /// </summary>
    private partial class RankStamp : CompositeDrawable
    {
        private readonly SpriteText rankText;

        public RankStamp(string rank)
        {
            Size = new Vector2(226, 214);

            InternalChildren = new Drawable[]
            {
                new SpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Text = "RANK",
                    Font = PauseTypography.Display(14),
                    Spacing = new Vector2(5, 0),
                    Colour = HomeControlColours.Cyan,
                },
                new HomeDashedRing(82, 25)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Y = -2,
                    Colour = HomeControlColours.Cyan,
                    Alpha = 0.32f,
                },
                new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Y = -4,
                    Size = new Vector2(136),
                    Masking = true,
                    CornerRadius = 68,
                    BorderThickness = 2,
                    BorderColour = HomeControlColours.Navy,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = HomeControlColours.Ivory,
                        },
                        new HomeDotField
                        {
                            Anchor = Anchor.BottomRight,
                            Origin = Anchor.BottomRight,
                            Position = new Vector2(-18, -24),
                            Size = new Vector2(68, 42),
                            Colour = new Color4(
                                HomeControlColours.Navy.R,
                                HomeControlColours.Navy.G,
                                HomeControlColours.Navy.B,
                                0.18f),
                        },
                        rankText = new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Y = -5,
                            Text = rank,
                            Font = PauseTypography.Display(
                                rank.Length switch
                                {
                                    <= 1 => 92,
                                    2 => 64,
                                    _ => 46,
                                }),
                            Colour = HomeControlColours.Navy,
                        },
                    },
                },
                createRankStar(9, 104),
                createRankStar(217, 104),
                new PauseSparkle(HomeControlColours.Cyan, 12, 2100)
                {
                    Position = new Vector2(6, 50),
                },
                new PauseSparkle(HomeControlColours.Pink, 10, 2650)
                {
                    Position = new Vector2(240, 178),
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            this.RotateTo(-3.5f, 240, Easing.OutQuint);
            rankText.FlashColour(HomeControlColours.Cyan, 420, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            this.RotateTo(0, 520, Easing.OutElastic);
        }
    }

    private static Drawable createRankStar(float x, float y) =>
        new SpriteIcon
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.Centre,
            Position = new Vector2(x, y),
            Size = new Vector2(23),
            Icon = FontAwesome.Solid.Star,
            Colour = HomeControlColours.Navy,
        };

    private static Drawable createSummaryMetric(
        Vector2 position,
        string label,
        string value,
        float ruleWidth) =>
        new Container
        {
            Position = position,
            Size = new Vector2(ruleWidth, 115),
            Children = new Drawable[]
            {
                new SpriteText
                {
                    Text = label,
                    Font = PauseTypography.Display(12),
                    Spacing = new Vector2(3, 0),
                    Colour = HomeControlColours.Cyan,
                },
                new SpriteText
                {
                    Position = new Vector2(0, 30),
                    Text = value,
                    Font = PauseTypography.Display(40),
                    Colour = HomeControlColours.Navy,
                },
                new Box
                {
                    Position = new Vector2(-25, 92),
                    Size = new Vector2(ruleWidth, 2),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.55f),
                },
            },
        };

    private Drawable createComboMetric(Vector2 position, float ruleWidth) =>
        new Container
        {
            Position = position,
            Size = new Vector2(ruleWidth, 115),
            Children = new Drawable[]
            {
                new SpriteText
                {
                    Text =
                        snapshot.JudgementConfiguration.Mode
                        == JudgementMode.Etterna
                            ? "COMBO · MISS"
                            : "COMBO",
                    Font = PauseTypography.Display(12),
                    Spacing = new Vector2(3, 0),
                    Colour = HomeControlColours.Cyan,
                },
                new FillFlowContainer
                {
                    Position = new Vector2(0, 30),
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Children = new Drawable[]
                    {
                        new SpriteText
                        {
                            Text =
                                snapshot.JudgementConfiguration.Mode
                                == JudgementMode.Etterna
                                    ? $"{snapshot.Combo} · {snapshot.MissCombo}"
                                    : snapshot.Combo.ToString(),
                            Font = PauseTypography.Display(40),
                            Colour = HomeControlColours.Navy,
                        },
                        new SpriteText
                        {
                            Margin = new MarginPadding
                            {
                                Left = 10,
                                Top = 8,
                            },
                            Text =
                                snapshot.JudgementConfiguration.Mode
                                == JudgementMode.Etterna
                                    ? $"/  MAX {snapshot.MaxCombo}"
                                      + $"  CB {snapshot.ComboBreaks}"
                                    : $"/  {snapshot.MaxCombo}",
                            Font = PauseTypography.Display(26),
                            Colour = HomeControlColours.Cyan,
                        },
                    },
                },
                new Box
                {
                    Position = new Vector2(-13, 92),
                    Size = new Vector2(ruleWidth + 13, 2),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.55f),
                },
            },
        };

    private Drawable createJudgementLedger()
    {
        string[] labels =
            snapshot.JudgementConfiguration.Mode
            == JudgementMode.Etterna
                ? ["MARVELOUS", "PERFECT", "GREAT", "GOOD", "BAD", "MISS"]
                : ["PERFECT", "GREAT", "GOOD", "OK", "MEH", "MISS"];
        (string Label, int Value, Color4 Colour)[] judgements =
        {
            (labels[0], snapshot.Perfect, HomeControlColours.Yellow),
            (labels[1], snapshot.Great, HomeControlColours.Cyan),
            (labels[2], snapshot.Good, new Color4(0.16f, 0.72f, 0.34f, 1f)),
            (labels[3], snapshot.Ok, new Color4(0.12f, 0.48f, 0.95f, 1f)),
            (labels[4], snapshot.Meh, new Color4(1f, 0.42f, 0.08f, 1f)),
            (labels[5], snapshot.Miss, HomeControlColours.Pink),
        };

        int totalJudged = 0;
        foreach ((_, int value, _) in judgements)
            totalJudged += value;

        var ledger = new Container
        {
            Position = new Vector2(performanceX - 2, 585),
            Size = new Vector2(612, 152),
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 2,
                    Colour = HomeControlColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(2, -18),
                    Text = "JUDGEMENT BREAKDOWN",
                    Font = PauseTypography.Display(10),
                    Spacing = new Vector2(2.2f, 0),
                    Colour = faintNavy,
                },
                new SpriteText
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Position = new Vector2(0, -18),
                    Text = $"TOTAL {totalJudged:N0}",
                    Font = PauseTypography.Display(10),
                    Spacing = new Vector2(1.6f, 0),
                    Colour = faintNavy,
                },
            },
        };

        const float cellWidth = 102;
        for (int i = 0; i < judgements.Length; i++)
        {
            (string label, int value, Color4 colour) = judgements[i];
            ledger.Add(new JudgementCell(
                $"0{i + 1}",
                label,
                value,
                colour,
                i < judgements.Length - 1)
            {
                X = i * cellWidth,
            });
        }

        ledger.Add(new HomeMicroLine
        {
            Position = new Vector2(0, 148),
            Width = 612,
            Colour = HomeControlColours.Navy,
        });
        return ledger;
    }

    private static Drawable createSuspendedAudioStrip() =>
        new SuspendedAudioStrip
        {
            Position = new Vector2(performanceX - 2, 754),
        };

    /// <summary>
    /// 判定明细格子。悬停时整格浮起淡色高光、计数数字弹跳放大、
    /// 底部色条伸长，方便逐项核对成绩构成。
    /// </summary>
    private partial class JudgementCell : CompositeDrawable
    {
        private const float cell_width = 102;

        private readonly Box highlight;
        private readonly SpriteText valueText;
        private readonly Box underline;

        public JudgementCell(
            string index,
            string label,
            int value,
            Color4 colour,
            bool showDivider)
        {
            Size = new Vector2(cell_width, 124);

            InternalChildren = new Drawable[]
            {
                highlight = new Box
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = 4,
                    Size = new Vector2(cell_width - 18, 112),
                    Colour = new Color4(
                        colour.R,
                        colour.G,
                        colour.B,
                        0.1f),
                    Alpha = 0,
                },
                new SpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = 10,
                    Text = index,
                    Font = PauseTypography.Display(9),
                    Spacing = new Vector2(1, 0),
                    Colour = new Color4(
                        colour.R,
                        colour.G,
                        colour.B,
                        0.55f),
                },
                new SpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = 26,
                    Text = label,
                    Font = PauseTypography.Display(11),
                    Spacing = new Vector2(1, 0),
                    Colour = colour,
                },
                valueText = new SpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = 58,
                    Text = value.ToString(),
                    Font = PauseTypography.Poster(30),
                    Colour = colour,
                },
                underline = new Box
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -10,
                    Size = new Vector2(48, 3),
                    Colour = colour,
                },
                createJudgementDivider(showDivider),
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            highlight.FadeIn(140, Easing.OutQuint);
            valueText.ScaleTo(1.18f, 260, Easing.OutBack);
            underline.ResizeWidthTo(66, 200, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            highlight.FadeOut(220, Easing.OutQuint);
            valueText.ScaleTo(1f, 260, Easing.OutQuint);
            underline.ResizeWidthTo(48, 220, Easing.OutQuint);
        }
    }

    private static Drawable createJudgementDivider(bool visible)
    {
        var divider = new Container
        {
            Anchor = Anchor.CentreRight,
            Origin = Anchor.CentreRight,
            Size = new Vector2(3, 70),
            Alpha = visible ? 1 : 0,
        };

        for (int i = 0; i < 9; i++)
        {
            divider.Add(new Circle
            {
                Y = i * 8.5f,
                Size = new Vector2(2.5f),
                Colour = new Color4(
                    HomeControlColours.Cyan.R,
                    HomeControlColours.Cyan.G,
                    HomeControlColours.Cyan.B,
                    0.48f),
            });
        }

        return divider;
    }

    private Drawable createMascot(Texture mascotTexture) =>
        new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new InteractiveMascot(mascotTexture, cycleBubblePhrase)
                {
                    Position = new Vector2(1192, 79),
                },
                bubble = new HomeMascotBubble(
                    YokkoStrings.Get("gameplay.pause.bubble"),
                    HomeMascotBubbleStyle.Rounded,
                    null,
                    cycleBubblePhrase)
                {
                    Position = new Vector2(1328, 454),
                },
                new PauseSparkle(HomeControlColours.Cyan, 13, 1950)
                {
                    Position = new Vector2(1314, 444),
                },
                new PauseSparkle(HomeControlColours.Yellow, 11, 2500)
                {
                    Position = new Vector2(1454, 566),
                },
            },
        };

    private void cycleBubblePhrase()
    {
        bubblePhraseIndex = (bubblePhraseIndex + 1) % bubblePhraseKeys.Length;
        bubble.SetText(YokkoStrings.Get(bubblePhraseKeys[bubblePhraseIndex]));
    }

    private static Container createSheetDecorations() =>
        new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                createSheetTickRuler(),
                createTapeStrip(new Vector2(28, -9), -22),
                createTapeStrip(new Vector2(1354, -9), 22),
                new HomeCrosshairMark
                {
                    Position = new Vector2(16, 16),
                },
                new HomeCrosshairMark
                {
                    Position = new Vector2(1470, 16),
                },
                new HomeCrosshairMark
                {
                    Position = new Vector2(16, 770),
                },
                new HomeDotField
                {
                    Position = new Vector2(1156, 711),
                    Size = new Vector2(66, 42),
                    Colour = new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        0.36f),
                },
            },
        };

    private static Drawable createSheetTickRuler()
    {
        var ruler = new Container
        {
            Position = new Vector2(1004, 0),
            Size = new Vector2(310, 10),
        };

        for (int i = 0; i <= 14; i++)
        {
            bool major = i % 5 == 0;
            ruler.Add(new Box
            {
                X = i * 22,
                Width = 1.5f,
                Height = major ? 9 : 5,
                Colour = HomeControlColours.Cyan,
                Alpha = major ? 0.5f : 0.28f,
            });
        }

        return ruler;
    }

    private static Drawable createTapeStrip(Vector2 position, float rotation) =>
        new Container
        {
            Position = position,
            Size = new Vector2(132, 26),
            Rotation = rotation,
            Masking = true,
            BorderThickness = 1,
            BorderColour = new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.1f),
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        HomeControlColours.PaleCyan.R,
                        HomeControlColours.PaleCyan.G,
                        HomeControlColours.PaleCyan.B,
                        0.6f),
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 9,
                    Colour = new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        0.28f),
                },
                new Box
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    RelativeSizeAxes = Axes.Y,
                    Width = 9,
                    Colour = new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        0.28f),
                },
            },
        };

    private string formatProgress() =>
        $"{formatTime(snapshot.GameplayTimeMilliseconds)} / "
        + formatTime(snapshot.TotalTimeMilliseconds);

    private static string formatTime(double milliseconds)
    {
        int totalSeconds = (int)Math.Max(0, milliseconds / 1000);
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    private void selectAction(int index)
    {
        selectedAction = index;

        for (int i = 0; i < actions.Length; i++)
            actions[i]?.SetSelected(i == selectedAction);
    }

    private bool matches(ManiaShortcutAction action, Key key) =>
        gameplaySettings.GetShortcutBinding(action) == key;

    private string formatKey(ManiaShortcutAction action) =>
        KeyModeBindings.FormatKey(
            gameplaySettings.GetShortcutBinding(action)).ToUpperInvariant();

    private string formatResumeKey()
    {
        Key key = gameplaySettings.GetShortcutBinding(
            ManiaShortcutAction.PauseOrBack);
        return key == Key.Escape
            ? "ESC"
            : KeyModeBindings.FormatKey(key).ToUpperInvariant();
    }

    /// <summary>
    /// 暂停界面的字体分工：海报体（ArchivoBlack，128px 基准图集，
    /// CJK 自动回退 Yokko 字体）负责大标题与主数字，Roboto 负责正文。
    /// </summary>
    private static class PauseTypography
    {
        public static FontUsage Poster(float size) => new("ArchivoBlack", size);

        public static FontUsage Display(float size) =>
            HomeTypography.Display(size);

        public static FontUsage Body(float size) => HomeTypography.Body(size);
    }

    private partial class AccuracyReadout : CompositeDrawable
    {
        private const float maximumFlowWidth = 408;

        private readonly FillFlowContainer flow;

        public AccuracyReadout(string value)
        {
            Size = new Vector2(412, 138);
            InternalChild = flow = new FillFlowContainer
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = value,
                        Font = PauseTypography.Display(102),
                        Colour = HomeControlColours.Navy,
                    },
                    new SpriteText
                    {
                        Margin = new MarginPadding
                        {
                            Left = 7,
                            Top = 46,
                        },
                        Text = "%",
                        Font = PauseTypography.Display(34),
                        Colour = HomeControlColours.Cyan,
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (flow.DrawWidth <= maximumFlowWidth)
                return;

            float scale = maximumFlowWidth / flow.DrawWidth;
            flow.Scale = new Vector2(scale);
        }
    }

    private partial class ScrollingSongTitle : CompositeDrawable
    {
        private readonly SpriteText title;

        public ScrollingSongTitle(LocalisableString text)
        {
            Masking = true;
            InternalChild = title = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = text,
                Font = PauseTypography.Display(33),
                Colour = HomeControlColours.Navy,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            float overflow = title.DrawWidth - DrawWidth;
            if (overflow <= 1)
                return;

            double duration = Math.Max(1800, overflow / 45 * 1000);
            title.Delay(1200)
                 .MoveToX(-overflow, duration, Easing.InOutSine)
                 .Delay(900)
                 .MoveToX(0, duration, Easing.InOutSine)
                 .Delay(1200)
                 .Loop();
        }
    }

    /// <summary>
    /// 暂停状态行：闪烁指示灯 + PAUSED 字样 + 本次暂停已持续的实时计时。
    /// </summary>
    private partial class PausedStatusRow : CompositeDrawable
    {
        private readonly SpriteText timer;
        private double pauseStartTime = double.MinValue;

        public PausedStatusRow()
        {
            Size = new Vector2(240, 22);

            InternalChildren = new Drawable[]
            {
                new PauseBlinkDot
                {
                    Position = new Vector2(1, 5),
                },
                new SpriteText
                {
                    Position = new Vector2(18, -1),
                    Text = "PAUSED",
                    Font = PauseTypography.Display(12),
                    Spacing = new Vector2(6, 0),
                    Colour = HomeControlColours.Cyan,
                },
                timer = new SpriteText
                {
                    Position = new Vector2(122, 2),
                    Text = "00:00",
                    Font = PauseTypography.Display(11),
                    Spacing = new Vector2(1.6f, 0),
                    Colour = faintNavy,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            pauseStartTime = Clock.CurrentTime;
        }

        protected override void Update()
        {
            base.Update();

            if (pauseStartTime == double.MinValue)
                return;

            timer.Text = formatTime(Clock.CurrentTime - pauseStartTime);
        }
    }

    /// <summary>
    /// 录像机式的暂停指示灯：光晕长明，核心以 1 秒节奏明暗呼吸。
    /// </summary>
    private partial class PauseBlinkDot : CompositeDrawable
    {
        public PauseBlinkDot()
        {
            Size = new Vector2(12);

            InternalChildren = new Drawable[]
            {
                new Circle
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(12),
                    Colour = new Color4(
                        HomeControlColours.Pink.R,
                        HomeControlColours.Pink.G,
                        HomeControlColours.Pink.B,
                        0.25f),
                },
                new BlinkCore(),
            };
        }

        private partial class BlinkCore : Circle
        {
            public BlinkCore()
            {
                Anchor = Anchor.Centre;
                Origin = Anchor.Centre;
                Size = new Vector2(7);
                Colour = HomeControlColours.Pink;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                this.FadeTo(0.3f, 480, Easing.InOutSine)
                    .Then().FadeTo(1f, 480, Easing.InOutSine)
                    .Loop();
            }
        }
    }

    /// <summary>
    /// 判定明细下方的冻结均衡器：音频已暂停，柱条停在随机高度，
    /// 只有少数几根偶尔抽动一下，暗示声音只是被按住而不是消失。
    /// </summary>
    private partial class SuspendedAudioStrip : CompositeDrawable
    {
        private const int bar_count = 44;

        private readonly Box[] bars = new Box[bar_count];
        private readonly float[] restHeights = new float[bar_count];

        public SuspendedAudioStrip()
        {
            Size = new Vector2(540, 48);

            var random = new Random(20260730);

            InternalChildren = new Drawable[]
            {
                new SpriteText
                {
                    Position = new Vector2(2, 0),
                    Text = "AUDIO SUSPENDED",
                    Font = PauseTypography.Display(9.5f),
                    Spacing = new Vector2(2.2f, 0),
                    Colour = faintNavy,
                },
                new Container
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Position = new Vector2(-2, 0),
                    Size = new Vector2(14, 12),
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = 4,
                            Colour = faintNavy,
                        },
                        new Box
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            RelativeSizeAxes = Axes.Y,
                            Width = 4,
                            Colour = faintNavy,
                        },
                    },
                },
            };

            for (int i = 0; i < bar_count; i++)
            {
                restHeights[i] = 5 + (float)random.NextDouble() * 21;
                bool cyan = i % 3 == 0;

                AddInternal(bars[i] = new Box
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Position = new Vector2(i * 7 + 2, -3),
                    Width = 4,
                    Height = restHeights[i],
                    Colour = cyan
                        ? new Color4(
                            HomeControlColours.Cyan.R,
                            HomeControlColours.Cyan.G,
                            HomeControlColours.Cyan.B,
                            0.5f)
                        : new Color4(
                            HomeControlColours.Navy.R,
                            HomeControlColours.Navy.G,
                            HomeControlColours.Navy.B,
                            0.3f),
                });
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            var random = new Random(7);

            for (int i = 3; i < bar_count; i += 9)
            {
                float peak = Math.Min(30, restHeights[i] + 6 + (float)random.NextDouble() * 8);
                bars[i]
                    .ResizeHeightTo(peak, 520, Easing.InOutSine)
                    .Then().ResizeHeightTo(restHeights[i], 620, Easing.InOutSine)
                    .Loop(i * 260 + 900);
            }
        }
    }

    /// <summary>
    /// 可戳的吉祥物：待机时缓慢呼吸浮动，悬停微微倾斜放大，
    /// 点击先压扁再回弹，并切换气泡台词。
    /// </summary>
    private partial class InteractiveMascot : CompositeDrawable
    {
        private readonly Container body;
        private readonly Action onPoke;
        private bool hovered;

        public InteractiveMascot(Texture texture, Action onPoke)
        {
            this.onPoke = onPoke;

            Size = new Vector2(630, 765);

            InternalChild = body = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Child = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Texture = texture,
                    FillMode = FillMode.Fit,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            body.MoveToY(-7, 1900, Easing.InOutSine)
                .Then().MoveToY(0, 1900, Easing.InOutSine)
                .Loop();
        }

        protected override bool OnHover(HoverEvent e)
        {
            hovered = true;
            body.ScaleTo(1.02f, 240, Easing.OutQuint);
            body.RotateTo(-1.2f, 240, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hovered = false;
            body.ScaleTo(1f, 320, Easing.OutQuint);
            body.RotateTo(0, 320, Easing.OutQuint);
        }

        protected override bool OnClick(ClickEvent e)
        {
            body.ClearTransforms(targetMember: nameof(Scale));
            body.ScaleTo(new Vector2(1.05f, 0.93f), 90, Easing.OutQuint)
                .Then().ScaleTo(hovered ? 1.02f : 1f, 620, Easing.OutElastic);
            onPoke();
            return true;
        }
    }

    /// <summary>
    /// 四点星光装饰，周期性弹出收回。Position 视为中心。
    /// </summary>
    private partial class PauseSparkle : CompositeDrawable
    {
        private readonly double loopPause;

        public PauseSparkle(Color4 colour, float size = 14, double loopPause = 1700)
        {
            this.loopPause = loopPause;

            Size = new Vector2(size);
            Origin = Anchor.Centre;
            Scale = Vector2.Zero;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(size, size * 0.22f),
                    Colour = colour,
                },
                new Box
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(size * 0.22f, size),
                    Colour = colour,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            this.ScaleTo(1f, 480, Easing.OutBack)
                .Then().ScaleTo(0f, 380, Easing.InBack)
                .Loop(loopPause);
        }
    }

    private partial class PauseActionButton : ClickableContainer
    {
        private readonly bool primary;
        private readonly Action hoverAction;
        private readonly Box background;
        private readonly Box accent;
        private readonly SpriteIcon chevron;
        private bool selected;

        public PauseActionButton(
            LocalisableString title,
            LocalisableString hint,
            IconUsage icon,
            bool primary,
            Color4 accentColour,
            Action action,
            Action hoverAction,
            int index = 0,
            int secondaryColumnCount = 3)
        {
            this.primary = primary;
            this.hoverAction = hoverAction;
            Action = action;
            bool compact = !primary && secondaryColumnCount >= 4;
            Size = primary
                ? new Vector2(primaryActionWidth, primaryActionHeight)
                : new Vector2(leftContentWidth / secondaryColumnCount, 76);

            float iconSize = primary ? 82 : compact ? 34 : 44;
            float iconInset = primary ? 22 : compact ? 7 : 10;
            float textX = primary ? 132 : compact ? 47 : 60;

            InternalChildren = new Drawable[]
            {
                primary
                    ? new Container { Alpha = 0 }
                    : new SpriteText
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Position = new Vector2(-9, 6),
                        Text = $"0{index}",
                        Font = PauseTypography.Display(8.5f),
                        Spacing = new Vector2(1.2f, 0),
                        Colour = new Color4(
                            HomeControlColours.Navy.R,
                            HomeControlColours.Navy.G,
                            HomeControlColours.Navy.B,
                            0.38f),
                    },
                primary
                    ? new Container
                    {
                        Position = new Vector2(0, 8),
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 13,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = paperShadow,
                        },
                    }
                    : new Container { Alpha = 0 },
                primary
                    ? new Container
                    {
                        Position = new Vector2(-3, -3),
                        Size = new Vector2(
                            primaryActionWidth + 6,
                            primaryActionHeight),
                        Masking = true,
                        CornerRadius = 15,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = HomeControlColours.Cyan,
                        },
                    }
                    : new Container { Alpha = 0 },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = primary ? 13 : 0,
                    BorderThickness = primary ? 2 : 0,
                    BorderColour = primary
                        ? HomeControlColours.Navy
                        : Color4.Transparent,
                    Children = new Drawable[]
                    {
                        background = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = primary
                                ? HomeControlColours.Navy
                                : Color4.Transparent,
                        },
                        primary
                            ? new HomeDotField
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                X = -88,
                                Size = new Vector2(86, 56),
                                Colour = new Color4(
                                    HomeControlColours.Cyan.R,
                                    HomeControlColours.Cyan.G,
                                    HomeControlColours.Cyan.B,
                                    0.2f),
                            }
                            : new Container { Alpha = 0 },
                        new Container
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            X = iconInset,
                            Size = new Vector2(iconSize),
                            Masking = true,
                            CornerRadius = primary ? 10 : 8,
                            BorderThickness = primary ? 2 : 0,
                            BorderColour = primary
                                ? Color4.White
                                : Color4.Transparent,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = primary
                                        ? Color4.White
                                        : new Color4(
                                            HomeControlColours.PaleCyan.R,
                                            HomeControlColours.PaleCyan.G,
                                            HomeControlColours.PaleCyan.B,
                                            0.82f),
                                },
                                new SpriteIcon
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Size = primary
                                        ? new Vector2(32)
                                        : new Vector2(compact ? 17 : 21),
                                    Icon = icon,
                                    Colour = HomeControlColours.Navy,
                                },
                            },
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            X = textX,
                            Y = primary ? -10 : 0,
                            Text = title,
                            Font = primary
                                ? PauseTypography.Display(38)
                                : PauseTypography.Display(compact ? 13 : 16),
                            Spacing = primary
                                ? Vector2.Zero
                                : new Vector2(0.4f, 0),
                            Colour = primary
                                ? Color4.White
                                : HomeControlColours.Navy,
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Position = new Vector2(textX, 28),
                            Text = hint,
                            Font = PauseTypography.Display(11),
                            Spacing = new Vector2(2, 0),
                            Colour = HomeControlColours.Cyan,
                            Alpha = primary ? 1 : 0,
                        },
                        chevron = new SpriteIcon
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            X = -20,
                            Size = primary
                                ? new Vector2(24)
                                : Vector2.Zero,
                            Icon = FontAwesome.Solid.ChevronRight,
                            Colour = HomeControlColours.Yellow,
                            Alpha = primary ? 1 : 0,
                        },
                    },
                },
                accent = new Box
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    X = primary ? 18 : 12,
                    Width = primary ? 112 : 42,
                    Height = primary ? 4 : 3,
                    Colour = accentColour,
                },
            };
        }

        public void SetSelected(bool selected)
        {
            this.selected = selected;

            background.FadeColour(
                primary
                    ? selected
                        ? new Color4(0.055f, 0.15f, 0.7f, 1f)
                        : HomeControlColours.Navy
                    : selected
                        ? HomeControlColours.PaleCyan
                        : Color4.Transparent,
                100,
                Easing.OutQuint);
            accent.ResizeWidthTo(
                selected
                    ? primary ? 274 : 64
                    : primary ? 112 : 42,
                130,
                Easing.OutQuint);
            if (primary)
                chevron.MoveToX(selected ? -12 : -20, 130, Easing.OutQuint);

            this.ScaleTo(selected ? 1.01f : 1f, 100, Easing.OutQuint);
        }

        public void Trigger() => Action?.Invoke();

        protected override bool OnHover(HoverEvent e)
        {
            hoverAction();
            return true;
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            this.ScaleTo((selected ? 1.01f : 1f) * 0.95f, 90, Easing.OutQuint);
            return base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            this.ScaleTo(selected ? 1.01f : 1f, 240, Easing.OutBack);
            base.OnMouseUp(e);
        }
    }
}
