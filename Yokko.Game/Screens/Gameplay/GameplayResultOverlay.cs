using System;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
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
    private const float designedWidth = 1920;
    private const float designedHeight = 1080;
    private const float contentLeft = 76;
    private const float contentWidth = 986;
    private const float judgementStripWidth = contentWidth;

    private static class ResultColours
    {
        public static readonly Color4 Navy =
            new(7 / 255f, 27 / 255f, 120 / 255f, 1f);
        public static readonly Color4 Cyan =
            new(98 / 255f, 216 / 255f, 248 / 255f, 1f);
        public static readonly Color4 RankCyan =
            new(217 / 255f, 247 / 255f, 255 / 255f, 1f);
        public static readonly Color4 SoftCyan =
            new(217 / 255f, 247 / 255f, 255 / 255f, 1f);
        public static readonly Color4 Yellow =
            new(255 / 255f, 230 / 255f, 111 / 255f, 1f);
        public static readonly Color4 Pink =
            new(255 / 255f, 99 / 255f, 199 / 255f, 1f);
        public static readonly Color4 Ivory =
            new(255 / 255f, 253 / 255f, 247 / 255f, 1f);
        public static readonly Color4 Muted =
            new(82 / 255f, 96 / 255f, 142 / 255f, 1f);
    }

    private static class ResultTypography
    {
        public static FontUsage Poster(float size) =>
            new FontUsage("PlusJakartaSans", size).With(weight: "Bold");
    }

    private readonly Action retry;
    private readonly Action watchReplay;
    private readonly Action returnToSongSelect;
    private readonly JudgementConfiguration judgementConfiguration;
    private readonly ManiaModSet mods;
    private readonly GameplayResultPresentation presentation;
    private ResultActionButton replayActionButton;
    private Container backdrop;
    private Container stage;
    private Container leftStageLayout;
    private Container rightStageContent;
    private Box scoreSignalRunner;
    private ResultScorePanel scorePanel;
    private ResultRankSeal rankSeal;
    private ResultStageDecorations stageDecorations;
    private Sprite resultCharacter;
    private ResultSongHeading songHeading;
    private float lastResponsiveStageScale;

    // Kept for existing result-flow tests; the supplied character artwork is
    // loaded as a separate foreground stage layer beside the live result UI.
    internal bool MascotReady => true;
    internal bool CharacterStageReady => rightStageContent != null;
    internal bool CharacterTextureReady => resultCharacter?.Texture != null;
    internal bool StageDecorationsReady => stageDecorations != null;
    internal bool RankSealReady => rankSeal != null;
    internal bool RankSealLabelFits => rankSeal?.LabelFits == true;
    internal string DisplayedRank => rankSeal?.DisplayedLabel ?? string.Empty;
    internal string RankSealEyebrow => rankSeal?.Eyebrow ?? string.Empty;
    internal string RankSealFooter => rankSeal?.Footer ?? string.Empty;
    internal int ActionCount => 3;
    internal float SongTitleUnderlineClearance =>
        songHeading?.UnderlineClearance ?? float.NegativeInfinity;
    internal string DisplayedMods { get; }
    internal bool PracticeSession { get; }
    internal bool ReplayAvailable { get; private set; }
    internal string DisplayedPlayerName => presentation.PlayerName;
    internal string DisplayedPlayerId => presentation.PlayerId;
    internal bool ScorePanelInteractionActive =>
        scorePanel?.InteractionActive == true;
    internal bool EntranceComplete =>
        backdrop.Alpha >= 0.999f
        && leftStageLayout.Alpha >= 0.999f
        && rightStageContent.Alpha >= 0.999f
        && Math.Abs(leftStageLayout.X) < 0.01
        && Math.Abs(rightStageContent.X) < 0.01;

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
        bool practiceSession = false,
        JudgementConfiguration? judgementConfiguration = null,
        bool replayAvailable = true,
        GameplayResultPresentation presentation = null)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        ArgumentNullException.ThrowIfNull(result);

        this.mods = mods ?? ManiaModSet.Empty;
        this.judgementConfiguration =
            judgementConfiguration ?? JudgementConfiguration.YokkoDefault;
        this.retry = retry;
        this.watchReplay = watchReplay;
        this.returnToSongSelect = returnToSongSelect;
        this.presentation = normalisePresentation(
            presentation ?? GameplayResultPresentation.LocalFallback());
        ReplayAvailable = replayAvailable && watchReplay != null;
        PracticeSession = practiceSession;

        string displayedMods = this.mods.IsEmpty
            ? "NM"
            : string.Join("  ", this.mods.DisplayLabels);
        if (this.judgementConfiguration.Mode == JudgementMode.Etterna)
        {
            displayedMods +=
                $"  ·  ETTERNA "
                + this.judgementConfiguration.EtternaJusticeLabel
                    .ToUpperInvariant();
        }
        else if (this.judgementConfiguration.Mode
                 == JudgementMode.OsuStable)
        {
            displayedMods += "  ·  OSU!STABLE";
        }
        else if (this.judgementConfiguration.Mode
                 == JudgementMode.BmsBeatoraja)
        {
            displayedMods += "  ·  BMS / BEATORAJA "
                             + (beatmap.BmsJudgement
                                ?? BmsJudgementMetadata.Default).DisplayLabel;
        }

        DisplayedMods = practiceSession
            ? $"{displayedMods}  ·  PRACTICE"
            : displayedMods;
        RelativeSizeAxes = Axes.Both;
        Depth = -10;

        bool etterna =
            this.judgementConfiguration.Mode == JudgementMode.Etterna;
        string rank = etterna
            ? EtternaScoringRules.GradeLabel(result.Accuracy)
            : result.Rank.ToDisplayLabel();

        InternalChildren = new Drawable[]
        {
            backdrop = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ResultColours.Ivory,
                },
            },
            stage = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(designedWidth, designedHeight),
                Alpha = 0,
                Children = new Drawable[]
                {
                    createStageBackground(),
                    createTickerStrip(),
                    leftStageLayout = new Container
                    {
                        Size = new Vector2(designedWidth, designedHeight),
                        Child = createResultContent(
                            beatmap,
                            result,
                            rank,
                            isNewBest),
                    },
                    rightStageContent = new Container
                    {
                        Size = new Vector2(designedWidth, designedHeight),
                        Children = new Drawable[]
                        {
                            createCharacterStagePlaceholder(),
                            resultCharacter = new Sprite
                            {
                                Anchor = Anchor.TopLeft,
                                Origin = Anchor.Centre,
                                Position = new Vector2(1600, 570),
                                Size = new Vector2(1040),
                                Alpha = 0,
                            },
                        },
                    },
                },
            },
        };
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        resultCharacter.Texture = textures.Get(
            "Gameplay/yokko-result-character-user");
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        stage.Alpha = 1;
        leftStageLayout.Alpha = 0;
        leftStageLayout.X = -18;
        rightStageContent.Alpha = 0;
        rightStageContent.X = 24;

        backdrop.FadeIn(360, Easing.OutQuint);
        leftStageLayout.Delay(70).FadeIn(300, Easing.OutQuint);
        leftStageLayout.Delay(70).MoveToX(0, 400, Easing.OutQuint);
        rightStageContent.Delay(130).FadeIn(340, Easing.OutQuint);
        rightStageContent.Delay(130).MoveToX(0, 350, Easing.OutQuint);

        resultCharacter.Scale = new Vector2(0.94f);
        resultCharacter.Delay(150).FadeIn(380, Easing.OutQuint);
        resultCharacter.Delay(150).ScaleTo(1, 620, Easing.OutBack);
        resultCharacter.Delay(900)
                       .MoveToY(564, 2800, Easing.InOutSine)
                       .Then()
                       .MoveToY(576, 2800, Easing.InOutSine)
                       .Loop();
        resultCharacter.Delay(900)
                       .MoveToX(1596, 3900, Easing.InOutSine)
                       .Then()
                       .MoveToX(1604, 3900, Easing.InOutSine)
                       .Loop();
        resultCharacter.Delay(900)
                       .RotateTo(-0.35f, 3600, Easing.InOutSine)
                       .Then()
                       .RotateTo(0.35f, 3600, Easing.InOutSine)
                       .Loop();
        resultCharacter.Delay(900)
                       .ScaleTo(1.006f, 2800, Easing.InOutSine)
                       .Then()
                       .ScaleTo(1, 2800, Easing.InOutSine)
                       .Loop();

        scoreSignalRunner
            .MoveToX(850, 2600, Easing.InOutSine)
            .Then()
            .MoveToX(30, 0)
            .Delay(650)
            .Loop();
        scoreSignalRunner
            .FadeTo(0.95f, 500, Easing.OutQuint)
            .Then()
            .FadeTo(0.28f, 1500, Easing.InOutSine)
            .Then()
            .FadeTo(0.78f, 700, Easing.InOutSine)
            .Loop();
    }

    internal void TriggerReplay()
    {
        if (ReplayAvailable)
            watchReplay?.Invoke();
    }

    internal void SetReplayAvailable(bool available)
    {
        ReplayAvailable = available && watchReplay != null;
        replayActionButton?.SetEnabled(ReplayAvailable);
    }

    internal void SetScorePanelInteraction(bool active) =>
        scorePanel?.SetInteractionState(active);

    internal void CompleteEntrance()
    {
        backdrop.FinishTransforms();
        backdrop.Alpha = 1;
        stage.FinishTransforms();
        stage.Alpha = 1;
        stage.Y = 0;
        leftStageLayout.FinishTransforms();
        leftStageLayout.Alpha = 1;
        leftStageLayout.X = 0;
        rightStageContent.FinishTransforms();
        rightStageContent.Alpha = 1;
        rightStageContent.X = 0;
    }

    protected override void Update()
    {
        base.Update();

        if (stage == null || DrawWidth <= 0 || DrawHeight <= 0)
            return;

        float stageScale = CalculateResponsiveStageScale(
            new Vector2(DrawWidth, DrawHeight));
        if (Math.Abs(stageScale - lastResponsiveStageScale) < 0.0001f)
            return;

        lastResponsiveStageScale = stageScale;
        stage.Scale = new Vector2(stageScale);
    }

    internal static float CalculateResponsiveStageScale(Vector2 viewport) =>
        MathF.Min(
            viewport.X / designedWidth,
            viewport.Y / designedHeight);

    private Drawable createResultContent(
        YokkoBeatmap beatmap,
        ManiaScoreResult result,
        string rank,
        bool isNewBest)
    {
        return new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                createHeader(),
                createSongBar(beatmap),
                createScorePanel(result, rank, isNewBest),
                createSummaryRail(result),
                createTimingStrip(),
                createJudgementStrip(result),
                createActionRow(),
            },
        };
    }

    private Drawable createHeader()
    {
        DateTimeOffset? localPlayedAt = presentation.PlayedAt?.ToLocalTime();
        string playedAt = localPlayedAt?.ToString(
            "yyyy.MM.dd  HH:mm:ss",
            CultureInfo.InvariantCulture) ?? "TIME UNAVAILABLE";

        return new Container
        {
            Position = new Vector2(contentLeft, 68),
            Size = new Vector2(contentWidth, 128),
            Children = new Drawable[]
            {
                new SpriteText
                {
                    Text = "RESULT",
                    Font = HomeTypography.Hero(88),
                    Colour = ResultColours.Navy,
                },
                new Box
                {
                    Position = new Vector2(0, 105),
                    Size = new Vector2(458, 4),
                    Colour = ResultColours.Cyan,
                },
                new HomeMicroLine
                {
                    Position = new Vector2(458, 102),
                    Width = 64,
                    Colour = ResultColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(574, 28),
                    Text = $"PLAYER  {presentation.PlayerName}",
                    Width = 404,
                    Truncate = true,
                    Font = HomeTypography.Display(28),
                    Colour = ResultColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(574, 68),
                    Text = $"UID {presentation.PlayerId}  //  PLAYED {playedAt}",
                    Width = 404,
                    Truncate = true,
                    Font = HomeTypography.Display(15),
                    Spacing = new Vector2(0.5f, 0),
                    Colour = ResultColours.Muted,
                },
                new Box
                {
                    Position = new Vector2(574, 101),
                    Size = new Vector2(120, 3),
                    Colour = ResultColours.Cyan,
                },
            },
        };
    }

    private Drawable createSongBar(YokkoBeatmap beatmap) =>
        new Container
        {
            Position = new Vector2(contentLeft, 212),
            Size = new Vector2(contentWidth, 84),
            Masking = true,
            CornerRadius = 6,
            BorderThickness = 2,
            BorderColour = ResultColours.Navy,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ResultColours.SoftCyan,
                    Alpha = 0.28f,
                },
                songHeading = new ResultSongHeading(
                    beatmap.Title,
                    beatmap.Artist)
                {
                    Position = new Vector2(24, 10),
                },
                createDifficultyChip(beatmap.DifficultyName),
                new SpriteText
                {
                    Position = new Vector2(808, 27),
                    Text = $"{(int)beatmap.KeyMode}K  ·  OD {beatmap.OverallDifficulty:0.#}",
                    Font = HomeTypography.Display(21),
                    Colour = ResultColours.Navy,
                },
            },
        };

    private static Drawable createDifficultyChip(string difficulty) =>
        new Container
        {
            Position = new Vector2(624, 20),
            Size = new Vector2(164, 44),
            Masking = true,
            CornerRadius = 8,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ResultColours.Pink,
                },
                new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = 140,
                    Truncate = true,
                    Text = difficulty.ToUpperInvariant(),
                    Font = HomeTypography.Display(17),
                    Colour = Color4.White,
                },
            },
        };

    private Drawable createScorePanel(
        ManiaScoreResult result,
        string rank,
        bool isNewBest)
    {
        string previousBest = presentation.PreviousBestScore?.ToString(
            "N0",
            CultureInfo.InvariantCulture) ?? "—";
        string delta = presentation.PreviousBestScore is long previous
            ? formatSignedScore(result.Score - previous)
            : "—";
        var interactionGlow = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = ResultColours.SoftCyan,
            Alpha = 0,
        };
        var interactionRail = new Box
        {
            Anchor = Anchor.CentreRight,
            Origin = Anchor.CentreRight,
            RelativePositionAxes = Axes.X,
            X = 1,
            Width = 4,
            Height = 0,
            Colour = ResultColours.Pink,
        };

        return scorePanel = new ResultScorePanel(
            interactionGlow,
            interactionRail)
        {
            Position = new Vector2(contentLeft, 310),
            Size = new Vector2(contentWidth, 224),
            Masking = true,
            CornerRadius = 6,
            BorderThickness = 1.5f,
            BorderColour = new Color4(
                ResultColours.Cyan.R,
                ResultColours.Cyan.G,
                ResultColours.Cyan.B,
                0.82f),
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourInfo.GradientHorizontal(
                        new Color4(0.035f, 0.12f, 0.49f, 1f),
                        new Color4(0.015f, 0.045f, 0.23f, 1f)),
                },
                interactionGlow,
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 5,
                    Colour = ResultColours.Cyan,
                },
                new Box
                {
                    Position = new Vector2(558, 0),
                    Size = new Vector2(118, 5),
                    Colour = ResultColours.Pink,
                },
                new Box
                {
                    Position = new Vector2(-82, -52),
                    Size = new Vector2(66, 360),
                    Rotation = 18,
                    Colour = Color4.White,
                    Alpha = 0.055f,
                },
                scoreSignalRunner = new Box
                {
                    Position = new Vector2(30, 12),
                    Size = new Vector2(72, 3),
                    Colour = ResultColours.SoftCyan,
                },
                rankSeal = new ResultRankSeal(
                    rank,
                    gradeColour(mods.AdjustRank(result.Rank)),
                    judgementConfiguration.Mode == JudgementMode.Etterna,
                    judgementConfiguration.EtternaJusticeLabel)
                {
                    Position = new Vector2(20, 14),
                },
                new Box
                {
                    Position = new Vector2(216, 26),
                    Size = new Vector2(2, 190),
                    Colour = new Color4(1, 1, 1, 0.58f),
                },
                new SpriteText
                {
                    Position = new Vector2(248, 28),
                    Text = isNewBest ? "NEW BEST" : "SCORE",
                    Font = HomeTypography.Display(19),
                    Spacing = new Vector2(1.6f, 0),
                    Colour = isNewBest
                        ? ResultColours.Pink
                        : ResultColours.Cyan,
                },
                new SpriteText
                {
                    Position = new Vector2(240, 57),
                    Width = 420,
                    Truncate = true,
                    Text = result.Score.ToString(
                        "N0",
                        CultureInfo.InvariantCulture),
                    Font = ResultTypography.Poster(72),
                    Colour = Color4.White,
                },
                new Box
                {
                    Position = new Vector2(240, 151),
                    Size = new Vector2(406, 2),
                    Colour = new Color4(1, 1, 1, 0.55f),
                },
                new SpriteText
                {
                    Position = new Vector2(240, 166),
                    Text = "PREVIOUS BEST",
                    Font = HomeTypography.Display(15),
                    Colour = ResultColours.Cyan,
                },
                new SpriteText
                {
                    Position = new Vector2(240, 187),
                    Text = previousBest,
                    Font = HomeTypography.Display(27),
                    Colour = Color4.White,
                },
                new SpriteText
                {
                    Position = new Vector2(476, 182),
                    Text = delta,
                    Font = HomeTypography.Display(30),
                    Colour = ResultColours.Yellow,
                },
                new Box
                {
                    Position = new Vector2(674, 26),
                    Size = new Vector2(2, 190),
                    Colour = new Color4(1, 1, 1, 0.72f),
                },
                new HomeDotField
                {
                    Position = new Vector2(832, 20),
                    Size = new Vector2(120, 42),
                    Colour = new Color4(
                        ResultColours.Cyan.R,
                        ResultColours.Cyan.G,
                        ResultColours.Cyan.B,
                        0.17f),
                },
                createScoreSideMetric(
                    "MISSES",
                    result.Miss.ToString(CultureInfo.InvariantCulture),
                    708,
                    154,
                    ResultColours.Pink),
                new Box
                {
                    Position = new Vector2(968, 10),
                    Size = new Vector2(12, 2),
                    Colour = ResultColours.Pink,
                },
                new Box
                {
                    Position = new Vector2(978, 10),
                    Size = new Vector2(2, 12),
                    Colour = ResultColours.Pink,
                },
                interactionRail,
            },
        };
    }

    private static Color4 gradeColour(ScoreRank rank) => rank switch
    {
        ScoreRank.X or ScoreRank.XH => ResultColours.RankCyan,
        ScoreRank.S or ScoreRank.SH => ResultColours.Cyan,
        ScoreRank.A => new Color4(0.62f, 0.94f, 0.25f, 1f),
        ScoreRank.B => ResultColours.Yellow,
        ScoreRank.C => new Color4(1f, 0.57f, 0.21f, 1f),
        _ => ResultColours.Pink,
    };

    private static Drawable createScoreSideMetric(
        string label,
        string value,
        float x,
        float y,
        Color4 accent) =>
        new Container
        {
            Position = new Vector2(x, y),
            Size = new Vector2(242, 52),
            Masking = true,
            CornerRadius = 8,
            BorderThickness = 1,
            BorderColour = new Color4(
                accent.R,
                accent.G,
                accent.B,
                0.72f),
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = accent,
                    Alpha = 0.10f,
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 16,
                    Text = label,
                    Font = HomeTypography.Display(16),
                    Colour = accent,
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -18,
                    Text = value,
                    Font = ResultTypography.Poster(32),
                    Colour = Color4.White,
                },
            },
        };

    private Drawable createSummaryRail(ManiaScoreResult result) =>
        new Container
        {
            Position = new Vector2(contentLeft, 546),
            Size = new Vector2(contentWidth, 84),
            Masking = true,
            CornerRadius = 6,
            BorderThickness = 1,
            BorderColour = new Color4(
                ResultColours.Navy.R,
                ResultColours.Navy.G,
                ResultColours.Navy.B,
                0.24f),
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ResultColours.SoftCyan,
                    Alpha = 0.20f,
                },
                createMetricCell(
                    judgementConfiguration.Mode == JudgementMode.Etterna
                        ? "WIFE3"
                        : "ACCURACY",
                    $"{result.Accuracy * 100:0.00}%",
                    0,
                    contentWidth / 4,
                    true),
                createMetricCell(
                    "MAX COMBO",
                    $"{result.MaxCombo:N0}×",
                    contentWidth / 4,
                    contentWidth / 4,
                    true),
                createMetricCell(
                    "RATE",
                    $"{mods.FixedRateSpeedChange:0.00}×",
                    contentWidth / 2,
                    contentWidth / 4),
                createMetricCell(
                    "RULESET",
                    rulesetLabel(),
                    contentWidth * 3 / 4,
                    contentWidth / 4),
            },
        };

    private static Drawable createMetricCell(
        LocalisableString label,
        string value,
        float x,
        float width,
        bool prominent = false) =>
        new ResultMetricCell(label, value, width, prominent)
        {
            Position = new Vector2(x, 0),
        };

    private Drawable createTimingStrip()
    {
        GameplayTimingStatistics timing = presentation.Timing;
        return new Container
        {
            Position = new Vector2(contentLeft, 652),
            Size = new Vector2(contentWidth, 48),
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 2,
                    Colour = ResultColours.Navy,
                    Alpha = 0.42f,
                },
                createInlineDatum(
                    "FAST",
                    formatDirectionalTiming(
                        timing?.EarlyCount,
                        timing?.EarlyAverageMilliseconds),
                    18,
                    ResultColours.Cyan,
                    56),
                createInlineDatum(
                    "ON TIME",
                    timing?.OnTimeCount.ToString(CultureInfo.InvariantCulture)
                        ?? "—",
                    225,
                    ResultColours.Cyan,
                    84),
                createInlineDatum(
                    "LATE",
                    formatDirectionalTiming(
                        timing?.LateCount,
                        timing?.LateAverageMilliseconds),
                    420,
                    ResultColours.Pink,
                    54),
                createInlineDatum(
                    "MEAN",
                    timing == null
                        ? "—"
                        : $"{timing.MeanMilliseconds:+0.0;-0.0;0.0} ms",
                    620,
                    ResultColours.Navy,
                    70),
                createInlineDatum(
                    "UR",
                    timing?.UnstableRate.ToString(
                        "0.0",
                        CultureInfo.InvariantCulture) ?? "—",
                    810,
                    ResultColours.Pink,
                    44),
            },
        };
    }

    private static Drawable createInlineDatum(
        string label,
        string value,
        float x,
        Color4 colour,
        float valueX = 74) =>
        new Container
        {
            Position = new Vector2(x, 14),
            Size = new Vector2(170, 30),
            Children = new Drawable[]
            {
                new SpriteText
                {
                    Text = label,
                    Font = HomeTypography.Display(14),
                    Colour = colour,
                },
                new SpriteText
                {
                    X = valueX,
                    Text = value,
                    Font = HomeTypography.Display(19),
                    Colour = ResultColours.Navy,
                },
            },
        };

    private static string formatDirectionalTiming(
        int? count,
        double? averageMilliseconds)
    {
        if (count is null)
            return "—";
        if (averageMilliseconds is null)
            return count.Value.ToString(CultureInfo.InvariantCulture);

        return count.Value.ToString(CultureInfo.InvariantCulture)
               + " / "
               + averageMilliseconds.Value.ToString(
                   "+0.0;-0.0;0.0",
                   CultureInfo.InvariantCulture)
               + "ms";
    }

    private Drawable createJudgementStrip(ManiaScoreResult result)
    {
        bool bms = judgementConfiguration.Mode
                   == JudgementMode.BmsBeatoraja;
        JudgementRating[] ratings = bms
            ?
            [
                JudgementRating.Perfect,
                JudgementRating.Great,
                JudgementRating.Good,
                JudgementRating.Ok,
                JudgementRating.Miss,
                JudgementRating.Meh,
            ]
            :
            [
                JudgementRating.Perfect,
                JudgementRating.Great,
                JudgementRating.Good,
                JudgementRating.Ok,
                JudgementRating.Meh,
                JudgementRating.Miss,
            ];
        int[] values = bms
            ?
            [
                result.Perfect,
                result.Great,
                result.Good,
                result.Ok,
                result.Miss,
                result.Meh,
            ]
            :
            [
                result.Perfect,
                result.Great,
                result.Good,
                result.Ok,
                result.Meh,
                result.Miss,
            ];
        Color4[] defaultColours =
        {
            ResultColours.Pink,
            ResultColours.Cyan,
            new Color4(0.14f, 0.72f, 0.42f, 1f),
            new Color4(1f, 0.62f, 0.12f, 1f),
            new Color4(0.56f, 0.42f, 0.91f, 1f),
            new Color4(1f, 0.36f, 0.48f, 1f),
        };
        var judgements =
            new (string Label, int Value, ColourInfo Colour)[6];
        for (int i = 0; i < ratings.Length; i++)
        {
            JudgementRating rating = ratings[i];
            judgements[i] = (
                judgementConfiguration.RatingLabel(rating),
                values[i],
                judgementConfiguration.Mode == JudgementMode.OsuStable
                    ? RatingColours.ForDisplay(
                        rating,
                        judgementConfiguration)
                    : defaultColours[i]);
        }

        int totalJudgements = judgements.Sum(static judgement =>
            judgement.Value);
        var flow = new FillFlowContainer
        {
            RelativeSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
        };
        foreach ((string label, int value, ColourInfo colour) in judgements)
        {
            flow.Add(new ResultJudgementCell(
                label,
                value,
                colour,
                totalJudgements == 0
                    ? 0
                    : value / (float)totalJudgements)
            {
                Width = judgementStripWidth / 6,
                RelativeSizeAxes = Axes.Y,
            });
        }

        return new Container
        {
            Position = new Vector2(contentLeft, 708),
            Size = new Vector2(judgementStripWidth, 88),
            Child = flow,
        };
    }

    private Drawable createActionRow() =>
        new Container
        {
            Position = new Vector2(contentLeft, 930),
            Size = new Vector2(contentWidth, 84),
            Children = new Drawable[]
            {
                new ResultActionButton(
                    YokkoStrings.Get("gameplay.result.retry"),
                    "R",
                    FontAwesome.Solid.Redo,
                    retry,
                    280,
                    true),
                replayActionButton = new ResultActionButton(
                    YokkoStrings.Get("gameplay.result.watch_replay"),
                    "V",
                    FontAwesome.Solid.Play,
                    watchReplay,
                    300,
                    false,
                    ReplayAvailable)
                {
                    X = 304,
                },
                new ResultActionButton(
                    YokkoStrings.Get("gameplay.result.return"),
                    "ESC",
                    FontAwesome.Solid.Music,
                    returnToSongSelect,
                    284,
                    false)
                {
                    X = 628,
                },
            },
        };

    private static GameplayResultPresentation normalisePresentation(
        GameplayResultPresentation value) =>
        value with
        {
            PlayerName = displayOr(value.PlayerName, "LOCAL PLAYER"),
            PlayerId = displayOr(value.PlayerId, "LOCAL"),
        };

    private static string displayOr(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string displayOrDash(string value) =>
        displayOr(value, "—");

    private static string formatSignedScore(long value) =>
        value > 0
            ? $"+{value.ToString("N0", CultureInfo.InvariantCulture)}"
            : value.ToString("N0", CultureInfo.InvariantCulture);

    private string rulesetLabel() =>
        judgementConfiguration.Mode switch
        {
            JudgementMode.Etterna =>
                $"ETTERNA {judgementConfiguration.EtternaJusticeLabel.ToUpperInvariant()}",
            JudgementMode.OsuStable => "OSU!STABLE",
            JudgementMode.BmsBeatoraja => "BMS / BEATORAJA",
            _ => "YOKKO",
        };

    private Drawable createTickerStrip() =>
        new Container
        {
            RelativeSizeAxes = Axes.X,
            Height = 36,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ResultColours.Navy,
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 38,
                    Text = "YOKKO RHYTHM CHART STUDIO  //  4K MANIA  //  RESULT ARCHIVE  //  VOL.01",
                    Font = HomeTypography.Display(12),
                    Spacing = new Vector2(1.2f, 0),
                    Colour = Color4.White,
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -38,
                    Text = presentation.PlayedAt?.ToLocalTime().ToString(
                        "yyyy.MM.dd",
                        CultureInfo.InvariantCulture)
                        ?? "RESULT SESSION",
                    Font = HomeTypography.Display(11),
                    Spacing = new Vector2(1.2f, 0),
                    Colour = ResultColours.Yellow,
                },
            },
        };

    private static Drawable createStageBackground() =>
        new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ResultColours.Ivory,
                },
                new Box
                {
                    Position = new Vector2(1170, -60),
                    Size = new Vector2(900, 1240),
                    Rotation = 7,
                    Colour = ResultColours.SoftCyan,
                },
                new Box
                {
                    Position = new Vector2(1220, -70),
                    Size = new Vector2(770, 220),
                    Rotation = 7,
                    Colour = ResultColours.Cyan,
                    Alpha = 0.5f,
                },
                new Box
                {
                    Position = new Vector2(1168, -60),
                    Size = new Vector2(5, 1240),
                    Rotation = 7,
                    Colour = Color4.White,
                },
            },
        };

    private Drawable createCharacterStagePlaceholder() =>
        stageDecorations = new ResultStageDecorations
        {
            Position = new Vector2(1160, 36),
            Size = new Vector2(760, 1044),
        };

    private partial class ResultStageDecorations : CompositeDrawable
    {
        private readonly Container orbitFrame;
        private readonly CircularContainer outerRing;
        private readonly Box pulseBar;

        public ResultStageDecorations()
        {
            InternalChildren = new Drawable[]
            {
                new HomeDotField
                {
                    Position = new Vector2(510, 74),
                    Size = new Vector2(140, 80),
                    Colour = new Color4(1, 1, 1, 0.56f),
                },
                new HomeMicroLine
                {
                    Position = new Vector2(504, 174),
                    Width = 118,
                    Colour = Color4.White,
                },
                new SpriteText
                {
                    Position = new Vector2(92, 112),
                    Text = "PLAY COMPLETE",
                    Font = HomeTypography.Display(20),
                    Spacing = new Vector2(2.2f, 0),
                    Colour = ResultColours.Navy,
                    Alpha = 0.78f,
                },
                new SpriteText
                {
                    Position = new Vector2(94, 143),
                    Text = "PERFORMANCE RECORD  //  ARCHIVE 08",
                    Font = HomeTypography.Display(9),
                    Spacing = new Vector2(1.2f, 0),
                    Colour = ResultColours.Navy,
                    Alpha = 0.48f,
                },
                new Box
                {
                    Position = new Vector2(94, 174),
                    Size = new Vector2(112, 4),
                    Colour = ResultColours.Pink,
                },
                new Box
                {
                    Position = new Vector2(206, 174),
                    Size = new Vector2(46, 4),
                    Colour = ResultColours.Yellow,
                },
                orbitFrame = new Container
                {
                    Position = new Vector2(112, 250),
                    Size = new Vector2(500, 560),
                    Rotation = -7,
                    Masking = true,
                    CornerRadius = 34,
                    BorderThickness = 2,
                    BorderColour = new Color4(
                        ResultColours.Navy.R,
                        ResultColours.Navy.G,
                        ResultColours.Navy.B,
                        0.18f),
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(1, 1, 1, 0.012f),
                    },
                },
                outerRing = new CircularContainer
                {
                    Position = new Vector2(150, 280),
                    Size = new Vector2(438),
                    Masking = true,
                    BorderThickness = 3,
                    BorderColour = new Color4(
                        ResultColours.Navy.R,
                        ResultColours.Navy.G,
                        ResultColours.Navy.B,
                        0.20f),
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(1, 1, 1, 0.008f),
                    },
                },
                new CircularContainer
                {
                    Position = new Vector2(238, 368),
                    Size = new Vector2(262),
                    Masking = true,
                    BorderThickness = 2,
                    BorderColour = new Color4(1, 1, 1, 0.62f),
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(1, 1, 1, 0.012f),
                    },
                },
                new Box
                {
                    Position = new Vector2(118, 498),
                    Size = new Vector2(492, 2),
                    Colour = ResultColours.Navy,
                    Alpha = 0.18f,
                },
                new Box
                {
                    Position = new Vector2(368, 250),
                    Size = new Vector2(2, 560),
                    Colour = ResultColours.Navy,
                    Alpha = 0.18f,
                },
                new Box
                {
                    Position = new Vector2(352, 482),
                    Size = new Vector2(34, 34),
                    Colour = ResultColours.Yellow,
                    Alpha = 0.82f,
                },
                new Box
                {
                    Position = new Vector2(360, 490),
                    Size = new Vector2(18, 18),
                    Colour = ResultColours.Pink,
                },
                pulseBar = new Box
                {
                    Position = new Vector2(114, 824),
                    Size = new Vector2(92, 5),
                    Colour = ResultColours.Pink,
                },
                new SpriteText
                {
                    Position = new Vector2(118, 842),
                    Text = "YOKKO // RESULT SESSION",
                    Font = HomeTypography.Display(10),
                    Spacing = new Vector2(1.3f, 0),
                    Colour = ResultColours.Navy,
                    Alpha = 0.62f,
                },
                new SpriteText
                {
                    Position = new Vector2(480, 690),
                    Text = "08",
                    Font = HomeTypography.Hero(146),
                    Colour = ResultColours.Navy,
                    Alpha = 0.10f,
                },
                new HomeDotField
                {
                    Position = new Vector2(74, 800),
                    Size = new Vector2(108, 64),
                    Colour = new Color4(
                        ResultColours.Navy.R,
                        ResultColours.Navy.G,
                        ResultColours.Navy.B,
                        0.16f),
                },
                new HomeCornerBracket
                {
                    Position = new Vector2(92, 224),
                    Height = 58,
                    Colour = ResultColours.Navy,
                },
                new HomeCornerBracket
                {
                    Position = new Vector2(626, 800),
                    Height = 58,
                    Rotation = 180,
                    Colour = ResultColours.Navy,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            orbitFrame.RotateTo(5, 7200, Easing.InOutSine)
                      .Then().RotateTo(-7, 7200, Easing.InOutSine)
                      .Loop();
            outerRing.ScaleTo(1.035f, 3200, Easing.InOutSine)
                     .Then().ScaleTo(1, 3200, Easing.InOutSine)
                     .Loop();
            pulseBar.ResizeWidthTo(174, 1800, Easing.InOutSine)
                    .Then().ResizeWidthTo(92, 1800, Easing.InOutSine)
                    .Loop();
        }
    }

    private partial class ResultRankSeal : CompositeDrawable
    {
        private readonly SpriteText rankText;
        private readonly Box accentBar;

        public string DisplayedLabel { get; }
        public string Eyebrow { get; }
        public string Footer { get; }

        public bool LabelFits =>
            rankText.Scale.X >= 0.99f && rankText.DrawWidth <= 151;

        public ResultRankSeal(
            string rank,
            Color4 accent,
            bool etterna,
            string etternaJusticeLabel)
        {
            string label = displayOr(rank, "—");
            bool etternaASequence = etterna
                                    && label.Length is >= 2 and <= 5
                                    && label.All(character => character == 'A');
            float rankSize = etternaASequence
                ? label.Length switch
                {
                    2 => 78,
                    3 => 57,
                    4 => 45,
                    5 => 35,
                    _ => 112,
                }
                : label.Length switch
                {
                    1 => 112,
                    2 => 82,
                    3 => 42,
                    4 => 34,
                    5 => 28,
                    _ => 24,
                };
            float rankTracking = etternaASequence
                ? label.Length switch
                {
                    2 => -3,
                    3 => -5.5f,
                    4 => -7,
                    5 => -8.5f,
                    _ => 0,
                }
                : 0;

            DisplayedLabel = label;
            Eyebrow = etterna ? "WIFE3 GRADE" : "GRADE";
            Footer = etterna
                ? $"ETTERNA // {etternaJusticeLabel.ToUpperInvariant()}"
                : "YOKKO // RANK";

            Size = new Vector2(176, 196);
            InternalChildren = new Drawable[]
            {
                new Container
                {
                    Position = new Vector2(5, 6),
                    Size = new Vector2(171, 190),
                    Masking = true,
                    CornerRadius = 17,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(
                            accent.R,
                            accent.G,
                            accent.B,
                            0.24f),
                    },
                },
                new Container
                {
                    Size = new Vector2(171, 190),
                    Masking = true,
                    CornerRadius = 16,
                    BorderThickness = 2,
                    BorderColour = new Color4(
                        accent.R,
                        accent.G,
                        accent.B,
                        0.92f),
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientVertical(
                                new Color4(0.075f, 0.24f, 0.62f, 1f),
                                new Color4(0.012f, 0.035f, 0.18f, 1f)),
                        },
                        new Box
                        {
                            Position = new Vector2(-40, -16),
                            Size = new Vector2(48, 238),
                            Rotation = 18,
                            Colour = Color4.White,
                            Alpha = 0.10f,
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 3,
                            Colour = accent,
                        },
                        new HomeDotField
                        {
                            Position = new Vector2(100, 17),
                            Size = new Vector2(55, 30),
                            Colour = new Color4(
                                accent.R,
                                accent.G,
                                accent.B,
                                0.24f),
                        },
                        new Container
                        {
                            Position = new Vector2(6),
                            Size = new Vector2(159, 178),
                            Masking = true,
                            CornerRadius = 11,
                            BorderThickness = 1,
                            BorderColour = new Color4(1, 1, 1, 0.24f),
                            Child = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Alpha = 0.01f,
                            },
                        },
                        new SpriteText
                        {
                            Position = new Vector2(16, 13),
                            Text = Eyebrow,
                            Font = HomeTypography.Display(11),
                            Spacing = new Vector2(1.5f, 0),
                            Colour = accent,
                        },
                        rankText = new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Y = -2,
                            Text = label,
                            Font = ResultTypography.Poster(rankSize),
                            Spacing = new Vector2(rankTracking, 0),
                            Colour = ColourInfo.GradientVertical(
                                Color4.White,
                                accent),
                            Shadow = true,
                            ShadowColour = new Color4(
                                0.01f,
                                0.02f,
                                0.11f,
                                0.82f),
                        },
                        accentBar = new Box
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Position = new Vector2(16, -15),
                            Size = new Vector2(42, 3),
                            Colour = accent,
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.BottomRight,
                            Origin = Anchor.BottomRight,
                            Position = new Vector2(-15, -12),
                            Text = Footer,
                            Font = HomeTypography.Display(8),
                            Spacing = new Vector2(0.8f, 0),
                            Colour = new Color4(1, 1, 1, 0.62f),
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            rankText.Scale = new Vector2(0.86f);
            rankText.Alpha = 0;
            rankText.FadeIn(240, Easing.OutQuint);
            rankText.ScaleTo(1, 460, Easing.OutBack);

            accentBar.Width = 0;
            accentBar.ResizeWidthTo(42, 420, Easing.OutQuint);
        }
    }

    private partial class ResultScorePanel : Container
    {
        private readonly Box interactionGlow;
        private readonly Box interactionRail;

        public bool InteractionActive { get; private set; }

        public override bool HandlePositionalInput => true;

        public ResultScorePanel(Box interactionGlow, Box interactionRail)
        {
            this.interactionGlow = interactionGlow;
            this.interactionRail = interactionRail;
        }

        public void SetInteractionState(bool active)
        {
            InteractionActive = active;
            this.MoveToY(active ? 306 : 310, 180, Easing.OutQuint);
            this.ScaleTo(active ? 1.006f : 1, 180, Easing.OutQuint);
            interactionGlow.FadeTo(active ? 0.055f : 0, 180, Easing.OutQuint);
            interactionRail.ResizeHeightTo(
                active ? 196 : 0,
                active ? 220 : 160,
                Easing.OutQuint);
        }

        protected override bool OnHover(HoverEvent e)
        {
            SetInteractionState(true);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e) =>
            SetInteractionState(false);
    }

    private partial class ResultSongHeading : CompositeDrawable
    {
        private const float underlineGap = 4;

        private readonly SpriteText title;
        private readonly Box underline;

        public float UnderlineClearance => underline.Y - title.DrawHeight;

        public ResultSongHeading(string titleText, string artist)
        {
            Size = new Vector2(570, 64);
            InternalChildren = new Drawable[]
            {
                title = new SpriteText
                {
                    Width = 570,
                    Truncate = true,
                    Text = displayOrDash(titleText),
                    Font = HomeTypography.Display(33),
                    Colour = ResultColours.Navy,
                },
                new SpriteText
                {
                    Y = 38,
                    Width = 570,
                    Truncate = true,
                    Text = displayOrDash(artist),
                    Font = HomeTypography.Display(16),
                    Colour = ResultColours.Muted,
                },
                underline = new Box
                {
                    X = -2,
                    Size = new Vector2(150, 3),
                    Colour = ResultColours.Cyan,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            underline.Y = MathF.Max(
                58,
                MathF.Ceiling(title.DrawHeight) + underlineGap);
        }
    }

    private partial class ResultMetricCell : Container
    {
        private readonly Box background;
        private readonly SpriteText valueText;
        private readonly Box hoverUnderline;
        private readonly float hoverUnderlineWidth;
        private readonly float restValueY;

        public override bool HandlePositionalInput => true;

        public ResultMetricCell(
            LocalisableString label,
            string value,
            float width,
            bool prominent)
        {
            hoverUnderlineWidth = Math.Max(24, width - 48);
            restValueY = prominent ? 31 : 35;
            Size = new Vector2(width, 84);
            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ResultColours.SoftCyan,
                    Alpha = 0,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 2,
                    Colour = ResultColours.Navy,
                    Alpha = 0.25f,
                },
                new SpriteText
                {
                    Position = new Vector2(24, 10),
                    Text = label,
                    Font = HomeTypography.Display(15),
                    Colour = ResultColours.Muted,
                },
                valueText = new SpriteText
                {
                    Position = new Vector2(24, restValueY),
                    Width = width - 34,
                    Truncate = true,
                    Text = value,
                    Font = HomeTypography.Display(
                        prominent ? 38 : 26),
                    Colour = ResultColours.Navy,
                },
                hoverUnderline = new Box
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Position = new Vector2(24, -2),
                    Size = new Vector2(0, 2),
                    Colour = ResultColours.Cyan,
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeTo(0.3f, 140, Easing.OutQuint);
            valueText.MoveToY(restValueY - 3, 150, Easing.OutQuint);
            hoverUnderline.ResizeWidthTo(
                hoverUnderlineWidth,
                180,
                Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeOut(160, Easing.OutQuint);
            valueText.MoveToY(restValueY, 170, Easing.OutQuint);
            hoverUnderline.ResizeWidthTo(0, 150, Easing.OutQuint);
        }
    }

    private partial class ResultJudgementCell : Container
    {
        private readonly Box background;
        private readonly SpriteText valueText;
        private readonly Box shareBar;
        private readonly float shareWidth;

        public override bool HandlePositionalInput => true;

        public ResultJudgementCell(
            string label,
            int value,
            ColourInfo colour,
            float share)
        {
            float cellWidth = judgementStripWidth / 6;
            shareWidth = Math.Clamp(
                (cellWidth - 24) * share,
                0,
                cellWidth - 24);

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colour,
                    Alpha = 0,
                },
                new Box
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    RelativeSizeAxes = Axes.Y,
                    Height = 0.72f,
                    Width = 1,
                    Colour = ResultColours.Navy,
                    Alpha = 0.24f,
                },
                new SpriteText
                {
                    Position = new Vector2(12, 6),
                    Width = cellWidth - 22,
                    Truncate = true,
                    Text = label,
                    Font = HomeTypography.Display(15),
                    Colour = colour,
                },
                valueText = new SpriteText
                {
                    Position = new Vector2(12, 28),
                    Text = value.ToString("N0", CultureInfo.InvariantCulture),
                    Font = HomeTypography.Display(30),
                    Colour = ResultColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(12, 59),
                    Text = $"{share * 100:0.00}%",
                    Font = HomeTypography.Display(14),
                    Colour = ResultColours.Navy,
                },
                shareBar = new Box
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Position = new Vector2(12, -1),
                    Size = new Vector2(0, 3),
                    Colour = colour,
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeTo(0.08f, 120, Easing.OutQuint);
            valueText.MoveToY(25, 140, Easing.OutQuint);
            shareBar.ResizeWidthTo(shareWidth, 190, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeOut(150, Easing.OutQuint);
            valueText.MoveToY(28, 160, Easing.OutQuint);
            shareBar.ResizeWidthTo(0, 150, Easing.OutQuint);
        }
    }

    private partial class ResultActionButton : ClickableContainer
    {
        private readonly Action requestedAction;
        private bool enabled;
        private readonly Box background;
        private readonly SpriteIcon icon;
        private readonly Color4 restColour;
        private readonly Color4 hoverColour;
        private readonly Box accentRail;
        private readonly Container keycap;

        public override bool HandlePositionalInput => enabled;

        public ResultActionButton(
            LocalisableString label,
            string key,
            IconUsage iconUsage,
            Action action,
            float width,
            bool primary,
            bool enabled = true)
        {
            requestedAction = action;
            Size = new Vector2(width, 82);
            restColour = primary ? ResultColours.Navy : Color4.White;
            hoverColour = primary
                ? new Color4(0.045f, 0.13f, 0.66f, 1f)
                : ResultColours.SoftCyan;
            Masking = true;
            CornerRadius = 9;
            BorderThickness = 2;
            BorderColour = ResultColours.Navy;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = restColour,
                },
                accentRail = new Box
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.X,
                    Width = 0.18f,
                    Height = 3,
                    Colour = primary
                        ? ResultColours.Yellow
                        : ResultColours.Pink,
                    Alpha = 0.72f,
                },
                icon = new SpriteIcon
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 28,
                    Size = new Vector2(28),
                    Icon = iconUsage,
                    Colour = primary ? Color4.White : ResultColours.Navy,
                },
                new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = width - 110,
                    Truncate = true,
                    Text = label,
                    Font = HomeTypography.Display(25),
                    Colour = primary ? Color4.White : ResultColours.Navy,
                },
                keycap = new Container
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -18,
                    Size = new Vector2(key.Length > 1 ? 48 : 34, 25),
                    Masking = true,
                    CornerRadius = 5,
                    BorderThickness = 1,
                    BorderColour = primary
                        ? ResultColours.Yellow
                        : ResultColours.Pink,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = primary
                                ? new Color4(1, 1, 1, 0.05f)
                                : ResultColours.SoftCyan,
                            Alpha = primary ? 1 : 0.42f,
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = key,
                            Font = HomeTypography.Display(11),
                            Colour = primary
                                ? ResultColours.Yellow
                                : ResultColours.Pink,
                        },
                    },
                },
            };

            SetEnabled(enabled);
        }

        public void SetEnabled(bool enabled)
        {
            this.enabled = enabled;
            Action = enabled ? requestedAction : null;
            Alpha = enabled ? 1 : 0.42f;
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeColour(hoverColour, 120);
            icon.ScaleTo(1.08f, 140, Easing.OutQuint);
            keycap.ScaleTo(1.06f, 140, Easing.OutQuint);
            accentRail.ResizeWidthTo(1, 180, Easing.OutQuint);
            accentRail.FadeTo(1, 120);
            this.MoveToY(-3, 140, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeColour(restColour, 140);
            icon.ScaleTo(1, 150, Easing.OutQuint);
            keycap.ScaleTo(1, 150, Easing.OutQuint);
            accentRail.ResizeWidthTo(0.18f, 170, Easing.OutQuint);
            accentRail.FadeTo(0.72f, 140);
            this.MoveToY(0, 160, Easing.OutQuint);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            this.ScaleTo(0.98f, 80, Easing.OutQuint);
            return base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            this.ScaleTo(1, 180, Easing.OutBack);
            base.OnMouseUp(e);
        }
    }
}
