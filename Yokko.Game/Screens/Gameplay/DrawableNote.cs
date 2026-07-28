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
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                RelativeSizeAxes = Axes.X,
                Height = 4,
                Colour = new Color4(1f, 1f, 1f, 0.35f),
            },
        };
    }

    public int HitObjectIndex { get; }

    public void ApplyJudgement(JudgementEvent judgement)
    {
        body.Colour = RatingColours.For(judgement.Rating);

        if (judgement.Phase is JudgementPhase.Tap or JudgementPhase.HoldTail)
        {
            resolved = true;
            Alpha = 0;
        }
    }

    public void UpdatePosition(
        double gameplayTimeMilliseconds,
        bool resolvedByState,
        bool holdActive,
        float topY,
        float judgementY,
        double approachTimeMilliseconds)
    {
        if (resolved || resolvedByState)
        {
            Alpha = Math.Min(Alpha, 0.18f);
            return;
        }

        float travel = judgementY - topY;

        if (hitObject.Kind == HitObjectKind.Hold && hitObject.EndTimeMilliseconds is double endTime)
        {
            double headProgress = 1 - (hitObject.StartTimeMilliseconds - gameplayTimeMilliseconds) / approachTimeMilliseconds;
            double tailProgress = 1 - (endTime - gameplayTimeMilliseconds) / approachTimeMilliseconds;
            float headY = topY + (float)(headProgress * travel);
            float tailY = topY + (float)(tailProgress * travel);

            if (holdActive && gameplayTimeMilliseconds >= hitObject.StartTimeMilliseconds)
            {
                headY = judgementY;
                body.Colour = YokkoPalette.Lime;
            }

            Y = Math.Min(headY, tailY);
            Height = Math.Max(24, Math.Abs(headY - tailY) + 24);
            Alpha = tailProgress >= -0.08 && headProgress <= 1.22 ? 1 : 0;
            return;
        }

        double progress = 1 - (hitObject.StartTimeMilliseconds - gameplayTimeMilliseconds) / approachTimeMilliseconds;
        Y = topY + (float)(progress * travel);
        Height = 24;
        Alpha = progress is >= -0.08 and <= 1.22 ? 1 : 0;
    }
}
