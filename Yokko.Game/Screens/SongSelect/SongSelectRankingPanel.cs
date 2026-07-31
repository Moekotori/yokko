using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
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
/// Dense score ribbons inspired by the information hierarchy used by
/// ppy/osu's MIT-licensed Song Select leaderboard. The implementation and
/// Yokko visual treatment are original; no osu! branding or resources are
/// reused.
/// </summary>
internal partial class SongSelectRankingPanel : ClickableContainer
{
    private const float panel_width = 760;
    private const float panel_height = 468;
    private const float rows_top = 62;
    private const float row_width = 736;
    private const float row_height = 58;
    private const float row_spacing = 6;

    private readonly Container content;
    private readonly Box globalTab;
    private readonly Box historyTab;
    private readonly SpriteText globalLabel;
    private readonly SpriteText historyLabel;
    private readonly SpriteText playerCount;
    private readonly SongSelectEntry entry;
    private readonly TextureStore textures;
    private readonly Action<SongSelectScoreView> viewChanged;
    private SongSelectScoreView view;

    public SongSelectScoreView View => view;
    internal Vector2 ContentSize => content.Size;

    public SongSelectRankingPanel(
        SongSelectEntry entry,
        TextureStore textures,
        Action<SongSelectScoreView> viewChanged)
    {
        this.entry = entry;
        this.textures = textures;
        this.viewChanged = viewChanged;
        Size = new Vector2(panel_width, panel_height);
        Action = () => setAndNotify(
            view == SongSelectScoreView.GlobalRanking
                ? SongSelectScoreView.Personal
                : SongSelectScoreView.GlobalRanking);

        Container panel = SongSelectSurface.CreateCard(
            out _,
            new Color4(
                SongSelectTheme.DeepNavy.R,
                SongSelectTheme.DeepNavy.G,
                SongSelectTheme.DeepNavy.B,
                0.80f),
            new Color4(
                SongSelectTheme.Cyan.R,
                SongSelectTheme.Cyan.G,
                SongSelectTheme.Cyan.B,
                0.42f),
            12,
            1.2f);

        InternalChildren =
        [
            SongSelectSurface.CreateShadow(12, 0.25f, 4),
            panel,
            new Box
            {
                Position = new Vector2(1),
                Size = new Vector2(panel_width - 2, 50),
                Colour = new Color4(
                    SongSelectTheme.SurfaceRaised.R,
                    SongSelectTheme.SurfaceRaised.G,
                    SongSelectTheme.SurfaceRaised.B,
                    0.86f),
            },
            new Box
            {
                Position = new Vector2(18, 50),
                Size = new Vector2(panel_width - 36, 1),
                Colour = new Color4(
                    SongSelectTheme.Cyan.R,
                    SongSelectTheme.Cyan.G,
                    SongSelectTheme.Cyan.B,
                    0.34f),
            },
            new SpriteText
            {
                Position = new Vector2(20, 14),
                Text = "RANKING",
                Font = HomeTypography.Display(17),
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Position = new Vector2(119, 17),
                Size = new Vector2(14),
                Icon = FontAwesome.Solid.Trophy,
                Colour = SongSelectTheme.Yellow,
            },
            createTab(
                "GLOBAL",
                164,
                out globalTab,
                out globalLabel,
                () => setAndNotify(SongSelectScoreView.GlobalRanking)),
            createTab(
                "MY HISTORY",
                258,
                out historyTab,
                out historyLabel,
                () => setAndNotify(SongSelectScoreView.Personal)),
            new Box
            {
                Position = new Vector2(390, 26),
                Size = new Vector2(222, 1),
                Colour = new Color4(
                    SongSelectTheme.Cyan.R,
                    SongSelectTheme.Cyan.G,
                    SongSelectTheme.Cyan.B,
                    0.40f),
            },
            playerCount = new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-20, 16),
                Text = "0 PLAYS",
                Font = HomeTypography.Display(9),
                Colour = SongSelectTheme.PaleCyan,
            },
            content = new Container
            {
                Position = new Vector2(12, rows_top),
                Size = new Vector2(
                    row_width,
                    row_height * 6 + row_spacing * 5),
                Masking = true,
            },
        ];

        SetView(SongSelectScoreView.GlobalRanking);
    }

    public void SetView(
        SongSelectScoreView newView,
        TextureStore ignored = null)
    {
        view = newView;
        bool global = view == SongSelectScoreView.GlobalRanking;
        globalTab.Colour = global
            ? SongSelectTheme.Cyan
            : new Color4(1, 1, 1, 0);
        historyTab.Colour = global
            ? new Color4(1, 1, 1, 0)
            : SongSelectTheme.Pink;
        globalLabel.Colour = global
            ? SongSelectTheme.DeepNavy
            : SongSelectTheme.PaleCyan;
        historyLabel.Colour = global
            ? SongSelectTheme.PaleCyan
            : Color4.White;
        rebuildRows(global ? entry.Ranking : entry.History);
    }

    private void setAndNotify(SongSelectScoreView newView)
    {
        SetView(newView);
        viewChanged(newView);
    }

    private void rebuildRows(
        System.Collections.Generic.IReadOnlyList<SongSelectScore> scores)
    {
        playerCount.Text =
            $"{scores.Count} {(scores.Count == 1 ? "PLAY" : "PLAYS")}";
        content.Clear();
        if (scores.Count == 0)
        {
            content.Add(new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 10,
                BorderThickness = 1,
                BorderColour = new Color4(
                    SongSelectTheme.Cyan.R,
                    SongSelectTheme.Cyan.G,
                    SongSelectTheme.Cyan.B,
                    0.24f),
                Children =
                [
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(
                            SongSelectTheme.Surface.R,
                            SongSelectTheme.Surface.G,
                            SongSelectTheme.Surface.B,
                            0.88f),
                    },
                    new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = view == SongSelectScoreView.Personal
                            ? "NO LOCAL PLAYS YET · PLAY TO START YOUR HISTORY"
                            : "NO RANKING DATA",
                        Font = HomeTypography.Display(12),
                        Colour = SongSelectTheme.PaleCyan,
                    },
                ],
            });
            return;
        }

        var flow = new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, row_spacing),
        };
        foreach (SongSelectScore score in scores.Take(6))
            flow.Add(createRow(score));
        content.Add(flow);
    }

    private Drawable createRow(SongSelectScore score)
    {
        Color4 accent = score.IsCurrentPlayer
            ? SongSelectTheme.Pink
            : score.Rank <= 3
                ? SongSelectTheme.Yellow
                : SongSelectTheme.Cyan;
        Color4 grade = gradeColour(score.Grade);
        Texture avatar = score.IsCurrentPlayer
            ? textures.Get("yokko")?.Crop(
                new RectangleF(270, 2200, 850, 850))
            : textures.Get(score.AvatarTexture);

        return new Container
        {
            Size = new Vector2(row_width, row_height),
            Masking = true,
            CornerRadius = 10,
            BorderThickness = score.IsCurrentPlayer ? 1.5f : 0.8f,
            BorderColour = new Color4(
                accent.R,
                accent.G,
                accent.B,
                score.IsCurrentPlayer ? 0.95f : 0.36f),
            Children =
            [
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        SongSelectTheme.Surface.R,
                        SongSelectTheme.Surface.G,
                        SongSelectTheme.Surface.B,
                        score.IsCurrentPlayer ? 0.96f : 0.89f),
                },
                new Box
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    RelativeSizeAxes = Axes.Y,
                    Width = 232,
                    Colour = ColourInfo.GradientHorizontal(
                        new Color4(accent.R, accent.G, accent.B, 0),
                        new Color4(accent.R, accent.G, accent.B, 0.58f)),
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 4,
                    Colour = accent,
                },
                text(
                    $"#{score.Rank}",
                    12,
                    19,
                    34,
                    13,
                    accent),
                new Container
                {
                    Position = new Vector2(48, 5),
                    Size = new Vector2(48),
                    Masking = true,
                    CornerRadius = 8,
                    BorderThickness = 1.25f,
                    BorderColour = accent,
                    Child = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Texture = avatar,
                        FillMode = FillMode.Fill,
                    },
                },
                text(
                    score.PlayerName,
                    108,
                    13,
                    176,
                    15,
                    score.IsCurrentPlayer
                        ? SongSelectTheme.Pink
                        : Color4.White),
                text(
                    score.Mods.Count == 0
                        ? "NM · LOCAL SCORE"
                        : $"{string.Join(" ", score.Mods)} · LOCAL SCORE",
                    108,
                    34,
                    176,
                    8,
                    SongSelectTheme.PaleCyan,
                    false),
                metric(
                    "MAX COMBO",
                    $"{score.MaxCombo:N0}×",
                    302),
                metric(
                    "ACCURACY",
                    $"{score.Accuracy:P2}",
                    402),
                new SpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -58,
                    Text = $"{score.Score:N0}",
                    Font = HomeTypography.Body(19),
                    Colour = Color4.White,
                },
                new Container
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    RelativeSizeAxes = Axes.Y,
                    Width = 48,
                    Children =
                    [
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = grade,
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = score.Grade.ToDisplayLabel(),
                            Font = HomeTypography.Display(20),
                            Colour = SongSelectTheme.DeepNavy,
                        },
                    ],
                },
            ],
        };
    }

    private static Drawable metric(
        string label,
        string value,
        float x) => new Container
    {
        Position = new Vector2(x, 7),
        Size = new Vector2(92, 44),
        Children =
        [
            new SpriteText
            {
                Text = label,
                Font = HomeTypography.Display(7),
                Colour = new Color4(
                    SongSelectTheme.PaleCyan.R,
                    SongSelectTheme.PaleCyan.G,
                    SongSelectTheme.PaleCyan.B,
                    0.72f),
            },
            new SpriteText
            {
                Y = 19,
                Text = value,
                Font = HomeTypography.Body(12),
                Colour = Color4.White,
            },
        ],
    };

    private static Drawable createTab(
        string label,
        float x,
        out Box background,
        out SpriteText textDrawable,
        Action action)
    {
        Box tabBackground = background = new Box
        {
            RelativeSizeAxes = Axes.Both,
        };
        SpriteText tabLabel = textDrawable = new SpriteText
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Text = label,
            Font = HomeTypography.Display(9),
        };
        return new ClickableContainer
        {
            Position = new Vector2(x, 9),
            Size = new Vector2(label == "GLOBAL" ? 86 : 118, 32),
            Masking = true,
            CornerRadius = 8,
            Action = action,
            Children = [tabBackground, tabLabel],
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

    private static Color4 gradeColour(ScoreRank rank) =>
        rank switch
        {
            ScoreRank.X or ScoreRank.XH => SongSelectTheme.PaleCyan,
            ScoreRank.S or ScoreRank.SH => SongSelectTheme.Cyan,
            ScoreRank.A => new Color4(0.55f, 0.94f, 0.26f, 1f),
            ScoreRank.B => SongSelectTheme.Yellow,
            _ => SongSelectTheme.Pink,
        };
}
