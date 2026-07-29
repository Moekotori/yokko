using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Gameplay;

internal partial class GameplayResultOverlay : CompositeDrawable
{
    private const float designedWidth = 1280;
    private const float designedHeight = 720;

    private readonly Action retry;
    private readonly Action watchReplay;
    private readonly Action returnToSongSelect;
    private Container stage;
    private AnimatedGifSprite mascot;

    internal int MascotFrameCount => mascot?.FrameCount ?? 0;
    internal int ActionCount => 3;
    internal string DisplayedMods { get; }
    internal bool PracticeSession { get; }

    public GameplayResultOverlay(
        YokkoBeatmap beatmap,
        ManiaScoreResult result,
        bool isNewBest,
        Action retry,
        Action watchReplay,
        Action returnToSongSelect)
        : this(
            beatmap,
            result,
            ManiaModSet.Empty,
            isNewBest,
            retry,
            watchReplay,
            returnToSongSelect)
    {
    }

    public GameplayResultOverlay(
        YokkoBeatmap beatmap,
        ManiaScoreResult result,
        ManiaModSet mods,
        bool isNewBest,
        Action retry,
        Action watchReplay,
        Action returnToSongSelect,
        bool practiceSession = false)
    {
        mods ??= ManiaModSet.Empty;
        this.retry = retry;
        this.watchReplay = watchReplay;
        this.returnToSongSelect = returnToSongSelect;
        PracticeSession = practiceSession;
        string displayedMods = mods.IsEmpty
            ? "NM"
            : string.Join("  ", mods.DisplayLabels);
        DisplayedMods = practiceSession
            ? $"{displayedMods}  ·  PRACTICE"
            : displayedMods;

        RelativeSizeAxes = Axes.Both;
        Depth = -10;

        string rank = result.Rank.ToDisplayLabel();

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = HomeControlColours.Cyan,
            },
            createIvoryStage(),
            stage = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(designedWidth, designedHeight),
                Alpha = 0,
                Children = new Drawable[]
                {
                    createDecorations(),
                    createResultContent(
                        beatmap,
                        result,
                        rank,
                        DisplayedMods,
                        isNewBest),
                    createMascotStage(),
                },
            },
        };
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        stage.FadeInFromZero(260, Easing.OutQuint)
             .MoveToOffset(new Vector2(0, -8))
             .MoveToOffset(new Vector2(0, 8), 420, Easing.OutQuint);
    }

    internal void TriggerReplay() => watchReplay();

    private Drawable createResultContent(
        YokkoBeatmap beatmap,
        ManiaScoreResult result,
        string rank,
        string displayedMods,
        bool isNewBest) =>
        new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new SpriteText
                {
                    Position = new Vector2(72, 38),
                    Text = YokkoStrings.Get("gameplay.result.title"),
                    Font = HomeTypography.Display(42),
                    Colour = HomeControlColours.Navy,
                },
                new Box
                {
                    Position = new Vector2(72, 94),
                    Size = new Vector2(205, 2),
                    Colour = HomeControlColours.Navy,
                },
                new HomeMicroLine
                {
                    Position = new Vector2(268, 92),
                    Width = 58,
                },
                new SpriteIcon
                {
                    Position = new Vector2(76, 134),
                    Size = new Vector2(18),
                    Icon = FontAwesome.Solid.Play,
                    Colour = HomeControlColours.Cyan,
                },
                new SpriteText
                {
                    Position = new Vector2(108, 127),
                    Text = $"{beatmap.Title} [{beatmap.DifficultyName}]",
                    Font = HomeTypography.Display(22),
                    Colour = HomeControlColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(108, 158),
                    Text = displayedMods,
                    Font = HomeTypography.Display(15),
                    Colour = HomeControlColours.Pink,
                },
                new SpriteText
                {
                    Position = new Vector2(92, 186),
                    Text = rank,
                    Font = HomeTypography.Hero(154),
                    Colour = HomeControlColours.Navy,
                },
                createNewBestBadge(isNewBest),
                new SpriteText
                {
                    Position = new Vector2(325, 270),
                    Text = $"{result.Score:0000000}",
                    Font = HomeTypography.Hero(61),
                    Colour = HomeControlColours.Navy,
                },
                new Box
                {
                    Position = new Vector2(325, 338),
                    Size = new Vector2(360, 11),
                    Colour = new Color4(
                        HomeControlColours.Yellow.R,
                        HomeControlColours.Yellow.G,
                        HomeControlColours.Yellow.B,
                        0.62f),
                },
                createSummaryRail(result),
                createJudgementStrip(result),
                createActionRow(),
            },
        };

    private Drawable createSummaryRail(ManiaScoreResult result) =>
        new Container
        {
            Position = new Vector2(76, 390),
            Size = new Vector2(610, 66),
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 2,
                    Colour = HomeControlColours.Navy,
                },
                new SpriteIcon
                {
                    Position = new Vector2(70, 23),
                    Size = new Vector2(24),
                    Icon = FontAwesome.Solid.Bullseye,
                    Colour = HomeControlColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(110, 15),
                    Text = $"{result.Accuracy * 100:0.00}%",
                    Font = HomeTypography.Display(30),
                    Colour = HomeControlColours.Navy,
                },
                new Box
                {
                    Position = new Vector2(310, 15),
                    Size = new Vector2(1, 32),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.35f),
                },
                new SpriteText
                {
                    Position = new Vector2(354, 23),
                    Text = YokkoStrings.Get("gameplay.result.max_combo"),
                    Font = HomeTypography.Display(15),
                    Colour = HomeControlColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(520, 15),
                    Text = result.MaxCombo.ToString(),
                    Font = HomeTypography.Display(30),
                    Colour = HomeControlColours.Navy,
                },
                new HomeMicroLine
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Position = new Vector2(610, -1),
                    Width = 52,
                },
            },
        };

    private Drawable createJudgementStrip(ManiaScoreResult result)
    {
        (string Label, int Value, Color4 Colour)[] judgements =
        {
            ("P", result.Perfect, HomeControlColours.Pink),
            ("G", result.Great, HomeControlColours.Cyan),
            ("GOOD", result.Good, new Color4(0.14f, 0.72f, 0.42f, 1f)),
            ("OK", result.Ok, new Color4(1f, 0.7f, 0.08f, 1f)),
            ("MEH", result.Meh, new Color4(0.65f, 0.27f, 0.96f, 1f)),
            ("MISS", result.Miss, new Color4(0.97f, 0.12f, 0.19f, 1f)),
        };

        var flow = new FillFlowContainer
        {
            RelativeSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
        };

        foreach ((string label, int value, Color4 colour) in judgements)
            flow.Add(createJudgementCell(label, value, colour));

        return new Container
        {
            Position = new Vector2(76, 462),
            Size = new Vector2(610, 92),
            Masking = true,
            CornerRadius = 6,
            BorderThickness = 1.5f,
            BorderColour = HomeControlColours.Navy,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
                flow,
            },
        };
    }

    private static Drawable createJudgementCell(
        string label,
        int value,
        Color4 colour) =>
        new Container
        {
            Width = 610f / 6,
            RelativeSizeAxes = Axes.Y,
            Children = new Drawable[]
            {
                new Box
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Width = 1,
                    RelativeSizeAxes = Axes.Y,
                    Height = 0.62f,
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.28f),
                },
                new SpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = 19,
                    Text = label,
                    Font = HomeTypography.Display(14),
                    Colour = colour,
                },
                new SpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = 50,
                    Text = value.ToString(),
                    Font = HomeTypography.Display(22),
                    Colour = HomeControlColours.Navy,
                },
            },
        };

    private Drawable createActionRow() =>
        new FillFlowContainer
        {
            Position = new Vector2(76, 584),
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(12, 0),
            Children = new Drawable[]
            {
                new ResultActionButton(
                    YokkoStrings.Get("gameplay.result.retry"),
                    "R",
                    FontAwesome.Solid.Play,
                    retry),
                new ResultActionButton(
                    YokkoStrings.Get("gameplay.result.watch_replay"),
                    "V",
                    FontAwesome.Solid.Redo,
                    watchReplay),
                new ResultActionButton(
                    YokkoStrings.Get("gameplay.result.return"),
                    "ESC",
                    FontAwesome.Solid.FolderOpen,
                    returnToSongSelect),
            },
        };

    private Drawable createMascotStage() =>
        new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new SpriteText
                {
                    Position = new Vector2(915, 26),
                    Text = "YOKKO",
                    Font = HomeTypography.Brand(72),
                    Colour = new Color4(1f, 1f, 1f, 0.12f),
                },
                new HomeDotField
                {
                    Position = new Vector2(1180, 286),
                    Size = new Vector2(76, 64),
                    Colour = new Color4(1f, 1f, 1f, 0.24f),
                },
                new HomeCrosshairMark
                {
                    Position = new Vector2(772, 516),
                    Colour = Color4.White,
                    Alpha = 0.7f,
                },
                mascot = new AnimatedGifSprite(
                    "Textures/Gameplay/kbn.gif")
                {
                    Position = new Vector2(825, 118),
                    Size = new Vector2(430),
                },
                new HomeBarcode("NO.004-KEY")
                {
                    Position = new Vector2(1160, 646),
                    Colour = HomeControlColours.Navy,
                },
            },
        };

    private static Drawable createNewBestBadge(bool isNewBest) =>
        new Container
        {
            Position = new Vector2(306, 216),
            Size = new Vector2(116, 38),
            Alpha = isNewBest ? 1 : 0,
            Masking = true,
            CornerRadius = 5,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = HomeControlColours.Pink,
                },
                new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = YokkoStrings.Get("gameplay.result.new_best"),
                    Font = HomeTypography.Display(17),
                    Colour = Color4.White,
                },
            },
        };

    private static Drawable createDecorations() =>
        new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new SpriteIcon
                {
                    Position = new Vector2(32, 26),
                    Size = new Vector2(11),
                    Icon = FontAwesome.Solid.Plus,
                    Colour = HomeControlColours.Cyan,
                },
                new SpriteIcon
                {
                    Position = new Vector2(36, 344),
                    Size = new Vector2(12),
                    Icon = FontAwesome.Solid.Plus,
                    Colour = HomeControlColours.Pink,
                },
                new SpriteIcon
                {
                    Position = new Vector2(1128, 86),
                    Size = new Vector2(18),
                    Icon = FontAwesome.Solid.Plus,
                    Colour = Color4.White,
                },
                new SpriteIcon
                {
                    Position = new Vector2(1220, 560),
                    Size = new Vector2(15),
                    Icon = FontAwesome.Solid.Plus,
                    Colour = Color4.White,
                },
                new Box
                {
                    Position = new Vector2(265, 238),
                    Size = new Vector2(8),
                    Rotation = 45,
                    Colour = HomeControlColours.Yellow,
                },
                new SpriteText
                {
                    Position = new Vector2(24, 300),
                    Rotation = -90,
                    Text = "RHYTHM CHART STUDIO  ·  VOL.01",
                    Font = HomeTypography.Display(10),
                    Spacing = new Vector2(2, 0),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.35f),
                },
            },
        };

    private static Drawable createIvoryStage() =>
        new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 700,
                    Colour = HomeControlColours.Ivory,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Position = new Vector2(660, -45),
                    Width = 205,
                    Height = 1.18f,
                    Rotation = 10,
                    Colour = HomeControlColours.Ivory,
                },
            },
        };

    private partial class ResultActionButton : ClickableContainer
    {
        private readonly Box background;

        public ResultActionButton(
            LocalisableString label,
            string key,
            IconUsage icon,
            Action action)
        {
            Action = action;
            Size = new Vector2(195, 70);
            Masking = true;
            CornerRadius = 6;
            BorderThickness = 1.5f;
            BorderColour = HomeControlColours.Navy;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
                new Container
                {
                    Position = new Vector2(10, 9),
                    Size = new Vector2(50),
                    CornerRadius = 7,
                    Masking = true,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = HomeControlColours.Navy,
                    },
                },
                new SpriteIcon
                {
                    Position = new Vector2(23, 22),
                    Size = new Vector2(24),
                    Icon = icon,
                    Colour = HomeControlColours.Cyan,
                },
                new SpriteText
                {
                    Position = new Vector2(72, 22),
                    Text = label,
                    Font = HomeTypography.Display(15),
                    Colour = HomeControlColours.Navy,
                },
                new Container
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -10,
                    Size = new Vector2(key.Length > 1 ? 34 : 24, 22),
                    Masking = true,
                    CornerRadius = 4,
                    BorderThickness = 1,
                    BorderColour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.28f),
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
                            Text = key,
                            Font = HomeTypography.Display(10),
                            Colour = HomeControlColours.Navy,
                        },
                    },
                },
                new Box
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    X = 12,
                    Y = -4,
                    Size = new Vector2(42, 3),
                    Colour = HomeControlColours.Pink,
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeColour(HomeControlColours.PaleCyan, 120);
            this.ScaleTo(1.012f, 120, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeColour(Color4.White, 140);
            this.ScaleTo(1, 140, Easing.OutQuint);
        }
    }
}
