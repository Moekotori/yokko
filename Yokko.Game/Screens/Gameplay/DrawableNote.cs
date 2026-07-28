using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
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
    private readonly Container holdBodyClip;
    private readonly Sprite holdBody;
    private readonly Sprite holdHead;
    private readonly Sprite holdTail;
    private readonly float headHeight;
    private readonly float tailHeight;
    private readonly float bodyTextureHeight;
    private readonly float minimumHeight;
    private readonly int noteBodyStyle;
    private readonly bool upsideDown;
    private readonly bool flipHoldHead;
    private readonly bool flipHoldBody;
    private readonly bool flipHoldTail;
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
        upsideDown = configuration.UpsideDown;

        if (hitObject.Kind == HitObjectKind.Tap)
        {
            Texture noteTexture = skin.GetTexture(configuration.NoteImages[lane]);

            if (noteTexture != null)
            {
                Height = minimumHeight = scaledHeight(
                    noteTexture,
                    configuration.WidthForNoteHeightScale,
                    24);
                InternalChild = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Scale = new Vector2(
                        1,
                        upsideDown && configuration.NoteFlipWhenUpsideDown[lane] ? -1 : 1),
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

        noteBodyStyle = configuration.NoteBodyStyles[lane];
        Texture headTexture = skin.GetTexture(configuration.HoldHeadImages[lane]);
        Texture bodyTexture = skin.GetTexture(
            configuration.HoldBodyImages[lane],
            repeatVertically: noteBodyStyle != 0);
        Texture tailTexture = skin.GetTexture(configuration.HoldTailImages[lane]) ?? headTexture;

        headHeight = scaledHeight(headTexture, configuration.WidthForNoteHeightScale, 12);
        tailHeight = scaledHeight(tailTexture, configuration.WidthForNoteHeightScale, headHeight);
        bodyTextureHeight = scaledHeight(bodyTexture, configuration.WidthForNoteHeightScale, 1);
        flipHoldHead = upsideDown && configuration.HoldHeadFlipWhenUpsideDown[lane];
        flipHoldBody = upsideDown && configuration.HoldBodyFlipWhenUpsideDown[lane];
        flipHoldTail = upsideDown && configuration.HoldTailFlipWhenUpsideDown[lane];
        Height = minimumHeight = Math.Max(24, headHeight + tailHeight);
        Masking = true;

        var children = new System.Collections.Generic.List<Drawable>();

        if (bodyTexture != null)
        {
            children.Add(holdBodyClip = new Container
            {
                Width = laneWidth,
                Masking = true,
                Child = holdBody = new Sprite
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.Centre,
                    Width = laneWidth,
                    Texture = bodyTexture,
                },
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
            children.Add(holdTail = new Sprite
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                Texture = tailTexture,
            });
        }

        if (headTexture != null)
        {
            children.Add(holdHead = new Sprite
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
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

            // Legacy mania pieces use a bottom origin for downscroll and a
            // top origin for upscroll. Keep the timing anchor on the edge of
            // the texture instead of treating it as the texture's top-left.
            Y = upsideDown ? headY : tailY - tailHeight;
            Height = Math.Max(
                minimumHeight,
                Math.Abs(headY - tailY) + tailHeight);
            updateHoldBody();
            Alpha = tailProgress >= -0.08 && headProgress <= 1.22 ? 1 : 0;
            return;
        }

        double progress = 1 - (hitObject.StartTimeMilliseconds - gameplayTimeMilliseconds) / approachTimeMilliseconds;
        float anchorY = topY + (float)(progress * travel);
        Y = upsideDown ? anchorY : anchorY - minimumHeight;
        Height = minimumHeight;
        Alpha = progress is >= -0.08 and <= 1.22 ? 1 : 0;
    }

    private void updateHoldBody()
    {
        float bodyY = upsideDown ? headHeight : tailHeight;
        float bodyHeight = Math.Max(0, Height - headHeight - tailHeight);

        if (holdBodyClip != null)
        {
            holdBodyClip.Y = bodyY;
            holdBodyClip.Height = bodyHeight;

            bool repeatBody = noteBodyStyle != 0 && bodyTextureHeight < bodyHeight;
            bool alignToHead = noteBodyStyle == 2 || noteBodyStyle == 3;

            if (repeatBody)
            {
                bool alignTextureEnd = alignToHead ^ flipHoldBody;
                float textureOffset = alignTextureEnd
                    ? bodyHeight - MathF.Ceiling(bodyHeight / bodyTextureHeight) * bodyTextureHeight
                    : 0;

                holdBody.Position = new Vector2(Width / 2, bodyHeight / 2);
                holdBody.Size = new Vector2(Width, bodyHeight);
                holdBody.TextureRelativeSizeAxes = Axes.None;
                holdBody.TextureRectangle = new RectangleF(
                    0,
                    textureOffset,
                    Width,
                    bodyTextureHeight);
            }
            else
            {
                float textureHeight = noteBodyStyle == 0 ? bodyHeight : bodyTextureHeight;
                float textureTop = alignToHead ? bodyHeight - textureHeight : 0;

                if (flipHoldBody)
                    textureTop = bodyHeight - textureTop - textureHeight;

                holdBody.Position = new Vector2(Width / 2, textureTop + textureHeight / 2);
                holdBody.Size = new Vector2(Width, textureHeight);
                holdBody.TextureRelativeSizeAxes = Axes.Both;
                holdBody.TextureRectangle = new RectangleF(0, 0, 1, 1);
            }

            holdBody.Scale = new Vector2(1, flipHoldBody ? -1 : 1);
        }

        if (fallbackBody != null && hitObject.Kind == HitObjectKind.Hold)
        {
            fallbackBody.Y = bodyY;
            fallbackBody.Height = bodyHeight;
        }

        float headY = upsideDown ? 0 : Height - headHeight;
        float tailY = upsideDown ? Height - tailHeight : 0;
        placePart(
            holdHead,
            headY,
            headHeight,
            flipHoldHead);
        placePart(
            holdTail,
            tailY,
            tailHeight,
            flipHoldTail);
    }

    private void placePart(Sprite sprite, float y, float height, bool flip)
    {
        if (sprite == null)
            return;

        sprite.Position = new Vector2(Width / 2, y + height / 2);
        sprite.Size = new Vector2(Width, height);
        sprite.Scale = new Vector2(1, flip ? -1 : 1);
    }

    private static float scaledHeight(Texture texture, float widthForHeightScale, float fallback)
    {
        if (texture == null || texture.DisplayWidth <= 0)
            return fallback;

        return Math.Max(1, texture.DisplayHeight * widthForHeightScale / texture.DisplayWidth);
    }
}
