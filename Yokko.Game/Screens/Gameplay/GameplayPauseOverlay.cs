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

    private readonly YokkoBeatmap beatmap;
    private readonly YokkoGameplaySettings gameplaySettings;
    private readonly GameplayPauseSnapshot snapshot;
    private readonly Action resume;
    private readonly Action retry;
    private readonly Action openSettings;
    private readonly Action exitGameplay;
    private readonly PauseActionButton[] actions = new PauseActionButton[4];

    private Container stage;
    private int selectedAction;

    internal int ActionCount => actions.Length;
    internal int SelectedAction => selectedAction;
    internal long DisplayedScore => snapshot.Score;
    internal double DisplayedAccuracy => snapshot.Accuracy;
    internal int DisplayedCombo => snapshot.Combo;
    internal int DisplayedMaxCombo => snapshot.MaxCombo;
    internal string DisplayedRank => snapshot.Rank;

    public GameplayPauseOverlay(
        YokkoBeatmap beatmap,
        YokkoGameplaySettings gameplaySettings,
        GameplayPauseSnapshot snapshot,
        Action resume,
        Action retry,
        Action openSettings,
        Action exitGameplay)
    {
        this.beatmap = beatmap;
        this.gameplaySettings = gameplaySettings;
        this.snapshot = snapshot;
        this.resume = resume;
        this.retry = retry;
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
                    createGameplayGrid(),
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

    private static Drawable createGameplayGrid()
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
                createLeftHeader(textures.Get("home-logo-hd")),
                createPauseCopy(titlePrefersCjkFallback),
                createActionColumn(),
                createKeyHintStrip(),
                createLeftFooter(),
                createSongHeader(),
                createPerformanceSummary(),
                createJudgementLedger(),
                createMascot(textures.Get("yokko")),
                createSheetDecorations(),
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
                    Position = new Vector2(-8, 33),
                    Size = new Vector2(282, 92),
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
                new SpriteText
                {
                    Position = new Vector2(1, 168),
                    Text = "PAUSED",
                    Font = PauseTypography.Display(12),
                    Spacing = new Vector2(6, 0),
                    Colour = HomeControlColours.Cyan,
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
            () => selectAction(1))
        {
            Position = new Vector2(leftContentX, 562),
        };
        actions[2] = new PauseActionButton(
            YokkoStrings.Get("gameplay.pause.settings"),
            string.Empty,
            FontAwesome.Solid.Cog,
            false,
            HomeControlColours.Cyan,
            openSettings,
            () => selectAction(2))
        {
            Position = new Vector2(
                leftContentX + leftContentWidth / 3,
                562),
        };
        actions[3] = new PauseActionButton(
            YokkoStrings.Get("gameplay.pause.exit"),
            string.Empty,
            FontAwesome.Solid.SignOutAlt,
            false,
            HomeControlColours.Pink,
            exitGameplay,
            () => selectAction(3))
        {
            Position = new Vector2(
                leftContentX + leftContentWidth * 2 / 3,
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
                        createSecondaryDivider(leftContentWidth / 3 + 5),
                        createSecondaryDivider(
                            leftContentWidth * 2 / 3 + 5),
                    },
                },
                actions[0],
                actions[1],
                actions[2],
                actions[3],
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
                createVerticalRule(new Vector2(447, 6), 205),
                new Container
                {
                    Position = new Vector2(126, 24),
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
                    Position = new Vector2(12, 50),
                },
                createRankStamp(),
                createVerticalRule(new Vector2(12, 248), 112),
                createSummaryMetric(
                    new Vector2(60, 268),
                    "SCORE",
                    $"{snapshot.Score:N0}",
                    270),
                createVerticalRule(new Vector2(348, 248), 112),
                createComboMetric(new Vector2(403, 268), 150),
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

    private Drawable createRankStamp() =>
        new Container
        {
            Position = new Vector2(500, 20),
            Size = new Vector2(245, 235),
            Children = new Drawable[]
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
                new HomeDashedRing(90, 26)
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
                    Size = new Vector2(144),
                    Masking = true,
                    CornerRadius = 72,
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
                        new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Y = -5,
                            Text = snapshot.Rank,
                            Font = PauseTypography.Poster(
                                snapshot.Rank.Length switch
                                {
                                    <= 1 => 108,
                                    2 => 72,
                                    _ => 52,
                                }),
                            Colour = HomeControlColours.Navy,
                        },
                    },
                },
                createRankStar(16, 108),
                createRankStar(234, 108),
                new PauseSparkle(HomeControlColours.Cyan, 12, 2100)
                {
                    Position = new Vector2(6, 50),
                },
                new PauseSparkle(HomeControlColours.Pink, 10, 2650)
                {
                    Position = new Vector2(240, 178),
                },
            },
        };

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
                    Text = "COMBO",
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
                            Text = snapshot.Combo.ToString(),
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
                            Text = $"/  {snapshot.MaxCombo}",
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
            ledger.Add(createJudgementCell(
                i * cellWidth,
                cellWidth,
                $"0{i + 1}",
                label,
                value,
                colour,
                i < judgements.Length - 1));
        }

        ledger.Add(new HomeMicroLine
        {
            Position = new Vector2(0, 148),
            Width = 612,
            Colour = HomeControlColours.Navy,
        });
        return ledger;
    }

    private static Drawable createJudgementCell(
        float x,
        float width,
        string index,
        string label,
        int value,
        Color4 colour,
        bool showDivider) =>
        new Container
        {
            Position = new Vector2(x, 0),
            Size = new Vector2(width, 124),
            Children = new Drawable[]
            {
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
                new SpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = 58,
                    Text = value.ToString(),
                    Font = PauseTypography.Poster(30),
                    Colour = colour,
                },
                new Box
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -10,
                    Size = new Vector2(48, 3),
                    Colour = colour,
                },
                createJudgementDivider(showDivider),
            },
        };

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

    private static Drawable createMascot(Texture mascotTexture) =>
        new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new Sprite
                {
                    Position = new Vector2(1192, 79),
                    Size = new Vector2(630, 765),
                    Texture = mascotTexture,
                    FillMode = FillMode.Fit,
                },
                new HomeMascotBubble(
                    YokkoStrings.Get("gameplay.pause.bubble"))
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

    private static Drawable createSheetDecorations() =>
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
        private const float maximumFlowWidth = 425;

        private readonly FillFlowContainer flow;

        public AccuracyReadout(string value)
        {
            Size = new Vector2(440, 160);
            InternalChild = flow = new FillFlowContainer
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = value,
                        Font = PauseTypography.Poster(118),
                        Colour = HomeControlColours.Navy,
                    },
                    new SpriteText
                    {
                        Margin = new MarginPadding
                        {
                            Left = 8,
                            Top = 56,
                        },
                        Text = "%",
                        Font = PauseTypography.Poster(42),
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

        public PauseActionButton(
            LocalisableString title,
            LocalisableString hint,
            IconUsage icon,
            bool primary,
            Color4 accentColour,
            Action action,
            Action hoverAction)
        {
            this.primary = primary;
            this.hoverAction = hoverAction;
            Action = action;
            Size = primary
                ? new Vector2(primaryActionWidth, primaryActionHeight)
                : new Vector2(leftContentWidth / 3, 76);

            float iconSize = primary ? 82 : 44;
            float iconInset = primary ? 22 : 10;
            float textX = primary ? 132 : 60;

            InternalChildren = new Drawable[]
            {
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
                                        : new Vector2(21),
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
                                : PauseTypography.Display(16),
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
    }
}
