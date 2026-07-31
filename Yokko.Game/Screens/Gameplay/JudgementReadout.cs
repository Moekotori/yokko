using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;
using Yokko.Core.Scoring;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

public partial class JudgementReadout : CompositeDrawable
{
    private readonly SpriteText ratingText;
    private readonly SpriteText errorText;
    private readonly bool showHitError;
    private readonly JudgementConfiguration configuration;
    private double hideAtMilliseconds;
    private bool editorPreview;

    internal string DisplayedRating =>
        ratingText?.Text.ToString() ?? string.Empty;

    internal string DisplayedError =>
        errorText?.Text.ToString() ?? string.Empty;

    public JudgementReadout(
        bool showHitError = true,
        JudgementConfiguration? configuration = null)
    {
        this.showHitError = showHitError;
        this.configuration =
            configuration ?? JudgementConfiguration.YokkoDefault;
        AutoSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Children = new Drawable[]
                {
                    ratingText = new SpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Font = FontUsage.Default.With(size: 44),
                        Colour = YokkoPalette.Text,
                    },
                    errorText = new SpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Font = FontUsage.Default.With(size: 20),
                        Colour = YokkoPalette.TextMuted,
                        Alpha = showHitError ? 1 : 0,
                    },
                },
            },
        };

        Alpha = 0;
    }

    public void Show(JudgementEvent judgement)
    {
        bool isMine = judgement.Phase == JudgementPhase.Mine;
        ratingText.Text = isMine
            ? "MINE HIT"
            : configuration.RatingLabel(judgement.Rating);
        ratingText.Colour = isMine
            ? YokkoPalette.Rose
            : RatingColours.For(judgement.Rating);
        if (showHitError)
        {
            errorText.Text = judgement.IsMiss && !isMine
                ? "missed"
                : $"{judgement.HitErrorMilliseconds:+0.0;-0.0;0.0} ms";
        }
        hideAtMilliseconds = Time.Current + 420;
        Alpha = 1;
    }

    internal void SetEditorPreview(bool preview)
    {
        editorPreview = preview;
        if (preview)
        {
            ratingText.Text = "GREAT";
            ratingText.Colour = RatingColours.For(
                JudgementRating.Great);
            if (showHitError)
                errorText.Text = "+12.0 ms";
            Alpha = 1;
            return;
        }

        Alpha = Time.Current < hideAtMilliseconds ? 1 : 0;
    }

    protected override void Update()
    {
        base.Update();

        if (editorPreview)
        {
            Alpha = 1;
            return;
        }

        if (Time.Current >= hideAtMilliseconds)
        {
            Alpha = 0;
            return;
        }

        Alpha = Math.Clamp((float)((hideAtMilliseconds - Time.Current) / 180), 0, 1);
    }
}
