using System;
using System.Collections.Generic;
using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
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
    private const float designedWidth = 1600;
    private const float designedHeight = 900;

    private readonly Action retry;
    private readonly Action watchReplay;
    private readonly Action returnToSongSelect;
    private readonly JudgementConfiguration judgementConfiguration;
    private Container backdrop;
    private Container stage;
    private Container leftStageLayout;
    private Container rightStageLayout;
    private Container rightStageContent;
    private Container leftDecorationLayout;
    private Container rightDecorationLayout;
    private Container brandHost;
    private Sprite mascot;
    private Sprite scoreRibbon;
    private readonly IReadOnlyList<string> modChipLabels;
    private int renderedModChipCount;
    private float lastResponsiveStageScale;

    internal bool MascotReady => mascot?.Texture != null;
    internal int ActionCount => 3;
    internal int RenderedModChipCount => renderedModChipCount;
    internal string DisplayedMods { get; }
    internal bool PracticeSession { get; }
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
        JudgementConfiguration? judgementConfiguration = null)
    {
        mods ??= ManiaModSet.Empty;
        this.judgementConfiguration =
            judgementConfiguration ?? JudgementConfiguration.YokkoDefault;
        this.retry = retry;
        this.watchReplay = watchReplay;
        this.returnToSongSelect = returnToSongSelect;
        PracticeSession = practiceSession;
        string displayedMods = mods.IsEmpty
            ? "NM"
            : string.Join("  ", mods.DisplayLabels);
        if (this.judgementConfiguration.Mode == JudgementMode.Etterna)
        {
            displayedMods +=
                $"  ·  ETTERNA "
                + this.judgementConfiguration.EtternaJusticeLabel
                    .ToUpperInvariant();
        }
        DisplayedMods = practiceSession
            ? $"{displayedMods}  ·  PRACTICE"
            : displayedMods;
        modChipLabels = createModChipLabels(
            mods,
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
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = HomeControlColours.Ivory,
                    },
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
                    createIvoryStage(),
                    createTickerStrip(),
                    createDecorations(),
                    leftStageLayout = new Container
                    {
                        Size = new Vector2(designedWidth, designedHeight),
                        Child = createResultContent(
                            beatmap,
                            result,
                            rank,
                            isNewBest),
                    },
                    rightStageLayout = new Container
                    {
                        Size = new Vector2(designedWidth, designedHeight),
                        Child = rightStageContent = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = createMascotStage(),
                        },
                    },
                },
            },
        };
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        Texture mascotTexture = textures
            .Get("Gameplay/yokko-result-standing")
            .Crop(new RectangleF(60, 150, 820, 1140));
        mascot.Texture = mascotTexture;
        scoreRibbon.Texture = textures
            .Get("Gameplay/yokko-result-ribbon")
            .Crop(new RectangleF(43, 297, 1598, 301));
        brandHost.Child = new HomeBrandLockup(
            textures.Get("home-logo-light"),
            HomeControlColours.Navy,
            HomeControlColours.Yellow)
        {
            Scale = Vector2.One,
        };
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        // Preserve the final gameplay frame while the result background,
        // score data, and mascot arrive as separate visual layers.
        stage.Alpha = 1;
        leftStageLayout.Alpha = 0;
        leftStageLayout.X = -18;
        rightStageContent.Alpha = 0;
        rightStageContent.X = 24;
        leftDecorationLayout.Alpha = 0;
        rightDecorationLayout.Alpha = 0;

        backdrop.FadeIn(360, Easing.OutQuint);
        leftDecorationLayout.Delay(70).FadeIn(260, Easing.OutQuint);
        rightDecorationLayout.Delay(110).FadeIn(260, Easing.OutQuint);
        leftStageLayout.Delay(70).FadeIn(300, Easing.OutQuint);
        leftStageLayout.Delay(70).MoveToX(0, 400, Easing.OutQuint);
        rightStageContent.Delay(130).FadeIn(340, Easing.OutQuint);
        rightStageContent.Delay(130).MoveToX(0, 350, Easing.OutQuint);
        mascot.MoveToY(mascot.Y + 4, 2100, Easing.InOutSine)
              .Then()
              .MoveToY(mascot.Y - 4, 2100, Easing.InOutSine)
              .Loop();
    }

    internal void TriggerReplay() => watchReplay();

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
        leftDecorationLayout.FinishTransforms();
        leftDecorationLayout.Alpha = 1;
        rightDecorationLayout.FinishTransforms();
        rightDecorationLayout.Alpha = 1;
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
        bool isNewBest) =>
        new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                brandHost = new Container
                {
                    Position = new Vector2(70, 67),
                    Size = new Vector2(430, 150),
                },
                new SpriteText
                {
                    Position = new Vector2(852, 68),
                    Text = YokkoStrings.Get("gameplay.result.title"),
                    Font = HomeTypography.Hero(130),
                    Colour = HomeControlColours.Navy,
                },
                new Box
                {
                    Position = new Vector2(1282, 145),
                    Size = new Vector2(70, 3),
                    Colour = HomeControlColours.Navy,
                },
                new HomeMicroLine
                {
                    Position = new Vector2(1356, 142),
                    Width = 62,
                },
                new HomeCrosshairMark
                {
                    Position = new Vector2(1495, 86),
                    Colour = HomeControlColours.Navy,
                    Scale = new Vector2(0.72f),
                },
                new SpriteText
                {
                    Position = new Vector2(860, 193),
                    Width = 610,
                    Truncate = true,
                    Text = $"{beatmap.Title} [{beatmap.DifficultyName}]",
                    Font = HomeTypography.Display(31),
                    Colour = HomeControlColours.Navy,
                },
                new Box
                {
                    Position = new Vector2(860, 226),
                    Size = new Vector2(176, 6),
                    Colour = HomeControlColours.Yellow,
                },
                createScorePanel(result, rank, isNewBest),
                createSummaryRail(result),
                createJudgementStrip(result),
                createActionRow(),
            },
        };

    private Drawable createScorePanel(
        ManiaScoreResult result,
        string rank,
        bool isNewBest)
        => new Container
        {
            Position = new Vector2(486, 258),
            Size = new Vector2(1140, 236),
            Children = new Drawable[]
            {
                scoreRibbon = new Sprite
                {
                    Size = new Vector2(1140, 236),
                },
                new SpriteText
                {
                    Position = new Vector2(132, -5),
                    Text = rank,
                    Font = HomeTypography.Hero(240),
                    Colour = HomeControlColours.Yellow,
                    Alpha = 0.92f,
                },
                new SpriteText
                {
                    Position = new Vector2(139, -11),
                    Text = rank,
                    Font = HomeTypography.Hero(240),
                    Colour = HomeControlColours.PaleCyan,
                },
                new Box
                {
                    Position = new Vector2(322, 42),
                    Size = new Vector2(2, 148),
                    Colour = new Color4(1f, 1f, 1f, 0.82f),
                },
                new SpriteIcon
                {
                    Position = new Vector2(362, 35),
                    Size = new Vector2(20),
                    Icon = FontAwesome.Solid.Star,
                    Colour = HomeControlColours.Yellow,
                },
                new SpriteText
                {
                    Position = new Vector2(396, 30),
                    Text = isNewBest
                        ? YokkoStrings.Get("gameplay.result.new_best")
                        : "SCORE",
                    Font = HomeTypography.Display(20),
                    Spacing = new Vector2(2.8f, 0),
                    Colour = isNewBest
                        ? HomeControlColours.Pink
                        : HomeControlColours.Cyan,
                },
                new SpriteText
                {
                    Position = new Vector2(374, 46),
                    Text = $"{result.Score:0000000}",
                    Font = HomeTypography.Hero(180),
                    Colour = Color4.White,
                },
                new SpriteText
                {
                    Position = new Vector2(374, 46),
                    Text = result.Score
                        .ToString("0000000", CultureInfo.InvariantCulture)[..1],
                    Font = HomeTypography.Hero(180),
                    Colour = HomeControlColours.Cyan,
                },
            },
        };

    private Drawable createSummaryRail(ManiaScoreResult result) =>
        new Container
        {
            Position = new Vector2(590, 516),
            Size = new Vector2(960, 138),
            Children = new Drawable[]
            {
                new Container
                {
                    Size = new Vector2(306, 132),
                    Children = new Drawable[]
                    {
                        new SpriteText
                        {
                            Position = new Vector2(0, 10),
                            Text = "MODS",
                            Font = HomeTypography.Display(17),
                            Spacing = new Vector2(1.6f, 0),
                            Colour = HomeControlColours.Navy,
                        },
                        new Box
                        {
                            Position = new Vector2(82, 26),
                            Size = new Vector2(142, 2),
                            Colour = HomeControlColours.Navy,
                        },
                        new Box
                        {
                            Position = new Vector2(231, 21),
                            Size = new Vector2(10),
                            Rotation = 45,
                            Colour = HomeControlColours.Yellow,
                        },
                        createModChipRail(),
                    },
                },
                new Box
                {
                    Position = new Vector2(326, 10),
                    Size = new Vector2(1, 104),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.42f),
                },
                createMetricCell(
                    judgementConfiguration.Mode == JudgementMode.Etterna
                        ? "WIFE3"
                        : "ACCURACY",
                    $"{result.Accuracy * 100:0.00}%",
                    376,
                    286),
                new Box
                {
                    Position = new Vector2(678, 10),
                    Size = new Vector2(1, 104),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.42f),
                },
                createMetricCell(
                    judgementConfiguration.Mode == JudgementMode.Etterna
                        ? "MAX COMBO · CB"
                        : YokkoStrings.Get("gameplay.result.max_combo"),
                    judgementConfiguration.Mode == JudgementMode.Etterna
                        ? $"{result.MaxCombo} · {result.Ok + result.Meh + result.Miss}"
                        : result.MaxCombo.ToString(),
                    738,
                    214),
                new Box
                {
                    Position = new Vector2(-54, 132),
                    Size = new Vector2(1010, 2),
                    Colour = HomeControlColours.Navy,
                },
                new Box
                {
                    Position = new Vector2(-57, 128),
                    Size = new Vector2(9),
                    Rotation = 45,
                    Colour = HomeControlColours.Cyan,
                },
                new Box
                {
                    Position = new Vector2(951, 128),
                    Size = new Vector2(9),
                    Rotation = 45,
                    Colour = HomeControlColours.Navy,
                },
            },
        };

    private static Drawable createMetricCell(
        LocalisableString label,
        string value,
        float x,
        float width) =>
        new Container
        {
            Position = new Vector2(x, 0),
            Size = new Vector2(width, 126),
            Children = new Drawable[]
            {
                new SpriteText
                {
                    Text = label,
                    Font = HomeTypography.Display(17),
                    Spacing = new Vector2(1.6f, 0),
                    Colour = HomeControlColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(0, 24),
                    Text = value,
                    Font = HomeTypography.Hero(68),
                    Colour = HomeControlColours.Navy,
                },
                new Box
                {
                    Position = new Vector2(0, 95),
                    Size = new Vector2(width - 18, 6),
                    Colour = HomeControlColours.Yellow,
                },
            },
        };

    private Drawable createJudgementStrip(ManiaScoreResult result)
    {
        string[] labels =
            judgementConfiguration.Mode == JudgementMode.Etterna
                ? ["MARV", "PERF", "GREAT", "GOOD", "BAD", "MISS"]
                : ["P", "G", "GOOD", "OK", "MEH", "MISS"];
        (string Label, int Value, Color4 Colour)[] judgements =
        {
            (labels[0], result.Perfect, HomeControlColours.Pink),
            (labels[1], result.Great, HomeControlColours.Cyan),
            (labels[2], result.Good, new Color4(0.14f, 0.72f, 0.42f, 1f)),
            (labels[3], result.Ok, new Color4(1f, 0.7f, 0.08f, 1f)),
            (labels[4], result.Meh, new Color4(0.65f, 0.27f, 0.96f, 1f)),
            (labels[5], result.Miss, new Color4(0.97f, 0.12f, 0.19f, 1f)),
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
            Position = new Vector2(548, 656),
            Size = new Vector2(1010, 86),
            Children = new Drawable[]
            {
                flow,
                new SpriteIcon
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = 0,
                    Size = new Vector2(13),
                    Icon = FontAwesome.Solid.Plus,
                    Colour = HomeControlColours.Navy,
                },
            },
        };
    }

    private static Drawable createJudgementCell(
        string label,
        int value,
        Color4 colour) =>
        new Container
        {
            Width = 1010f / 6,
            RelativeSizeAxes = Axes.Y,
            Children = new Drawable[]
            {
                new Box
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Width = 1,
                    RelativeSizeAxes = Axes.Y,
                    Height = 0.7f,
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
                    Y = 8,
                    Text = label,
                    Font = HomeTypography.Display(18),
                    Colour = colour,
                },
                new SpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = 34,
                    Text = value.ToString(),
                    Font = HomeTypography.Display(44),
                    Colour = HomeControlColours.Navy,
                },
            },
        };

    private Drawable createActionRow() =>
        new Container
        {
            Position = new Vector2(590, 762),
            Size = new Vector2(960, 102),
            Children = new Drawable[]
            {
                new ResultActionButton(
                    YokkoStrings.Get("gameplay.result.retry"),
                    "R",
                    FontAwesome.Solid.Redo,
                    retry,
                    405,
                    true),
                new ResultActionButton(
                    YokkoStrings.Get("gameplay.result.watch_replay"),
                    "V",
                    FontAwesome.Solid.Play,
                    watchReplay,
                    257,
                    false)
                {
                    X = 428,
                },
                new ResultActionButton(
                    YokkoStrings.Get("gameplay.result.return"),
                    "ESC",
                    FontAwesome.Solid.Music,
                    returnToSongSelect,
                    265,
                    false)
                {
                    X = 703,
                },
            },
        };

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
            var fixedRateLabels = new List<string>();
            var detailLabels = new List<string>();

            for (int i = 0; i < mods.Acronyms.Count; i++)
            {
                string acronym = mods.Acronyms[i].ToUpperInvariant();
                if (isFixedRateAcronym(acronym))
                    fixedRateLabels.Add(acronym);
                else
                    labels.Add(acronym);

                string displayLabel = mods.DisplayLabels[i];
                string prefix = acronym + " ";
                if (!displayLabel.StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                string detail = displayLabel[prefix.Length..]
                    .ToUpperInvariant();
                if (detail.Length <= 12
                    && (detail.Contains('×')
                        || detail.Contains('→')
                        || detail == "MAX"))
                    detailLabels.Add(detail);
            }

            labels.AddRange(fixedRateLabels);
            if (fixedRateLabels.Count > 0
                && Math.Abs(mods.FixedRateSpeedChange - 1) > 0.001)
            {
                labels.Add(
                    mods.FixedRateSpeedChange.ToString(
                        "0.00",
                        CultureInfo.InvariantCulture)
                    + "×");
            }

            foreach (string detail in detailLabels)
            {
                if (!labels.Contains(detail))
                    labels.Add(detail);
            }
        }

        if (judgementConfiguration.Mode == JudgementMode.Etterna)
        {
            labels.Add(
                "ET "
                + judgementConfiguration.EtternaJusticeLabel
                    .ToUpperInvariant());
        }

        if (practiceSession)
            labels.Add("PRACTICE");

        return labels;
    }

    private static bool isFixedRateAcronym(string acronym) =>
        acronym is "HT" or "DC" or "DT" or "NC";

    private Drawable createModChipRail()
    {
        const float maxWidth = 306;
        const float spacing = 10;
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
            Position = new Vector2(0, 36),
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(spacing, 0),
        };

        foreach ((string label, float width) in visible)
            flow.Add(createModChip(label, width));

        return flow;
    }

    private static float calculateModChipWidth(string label) =>
        Math.Clamp(30 + label.Length * 11, 62, 118);

    private static Drawable createModChip(string label, float width) =>
        new Container
        {
            Size = new Vector2(width, 58),
            Masking = true,
            CornerRadius = 8,
            BorderThickness = 2,
            BorderColour = HomeControlColours.Navy,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = HomeControlColours.Navy,
                },
                new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = label,
                    Font = HomeTypography.Display(20),
                    Colour = Color4.White,
                },
                new Box
                {
                    Position = new Vector2(0, 0),
                    Size = new Vector2(width, 4),
                    Colour = HomeControlColours.Yellow,
                },
            },
        };

    private static Drawable createTickerStrip() =>
        new Container
        {
            RelativeSizeAxes = Axes.X,
            Height = 26,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = HomeControlColours.Navy,
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 24,
                    Text =
                        "YOKKO RHYTHM STUDIO // EST. 2025 // VOL.01   +   "
                        + "YOKKO RHYTHM STUDIO // 4K MANIA   +   "
                        + "CHART LAB // FEEL THE BEAT   +   "
                        + "RHYTHM CHART STUDIO // EST. 2025 // VOL.01   +   "
                        + "YOKKO RHYTHM STUDIO // 4K MANIA",
                    Font = HomeTypography.Display(10),
                    Spacing = new Vector2(1.8f, 0),
                    Colour = Color4.White,
                },
            },
        };

    private Drawable createMascotStage() =>
        new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new HomeDotField
                {
                    Position = new Vector2(26, 268),
                    Size = new Vector2(92, 58),
                    Colour = new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        0.52f),
                },
                new HomeMicroLine
                {
                    Position = new Vector2(38, 247),
                    Width = 74,
                    Colour = HomeControlColours.Cyan,
                },
                new HomeCrosshairMark
                {
                    Position = new Vector2(80, 302),
                    Colour = HomeControlColours.Navy,
                    Scale = new Vector2(0.72f),
                },
                new Circle
                {
                    Position = new Vector2(56, 849),
                    Size = new Vector2(430, 30),
                    Colour = new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        0.38f),
                },
                mascot = new Sprite
                {
                    Origin = Anchor.Centre,
                    Position = new Vector2(272, 536),
                    Size = new Vector2(470, 654),
                },
                new SpriteText
                {
                    Position = new Vector2(34, 400),
                    Rotation = -90,
                    Text = "RHYTHM CHART STUDIO  //  VOL.01",
                    Font = HomeTypography.Display(10),
                    Spacing = new Vector2(1.7f, 0),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.38f),
                },
                new HomeDotField
                {
                    Position = new Vector2(480, 796),
                    Size = new Vector2(70, 48),
                    Colour = new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        0.38f),
                },
                new SpriteIcon
                {
                    Position = new Vector2(484, 735),
                    Size = new Vector2(14),
                    Icon = FontAwesome.Solid.Plus,
                    Colour = HomeControlColours.Pink,
                },
                new Circle
                {
                    Position = new Vector2(526, 186),
                    Size = new Vector2(18),
                    BorderThickness = 2,
                    BorderColour = HomeControlColours.Cyan,
                    Masking = true,
                    Colour = HomeControlColours.Ivory,
                },
            },
        };

    private Drawable createDecorations() =>
        new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                leftDecorationLayout = new Container
                {
                    Size = new Vector2(designedWidth, designedHeight),
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Position = new Vector2(52, 70),
                            Size = new Vector2(13),
                            Icon = FontAwesome.Solid.Plus,
                            Colour = HomeControlColours.Pink,
                        },
                        new SpriteIcon
                        {
                            Position = new Vector2(111, 62),
                            Size = new Vector2(11),
                            Icon = FontAwesome.Solid.Plus,
                            Colour = HomeControlColours.Cyan,
                        },
                        new Box
                        {
                            Position = new Vector2(26, 330),
                            Size = new Vector2(2, 330),
                            Colour = new Color4(
                                HomeControlColours.Navy.R,
                                HomeControlColours.Navy.G,
                                HomeControlColours.Navy.B,
                                0.68f),
                        },
                        new Box
                        {
                            Position = new Vector2(26, 330),
                            Size = new Vector2(16, 2),
                            Colour = HomeControlColours.Navy,
                        },
                        new HomeConnectorPlus
                        {
                            Position = new Vector2(27, 544),
                            Scale = new Vector2(0.78f),
                        },
                        new HomeDotField
                        {
                            Position = new Vector2(20, 708),
                            Size = new Vector2(64, 44),
                            Colour = new Color4(
                                HomeControlColours.Cyan.R,
                                HomeControlColours.Cyan.G,
                                HomeControlColours.Cyan.B,
                                0.35f),
                        },
                        new HomeHazardStripes(
                            410,
                            new Color4(
                                HomeControlColours.Navy.R,
                                HomeControlColours.Navy.G,
                                HomeControlColours.Navy.B,
                                0.38f))
                        {
                            Position = new Vector2(0, 892),
                        },
                    },
                },
                rightDecorationLayout = new Container
                {
                    Size = new Vector2(designedWidth, designedHeight),
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Position = new Vector2(1502, 74),
                            Size = new Vector2(15),
                            Icon = FontAwesome.Solid.Plus,
                            Colour = HomeControlColours.Pink,
                        },
                        new SpriteIcon
                        {
                            Position = new Vector2(1452, 134),
                            Size = new Vector2(12),
                            Icon = FontAwesome.Solid.Plus,
                            Colour = HomeControlColours.Cyan,
                        },
                        new SpriteIcon
                        {
                            Position = new Vector2(1571, 399),
                            Size = new Vector2(11),
                            Icon = FontAwesome.Solid.Plus,
                            Colour = HomeControlColours.Yellow,
                        },
                        new HomeDotField
                        {
                            Position = new Vector2(1432, 55),
                            Size = new Vector2(110, 42),
                            Colour = new Color4(
                                HomeControlColours.Cyan.R,
                                HomeControlColours.Cyan.G,
                                HomeControlColours.Cyan.B,
                                0.3f),
                        },
                        new HomeDotField
                        {
                            Position = new Vector2(1430, 548),
                            Size = new Vector2(108, 58),
                            Colour = new Color4(
                                HomeControlColours.Cyan.R,
                                HomeControlColours.Cyan.G,
                                HomeControlColours.Cyan.B,
                                0.24f),
                        },
                        createResultPulseLine(),
                    },
                },
            },
        };

    private static Drawable createResultPulseLine() =>
        new Container
        {
            Position = new Vector2(1436, 872),
            Size = new Vector2(124, 22),
            Children = new Drawable[]
            {
                new Circle
                {
                    Position = new Vector2(0, 8),
                    Size = new Vector2(7),
                    Colour = HomeControlColours.Pink,
                },
                new Box
                {
                    Position = new Vector2(12, 10),
                    Size = new Vector2(48, 2),
                    Colour = HomeControlColours.Pink,
                },
                new Box
                {
                    Position = new Vector2(58, 6),
                    Size = new Vector2(14, 2),
                    Rotation = -48,
                    Colour = HomeControlColours.Pink,
                },
                new Box
                {
                    Position = new Vector2(68, 6),
                    Size = new Vector2(16, 2),
                    Rotation = 62,
                    Colour = HomeControlColours.Pink,
                },
                new Box
                {
                    Position = new Vector2(80, 10),
                    Size = new Vector2(44, 2),
                    Colour = HomeControlColours.Pink,
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
                    RelativeSizeAxes = Axes.Both,
                    Colour = HomeControlColours.Ivory,
                },
                new Box
                {
                    Position = new Vector2(500, 494),
                    Size = new Vector2(1100, 406),
                    Colour = new Color4(
                        HomeControlColours.PaleCyan.R,
                        HomeControlColours.PaleCyan.G,
                        HomeControlColours.PaleCyan.B,
                        0.1f),
                },
                new Box
                {
                    Position = new Vector2(780, -129),
                    Size = new Vector2(220, 1400),
                    Rotation = 36,
                    Colour = HomeControlColours.Cyan,
                },
                new Box
                {
                    Position = new Vector2(780, -129),
                    Size = new Vector2(64, 1400),
                    Rotation = 36,
                    Colour = HomeControlColours.PaleCyan,
                },
            },
        };

    private partial class ResultActionButton : ClickableContainer
    {
        private readonly Box background;
        private readonly Color4 restColour;
        private readonly Color4 hoverColour;

        public ResultActionButton(
            LocalisableString label,
            string key,
            IconUsage icon,
            Action action,
            float width,
            bool primary)
        {
            Action = action;
            _ = key;
            Size = new Vector2(width, 98);
            restColour = primary
                ? HomeControlColours.Navy
                : Color4.White;
            hoverColour = primary
                ? new Color4(0.045f, 0.13f, 0.66f, 1f)
                : HomeControlColours.PaleCyan;

            InternalChildren = new Drawable[]
            {
                new Container
                {
                    Position = new Vector2(0, 7),
                    Size = new Vector2(width, 91),
                    Masking = true,
                    CornerRadius = 8,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(0.015f, 0.045f, 0.28f, 0.3f),
                    },
                },
                new Container
                {
                    Size = new Vector2(width, 91),
                    Masking = true,
                    CornerRadius = 8,
                    BorderThickness = 2,
                    BorderColour = HomeControlColours.Navy,
                    Children = new Drawable[]
                    {
                        background = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = restColour,
                        },
                        new HomeDotField
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Position = new Vector2(-25, 0),
                            Size = new Vector2(92, 50),
                            Alpha = primary ? 0.18f : 0.09f,
                            Colour = primary
                                ? HomeControlColours.Cyan
                                : HomeControlColours.Navy,
                        },
                    },
                },
                new Container
                {
                    Position = new Vector2(
                        14,
                        primary ? 12 : 20),
                    Size = new Vector2(primary ? 66 : 54),
                    CornerRadius = 7,
                    Masking = true,
                    BorderThickness = primary ? 0 : 1.5f,
                    BorderColour = HomeControlColours.Navy,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = primary
                                ? Color4.White
                                : HomeControlColours.PaleCyan,
                        },
                        new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(primary ? 31 : 26),
                            Icon = icon,
                            Colour = HomeControlColours.Navy,
                        },
                    },
                },
                new SpriteText
                {
                    Position = new Vector2(
                        primary ? 98 : 78,
                        primary ? 18 : 33),
                    Width = width - 126,
                    Truncate = true,
                    Text = label,
                    Font = HomeTypography.Display(primary ? 46 : 18),
                    Colour = primary
                        ? Color4.White
                        : HomeControlColours.Navy,
                },
                new SpriteIcon
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Position = new Vector2(-18, 0),
                    Size = new Vector2(14, 22),
                    Icon = FontAwesome.Solid.ChevronRight,
                    Colour = primary
                        ? HomeControlColours.Yellow
                        : HomeControlColours.Pink,
                },
                new Box
                {
                    Position = new Vector2(width - 11, -5),
                    Size = new Vector2(10),
                    Rotation = 45,
                    Colour = HomeControlColours.Yellow,
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeColour(hoverColour, 120);
            this.ScaleTo(1.006f, 120, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeColour(restColour, 140);
            this.ScaleTo(1, 140, Easing.OutQuint);
        }
    }
}
