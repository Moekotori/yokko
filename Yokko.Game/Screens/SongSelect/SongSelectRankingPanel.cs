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

internal partial class SongSelectRankingPanel : ClickableContainer
{
    private const float panel_width = 800;
    private const float panel_height = 340;
    private const float rows_top = 78;
    private const float row_height = 42;

    private readonly Container content;
    private readonly Box globalTab;
    private readonly Box historyTab;
    private readonly SpriteText globalLabel;
    private readonly SpriteText historyLabel;
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
            SongSelectSurface.Ivory(0.975f),
            new Color4(
                SongSelectTheme.Cyan.R,
                SongSelectTheme.Cyan.G,
                SongSelectTheme.Cyan.B,
                0.54f),
            12,
            1.25f);

        InternalChildren =
        [
            SongSelectSurface.CreateShadow(12, 0.32f, 5),
            panel,
            new SpriteText
            {
                Position = new Vector2(28, 16),
                Text = "RANKING",
                Font = HomeTypography.Display(20),
                Colour = SongSelectTheme.Navy,
            },
            new SpriteIcon
            {
                Position = new Vector2(132, 18),
                Size = new Vector2(16),
                Icon = FontAwesome.Solid.Trophy,
                Colour = SongSelectTheme.Yellow,
            },
            createTab("GLOBAL", 208, out globalTab, out globalLabel,
                () => setAndNotify(SongSelectScoreView.GlobalRanking)),
            createTab("MY HISTORY", 318, out historyTab, out historyLabel,
                () => setAndNotify(SongSelectScoreView.Personal)),
            createColumnHeader(),
            content = new Container
            {
                Position = new Vector2(18, rows_top),
                Size = new Vector2(panel_width - 36, row_height * 6),
                Masking = true,
            },
            new SpriteIcon
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Position = new Vector2(-22, -15),
                Size = new Vector2(12),
                Icon = FontAwesome.Solid.Plus,
                Colour = SongSelectTheme.Pink,
            },
            new Sprite
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-10, 1),
                Size = new Vector2(52, 30),
                Texture = textures.Get("SongSelect/Cute/tape-long"),
                FillMode = FillMode.Fit,
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
        globalTab.Colour = global ? SongSelectTheme.Navy : new Color4(1, 1, 1, 0);
        historyTab.Colour = global ? new Color4(1, 1, 1, 0) : SongSelectTheme.Navy;
        globalLabel.Colour = global ? Color4.White : SongSelectTheme.Navy;
        historyLabel.Colour = global ? SongSelectTheme.Navy : Color4.White;
        rebuildRows(global ? entry.Ranking : entry.History);
    }

    private void setAndNotify(SongSelectScoreView newView)
    {
        SetView(newView);
        viewChanged(newView);
    }

    private void rebuildRows(System.Collections.Generic.IReadOnlyList<SongSelectScore> scores)
    {
        content.Clear();
        if (scores.Count == 0)
        {
            content.Add(new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = view == SongSelectScoreView.Personal
                    ? "NO LOCAL PLAYS YET · PLAY TO START YOUR HISTORY"
                    : "NO RANKING DATA",
                Font = HomeTypography.Display(12),
                Colour = new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.62f),
            });
            return;
        }

        var flow = new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 1),
        };
        foreach (SongSelectScore score in scores.Take(6))
            flow.Add(createRow(score));
        content.Add(flow);
    }

    private Drawable createRow(SongSelectScore score)
    {
        Color4 accent = score.IsCurrentPlayer
            ? SongSelectTheme.Pink
            : score.Rank <= 3 ? SongSelectTheme.Yellow : SongSelectTheme.Cyan;
        Texture avatar = score.IsCurrentPlayer
            ? textures.Get("yokko")?.Crop(new RectangleF(270, 2200, 850, 850))
            : textures.Get(score.AvatarTexture);

        return new Container
        {
            Size = new Vector2(panel_width - 36, row_height),
            Masking = true,
            CornerRadius = 4,
            BorderThickness = score.IsCurrentPlayer ? 1.5f : 0,
            BorderColour = accent,
            Children =
            [
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = score.IsCurrentPlayer
                        ? new Color4(1f, 0.82f, 0.91f, 0.78f)
                        : new Color4(1f, 1f, 1f, 0.70f),
                },
                text($"{score.Rank}", 12, 12, 32, 16, accent),
                new Container
                {
                    Position = new Vector2(44, 4),
                    Size = new Vector2(34),
                    Masking = true,
                    CornerRadius = 17,
                    BorderThickness = 1,
                    BorderColour = accent,
                    Child = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Texture = avatar,
                        FillMode = FillMode.Fill,
                    },
                },
                text(score.PlayerName, 90, 12, 112, 15,
                    score.IsCurrentPlayer ? SongSelectTheme.Pink : SongSelectTheme.Navy),
                text(score.Grade.ToDisplayLabel(), 218, 9, 42, 20,
                    gradeColour(score.Grade)),
                text($"{score.Score:N0}", 278, 12, 132, 14, SongSelectTheme.Navy),
                text($"{score.Accuracy:P2}", 430, 12, 90, 14, SongSelectTheme.Navy),
                text($"{score.MaxCombo:N0}×", 540, 12, 88, 14, SongSelectTheme.Navy),
                text(score.Mods.Count == 0 ? "NM" : string.Join(" ", score.Mods),
                    650, 12, 92, 13, SongSelectTheme.Pink),
            ],
        };
    }

    private static Drawable createColumnHeader() => new Container
    {
        Position = new Vector2(18, 54),
        Size = new Vector2(panel_width - 36, 22),
        Children =
        [
            text("#", 12, 0, 32, 10, SongSelectTheme.Cyan),
            text("PLAYER", 90, 0, 112, 10, SongSelectTheme.Cyan),
            text("GRADE", 218, 0, 48, 10, SongSelectTheme.Cyan),
            text("SCORE", 278, 0, 132, 10, SongSelectTheme.Cyan),
            text("ACCURACY", 430, 0, 90, 10, SongSelectTheme.Cyan),
            text("MAX COMBO", 540, 0, 88, 10, SongSelectTheme.Cyan),
            text("MODS", 650, 0, 92, 10, SongSelectTheme.Cyan),
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
            Font = HomeTypography.Display(11),
        };
        return new ClickableContainer
        {
            Position = new Vector2(x, 8),
            Size = new Vector2(label == "GLOBAL" ? 96 : 126, 34),
            Masking = true,
            CornerRadius = 17,
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
        Color4 colour) => new()
    {
        Position = new Vector2(x, y),
        Width = width,
        Truncate = true,
        Text = value,
        Font = HomeTypography.Display(size),
        Colour = colour,
    };

    private static Color4 gradeColour(Yokko.Core.Scoring.ScoreRank rank) =>
        rank switch
        {
            Yokko.Core.Scoring.ScoreRank.X or Yokko.Core.Scoring.ScoreRank.XH
                => SongSelectTheme.Yellow,
            Yokko.Core.Scoring.ScoreRank.S or Yokko.Core.Scoring.ScoreRank.SH
                => SongSelectTheme.Cyan,
            Yokko.Core.Scoring.ScoreRank.A
                => new Color4(0.32f, 0.72f, 0.38f, 1f),
            _ => SongSelectTheme.Pink,
        };
}
