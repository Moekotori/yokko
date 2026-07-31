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
    private const float designedWidth = 1280;
    private const float designedHeight = 720;

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

        string rank = result.Rank.ToDisplayLabel();

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
                        Colour = HomeControlColours.Cyan,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Width = 0.5f,
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
        Texture mascotTexture = textures.Get("yokko")
                                        .Crop(new RectangleF(
                                            80,
                                            1840,
                                            1200,
                                            1360));
        mascot.Texture = mascotTexture;
        brandHost.Child = new HomeBrandLockup(
            textures.Get("home-logo-light"),
            HomeControlColours.Navy,
            HomeControlColours.Yellow)
        {
            Scale = new Vector2(0.6f),
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
                    Position = new Vector2(60, 42),
                    Size = new Vector2(310, 105),
                },
                new SpriteText
                {
                    Position = new Vector2(78, 174),
                    Text = YokkoStrings.Get("gameplay.result.title"),
                    Font = HomeTypography.Display(34),
                    Colour = HomeControlColours.Navy,
                },
                new Box
                {
                    Position = new Vector2(240, 191),
                    Size = new Vector2(58, 2),
                    Colour = HomeControlColours.Navy,
                },
                new HomeMicroLine
                {
                    Position = new Vector2(302, 189),
                    Width = 44,
                },
                new HomeCrosshairMark
                {
                    Position = new Vector2(303, 177),
                    Colour = HomeControlColours.Navy,
                    Scale = new Vector2(0.72f),
                },
                new SpriteText
                {
                    Position = new Vector2(79, 213),
                    Width = 505,
                    Truncate = true,
                    Text = $"{beatmap.Title} [{beatmap.DifficultyName}]",
                    Font = HomeTypography.Display(19),
                    Colour = HomeControlColours.Navy,
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
    {
        Drawable modRail = createModChipRail();

        return new Container
        {
            Position = new Vector2(74, 242),
            Size = new Vector2(522, 157),
            Children = new Drawable[]
            {
                new Container
                {
                    Position = new Vector2(0, 5),
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 8,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(0.015f, 0.045f, 0.28f, 0.34f),
                    },
                },
                new Container
                {
                    Size = new Vector2(522, 153),
                    Masking = true,
                    CornerRadius = 8,
                    BorderThickness = 2,
                    BorderColour = HomeControlColours.Cyan,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = HomeControlColours.Navy,
                        },
                        new HomeDotField
                        {
                            Position = new Vector2(392, 18),
                            Size = new Vector2(112, 72),
                            Colour = new Color4(0.35f, 0.78f, 1f, 0.16f),
                        },
                        new Container
                        {
                            Position = new Vector2(34, 22),
                            Size = new Vector2(110, 114),
                            Masking = true,
                            CornerRadius = 8,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = HomeControlColours.PaleCyan,
                                },
                                new SpriteText
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Text = rank,
                                    Font = HomeTypography.Hero(82),
                                    Colour = HomeControlColours.Navy,
                                },
                            },
                        },
                        new SpriteText
                        {
                            Position = new Vector2(168, 24),
                            Text = isNewBest
                                ? YokkoStrings.Get("gameplay.result.new_best")
                                : "SCORE",
                            Font = HomeTypography.Display(14),
                            Spacing = new Vector2(2.2f, 0),
                            Colour = isNewBest
                                ? HomeControlColours.Pink
                                : HomeControlColours.Cyan,
                        },
                        new SpriteText
                        {
                            Position = new Vector2(168, 48),
                            Text = $"{result.Score:0000000}",
                            Font = HomeTypography.Hero(53),
                            Colour = Color4.White,
                        },
                        new SpriteText
                        {
                            Position = new Vector2(168, 117),
                            Text = "MODS",
                            Font = HomeTypography.Display(11),
                            Spacing = new Vector2(1.5f, 0),
                            Colour = HomeControlColours.Cyan,
                        },
                        modRail,
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Position = new Vector2(-22, 0),
                            Size = new Vector2(22, 30),
                            Icon = FontAwesome.Solid.ChevronRight,
                            Colour = HomeControlColours.Yellow,
                        },
                        new Box
                        {
                            Position = new Vector2(12, 12),
                            Size = new Vector2(16, 3),
                            Colour = HomeControlColours.Cyan,
                        },
                        new Box
                        {
                            Position = new Vector2(12, 12),
                            Size = new Vector2(3, 16),
                            Colour = HomeControlColours.Cyan,
                        },
                    },
                },
                new Box
                {
                    Position = new Vector2(508, -5),
                    Size = new Vector2(11),
                    Rotation = 45,
                    Colour = HomeControlColours.Yellow,
                },
            },
        };
    }

    private Drawable createSummaryRail(ManiaScoreResult result) =>
        new Container
        {
            Position = new Vector2(74, 413),
            Size = new Vector2(522, 69),
            Masking = true,
            CornerRadius = 7,
            BorderThickness = 1.4f,
            BorderColour = HomeControlColours.Navy,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
                createMetricCell(
                    FontAwesome.Solid.Bullseye,
                    "ACCURACY",
                    $"{result.Accuracy * 100:0.00}%",
                    0),
                new Box
                {
                    Position = new Vector2(273, 12),
                    Size = new Vector2(1, 44),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.5f),
                },
                createMetricCell(
                    FontAwesome.Solid.Trophy,
                    YokkoStrings.Get("gameplay.result.max_combo"),
                    result.MaxCombo.ToString(),
                    278),
            },
        };

    private static Drawable createMetricCell(
        IconUsage icon,
        LocalisableString label,
        string value,
        float x) =>
        new Container
        {
            Position = new Vector2(x, 0),
            Size = new Vector2(244, 69),
            Children = new Drawable[]
            {
                new Container
                {
                    Position = new Vector2(15, 10),
                    Size = new Vector2(52, 49),
                    Masking = true,
                    CornerRadius = 6,
                    BorderThickness = 1,
                    BorderColour = HomeControlColours.Navy,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = HomeControlColours.PaleCyan,
                        },
                        new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(25),
                            Icon = icon,
                            Colour = HomeControlColours.Navy,
                        },
                    },
                },
                new SpriteText
                {
                    Position = new Vector2(84, 12),
                    Text = label,
                    Font = HomeTypography.Display(11),
                    Spacing = new Vector2(1.2f, 0),
                    Colour = HomeControlColours.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(84, 30),
                    Text = value,
                    Font = HomeTypography.Display(25),
                    Colour = HomeControlColours.Navy,
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
            Position = new Vector2(74, 491),
            Size = new Vector2(522, 47),
            Children = new Drawable[]
            {
                flow,
                new SpriteIcon
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -1,
                    Size = new Vector2(10),
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
            Width = 522f / 6,
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
                    Y = 2,
                    Text = label,
                    Font = HomeTypography.Display(9),
                    Colour = colour,
                },
                new SpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = 23,
                    Text = value.ToString(),
                    Font = HomeTypography.Display(18),
                    Colour = HomeControlColours.Navy,
                },
            },
        };

    private Drawable createActionRow() =>
        new Container
        {
            Position = new Vector2(72, 542),
            Size = new Vector2(526, 124),
            Children = new Drawable[]
            {
                new ResultActionButton(
                    YokkoStrings.Get("gameplay.result.retry"),
                    "R",
                    FontAwesome.Solid.Play,
                    retry,
                    522,
                    true),
                new ResultActionButton(
                    YokkoStrings.Get("gameplay.result.watch_replay"),
                    "V",
                    FontAwesome.Solid.Redo,
                    watchReplay,
                    252,
                    false)
                {
                    Y = 67,
                },
                new ResultActionButton(
                    YokkoStrings.Get("gameplay.result.return"),
                    "ESC",
                    FontAwesome.Solid.Music,
                    returnToSongSelect,
                    252,
                    false)
                {
                    Position = new Vector2(270, 67),
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
        const float maxWidth = 268;
        const float spacing = 7;
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
            Position = new Vector2(219, 109),
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(spacing, 0),
        };

        foreach ((string label, float width) in visible)
            flow.Add(createModChip(label, width));

        return flow;
    }

    private static float calculateModChipWidth(string label) =>
        Math.Clamp(24 + label.Length * 7, 45, 104);

    private static Drawable createModChip(string label, float width) =>
        new Container
        {
            Size = new Vector2(width, 27),
            Masking = true,
            CornerRadius = 4,
            BorderThickness = 1.2f,
            BorderColour = HomeControlColours.Yellow,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.02f, 0.07f, 0.42f, 0.94f),
                },
                new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = label,
                    Font = HomeTypography.Display(10),
                    Colour = Color4.White,
                },
            },
        };

    private static Drawable createTickerStrip() =>
        new Container
        {
            RelativeSizeAxes = Axes.X,
            Height = 20,
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
                    X = 14,
                    Text =
                        "4K MANIA   +   CHART LAB // FEEL THE BEAT   +   "
                        + "RHYTHM CHART STUDIO // EST. 2025 // VOL.01   "
                        + "YOKKO RHYTHM STATION // 4K MANIA   +   "
                        + "CHART LAB // FEEL THE BEAT   +   "
                        + "RHYTHM CHART STUDIO // EST. 2025 // VOL.01",
                    Font = HomeTypography.Display(8),
                    Spacing = new Vector2(1.7f, 0),
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
                new SpriteText
                {
                    Position = new Vector2(840, 70),
                    Text = "RESULT SIGNAL / CLEAR",
                    Font = HomeTypography.Display(10),
                    Spacing = new Vector2(2, 0),
                    Colour = Color4.White,
                },
                new HomeDotField
                {
                    Position = new Vector2(838, 93),
                    Size = new Vector2(142, 38),
                    Colour = new Color4(1f, 1f, 1f, 0.44f),
                },
                new Circle
                {
                    Position = new Vector2(833, 146),
                    Size = new Vector2(7),
                    Colour = HomeControlColours.Pink,
                },
                new SpriteText
                {
                    Position = new Vector2(848, 140),
                    Text = "SYNC // 04.00.128",
                    Font = HomeTypography.Display(9),
                    Spacing = new Vector2(1, 0),
                    Colour = Color4.White,
                },
                new HomeMicroLine
                {
                    Position = new Vector2(840, 166),
                    Width = 102,
                    Colour = Color4.White,
                },
                new HomeCrosshairMark
                {
                    Position = new Vector2(718, 524),
                    Colour = HomeControlColours.Navy,
                    Alpha = 0.85f,
                },
                new HomeBarcode("NO.004-KEY")
                {
                    Position = new Vector2(838, 190),
                    Colour = HomeControlColours.Navy,
                },
                mascot = new Sprite
                {
                    Origin = Anchor.Centre,
                    Position = new Vector2(1000, 527),
                    Size = new Vector2(615, 697),
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.Centre,
                    Position = new Vector2(1258, 340),
                    Rotation = 90,
                    Text = "4K MANIA  ·  CHART LAB",
                    Font = HomeTypography.Display(9),
                    Spacing = new Vector2(1.7f, 0),
                    Colour = Color4.White,
                },
                new HomeDotField
                {
                    Position = new Vector2(1189, 221),
                    Size = new Vector2(63, 43),
                    Colour = new Color4(1f, 1f, 1f, 0.34f),
                },
                new SpriteIcon
                {
                    Position = new Vector2(1222, 598),
                    Size = new Vector2(11),
                    Icon = FontAwesome.Solid.Plus,
                    Colour = HomeControlColours.Pink,
                },
                new Circle
                {
                    Position = new Vector2(1241, 435),
                    Size = new Vector2(14),
                    BorderThickness = 2,
                    BorderColour = HomeControlColours.Yellow,
                    Masking = true,
                    Colour = HomeControlColours.Navy,
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
                            Position = new Vector2(45, 51),
                            Size = new Vector2(10),
                            Icon = FontAwesome.Solid.Plus,
                            Colour = HomeControlColours.Pink,
                        },
                        new SpriteIcon
                        {
                            Position = new Vector2(82, 53),
                            Size = new Vector2(9),
                            Icon = FontAwesome.Solid.Plus,
                            Colour = HomeControlColours.Cyan,
                        },
                        new Box
                        {
                            Position = new Vector2(24, 188),
                            Size = new Vector2(2, 252),
                            Colour = new Color4(
                                HomeControlColours.Navy.R,
                                HomeControlColours.Navy.G,
                                HomeControlColours.Navy.B,
                                0.68f),
                        },
                        new Box
                        {
                            Position = new Vector2(24, 188),
                            Size = new Vector2(14, 2),
                            Colour = HomeControlColours.Navy,
                        },
                        new HomeConnectorPlus
                        {
                            Position = new Vector2(25, 383),
                            Scale = new Vector2(0.78f),
                        },
                        new SpriteText
                        {
                            Position = new Vector2(17, 436),
                            Rotation = -90,
                            Text = "RHYTHM CHART STUDIO  ·  VOL.01",
                            Font = HomeTypography.Display(8),
                            Spacing = new Vector2(2, 0),
                            Colour = new Color4(
                                HomeControlColours.Navy.R,
                                HomeControlColours.Navy.G,
                                HomeControlColours.Navy.B,
                                0.35f),
                        },
                        new HomeDotField
                        {
                            Position = new Vector2(4, 499),
                            Size = new Vector2(55, 36),
                            Colour = new Color4(
                                HomeControlColours.Cyan.R,
                                HomeControlColours.Cyan.G,
                                HomeControlColours.Cyan.B,
                                0.35f),
                        },
                        new HomeHazardStripes(
                            655,
                            new Color4(
                                HomeControlColours.Navy.R,
                                HomeControlColours.Navy.G,
                                HomeControlColours.Navy.B,
                                0.38f))
                        {
                            Position = new Vector2(0, 712),
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
                            Position = new Vector2(1102, 105),
                            Size = new Vector2(12),
                            Icon = FontAwesome.Solid.Plus,
                            Colour = HomeControlColours.Pink,
                        },
                        new SpriteIcon
                        {
                            Position = new Vector2(1195, 151),
                            Size = new Vector2(11),
                            Icon = FontAwesome.Solid.Plus,
                            Colour = Color4.White,
                        },
                        new SpriteIcon
                        {
                            Position = new Vector2(1050, 164),
                            Size = new Vector2(10),
                            Icon = FontAwesome.Solid.Plus,
                            Colour = HomeControlColours.Yellow,
                        },
                    },
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
                    Width = 650,
                    Colour = HomeControlColours.Ivory,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Position = new Vector2(604, -45),
                    Width = 205,
                    Height = 1.18f,
                    Rotation = 12,
                    Colour = HomeControlColours.Ivory,
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
            Size = new Vector2(width, 57);
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
                    Position = new Vector2(0, 4),
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 6,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(0.015f, 0.045f, 0.28f, 0.3f),
                    },
                },
                new Container
                {
                    Size = new Vector2(width, 53),
                    Masking = true,
                    CornerRadius = 6,
                    BorderThickness = 1.5f,
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
                            Position = new Vector2(-18, 0),
                            Size = new Vector2(68, 36),
                            Alpha = primary ? 0.16f : 0.08f,
                            Colour = primary
                                ? HomeControlColours.Cyan
                                : HomeControlColours.Navy,
                        },
                    },
                },
                new Container
                {
                    Position = new Vector2(15, 8),
                    Size = new Vector2(39),
                    CornerRadius = 5,
                    Masking = true,
                    BorderThickness = primary ? 0 : 1,
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
                            Size = new Vector2(19),
                            Icon = icon,
                            Colour = HomeControlColours.Navy,
                        },
                    },
                },
                new SpriteText
                {
                    Position = new Vector2(76, 14),
                    Width = primary ? width - 180 : width - 145,
                    Truncate = true,
                    Text = label,
                    Font = HomeTypography.Display(primary ? 16 : 13),
                    Colour = primary
                        ? Color4.White
                        : HomeControlColours.Navy,
                },
                new Container
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Position = new Vector2(-48, -2),
                    Size = new Vector2(key.Length > 1 ? 38 : 26, 24),
                    Masking = true,
                    CornerRadius = 4,
                    BorderThickness = 1,
                    BorderColour = new Color4(
                        primary ? 1f : HomeControlColours.Navy.R,
                        primary ? 1f : HomeControlColours.Navy.G,
                        primary ? 1f : HomeControlColours.Navy.B,
                        primary ? 0.76f : 0.42f),
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = primary
                                ? new Color4(0.02f, 0.06f, 0.4f, 1f)
                                : HomeControlColours.Ivory,
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = key,
                            Font = HomeTypography.Display(10),
                            Colour = primary
                                ? Color4.White
                                : HomeControlColours.Navy,
                        },
                    },
                },
                new SpriteIcon
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Position = new Vector2(-14, -2),
                    Size = new Vector2(12, 18),
                    Icon = FontAwesome.Solid.ChevronRight,
                    Colour = HomeControlColours.Pink,
                },
                new Box
                {
                    Position = new Vector2(width - 10, -4),
                    Size = new Vector2(9),
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
