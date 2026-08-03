using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Scoring;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

/// <summary>
/// Yokko's relaxed score-ribbon presentation. The hierarchy follows the
/// proven rhythm-game pattern used by osu!lazer while retaining original
/// Yokko surfaces, typography, spacing and artwork.
/// </summary>
internal partial class SongSelectRankingPanel : ClickableContainer
{
    private const float panel_width = 850;
    private const float panel_height = 508;
    private const float compact_panel_height = 166;
    private const float rows_top = 44;
    private const float content_width = 850;
    private const float content_height = 450;
    private const float compact_content_height = 112;
    private const float row_width = 826;
    private const float row_height = 70;
    private const float row_spacing = 0;
    private const float score_column_right = 476;
    private const float accuracy_column_right = 586;
    private const float combo_column_right = 696;

    private readonly Container content;
    private readonly Container paper;
    private readonly SpriteText playerCount;
    private readonly SongSelectEntry entry;
    private readonly TextureStore textures;
    private readonly Action<SongSelectScore> scoreSelected;
    private Container activeContentLayer;
    private int contentTransitionVersion;

    public SongSelectScoreView View => SongSelectScoreView.GlobalRanking;
    internal Vector2 ContentSize => content.Size;
    internal Vector2 PaperPosition => paper.Position;
    internal Vector2 PaperSize => paper.Size;
    internal int ContentLayerCount => content.Children.Count();
    internal int ContentTransitionVersion => contentTransitionVersion;
    internal bool EmptyStateVisible => activeContentLayer?
        .Children.Any(child => child is SongSelectRankingEmptyState) == true;
    internal static Vector3 MetricColumnRightEdges => new(
        score_column_right,
        accuracy_column_right,
        combo_column_right);

    public SongSelectRankingPanel(
        SongSelectEntry entry,
        TextureStore textures,
        Action<SongSelectScore> scoreSelected)
    {
        this.entry = entry;
        this.textures = textures;
        this.scoreSelected = scoreSelected;
        bool compact = entry.Ranking.Count == 0;
        float activeContentHeight = compact
            ? compact_content_height
            : content_height;
        Size = new Vector2(
            panel_width,
            compact ? compact_panel_height : panel_height);

        Container paperSurface = SongSelectSurface.CreateCard(
            out _,
            SongSelectSurface.Ivory(0.94f),
            SongSelectSurface.Border(0.20f),
            14,
            1);

        InternalChildren =
        [
            paper = new Container
            {
                Size = new Vector2(
                    panel_width,
                    rows_top + activeContentHeight),
                Children =
                [
                    SongSelectSurface.CreateShadow(14, 0.09f, 2),
                    paperSurface,
                ],
            },
            new Box
            {
                Position = new Vector2(16, 42),
                Size = new Vector2(panel_width - 32, 1),
                Colour = new Color4(
                    SongSelectTheme.Cyan.R,
                    SongSelectTheme.Cyan.G,
                    SongSelectTheme.Cyan.B,
                    0.22f),
            },
            createTab(
                "RANKING",
                8,
                FontAwesome.Solid.Trophy),
            playerCount = new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-214, 14),
                Text = "0 PLAYS",
                Font = HomeTypography.Display(9),
                Colour = new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.70f),
            },
            content = new Container
            {
                Position = new Vector2(0, rows_top),
                Size = new Vector2(
                    content_width,
                    activeContentHeight),
                Masking = true,
            },
        ];

        applyView(false);
    }

    public void SetView(
        SongSelectScoreView newView,
        TextureStore ignored = null)
        => applyView(activeContentLayer != null);

    private void applyView(bool animate)
    {
        rebuildRows(
            entry.Ranking,
            animate,
            1);
    }

    private void rebuildRows(
        System.Collections.Generic.IReadOnlyList<SongSelectScore> scores,
        bool animate,
        float direction)
    {
        playerCount.Text =
            $"{scores.Count} {(scores.Count == 1 ? "PLAY" : "PLAYS")}";
        var nextLayer = new Container
        {
            RelativeSizeAxes = Axes.Both,
        };
        if (scores.Count == 0)
        {
            nextLayer.Add(new SongSelectRankingEmptyState(
                false));
            presentContent(nextLayer, animate, direction);
            return;
        }

        var flow = new FillFlowContainer
        {
            Position = new Vector2(12, 10),
            Width = row_width,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, row_spacing),
        };
        foreach (SongSelectScore score in scores.Take(7))
            flow.Add(createRow(score));
        nextLayer.Add(flow);
        presentContent(nextLayer, animate, direction);
    }

    private void presentContent(
        Container next,
        bool animate,
        float direction)
    {
        if (!animate || activeContentLayer == null)
        {
            content.Clear();
            next.Alpha = 1;
            next.X = 0;
            content.Add(activeContentLayer = next);
            return;
        }

        Container outgoing = activeContentLayer;
        contentTransitionVersion++;
        outgoing.ClearTransforms();
        outgoing.FadeOut(80, Easing.OutQuint);
        outgoing.MoveToX(-direction * 6, 130, Easing.OutQuint);

        next.Alpha = 0;
        next.X = direction * 8;
        content.Add(activeContentLayer = next);
        next.FadeIn(130, Easing.OutQuint);
        next.MoveToX(0, 160, Easing.OutQuint);

        Scheduler.AddDelayed(() =>
        {
            if (outgoing.Parent == content)
                content.Remove(outgoing, true);
        }, 180);
    }

    private Drawable createRow(SongSelectScore score)
    {
        bool current = score.IsCurrentPlayer;
        Color4 accent = rankAccent(score.Rank, current);
        Color4 grade = gradeColour(score.Grade);
        Color4 primary = SongSelectTheme.Navy;
        Color4 secondary = new Color4(
            SongSelectTheme.Navy.R,
            SongSelectTheme.Navy.G,
            SongSelectTheme.Navy.B,
            0.68f);
        Texture avatar = textures.Get("SongSelect/Ui/yokko-avatar-256");

        return new ClickableContainer
        {
            Size = new Vector2(row_width, row_height),
            Action = () => scoreSelected?.Invoke(score),
            Masking = current,
            CornerRadius = 8,
            BorderThickness = current ? 1.5f : 0,
            BorderColour = new Color4(
                accent.R,
                accent.G,
                accent.B,
                current ? 0.98f : 0),
            Children =
            [
                SongSelectSurface.CreateShadow(
                    8,
                    current ? 0.10f : 0,
                    2),
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = current
                        ? SongSelectSurface.Ivory(0.92f)
                        : Color4.Transparent,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = current ? 4 : 0,
                    Colour = SongSelectTheme.Pink,
                },
                new Box
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Width = row_width - 30,
                    Height = 1,
                    Colour = new Color4(
                        SongSelectTheme.Navy.R,
                        SongSelectTheme.Navy.G,
                        SongSelectTheme.Navy.B,
                        current ? 0 : 0.12f),
                },
                new SpriteIcon
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 13,
                    Size = new Vector2(11),
                    Icon = FontAwesome.Solid.Play,
                    Colour = SongSelectTheme.Pink,
                    Alpha = current ? 1 : 0,
                },
                new SpriteIcon
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 12,
                    Size = new Vector2(13),
                    Icon = FontAwesome.Solid.Crown,
                    Colour = SongSelectTheme.Yellow,
                    Alpha = !current && score.Rank == 1 ? 1 : 0,
                },
                text(
                    current ? $"#{score.Rank}" : score.Rank.ToString(),
                    current || score.Rank == 1 ? 30 : 20,
                    25,
                    34,
                    20,
                    current ? SongSelectTheme.Pink : accent),
                new Container
                {
                    Position = new Vector2(72, 7),
                    Size = new Vector2(56),
                    Masking = true,
                    CornerRadius = 28,
                    BorderThickness = current ? 2 : 1.2f,
                    BorderColour = current ? SongSelectTheme.Pink : accent,
                    Child = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Texture = avatar,
                        FillMode = FillMode.Fill,
                    },
                },
                text(
                    score.PlayerName,
                    144,
                    12,
                    188,
                    17,
                    current ? SongSelectTheme.Pink : primary),
                text(
                    score.Mods.Count == 0
                        ? "NM"
                        : string.Join("   ", score.Mods),
                    144,
                    39,
                    188,
                    10,
                    secondary,
                    false),
                numericText(
                    $"{score.Score:N0}",
                    score_column_right,
                    21,
                    18,
                    primary),
                numericText(
                    $"{score.Accuracy:P2}",
                    accuracy_column_right,
                    24,
                    13,
                    secondary),
                numericText(
                    $"{score.MaxCombo:N0}×",
                    combo_column_right,
                    24,
                    13,
                    secondary),
                new SongSelectGradeBadge(score.Grade, grade, current)
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -62,
                },
                new SpriteIcon
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -20,
                    Size = new Vector2(13),
                    Icon = FontAwesome.Solid.ChevronRight,
                    Colour = score.ReplayPath == null
                        ? new Color4(
                            SongSelectTheme.Navy.R,
                            SongSelectTheme.Navy.G,
                            SongSelectTheme.Navy.B,
                            0.34f)
                        : SongSelectTheme.Cyan,
                },
            ],
        };
    }

    private static Drawable createTab(
        string label,
        float x,
        IconUsage icon)
    {
        Box tabUnderline = new Box
        {
            Anchor = Anchor.BottomCentre,
            Origin = Anchor.BottomCentre,
            Y = -1,
            Width = 132,
            Height = 3,
            Colour = SongSelectTheme.Pink,
        };
        SpriteText tabLabel = new SpriteText
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            X = 31,
            Text = label,
            Font = HomeTypography.Display(11),
        };
        return new ClickableContainer
        {
            Position = new Vector2(x, 0),
            Size = new Vector2(142, 40),
            Children =
            [
                new SpriteIcon
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 9,
                    Size = new Vector2(13),
                    Icon = icon,
                    Colour = SongSelectTheme.Pink,
                },
                tabLabel,
                tabUnderline,
            ],
        };
    }

    private static SpriteText text(
        string value,
        float x,
        float y,
        float width,
        float size,
        Color4 colour,
        bool strong = true) => new()
        {
            Position = new Vector2(x, y),
            Width = width,
            Truncate = true,
            Text = value,
            Font = strong
            ? HomeTypography.Display(size)
            : HomeTypography.Body(size),
            Colour = colour,
        };

    private static SpriteText numericText(
        string value,
        float right,
        float y,
        float size,
        Color4 colour) => new()
        {
            Origin = Anchor.TopRight,
            Position = new Vector2(right, y),
            Text = value,
            Font = HomeTypography.Display(size),
            Colour = colour,
        };

    private static Color4 rankAccent(int rank, bool current) =>
        current
            ? SongSelectTheme.Pink
            : rank switch
            {
                1 => SongSelectTheme.Yellow,
                2 => SongSelectTheme.Cyan,
                3 => SongSelectTheme.Pink,
                _ => SongSelectTheme.Cyan,
            };

    private static Color4 gradeColour(ScoreRank rank) =>
        rank switch
        {
            ScoreRank.X or ScoreRank.XH => SongSelectTheme.PaleCyan,
            ScoreRank.S or ScoreRank.SH => SongSelectTheme.Cyan,
            ScoreRank.A => new Color4(0.62f, 0.94f, 0.25f, 1f),
            ScoreRank.B => SongSelectTheme.Yellow,
            _ => SongSelectTheme.Pink,
        };
}

internal partial class SongSelectRankingEmptyState : CompositeDrawable
{
    internal bool PersonalHistory { get; }

    internal SongSelectRankingEmptyState(bool personalHistory)
    {
        PersonalHistory = personalHistory;
        RelativeSizeAxes = Axes.Both;
        InternalChild = new Container
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Y = -2,
            Size = new Vector2(610, 78),
            Masking = true,
            CornerRadius = 11,
            BorderThickness = 1,
            BorderColour = new Color4(
                SongSelectTheme.Cyan.R,
                SongSelectTheme.Cyan.G,
                SongSelectTheme.Cyan.B,
                0.30f),
            Children =
            [
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        SongSelectTheme.PaleCyan.R,
                        SongSelectTheme.PaleCyan.G,
                        SongSelectTheme.PaleCyan.B,
                        0.24f),
                },
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 3,
                    Colour = SongSelectTheme.Cyan,
                    Alpha = 0.74f,
                },
                new Container
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 18,
                    Size = new Vector2(46),
                    Masking = true,
                    CornerRadius = 23,
                    BorderThickness = 1.5f,
                    BorderColour = new Color4(
                        SongSelectTheme.Cyan.R,
                        SongSelectTheme.Cyan.G,
                        SongSelectTheme.Cyan.B,
                        0.52f),
                    Children =
                    [
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = SongSelectSurface.Ivory(0.82f),
                        },
                        new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(19),
                            Icon = personalHistory
                                ? FontAwesome.Solid.Archive
                                : FontAwesome.Solid.Users,
                            Colour = personalHistory
                                ? SongSelectTheme.Pink
                                : SongSelectTheme.Cyan,
                        },
                    ],
                },
                new SpriteText
                {
                    Position = new Vector2(82, 18),
                    Text = personalHistory
                        ? "NO LOCAL PLAYS YET"
                        : "NO RANKING DATA",
                    Font = HomeTypography.Display(15),
                    Colour = SongSelectTheme.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(82, 43),
                    Text = personalHistory
                        ? "YOUR RESULTS AND REPLAYS WILL LIVE HERE"
                        : "COMPLETE THIS CHART TO CREATE THE FIRST RESULT",
                    Font = HomeTypography.Body(11),
                    Colour = new Color4(
                        SongSelectTheme.Navy.R,
                        SongSelectTheme.Navy.G,
                        SongSelectTheme.Navy.B,
                        0.64f),
                },
            ],
        };
    }
}

internal partial class SongSelectGradeBadge : CompositeDrawable
{
    internal ScoreRank Grade { get; }
    internal bool Highlighted { get; }

    internal SongSelectGradeBadge(
        ScoreRank grade,
        Color4 accent,
        bool highlighted)
    {
        Grade = grade;
        Highlighted = highlighted;
        Size = new Vector2(36, 32);
        InternalChildren =
        [
            new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Y = -1,
                Text = grade.ToDisplayLabel(),
                Font = HomeTypography.Display(17),
                Colour = accent,
            },
        ];
    }
}
