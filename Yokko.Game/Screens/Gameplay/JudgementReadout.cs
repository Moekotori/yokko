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
    private double hideAtMilliseconds;

    public JudgementReadout()
    {
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
                    },
                },
            },
        };

        Alpha = 0;
    }

    public void Show(JudgementEvent judgement)
    {
        ratingText.Text = judgement.Rating.ToString().ToUpperInvariant();
        ratingText.Colour = RatingColours.For(judgement.Rating);
        errorText.Text = judgement.IsMiss ? "missed" : $"{judgement.HitErrorMilliseconds:+0.0;-0.0;0.0} ms";
        hideAtMilliseconds = Time.Current + 420;
        Alpha = 1;
    }

    protected override void Update()
    {
        base.Update();

        if (Time.Current >= hideAtMilliseconds)
        {
            Alpha = 0;
            return;
        }

        Alpha = Math.Clamp((float)((hideAtMilliseconds - Time.Current) / 180), 0, 1);
    }
}
