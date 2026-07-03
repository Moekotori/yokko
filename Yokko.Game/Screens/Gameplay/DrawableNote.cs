using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Beatmaps;
using Yokko.Core.Scoring;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

public partial class DrawableNote : CompositeDrawable
{
    private readonly YokkoHitObject hitObject;
    private readonly Box body;
    private bool resolved;

    public DrawableNote(int hitObjectIndex, YokkoHitObject hitObject)
    {
        HitObjectIndex = hitObjectIndex;
        this.hitObject = hitObject;

        Height = 24;
        Masking = true;

        InternalChildren = new Drawable[]
        {
            body = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = YokkoPalette.Cyan,
            },
            new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 4,
                Colour = new Color4(1f, 1f, 1f, 0.35f),
            },
        };
    }

    public int HitObjectIndex { get; }

    public void Resolve(JudgementRating rating)
    {
        resolved = true;
        Alpha = 0;
        body.Colour = RatingColours.For(rating);
    }

    public void UpdatePosition(
        double gameplayTimeMilliseconds,
        bool resolvedByState,
        float topY,
        float judgementY,
        double approachTimeMilliseconds)
    {
        if (resolved || resolvedByState)
        {
            Alpha = Math.Min(Alpha, 0.18f);
            return;
        }

        double untilHit = hitObject.StartTimeMilliseconds - gameplayTimeMilliseconds;
        double progress = 1 - untilHit / approachTimeMilliseconds;
        float travel = judgementY - topY;
        Y = topY + (float)(progress * travel);

        Alpha = progress is >= -0.08 and <= 1.22 ? 1 : 0;
    }
}
