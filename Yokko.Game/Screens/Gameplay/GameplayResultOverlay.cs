using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
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
            new("ArchivoBlack", size);
    }

    private readonly Action retry;
    private readonly Action watchReplay;
    private readonly Action returnToSongSelect;
    private readonly JudgementConfiguration judgementConfiguration;
    private readonly ManiaModSet mods;
    private readonly GameplayResultPresentation presentation;
    private readonly IReadOnlyList<string> modChipLabels;
    private ResultActionButton replayActionButton;
    private Container backdrop;
    private Container stage;
    private Container leftStageLayout;
    private Container rightStageContent;
    private Box scoreSignalRunner;
    private ResultScorePanel scorePanel;
    private ResultRankSeal rankSeal;
    private ResultSongHeading songHeading;
    private int renderedModChipCount;
    private float lastResponsiveStageScale;

    // Kept for existing result-flow tests while the selected design's
    // character layer is intentionally deferred.
    internal bool MascotReady => true;
    internal bool CharacterStageReady => rightStageContent != null;
    internal bool RankSealReady => rankSeal != null;
    internal bool RankSealLabelFits => rankSeal?.LabelFits == true;
    internal string DisplayedRank => rankSeal?.DisplayedLabel ?? string.Empty;
    internal string RankSealEyebrow => rankSeal?.Eyebrow ?? string.Empty;
    internal string RankSealFooter => rankSeal?.Footer ?? string.Empty;
    internal int ActionCount => 3;
    internal int RenderedModChipCount => renderedModChipCount;
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

        DisplayedMods = practiceSession
            ? $"{displayedMods}  ·  PRACTICE"
            : displayedMods;
        modChipLabels = createModChipLabels(
            this.mods,
            this.judgementConfiguration,
            practiceSession);

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
                        Child = createCharacterStagePlaceholder(),
                    },
                },
            },
        };
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
        string chartFingerprint = YokkoBeatmapFingerprint.Compute(beatmap);

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
                createDetailsLedger(beatmap, result, isNewBest),
                createProvenanceRail(beatmap, chartFingerprint),
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
                    Position = new Vector2(574, 16),
                    Text = "PLAYER",
                    Font = HomeTypography.Display(15),
                    Spacing = new Vector2(1.4f, 0),
                    Colour = ResultColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(574, 43),
                    Text = presentation.PlayerName,
                    Width = 205,
                    Truncate = true,
                    Font = HomeTypography.Display(29),
                    Colour = ResultColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(788, 51),
                    Text = $"UID {presentation.PlayerId}",
                    Width = 190,
                    Truncate = true,
                    Font = HomeTypography.Display(15),
                    Colour = ResultColours.Muted,
                },
                new Box
                {
                    Position = new Vector2(574, 80),
                    Size = new Vector2(404, 2),
                    Colour = new Color4(0.51f, 0.58f, 0.76f, 0.5f),
                },
                new SpriteText
                {
                    Position = new Vector2(574, 91),
                    Text = "PLAYED AT",
                    Font = HomeTypography.Display(13),
                    Colour = ResultColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(680, 91),
                    Text = playedAt,
                    Font = HomeTypography.Display(15),
                    Colour = ResultColours.Navy,
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
                    Font = HomeTypography.Display(19),
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
                    Font = HomeTypography.Display(16),
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

        return scorePanel = new ResultScorePanel
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
                    Font = HomeTypography.Display(17),
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
                    Font = HomeTypography.Display(13),
                    Colour = ResultColours.Cyan,
                },
                new SpriteText
                {
                    Position = new Vector2(240, 187),
                    Text = previousBest,
                    Font = HomeTypography.Display(24),
                    Colour = Color4.White,
                },
                new SpriteText
                {
                    Position = new Vector2(476, 182),
                    Text = delta,
                    Font = HomeTypography.Display(28),
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
                    "COMBO BREAKS",
                    result.ComboBreaks.ToString(CultureInfo.InvariantCulture),
                    708,
                    26,
                    ResultColours.Cyan),
                new Box
                {
                    Position = new Vector2(708, 112),
                    Size = new Vector2(236, 2),
                    Colour = new Color4(1, 1, 1, 0.5f),
                },
                createScoreSideMetric(
                    "MISSES",
                    result.Miss.ToString(CultureInfo.InvariantCulture),
                    708,
                    132,
                    ResultColours.Pink),
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
            Size = new Vector2(246, 80),
            Children = new Drawable[]
            {
                new SpriteText
                {
                    Text = label,
                    Font = HomeTypography.Display(14),
                    Colour = accent,
                },
                new SpriteText
                {
                    Y = 24,
                    Text = value,
                    Font = ResultTypography.Poster(38),
                    Colour = Color4.White,
                },
            },
        };

    private Drawable createSummaryRail(ManiaScoreResult result) =>
        new Container
        {
            Position = new Vector2(contentLeft, 554),
            Size = new Vector2(contentWidth, 72),
            Children = new Drawable[]
            {
                createMetricCell(
                    judgementConfiguration.Mode == JudgementMode.Etterna
                        ? "WIFE3"
                        : "ACCURACY",
                    $"{result.Accuracy * 100:0.00}%",
                    0,
                    300),
                createMetricCell(
                    "MAX COMBO",
                    $"{result.MaxCombo:N0}×",
                    326,
                    300),
                createMetricCell(
                    "RATE",
                    $"{mods.FixedRateSpeedChange:0.00}×",
                    652,
                    160),
                createMetricCell(
                    "RULESET",
                    rulesetLabel(),
                    812,
                    174),
            },
        };

    private static Drawable createMetricCell(
        LocalisableString label,
        string value,
        float x,
        float width) =>
        new ResultMetricCell(label, value, width)
        {
            Position = new Vector2(x, 0),
        };

    private Drawable createTimingStrip()
    {
        GameplayTimingSummary timing = presentation.Timing;
        return new Container
        {
            Position = new Vector2(contentLeft, 640),
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
                    "EARLY",
                    timing?.EarlyCount.ToString(CultureInfo.InvariantCulture)
                        ?? "—",
                    18,
                    ResultColours.Cyan),
                createInlineDatum(
                    "ON TIME",
                    timing?.OnTimeCount.ToString(CultureInfo.InvariantCulture)
                        ?? "—",
                    190,
                    ResultColours.Cyan),
                createInlineDatum(
                    "LATE",
                    timing?.LateCount.ToString(CultureInfo.InvariantCulture)
                        ?? "—",
                    390,
                    ResultColours.Pink),
                createInlineDatum(
                    "MEAN",
                    timing == null
                        ? "—"
                        : $"{timing.MeanMilliseconds:+0.0;-0.0;0.0} ms",
                    560,
                    ResultColours.Navy),
                createInlineDatum(
                    "UR",
                    timing?.UnstableRate.ToString(
                        "0.0",
                        CultureInfo.InvariantCulture) ?? "—",
                    810,
                    ResultColours.Pink),
            },
        };
    }

    private static Drawable createInlineDatum(
        string label,
        string value,
        float x,
        Color4 colour) =>
        new Container
        {
            Position = new Vector2(x, 14),
            Size = new Vector2(170, 30),
            Children = new Drawable[]
            {
                new SpriteText
                {
                    Text = label,
                    Font = HomeTypography.Display(12),
                    Colour = colour,
                },
                new SpriteText
                {
                    X = 74,
                    Text = value,
                    Font = HomeTypography.Display(17),
                    Colour = ResultColours.Navy,
                },
            },
        };

    private Drawable createJudgementStrip(ManiaScoreResult result)
    {
        string[] labels =
            judgementConfiguration.Mode == JudgementMode.Etterna
                ? ["MARVELOUS", "PERFECT", "GREAT", "GOOD", "BAD", "MISS"]
                : ["PERFECT", "GREAT", "GOOD", "OK", "MEH", "MISS"];
        (string Label, int Value, Color4 Colour)[] judgements =
        {
            (labels[0], result.Perfect, ResultColours.Pink),
            (labels[1], result.Great, ResultColours.Cyan),
            (labels[2], result.Good, new Color4(0.14f, 0.72f, 0.42f, 1f)),
            (labels[3], result.Ok, new Color4(1f, 0.62f, 0.12f, 1f)),
            (labels[4], result.Meh, new Color4(0.56f, 0.42f, 0.91f, 1f)),
            (labels[5], result.Miss, new Color4(1f, 0.36f, 0.48f, 1f)),
        };

        int totalJudgements = judgements.Sum(static judgement =>
            judgement.Value);
        var flow = new FillFlowContainer
        {
            RelativeSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
        };
        foreach ((string label, int value, Color4 colour) in judgements)
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
            Position = new Vector2(contentLeft, 696),
            Size = new Vector2(judgementStripWidth, 88),
            Child = flow,
        };
    }

    private Drawable createDetailsLedger(
        YokkoBeatmap beatmap,
        ManiaScoreResult result,
        bool isNewBest)
    {
        string replayStatus = presentation.ReplaySaved
            ? "SAVED"
            : ReplayAvailable ? "READY" : "NONE";

        return new Container
        {
            Position = new Vector2(contentLeft, 800),
            Size = new Vector2(contentWidth, 112),
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 2,
                    Colour = ResultColours.Navy,
                    Alpha = 0.42f,
                },
                createDetailColumn(
                    0,
                    250,
                    ("CLEAR TYPE", isNewBest ? "NEW BEST" : "CLEARED"),
                    ("RULESET", rulesetLabel()),
                    ("RATE", $"{mods.FixedRateSpeedChange:0.00}×")),
                createDetailColumn(
                    276,
                    236,
                    ("DEVICE", "KEYBOARD"),
                    ("REPLAY", replayStatus),
                    ("MAX MISS", result.MaxMissCombo.ToString(
                        CultureInfo.InvariantCulture))),
                createDetailColumn(
                    538,
                    448,
                    ("MAPPER", displayOrDash(beatmap.Creator)),
                    ("BPM / LENGTH", $"{formatBpm(beatmap)}  ·  {formatDuration(beatmap)}"),
                    ("NOTES", beatmap.NoteCount.ToString(
                        "N0",
                        CultureInfo.InvariantCulture))),
                new SpriteText
                {
                    Position = new Vector2(730, 78),
                    Text = "MODS",
                    Font = HomeTypography.Display(11),
                    Colour = ResultColours.Muted,
                },
                new Container
                {
                    Position = new Vector2(780, 72),
                    Size = new Vector2(206, 30),
                    Child = createModChipRail(206),
                },
            },
        };
    }

    private static Drawable createDetailColumn(
        float x,
        float width,
        params (string Label, string Value)[] rows)
    {
        var container = new Container
        {
            Position = new Vector2(x, 12),
            Size = new Vector2(width, 92),
        };
        for (int i = 0; i < rows.Length; i++)
        {
            float y = i * 29;
            container.Add(new SpriteText
            {
                Position = new Vector2(4, y),
                Text = rows[i].Label,
                Font = HomeTypography.Display(11),
                Colour = ResultColours.Muted,
            });
            container.Add(new SpriteText
            {
                Position = new Vector2(width < 260 ? 112 : 126, y),
                Width = width - (width < 260 ? 118 : 132),
                Truncate = true,
                Text = rows[i].Value,
                Font = HomeTypography.Display(14),
                Colour = ResultColours.Navy,
            });
            container.Add(new Box
            {
                Position = new Vector2(0, y + 23),
                Size = new Vector2(width - 12, 1),
                Colour = ResultColours.Navy,
                Alpha = 0.22f,
            });
        }

        return container;
    }

    private Drawable createProvenanceRail(
        YokkoBeatmap beatmap,
        string fingerprint) =>
        new Container
        {
            Position = new Vector2(contentLeft, 916),
            Size = new Vector2(contentWidth, 38),
            Children = new Drawable[]
            {
                createInlineDatum(
                    "SOURCE",
                    beatmap.SourceFormat.ToString().ToUpperInvariant(),
                    0,
                    ResultColours.Navy),
                createInlineDatum(
                    "CHART ID",
                    beatmap.OnlineBeatmapId > 0
                        ? beatmap.OnlineBeatmapId.ToString(
                            CultureInfo.InvariantCulture)
                        : fingerprint[..8].ToUpperInvariant(),
                    320,
                    ResultColours.Navy),
                createInlineDatum(
                    "HASH",
                    formatHash(fingerprint),
                    650,
                    ResultColours.Navy),
            },
        };

    private Drawable createActionRow() =>
        new Container
        {
            Position = new Vector2(contentLeft, 964),
            Size = new Vector2(contentWidth, 76),
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
            _ => "YOKKO",
        };

    private static string formatBpm(YokkoBeatmap beatmap)
    {
        double[] bpms = beatmap.TimingPoints
            .Where(static point => point.Uninherited
                                   && point.BeatsPerMinute > 0)
            .Select(static point => point.BeatsPerMinute)
            .ToArray();
        if (bpms.Length == 0)
            return "—";

        double minimum = bpms.Min();
        double maximum = bpms.Max();
        return Math.Abs(maximum - minimum) < 0.05
            ? minimum.ToString("0.##", CultureInfo.InvariantCulture)
            : $"{minimum.ToString("0.##", CultureInfo.InvariantCulture)}-{maximum.ToString("0.##", CultureInfo.InvariantCulture)}";
    }

    private static string formatDuration(YokkoBeatmap beatmap)
    {
        if (beatmap.HitObjects.Count == 0)
            return "00:00";

        double durationMilliseconds = beatmap.HitObjects.Max(
            static hitObject =>
                hitObject.EndTimeMilliseconds
                ?? hitObject.StartTimeMilliseconds);
        TimeSpan duration = TimeSpan.FromMilliseconds(
            Math.Max(0, durationMilliseconds));
        return duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : duration.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    }

    private static string formatHash(string fingerprint) =>
        fingerprint.Length <= 12
            ? fingerprint.ToUpperInvariant()
            : $"{fingerprint[..4].ToUpperInvariant()}…{fingerprint[^4..].ToUpperInvariant()}";

    private static IReadOnlyList<string> createModChipLabels(
        ManiaModSet mods,
        JudgementConfiguration judgementConfiguration,
        bool practiceSession)
    {
        var labels = new List<string>();

        if (mods.IsEmpty)
        {
            labels.Add("NM");
        }
        else
        {
            labels.AddRange(mods.Acronyms.Select(static acronym =>
                acronym.ToUpperInvariant()));
            if (Math.Abs(mods.FixedRateSpeedChange - 1) > 0.001)
            {
                labels.Add(
                    mods.FixedRateSpeedChange.ToString(
                        "0.00",
                        CultureInfo.InvariantCulture)
                    + "×");
            }
        }

        if (judgementConfiguration.Mode == JudgementMode.Etterna)
        {
            labels.Add(
                "ET "
                + judgementConfiguration.EtternaJusticeLabel
                    .ToUpperInvariant());
        }
        else if (judgementConfiguration.Mode == JudgementMode.OsuStable)
        {
            labels.Add("STABLE");
        }

        if (practiceSession)
            labels.Add("PRACTICE");

        return labels;
    }

    private Drawable createModChipRail(float maxWidth = 300)
    {
        const float spacing = 8;
        var visible = new List<(string Label, float Width)>();
        float usedWidth = 0;

        foreach (string label in modChipLabels)
        {
            float width = calculateModChipWidth(label);
            float nextWidth = usedWidth
                              + (visible.Count > 0 ? spacing : 0)
                              + width;
            if (nextWidth > maxWidth)
                break;

            visible.Add((label, width));
            usedWidth = nextWidth;
        }

        int hiddenCount = modChipLabels.Count - visible.Count;
        if (hiddenCount > 0)
        {
            string overflowLabel = $"+{hiddenCount}";
            float overflowWidth = calculateModChipWidth(overflowLabel);
            while (visible.Count > 0
                   && usedWidth + spacing + overflowWidth > maxWidth)
            {
                (string _, float removedWidth) = visible[^1];
                visible.RemoveAt(visible.Count - 1);
                usedWidth -= removedWidth;
                if (visible.Count > 0)
                    usedWidth -= spacing;
                hiddenCount++;
                overflowLabel = $"+{hiddenCount}";
                overflowWidth = calculateModChipWidth(overflowLabel);
            }

            visible.Add((overflowLabel, overflowWidth));
        }

        renderedModChipCount = visible.Count;
        var flow = new FillFlowContainer
        {
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(spacing, 0),
        };
        foreach ((string label, float width) in visible)
            flow.Add(new ResultModChip(label, width));
        return flow;
    }

    private static float calculateModChipWidth(string label) =>
        Math.Clamp(24 + label.Length * 9, 54, 112);

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

    private static Drawable createCharacterStagePlaceholder() =>
        new Container
        {
            Position = new Vector2(1160, 36),
            Size = new Vector2(760, 1044),
            Children = new Drawable[]
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
            },
        };

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
        public bool InteractionActive { get; private set; }

        public override bool HandlePositionalInput => true;

        public void SetInteractionState(bool active)
        {
            InteractionActive = active;
            this.MoveToY(active ? 306 : 310, 180, Easing.OutQuint);
            this.ScaleTo(active ? 1.006f : 1, 180, Easing.OutQuint);
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
                    Font = HomeTypography.Display(31),
                    Colour = ResultColours.Navy,
                },
                new SpriteText
                {
                    Y = 38,
                    Width = 570,
                    Truncate = true,
                    Text = displayOrDash(artist),
                    Font = HomeTypography.Display(15),
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

        public override bool HandlePositionalInput => true;

        public ResultMetricCell(
            LocalisableString label,
            string value,
            float width)
        {
            Size = new Vector2(width, 72);
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
                    Position = new Vector2(24, 4),
                    Text = label,
                    Font = HomeTypography.Display(13),
                    Colour = ResultColours.Muted,
                },
                valueText = new SpriteText
                {
                    Position = new Vector2(24, 27),
                    Width = width - 34,
                    Truncate = true,
                    Text = value,
                    Font = HomeTypography.Display(width < 180 ? 22 : 31),
                    Colour = ResultColours.Navy,
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeTo(0.3f, 140, Easing.OutQuint);
            valueText.MoveToY(24, 150, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeOut(160, Easing.OutQuint);
            valueText.MoveToY(27, 170, Easing.OutQuint);
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
            Color4 colour,
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
                    Font = HomeTypography.Display(13),
                    Colour = colour,
                },
                valueText = new SpriteText
                {
                    Position = new Vector2(12, 28),
                    Text = value.ToString("N0", CultureInfo.InvariantCulture),
                    Font = HomeTypography.Display(27),
                    Colour = ResultColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(12, 59),
                    Text = $"{share * 100:0.00}%",
                    Font = HomeTypography.Display(13),
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

    private partial class ResultModChip : Container
    {
        public ResultModChip(string label, float width)
        {
            Size = new Vector2(width, 28);
            Masking = true;
            CornerRadius = 5;
            BorderThickness = 1.5f;
            BorderColour = ResultColours.Navy;
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
                new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = label,
                    Font = HomeTypography.Display(12),
                    Colour = ResultColours.Navy,
                },
            };
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
            Size = new Vector2(width, 74);
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
                icon = new SpriteIcon
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 28,
                    Size = new Vector2(24),
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
                    Font = HomeTypography.Display(22),
                    Colour = primary ? Color4.White : ResultColours.Navy,
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -22,
                    Text = key,
                    Font = HomeTypography.Display(11),
                    Colour = primary
                        ? ResultColours.Yellow
                        : ResultColours.Pink,
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
            this.MoveToY(-3, 140, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeColour(restColour, 140);
            icon.ScaleTo(1, 150, Easing.OutQuint);
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
