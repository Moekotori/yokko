using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Beatmaps;
using Yokko.Core.Scoring;
using Yokko.Game.Presentation;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Screens.Gameplay;

public partial class DrawableNote : CompositeDrawable
{
    private readonly YokkoHitObject hitObject;
    private readonly Box fallbackBody;
    private readonly Sprite holdBody;
    private readonly float headHeight;
    private readonly float tailHeight;
    private readonly float minimumHeight;
    private bool resolved;

    internal DrawableNote(
        int hitObjectIndex,
        YokkoHitObject hitObject,
        float laneWidth,
        OsuManiaSkin skin = null)
    {
        HitObjectIndex = hitObjectIndex;
        this.hitObject = hitObject;
        Width = laneWidth;

        if (skin == null)
        {
            Height = minimumHeight = 24;
            Masking = true;

            InternalChildren = new Drawable[]
            {
                fallbackBody = new Box
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
            return;
        }

        OsuManiaSkinConfiguration configuration = skin.Configuration;
        int lane = hitObject.Lane;

        if (hitObject.Kind == HitObjectKind.Tap)
        {
            Texture noteTexture = skin.GetTexture(configuration.NoteImages[lane]);

            if (noteTexture != null)
            {
                Height = minimumHeight = scaledHeight(noteTexture, laneWidth, 24);
                InternalChild = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Texture = noteTexture,
                };
                return;
            }

            Height = minimumHeight = 24;
            Masking = true;
            InternalChild = fallbackBody = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = YokkoPalette.Cyan,
            };
            return;
        }

        Texture headTexture = skin.GetTexture(configuration.HoldHeadImages[lane]);
        Texture bodyTexture = skin.GetTexture(configuration.HoldBodyImages[lane]);
        Texture tailTexture = skin.GetTexture(configuration.HoldTailImages[lane]) ?? headTexture;

        headHeight = scaledHeight(headTexture, laneWidth, 12);
        tailHeight = scaledHeight(tailTexture, laneWidth, headHeight);
        Height = minimumHeight = Math.Max(24, headHeight + tailHeight);

        var children = new System.Collections.Generic.List<Drawable>();

        if (bodyTexture != null)
        {
            children.Add(holdBody = new Sprite
            {
                Width = laneWidth,
                Texture = bodyTexture,
            });
        }
        else
        {
            children.Add(fallbackBody = new Box
            {
                Width = laneWidth,
                Colour = YokkoPalette.Cyan,
            });
        }

        if (tailTexture != null)
        {
            children.Add(new Sprite
            {
                Width = laneWidth,
                Height = tailHeight,
                Texture = tailTexture,
            });
        }

        if (headTexture != null)
        {
            children.Add(new Sprite
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Width = laneWidth,
                Height = headHeight,
                Texture = headTexture,
            });
        }

        InternalChildren = children.ToArray();
        updateHoldBody();
    }

    public int HitObjectIndex { get; }

    public void ApplyJudgement(JudgementEvent judgement)
    {
        if (fallbackBody != null)
            fallbackBody.Colour = RatingColours.For(judgement.Rating);

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

                if (fallbackBody != null)
                    fallbackBody.Colour = YokkoPalette.Lime;
            }

            Y = Math.Min(headY, tailY);
            Height = Math.Max(minimumHeight, Math.Abs(headY - tailY) + headHeight);
            updateHoldBody();
            Alpha = tailProgress >= -0.08 && headProgress <= 1.22 ? 1 : 0;
            return;
        }

        double progress = 1 - (hitObject.StartTimeMilliseconds - gameplayTimeMilliseconds) / approachTimeMilliseconds;
        Y = topY + (float)(progress * travel);
        Height = minimumHeight;
        Alpha = progress is >= -0.08 and <= 1.22 ? 1 : 0;
    }

    private void updateHoldBody()
    {
        float bodyY = tailHeight;
        float bodyHeight = Math.Max(0, Height - headHeight - tailHeight);

        if (holdBody != null)
        {
            holdBody.Y = bodyY;
            holdBody.Height = bodyHeight;
        }

        if (fallbackBody != null && hitObject.Kind == HitObjectKind.Hold)
        {
            fallbackBody.Y = bodyY;
            fallbackBody.Height = bodyHeight;
        }
    }

    private static float scaledHeight(Texture texture, float width, float fallback)
    {
        if (texture == null || texture.DisplayWidth <= 0)
            return fallback;

        return Math.Max(1, texture.DisplayHeight * width / texture.DisplayWidth);
    }
}
