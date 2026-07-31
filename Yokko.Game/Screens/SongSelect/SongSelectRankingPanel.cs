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
    private const float panel_width = 636;
    private const float panel_height = 286;
    private const float rows_top = 68;
    private const float row_height = 34;

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

        InternalChildren =
        [
            paperPanel(),
            new SpriteText
            {
                Position = new Vector2(22, 13),
                Text = "RANKING",
                Font = HomeTypography.Display(16),
                Colour = SongSelectTheme.Navy,
            },
            new SpriteIcon
            {
                Position = new Vector2(112, 14),
                Size = new Vector2(14),
                Icon = FontAwesome.Solid.Trophy,
                Colour = SongSelectTheme.Yellow,
            },
            createTab("GLOBAL", 226, out globalTab, out globalLabel,
                () => setAndNotify(SongSelectScoreView.GlobalRanking)),
            createTab("MY HISTORY", 322, out historyTab, out historyLabel,
                () => setAndNotify(SongSelectScoreView.Personal)),
            createColumnHeader(),
            content = new Container
            {
                Position = new Vector2(14, rows_top),
                Size = new Vector2(panel_width - 28, row_height * 6),
                Masking = true,
            },
            new SpriteIcon
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Position = new Vector2(-17, -12),
                Size = new Vector2(12),
                Icon = FontAwesome.Solid.Plus,
                Colour = SongSelectTheme.Pink,
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
            Size = new Vector2(panel_width - 28, row_height),
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
                        : new Color4(1f, 1f, 1f, 0.62f),
                },
                text($"{score.Rank}", 10, 9, 28, 14, accent),
                new Container
                {
                    Position = new Vector2(38, 3),
                    Size = new Vector2(28),
                    Masking = true,
                    CornerRadius = 14,
                    BorderThickness = 1,
                    BorderColour = accent,
                    Child = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Texture = avatar,
                        FillMode = FillMode.Fill,
                    },
                },
                text(score.PlayerName, 76, 9, 88, 13,
                    score.IsCurrentPlayer ? SongSelectTheme.Pink : SongSelectTheme.Navy),
                text(score.Grade.ToDisplayLabel(), 176, 7, 38, 17,
                    gradeColour(score.Grade)),
                text($"{score.Score:N0}", 226, 9, 108, 12, SongSelectTheme.Navy),
                text($"{score.Accuracy:P2}", 348, 9, 74, 12, SongSelectTheme.Navy),
                text($"{score.MaxCombo:N0}×", 438, 9, 70, 12, SongSelectTheme.Navy),
                text(score.Mods.Count == 0 ? "NM" : string.Join(" ", score.Mods),
                    530, 9, 70, 11, SongSelectTheme.Pink),
            ],
        };
    }

    private static Drawable createColumnHeader() => new Container
    {
        Position = new Vector2(14, 48),
        Size = new Vector2(panel_width - 28, 18),
        Children =
        [
            text("#", 10, 0, 28, 9, SongSelectTheme.Cyan),
            text("PLAYER", 76, 0, 88, 9, SongSelectTheme.Cyan),
            text("GRADE", 176, 0, 48, 9, SongSelectTheme.Cyan),
            text("SCORE", 226, 0, 108, 9, SongSelectTheme.Cyan),
            text("ACC", 348, 0, 74, 9, SongSelectTheme.Cyan),
            text("COMBO", 438, 0, 70, 9, SongSelectTheme.Cyan),
            text("MODS", 530, 0, 70, 9, SongSelectTheme.Cyan),
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
            Size = new Vector2(label == "GLOBAL" ? 88 : 112, 30),
            Masking = true,
            CornerRadius = 15,
            Action = action,
            Children = [tabBackground, tabLabel],
        };
    }

    private static Drawable paperPanel() => new Container
    {
        RelativeSizeAxes = Axes.Both,
        Masking = true,
        CornerRadius = 9,
        BorderThickness = 1.5f,
        BorderColour = new Color4(
            SongSelectTheme.Cyan.R,
            SongSelectTheme.Cyan.G,
            SongSelectTheme.Cyan.B,
            0.78f),
        Children =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(1f, 0.985f, 0.94f, 0.96f),
            },
            new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 3,
                Colour = SongSelectTheme.Cyan,
            },
        ],
    };

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
