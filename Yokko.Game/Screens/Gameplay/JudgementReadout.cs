using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;
using Yokko.Core.Scoring;
using Yokko.Game.Gameplay;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

public partial class JudgementReadout : CompositeDrawable
{
    private readonly SpriteText ratingText;
    private readonly SpriteText errorText;
    private readonly Drawable content;
    private bool showHitError;
    private readonly JudgementConfiguration configuration;
    private double displayDurationMilliseconds;
    private double hideAtMilliseconds;
    private bool editorPreview;

    internal string DisplayedRating =>
        ratingText?.Text.ToString() ?? string.Empty;

    internal string DisplayedError =>
        errorText?.Text.ToString() ?? string.Empty;

    internal double DisplayDurationForTest =>
        displayDurationMilliseconds;

    internal float ContentOpacityForTest => content.Alpha;

    internal bool ShowsHitErrorForTest => showHitError;

    public JudgementReadout(
        bool showHitError = true,
        JudgementConfiguration? configuration = null,
        double displayDurationMilliseconds =
            YokkoGameplaySettings
                .DefaultJudgementDisplayDurationMilliseconds,
        double opacity = YokkoGameplaySettings.MaximumJudgementOpacity)
    {
        this.showHitError = showHitError;
        this.configuration =
            configuration ?? JudgementConfiguration.YokkoDefault;
        this.displayDurationMilliseconds = Math.Clamp(
            displayDurationMilliseconds,
            YokkoGameplaySettings
                .MinimumJudgementDisplayDurationMilliseconds,
            YokkoGameplaySettings
                .MaximumJudgementDisplayDurationMilliseconds);
        AutoSizeAxes = Axes.Both;

        InternalChild = content = new FillFlowContainer
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
        };

        SetOpacity(opacity);
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
            : RatingColours.ForDisplay(judgement.Rating, configuration);
        errorText.Text = judgement.IsMiss && !isMine
            ? "missed"
            : $"{judgement.HitErrorMilliseconds:+0.0;-0.0;0.0} ms";
        hideAtMilliseconds = Time.Current + displayDurationMilliseconds;
        Alpha = 1;
    }

    internal void Clear()
    {
        hideAtMilliseconds = double.NegativeInfinity;
        ratingText.Text = string.Empty;
        errorText.Text = string.Empty;
        Alpha = 0;
    }

    internal void SetDisplayDuration(double milliseconds)
    {
        displayDurationMilliseconds = Math.Clamp(
            milliseconds,
            YokkoGameplaySettings
                .MinimumJudgementDisplayDurationMilliseconds,
            YokkoGameplaySettings
                .MaximumJudgementDisplayDurationMilliseconds);
    }

    internal void SetOpacity(double opacity) =>
        content.Alpha = (float)Math.Clamp(
            opacity,
            YokkoGameplaySettings.MinimumJudgementOpacity,
            YokkoGameplaySettings.MaximumJudgementOpacity);

    internal void SetShowHitError(bool show)
    {
        showHitError = show;
        errorText.Alpha = show ? 1 : 0;
    }

    internal void SetEditorPreview(bool preview)
    {
        editorPreview = preview;
        if (preview)
        {
            ratingText.Text = configuration.RatingLabel(
                JudgementRating.Great);
            ratingText.Colour = RatingColours.ForDisplay(
                JudgementRating.Great,
                configuration);
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
            return;

        if (Time.Current >= hideAtMilliseconds)
        {
            Alpha = 0;
            return;
        }

        double fadeDuration = Math.Min(
            180,
            displayDurationMilliseconds * 0.45);
        Alpha = Math.Clamp(
            (float)((hideAtMilliseconds - Time.Current)
                    / Math.Max(1, fadeDuration)),
            0,
            1);
    }
}
