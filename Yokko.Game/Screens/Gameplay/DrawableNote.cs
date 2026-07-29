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
using Yokko.Core.Timing;
using Yokko.Game.Presentation;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Screens.Gameplay;

public partial class DrawableNote : CompositeDrawable
{
    internal const double MinimumVisibleProgress = -0.08;
    internal const double MaximumVisibleProgress = 1.22;

    private static readonly ScrollVelocityMap defaultScrollVelocityMap =
        new([]);
    private static readonly ScrollSpeedFactorMap defaultScrollSpeedFactorMap =
        new([]);

    private readonly YokkoHitObject hitObject;
    private readonly Box fallbackBody;
    private readonly Container holdBodyClip;
    private readonly Sprite holdBody;
    private readonly Sprite holdHead;
    private readonly Sprite holdTail;
    private readonly float baseWidth;
    private readonly float baseHeadHeight;
    private readonly float baseTailHeight;
    private readonly float baseBodyTextureHeight;
    private readonly float baseMinimumHeight;
    private readonly int noteBodyStyle;
    private readonly bool upsideDown;
    private readonly bool flipHoldHead;
    private readonly bool flipHoldBody;
    private readonly bool flipHoldTail;
    private float holdBodyY;
    private float holdBodyHeight;
    private float holdHeadY;
    private float holdTailY;
    private float columnScale = 1;
    private bool resolved;

    private float headHeight => baseHeadHeight * columnScale;

    private float tailHeight => baseTailHeight * columnScale;

    private float bodyTextureHeight => baseBodyTextureHeight * columnScale;

    private float minimumHeight => baseMinimumHeight * columnScale;

    internal DrawableNote(
        int hitObjectIndex,
        YokkoHitObject hitObject,
        float laneWidth,
        OsuManiaSkin skin = null)
    {
        HitObjectIndex = hitObjectIndex;
        this.hitObject = hitObject;
        baseWidth = laneWidth;
        Width = laneWidth;

        if (skin == null)
        {
            Height = baseMinimumHeight = 24;
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
                Height = baseMinimumHeight = scaledHeight(
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

            Height = baseMinimumHeight = 24;
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

        baseHeadHeight = scaledHeight(headTexture, configuration.WidthForNoteHeightScale, 12);
        baseTailHeight = scaledHeight(tailTexture, configuration.WidthForNoteHeightScale, baseHeadHeight);
        baseBodyTextureHeight = scaledHeight(bodyTexture, configuration.WidthForNoteHeightScale, 1);
        flipHoldHead = upsideDown && configuration.HoldHeadFlipWhenUpsideDown[lane];
        flipHoldBody = upsideDown && configuration.HoldBodyFlipWhenUpsideDown[lane];
        flipHoldTail = upsideDown && configuration.HoldTailFlipWhenUpsideDown[lane];
        Height = baseMinimumHeight = Math.Max(24, baseHeadHeight + baseTailHeight);
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
        holdBodyY = upsideDown ? headHeight : tailHeight;
        holdBodyHeight = Math.Max(0, Height - headHeight - tailHeight);
        holdHeadY = upsideDown ? 0 : Height - headHeight;
        holdTailY = upsideDown ? Height - tailHeight : 0;
        updateHoldBody();
    }

    public int HitObjectIndex { get; }

    public void SetColumnScale(float value)
    {
        value = Math.Max(0.01f, value);
        columnScale = value;
        Width = baseWidth * value;

        if (hitObject.Kind == HitObjectKind.Tap)
        {
            Height = minimumHeight;
            return;
        }

        if (holdBodyClip != null)
            holdBodyClip.Width = Width;

        if (fallbackBody != null)
            fallbackBody.Width = Width;

        updateHoldBody();
    }

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
        double approachTimeMilliseconds,
        ScrollVelocityMap scrollVelocityMap = null,
        ScrollSpeedFactorMap scrollSpeedFactorMap = null)
    {
        scrollVelocityMap ??= defaultScrollVelocityMap;
        scrollSpeedFactorMap ??= defaultScrollSpeedFactorMap;
        double scrollSpeedFactor =
            scrollSpeedFactorMap.FactorAt(gameplayTimeMilliseconds);
        double currentPosition =
            scrollVelocityMap.PositionAt(gameplayTimeMilliseconds);
        double startPosition =
            scrollVelocityMap.PositionAt(hitObject.StartTimeMilliseconds);
        double endPosition = hitObject.EndTimeMilliseconds is double endTime
            ? scrollVelocityMap.PositionAt(endTime)
            : startPosition;
        ScrollPositionRange pathRange =
            hitObject.EndTimeMilliseconds is double holdEndTime
                ? scrollVelocityMap.PositionRangeBetween(
                    hitObject.StartTimeMilliseconds,
                    holdEndTime)
                : new ScrollPositionRange(startPosition, startPosition);

        UpdatePosition(
            gameplayTimeMilliseconds,
            resolvedByState,
            holdActive,
            topY,
            judgementY,
            approachTimeMilliseconds,
            scrollVelocityMap,
            scrollSpeedFactor,
            currentPosition,
            startPosition,
            endPosition,
            pathRange);
    }

    internal void UpdatePosition(
        double gameplayTimeMilliseconds,
        bool resolvedByState,
        bool holdActive,
        float topY,
        float judgementY,
        double approachTimeMilliseconds,
        ScrollVelocityMap scrollVelocityMap,
        double scrollSpeedFactor,
        double currentPosition,
        double startPosition,
        double endPosition,
        ScrollPositionRange fullPathRange)
    {

        if (resolved || resolvedByState)
        {
            Alpha = Math.Min(Alpha, 0.18f);
            return;
        }

        float travel = judgementY - topY;

        if (hitObject.Kind == HitObjectKind.Hold && hitObject.EndTimeMilliseconds is double endTime)
        {
            double headProgress = 1 - (startPosition - currentPosition)
                / approachTimeMilliseconds
                * scrollSpeedFactor;
            double tailProgress = 1 - (endPosition - currentPosition)
                / approachTimeMilliseconds
                * scrollSpeedFactor;
            float headY = topY + (float)(headProgress * travel);
            float tailY = topY + (float)(tailProgress * travel);
            double visibleStartTime = hitObject.StartTimeMilliseconds;

            if (holdActive && gameplayTimeMilliseconds >= hitObject.StartTimeMilliseconds)
            {
                headY = judgementY;
                visibleStartTime = gameplayTimeMilliseconds;

                if (fallbackBody != null)
                    fallbackBody.Colour = YokkoPalette.Lime;
            }

            ScrollPositionRange pathRange = holdActive
                                            && gameplayTimeMilliseconds
                                            >= hitObject.StartTimeMilliseconds
                ? scrollVelocityMap.PositionRangeBetween(
                    visibleStartTime,
                    endTime)
                : fullPathRange;
            double minimumProgress =
                1 - (pathRange.Maximum - currentPosition)
                / approachTimeMilliseconds
                * scrollSpeedFactor;
            double maximumProgress =
                1 - (pathRange.Minimum - currentPosition)
                / approachTimeMilliseconds
                * scrollSpeedFactor;

            if (minimumProgress > maximumProgress)
            {
                (minimumProgress, maximumProgress) =
                    (maximumProgress, minimumProgress);
            }
            float pathY1 = topY + (float)(minimumProgress * travel);
            float pathY2 = topY + (float)(maximumProgress * travel);
            float minimumAnchorY = Math.Min(
                Math.Min(pathY1, pathY2),
                Math.Min(headY, tailY));
            float maximumAnchorY = Math.Max(
                Math.Max(pathY1, pathY2),
                Math.Max(headY, tailY));
            float partPadding = Math.Max(headHeight, tailHeight);

            // Legacy mania pieces anchor on the top edge for upscroll and
            // the bottom edge for downscroll. Direction-changing SV can put
            // either endpoint at either side, so position each part from its
            // actual integrated anchor instead of assuming head > tail.
            Y = upsideDown ? minimumAnchorY : minimumAnchorY - partPadding;
            Height = Math.Max(
                minimumHeight,
                maximumAnchorY - minimumAnchorY + partPadding);
            float headCentreY = headY
                                + (upsideDown ? headHeight : -headHeight)
                                / 2;
            float tailCentreY = tailY
                                + (upsideDown ? tailHeight : -tailHeight)
                                / 2;
            float endpointMinimumY = Math.Min(headY, tailY);
            float endpointMaximumY = Math.Max(headY, tailY);
            float bodyMinimumY = Math.Min(headCentreY, tailCentreY);
            float bodyMaximumY = Math.Max(headCentreY, tailCentreY);

            // The body must run underneath the translucent edges of the
            // head and tail. Stopping at their rectangular bounds leaves a
            // visible gap on round or arrow-shaped legacy textures.
            //
            // Preserve any additional bounds introduced by reversed SV
            // without treating the normal endpoint anchors as body edges.
            if (pathY1 < endpointMinimumY - 0.01f)
                bodyMinimumY = Math.Min(bodyMinimumY, pathY1);

            if (pathY2 < endpointMinimumY - 0.01f)
                bodyMinimumY = Math.Min(bodyMinimumY, pathY2);

            if (pathY1 > endpointMaximumY + 0.01f)
                bodyMaximumY = Math.Max(bodyMaximumY, pathY1);

            if (pathY2 > endpointMaximumY + 0.01f)
                bodyMaximumY = Math.Max(bodyMaximumY, pathY2);

            holdBodyY = bodyMinimumY - Y;
            holdBodyHeight = Math.Max(0, bodyMaximumY - bodyMinimumY);
            holdHeadY = (upsideDown ? headY : headY - headHeight) - Y;
            holdTailY = (upsideDown ? tailY : tailY - tailHeight) - Y;
            updateHoldBody();
            Alpha = maximumProgress >= MinimumVisibleProgress
                    && minimumProgress <= MaximumVisibleProgress
                ? 1
                : 0;
            return;
        }

        double progress = 1 - (startPosition - currentPosition)
            / approachTimeMilliseconds
            * scrollSpeedFactor;
        float anchorY = topY + (float)(progress * travel);
        Y = upsideDown ? anchorY : anchorY - minimumHeight;
        Height = minimumHeight;
        Alpha = progress is
            >= MinimumVisibleProgress
            and <= MaximumVisibleProgress
            ? 1
            : 0;
    }

    internal void HideOutsideVisibleRange() =>
        Alpha = 0;

    private void updateHoldBody()
    {
        if (holdBodyClip != null)
        {
            holdBodyClip.Y = holdBodyY;
            holdBodyClip.Height = holdBodyHeight;

            bool repeatBody = noteBodyStyle != 0
                              && bodyTextureHeight < holdBodyHeight;
            bool alignToHead = noteBodyStyle == 2 || noteBodyStyle == 3;

            if (repeatBody)
            {
                bool alignTextureEnd = alignToHead ^ flipHoldBody;
                float textureOffset = alignTextureEnd
                    ? holdBodyHeight
                      - MathF.Ceiling(holdBodyHeight / bodyTextureHeight)
                      * bodyTextureHeight
                    : 0;

                holdBody.Position = new Vector2(
                    Width / 2,
                    holdBodyHeight / 2);
                holdBody.Size = new Vector2(Width, holdBodyHeight);
                holdBody.TextureRelativeSizeAxes = Axes.None;
                holdBody.TextureRectangle = new RectangleF(
                    0,
                    textureOffset,
                    Width,
                    bodyTextureHeight);
            }
            else
            {
                float textureHeight = noteBodyStyle == 0
                    ? holdBodyHeight
                    : bodyTextureHeight;
                float textureTop = alignToHead
                    ? holdBodyHeight - textureHeight
                    : 0;

                if (flipHoldBody)
                {
                    textureTop =
                        holdBodyHeight - textureTop - textureHeight;
                }

                holdBody.Position = new Vector2(Width / 2, textureTop + textureHeight / 2);
                holdBody.Size = new Vector2(Width, textureHeight);
                holdBody.TextureRelativeSizeAxes = Axes.Both;
                holdBody.TextureRectangle = new RectangleF(0, 0, 1, 1);
            }

            holdBody.Scale = new Vector2(1, flipHoldBody ? -1 : 1);
        }

        if (fallbackBody != null && hitObject.Kind == HitObjectKind.Hold)
        {
            fallbackBody.Y = holdBodyY;
            fallbackBody.Height = holdBodyHeight;
        }

        placePart(
            holdHead,
            holdHeadY,
            headHeight,
            flipHoldHead);
        placePart(
            holdTail,
            holdTailY,
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
