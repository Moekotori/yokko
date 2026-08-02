using System;
using System.IO;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Scoring;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectScoreDetailOverlay : CompositeDrawable
{
    private readonly Action closeRequested;
    private readonly Action replayRequested;
    private readonly Container card;
    private readonly SongSelectScoreDetailButton replayButton;
    private readonly SpriteText replayStatus;
    private bool closing;

    internal SongSelectScore Score { get; }
    internal bool ReplayAvailable { get; }
    internal bool ReplayLoading { get; private set; }
    internal string ReplayStatus => replayStatus.Text.ToString();

    public override bool HandlePositionalInput => true;
    public override bool HandleNonPositionalInput => true;

    public SongSelectScoreDetailOverlay(
        SongSelectScore score,
        Texture avatar,
        Action closeRequested,
        Action replayRequested)
    {
        Score = score;
        ReplayAvailable = !string.IsNullOrWhiteSpace(score.ReplayPath)
                          && File.Exists(score.ReplayPath);
        this.closeRequested = closeRequested;
        this.replayRequested = replayRequested;
        RelativeSizeAxes = Axes.Both;
        Depth = float.MinValue;

        Container cardSurface = SongSelectSurface.CreateCard(
            out _,
            SongSelectSurface.Ivory(0.985f),
            SongSelectSurface.Border(0.22f),
            20,
            1.5f);

        InternalChildren =
        [
            new ClickableContainer
            {
                RelativeSizeAxes = Axes.Both,
                Action = Close,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        SongSelectTheme.Navy.R,
                        SongSelectTheme.Navy.G,
                        SongSelectTheme.Navy.B,
                        0.64f),
                },
            },
            card = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(920, 610),
                Children =
                [
                    SongSelectSurface.CreateShadow(20, 0.18f, 8),
                    cardSurface,
                    createHeader(avatar),
                    createSummary(),
                    createJudgements(),
                    createFooter(),
                ],
            },
        ];

        replayButton = card.ChildrenOfType<SongSelectScoreDetailButton>()
                           .Single(button => button.Primary);
        replayStatus = card.ChildrenOfType<SpriteText>()
                           .Single(text => text.Name == "replay-status");
        replayButton.SetEnabled(ReplayAvailable);
        replayStatus.Text = ReplayAvailable
            ? "Replay is ready"
            : "Replay unavailable for this play";

        Alpha = 0;
        card.Alpha = 0;
        card.Y = 14;
        card.Scale = new Vector2(0.985f);
        this.FadeIn(140, Easing.OutQuint);
        card.FadeIn(120, Easing.OutQuint)
            .MoveToY(0, 180, Easing.OutQuint)
            .ScaleTo(1, 180, Easing.OutQuint);
    }

    internal void Close()
    {
        if (closing || ReplayLoading)
            return;

        closing = true;
        card.ClearTransforms();
        card.MoveToY(8, 110, Easing.InQuad)
            .ScaleTo(0.99f, 110, Easing.InQuad)
            .FadeOut(90, Easing.InQuad);
        this.FadeOut(120, Easing.InQuad);
        closeRequested();
    }

    internal void WatchReplay()
    {
        if (!ReplayAvailable || ReplayLoading || closing)
            return;

        ReplayLoading = true;
        replayButton.SetLoading(true);
        replayStatus.Text = "Loading replay...";
        replayRequested();
    }

    internal void ShowReplayError(string message)
    {
        ReplayLoading = false;
        replayButton.SetLoading(false);
        replayStatus.Text = string.IsNullOrWhiteSpace(message)
            ? "Replay could not be opened"
            : message;
    }

    private Drawable createHeader(Texture avatar) => new Container
    {
        Position = new Vector2(34, 30),
        Size = new Vector2(852, 142),
        Children =
        [
            new Container
            {
                Size = new Vector2(128),
                Masking = true,
                CornerRadius = 64,
                BorderThickness = 3,
                BorderColour = SongSelectTheme.Pink,
                Child = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Texture = avatar,
                    FillMode = FillMode.Fill,
                },
            },
            new SpriteText
            {
                Position = new Vector2(156, 12),
                Text = Score.PlayerName,
                Font = HomeTypography.Display(25),
                Colour = SongSelectTheme.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(158, 54),
                Text = Score.Mods.Count == 0
                    ? "NO MOD"
                    : string.Join("  ·  ", Score.Mods),
                Font = HomeTypography.Body(13),
                Colour = SongSelectTheme.Pink,
            },
            new SpriteText
            {
                Position = new Vector2(158, 85),
                Text = Score.PlayedAt?.ToLocalTime()
                            .ToString("yyyy-MM-dd  HH:mm")
                       ?? "PLAY TIME UNKNOWN",
                Font = HomeTypography.Body(11),
                Colour = mutedNavy(0.62f),
            },
            new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-6, 6),
                Text = Score.Grade.ToDisplayLabel(),
                Font = HomeTypography.Display(72),
                Colour = gradeColour(Score.Grade),
            },
            new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-8, 95),
                Text = Score.IsCurrentPlayer
                    ? $"PERSONAL #{Score.Rank}"
                    : $"RANKING #{Score.Rank}",
                Font = HomeTypography.Display(10),
                Colour = mutedNavy(0.64f),
            },
        ],
    };

    private Drawable createSummary() => new Container
    {
        Position = new Vector2(34, 190),
        Size = new Vector2(852, 104),
        Children =
        [
            summaryMetric("SCORE", $"{Score.Score:N0}", 0, 320),
            summaryMetric("ACCURACY", $"{Score.Accuracy:P2}", 336, 238),
            summaryMetric("MAX COMBO", $"{Score.MaxCombo:N0}×", 590, 262),
        ],
    };

    private Drawable createJudgements()
    {
        JudgementConfiguration configuration =
            Score.JudgementConfiguration
            ?? JudgementConfiguration.YokkoDefault;
        JudgementRating[] ratings =
        [
            JudgementRating.Perfect,
            JudgementRating.Great,
            JudgementRating.Good,
            JudgementRating.Ok,
            JudgementRating.Meh,
            JudgementRating.Miss,
        ];
        int[] values =
        [
            Score.Perfect,
            Score.Great,
            Score.Good,
            Score.Ok,
            Score.Meh,
            Score.Miss,
        ];
        Color4[] defaultColours =
        [
            SongSelectTheme.PaleCyan,
            SongSelectTheme.Cyan,
            new Color4(0.62f, 0.94f, 0.25f, 1f),
            SongSelectTheme.Yellow,
            new Color4(1f, 0.58f, 0.38f, 1f),
            SongSelectTheme.Pink,
        ];
        var judgements =
            new (string Label, int Value, ColourInfo Accent, Color4 Border,
                bool AccentText)[6];
        for (int i = 0; i < ratings.Length; i++)
        {
            JudgementRating rating = ratings[i];
            bool stable = configuration.Mode == JudgementMode.OsuStable;
            judgements[i] = (
                configuration.RatingLabel(rating),
                values[i],
                stable
                    ? RatingColours.ForDisplay(rating, configuration)
                    : defaultColours[i],
                stable
                    ? RatingColours.StableSolid(rating)
                    : defaultColours[i],
                stable);
        }
        var flow = new FillFlowContainer
        {
            Position = new Vector2(34, 314),
            Size = new Vector2(852, 166),
            Direction = FillDirection.Full,
            Spacing = new Vector2(12),
        };
        foreach ((string label, int value, ColourInfo accent, Color4 border,
                     bool accentText)
                 in judgements)
        {
            flow.Add(judgementMetric(
                label,
                value,
                accent,
                border,
                accentText));
        }
        return flow;
    }

    private Drawable createFooter() => new Container
    {
        Anchor = Anchor.BottomCentre,
        Origin = Anchor.BottomCentre,
        Position = new Vector2(0, -28),
        Size = new Vector2(852, 72),
        Children =
        [
            new SpriteText
            {
                Name = "replay-status",
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 4,
                Font = HomeTypography.Body(11),
                Colour = mutedNavy(0.62f),
            },
            new SongSelectScoreDetailButton(
                "CLOSE",
                FontAwesome.Solid.Times,
                Close,
                false)
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -250,
            },
            new SongSelectScoreDetailButton(
                "WATCH REPLAY",
                FontAwesome.Solid.Play,
                WatchReplay,
                true)
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
            },
        ],
    };

    private static Drawable summaryMetric(
        string label,
        string value,
        float x,
        float width) => new Container
    {
        Position = new Vector2(x, 0),
        Size = new Vector2(width, 104),
        Children =
        [
            new SpriteText
            {
                Text = label,
                Font = HomeTypography.Display(10),
                Colour = mutedNavy(0.58f),
            },
            new SpriteText
            {
                Y = 28,
                Text = value,
                Font = HomeTypography.Display(28),
                Colour = SongSelectTheme.Navy,
            },
            new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Width = width - 18,
                Height = 3,
                Colour = SongSelectTheme.Cyan,
            },
        ],
    };

    private static Drawable judgementMetric(
        string label,
        int value,
        ColourInfo accent,
        Color4 borderAccent,
        bool accentText) => new Container
    {
        Size = new Vector2(276, 77),
        Masking = true,
        CornerRadius = 10,
        BorderThickness = 1,
        BorderColour = new Color4(
            borderAccent.R,
            borderAccent.G,
            borderAccent.B,
            0.52f),
        Children =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SongSelectSurface.Ivory(0.74f),
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 5,
                Colour = accent,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 22,
                Text = label,
                Font = HomeTypography.Display(11),
                Colour = accentText ? accent : mutedNavy(0.66f),
            },
            new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -20,
                Text = $"{value:N0}",
                Font = HomeTypography.Display(21),
                Colour = accentText ? accent : SongSelectTheme.Navy,
            },
        ],
    };

    private static Color4 mutedNavy(float alpha) => new(
        SongSelectTheme.Navy.R,
        SongSelectTheme.Navy.G,
        SongSelectTheme.Navy.B,
        alpha);

    private static Color4 gradeColour(Yokko.Core.Scoring.ScoreRank rank) =>
        rank switch
        {
            Yokko.Core.Scoring.ScoreRank.X or
                Yokko.Core.Scoring.ScoreRank.XH => SongSelectTheme.PaleCyan,
            Yokko.Core.Scoring.ScoreRank.S or
                Yokko.Core.Scoring.ScoreRank.SH => SongSelectTheme.Cyan,
            Yokko.Core.Scoring.ScoreRank.A =>
                new Color4(0.62f, 0.94f, 0.25f, 1f),
            Yokko.Core.Scoring.ScoreRank.B => SongSelectTheme.Yellow,
            _ => SongSelectTheme.Pink,
        };
}

internal partial class SongSelectScoreDetailButton : ClickableContainer
{
    private readonly SpriteText label;
    private readonly Box background;
    private readonly SpriteIcon icon;
    private bool enabled = true;
    private bool loading;

    internal bool Primary { get; }

    public SongSelectScoreDetailButton(
        string text,
        IconUsage iconUsage,
        Action action,
        bool primary)
    {
        Primary = primary;
        Size = new Vector2(primary ? 232 : 150, 58);
        Masking = true;
        CornerRadius = 12;
        Action = () =>
        {
            if (enabled && !loading)
                action();
        };
        Children =
        [
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = primary ? SongSelectTheme.Navy : SongSelectSurface.Ivory(),
            },
            icon = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                Position = new Vector2(28, 0),
                Size = new Vector2(15),
                Icon = iconUsage,
                Colour = primary ? SongSelectTheme.Ivory : SongSelectTheme.Pink,
            },
            label = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                X = 10,
                Text = text,
                Font = HomeTypography.Display(11),
                Colour = primary ? SongSelectTheme.Ivory : SongSelectTheme.Navy,
            },
        ];
    }

    internal void SetEnabled(bool value)
    {
        enabled = value;
        Alpha = value ? 1 : 0.42f;
    }

    internal void SetLoading(bool value)
    {
        loading = value;
        label.Text = value ? "LOADING..." : "WATCH REPLAY";
        icon.Icon = value ? FontAwesome.Solid.Clock : FontAwesome.Solid.Play;
        background.FadeColour(
            value ? SongSelectTheme.Cyan : SongSelectTheme.Navy,
            120,
            Easing.OutQuint);
    }
}
