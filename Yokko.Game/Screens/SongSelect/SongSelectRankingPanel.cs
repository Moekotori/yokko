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
    private const float panel_width = 793;
    private const float panel_height = 466;
    private const float rows_top = 42;
    private const float row_width = 793;
    private const float row_height = 56;
    private const float row_spacing = 5;

    private readonly Container content;
    private readonly Box globalUnderline;
    private readonly Box historyUnderline;
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

        InternalChildren =
        [
            new Box
            {
                Position = new Vector2(0, 40),
                Size = new Vector2(panel_width, 1),
                Colour = new Color4(
                    SongSelectTheme.Cyan.R,
                    SongSelectTheme.Cyan.G,
                    SongSelectTheme.Cyan.B,
                    0.22f),
            },
            createTab(
                "GLOBAL",
                8,
                FontAwesome.Solid.Users,
                out globalUnderline,
                out globalLabel,
                () => setAndNotify(SongSelectScoreView.GlobalRanking)),
            createTab(
                "MY HISTORY",
                142,
                FontAwesome.Solid.Archive,
                out historyUnderline,
                out historyLabel,
                () => setAndNotify(SongSelectScoreView.Personal)),
            playerCount = new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-4, 14),
                Text = "0 PLAYS",
                Font = HomeTypography.Display(8),
                Colour = new Color4(
                    SongSelectTheme.PaleCyan.R,
                    SongSelectTheme.PaleCyan.G,
                    SongSelectTheme.PaleCyan.B,
                    0.70f),
            },
            content = new Container
            {
                Position = new Vector2(0, rows_top),
                Size = new Vector2(
                    row_width,
                    row_height * 7 + row_spacing * 6),
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
        globalLabel.Colour = global
            ? SongSelectTheme.Yellow
            : new Color4(1f, 1f, 1f, 0.58f);
        historyLabel.Colour = global
            ? new Color4(1f, 1f, 1f, 0.58f)
            : SongSelectTheme.Pink;
        globalUnderline.Alpha = global ? 1 : 0;
        historyUnderline.Alpha = global ? 0 : 1;
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
                CornerRadius = 9,
                BorderThickness = 1,
                BorderColour = new Color4(
                    SongSelectTheme.Cyan.R,
                    SongSelectTheme.Cyan.G,
                    SongSelectTheme.Cyan.B,
                    0.28f),
                Children =
                [
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(
                            SongSelectTheme.Surface.R,
                            SongSelectTheme.Surface.G,
                            SongSelectTheme.Surface.B,
                            0.90f),
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
        foreach (SongSelectScore score in scores.Take(7))
            flow.Add(createRow(score));
        content.Add(flow);
    }

    private Drawable createRow(SongSelectScore score)
    {
        bool current = score.IsCurrentPlayer;
        Color4 accent = rankAccent(score.Rank, current);
        Color4 grade = gradeColour(score.Grade);
        Color4 primary = current ? SongSelectTheme.Navy : Color4.White;
        Color4 secondary = current
            ? new Color4(
                SongSelectTheme.Navy.R,
                SongSelectTheme.Navy.G,
                SongSelectTheme.Navy.B,
                0.72f)
            : new Color4(
                SongSelectTheme.PaleCyan.R,
                SongSelectTheme.PaleCyan.G,
                SongSelectTheme.PaleCyan.B,
                0.78f);
        Texture avatar = current
            ? textures.Get("yokko")?.Crop(
                new RectangleF(270, 2200, 850, 850))
            : textures.Get(score.AvatarTexture);

        return new Container
        {
            Size = new Vector2(row_width, row_height),
            Masking = true,
            CornerRadius = 8,
            BorderThickness = current ? 1.5f : 0.7f,
            BorderColour = new Color4(
                accent.R,
                accent.G,
                accent.B,
                current ? 0.98f : 0.32f),
            Children =
            [
                SongSelectSurface.CreateShadow(8, current ? 0.24f : 0.14f, 2),
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = current
                        ? SongSelectSurface.Ivory(0.99f)
                        : new Color4(
                            SongSelectTheme.Surface.R,
                            SongSelectTheme.Surface.G,
                            SongSelectTheme.Surface.B,
                            0.91f),
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 6,
                    Colour = accent,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 76,
                    Colour = new Color4(
                        accent.R,
                        accent.G,
                        accent.B,
                        current ? 0.08f : 0.15f),
                },
                text(
                    score.Rank.ToString(),
                    22,
                    14,
                    42,
                    18,
                    current ? SongSelectTheme.Pink : accent),
                new Container
                {
                    Position = new Vector2(82, 3),
                    Size = new Vector2(50),
                    Masking = true,
                    CornerRadius = 25,
                    BorderThickness = 1.5f,
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
                    144,
                    8,
                    218,
                    15,
                    current ? SongSelectTheme.Pink : primary),
                text(
                    score.Mods.Count == 0
                        ? "NM"
                        : string.Join("   ", score.Mods),
                    144,
                    31,
                    218,
                    8,
                    secondary,
                    false),
                text(
                    $"{score.MaxCombo:N0}×",
                    438,
                    8,
                    104,
                    12,
                    primary,
                    false),
                text(
                    $"{score.Accuracy:P2}",
                    438,
                    29,
                    104,
                    10,
                    secondary,
                    false),
                new SpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -70,
                    Text = $"{score.Score:N0}",
                    Font = HomeTypography.Display(20),
                    Colour = primary,
                },
                new Container
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    RelativeSizeAxes = Axes.Y,
                    Width = 54,
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

    private static Drawable createTab(
        string label,
        float x,
        IconUsage icon,
        out Box underline,
        out SpriteText textDrawable,
        Action action)
    {
        Box tabUnderline = underline = new Box
        {
            Anchor = Anchor.BottomCentre,
            Origin = Anchor.BottomCentre,
            Y = -1,
            Width = label == "GLOBAL" ? 108 : 126,
            Height = 3,
            Colour = label == "GLOBAL"
                ? SongSelectTheme.Yellow
                : SongSelectTheme.Pink,
        };
        SpriteText tabLabel = textDrawable = new SpriteText
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            X = 31,
            Text = label,
            Font = HomeTypography.Display(10),
        };
        return new ClickableContainer
        {
            Position = new Vector2(x, 0),
            Size = new Vector2(label == "GLOBAL" ? 118 : 136, 40),
            Action = action,
            Children =
            [
                new SpriteIcon
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 9,
                    Size = new Vector2(13),
                    Icon = icon,
                    Colour = label == "GLOBAL"
                        ? SongSelectTheme.Yellow
                        : SongSelectTheme.Pink,
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
