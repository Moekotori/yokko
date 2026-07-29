using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly LegacyManiaAnimatedSprite holdBody;
    private readonly LegacyManiaAnimatedSprite holdHead;
    private readonly LegacyManiaAnimatedSprite holdTail;
    private readonly Container mineVisual;
    private readonly float baseWidth;
    private float baseHeadHeight;
    private float baseTailHeight;
    private float baseBodyTextureHeight;
    private float baseMinimumHeight;
    private readonly int noteBodyStyle;
    private readonly bool upsideDown;
    private readonly bool flipHoldHead;
    private readonly bool flipHoldBody;
    private readonly bool flipHoldTail;
    private readonly bool legacyLongNoteRendering;
    private bool reverseHoldTailForScrollVelocity;
    private float holdBodyY;
    private float holdBodyHeight;
    private float holdHeadY;
    private float holdTailY;
    private float columnScale = 1;
    private bool resolved;
    private bool bodyAnimationActive;

    private float headHeight => baseHeadHeight * columnScale;

    private float tailHeight => baseTailHeight * columnScale;

    private float bodyTextureHeight => baseBodyTextureHeight * columnScale;

    private float minimumHeight => baseMinimumHeight * columnScale;

    internal DrawableNote(
        int hitObjectIndex,
        YokkoHitObject hitObject,
        float laneWidth,
        OsuManiaSkin skin = null,
        bool legacyLongNoteRendering = false)
    {
        HitObjectIndex = hitObjectIndex;
        this.hitObject = hitObject;
        this.legacyLongNoteRendering = legacyLongNoteRendering;
        baseWidth = laneWidth;
        Width = laneWidth;

        if (hitObject.Kind == HitObjectKind.Mine)
        {
            float mineSize = Math.Clamp(laneWidth * 0.72f, 18, 42);
            Height = baseMinimumHeight = mineSize;
            mineVisual = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(mineSize),
                Masking = true,
                CornerRadius = mineSize / 2,
                Children = new Drawable[]
                {
                    new Circle
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = YokkoPalette.Rose,
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(mineSize * 0.55f),
                        Icon = FontAwesome.Solid.Bomb,
                        Colour = Color4.White,
                    },
                },
            };
            InternalChild = mineVisual;
            return;
        }

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
        OsuManiaSkinConfiguration fallback =
            skin.FallbackConfiguration;
        int lane = hitObject.Lane;
        upsideDown = configuration.UpsideDown;

        if (hitObject.Kind == HitObjectKind.Tap)
        {
            IReadOnlyList<Texture> noteFrames =
                skin.GetAnimationFrames(
                    configuration.NoteImages[lane],
                    fallback.NoteImages[lane]);
            Texture noteTexture = noteFrames.FirstOrDefault();

            if (noteTexture != null)
            {
                Height = baseMinimumHeight = scaledHeight(
                    noteTexture,
                    configuration.WidthForNoteHeightScale,
                    24);
                var noteSprite = new LegacyManiaAnimatedSprite(noteFrames)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Scale = new Vector2(
                        1,
                        upsideDown && configuration.NoteFlipWhenUpsideDown[lane] ? -1 : 1),
                };
                noteSprite.FrameChanged += texture =>
                {
                    baseMinimumHeight = scaledHeight(
                        texture,
                        configuration.WidthForNoteHeightScale,
                        24);
                    Height = minimumHeight;
                };
                InternalChild = noteSprite;
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
        IReadOnlyList<Texture> tapFrames =
            skin.GetAnimationFrames(
                configuration.NoteImages[lane],
                fallback.NoteImages[lane]);
        IReadOnlyList<Texture> headFrames =
            skin.GetAnimationFrames(
                configuration.HoldHeadImages[lane],
                fallback.HoldHeadImages[lane]);
        if (headFrames.Count == 0)
            headFrames = tapFrames;
        IReadOnlyList<Texture> bodyFrames = skin.GetAnimationFrames(
            configuration.HoldBodyImages[lane],
            fallback.HoldBodyImages[lane],
            // Legacy style 1 repeats the complete image. Styles 2-4 extend
            // edge rows and therefore require clamp-to-edge sampling.
            repeatVertically: noteBodyStyle == 1);
        IReadOnlyList<Texture> tailFrames =
            skin.GetAnimationFrames(
                configuration.HoldTailImages[lane],
                fallback.HoldTailImages[lane]);
        if (tailFrames.Count == 0)
            tailFrames = headFrames.Count > 0 ? headFrames : tapFrames;
        Texture headTexture = headFrames.FirstOrDefault();
        Texture bodyTexture = bodyFrames.FirstOrDefault();
        Texture tailTexture = tailFrames.FirstOrDefault();

        baseHeadHeight = scaledHeight(headTexture, configuration.WidthForNoteHeightScale, 12);
        baseTailHeight = scaledHeight(tailTexture, configuration.WidthForNoteHeightScale, baseHeadHeight);
        baseBodyTextureHeight = scaledHeight(bodyTexture, configuration.WidthForNoteHeightScale, 1);
        flipHoldHead = upsideDown && configuration.HoldHeadFlipWhenUpsideDown[lane];
        flipHoldBody = upsideDown && configuration.HoldBodyFlipWhenUpsideDown[lane];
        // The release end uses the opposite scrolling direction to the head
        // in osu!stable. Upside-down flip settings then toggle that baseline.
        flipHoldTail = true
                       ^ (upsideDown
                          && configuration.HoldTailFlipWhenUpsideDown[lane]);
        Height = baseMinimumHeight = Math.Max(24, baseHeadHeight + baseTailHeight);
        Masking = true;

        var children = new System.Collections.Generic.List<Drawable>();

        if (bodyTexture != null)
        {
            holdBody = new LegacyManiaAnimatedSprite(bodyFrames, 30)
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                Width = laneWidth,
                IsPlaying = false,
            };
            holdBody.FrameChanged += texture =>
            {
                baseBodyTextureHeight = scaledHeight(
                    texture,
                    configuration.WidthForNoteHeightScale,
                    1);
                updateHoldBody();
            };
            children.Add(holdBodyClip = new Container
            {
                Width = laneWidth,
                Masking = true,
                Child = holdBody,
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
            holdTail = new LegacyManiaAnimatedSprite(tailFrames)
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
            };
            holdTail.FrameChanged += texture =>
            {
                baseTailHeight = scaledHeight(
                    texture,
                    configuration.WidthForNoteHeightScale,
                    baseHeadHeight);
                updateHoldBody();
            };
            children.Add(holdTail);
        }

        if (headTexture != null)
        {
            holdHead = new LegacyManiaAnimatedSprite(headFrames)
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
            };
            holdHead.FrameChanged += texture =>
            {
                baseHeadHeight = scaledHeight(
                    texture,
                    configuration.WidthForNoteHeightScale,
                    12);
                updateHoldBody();
            };
            children.Add(holdHead);
        }

        InternalChildren = children.ToArray();
        holdBodyY = upsideDown ? headHeight : tailHeight;
        holdBodyHeight = Math.Max(0, Height - headHeight - tailHeight);
        holdHeadY = upsideDown ? 0 : Height - headHeight;
        holdTailY = upsideDown ? Height - tailHeight : 0;
        updateHoldBody();
    }

    public int HitObjectIndex { get; }

    internal bool ReverseHoldTailForScrollVelocity =>
        reverseHoldTailForScrollVelocity;

    internal bool IsMine => hitObject.Kind == HitObjectKind.Mine;

    public void SetColumnScale(float value)
    {
        value = Math.Max(0.01f, value);
        columnScale = value;
        Width = baseWidth * value;

        if (hitObject.Kind is HitObjectKind.Tap
            or HitObjectKind.Mine)
        {
            Height = minimumHeight;
            if (mineVisual != null)
                mineVisual.Scale = new Vector2(value);
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

        if (judgement.Phase is JudgementPhase.Tap
            or JudgementPhase.HoldTail
            or JudgementPhase.Mine)
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
                ? legacyLongNoteRendering
                    ? positionRange(startPosition, endPosition)
                    : scrollVelocityMap.PositionRangeBetween(
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
        if (holdBody != null && bodyAnimationActive != holdActive)
        {
            bodyAnimationActive = holdActive;
            holdBody.IsPlaying = holdActive;

            if (!holdActive)
                holdBody.GotoFrame(0);
        }

        if (resolved || resolvedByState)
        {
            Alpha = Math.Min(Alpha, 0.18f);
            return;
        }

        float travel = judgementY - topY;

        if (hitObject.Kind == HitObjectKind.Hold && hitObject.EndTimeMilliseconds is double endTime)
        {
            reverseHoldTailForScrollVelocity =
                scrollVelocityMap.IsNegativeDirectionAt(endTime);
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
                ? legacyLongNoteRendering
                    ? positionRange(currentPosition, endPosition)
                    : scrollVelocityMap.PositionRangeBetween(
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

    private static ScrollPositionRange positionRange(
        double first,
        double second) =>
        new(Math.Min(first, second), Math.Max(first, second));

    private void updateHoldBody()
    {
        if (holdBodyClip != null)
        {
            holdBodyClip.Y = holdBodyY;
            holdBodyClip.Height = holdBodyHeight;

            holdBody.Position = new Vector2(
                Width / 2,
                holdBodyHeight / 2);
            holdBody.Size = new Vector2(Width, holdBodyHeight);

            if (noteBodyStyle == 0)
            {
                // Stretch the full texture over the complete body.
                holdBody.TextureRelativeSizeAxes = Axes.Both;
                holdBody.TextureRectangle =
                    new RectangleF(0, 0, 1, 1);
            }
            else
            {
                float textureWidth =
                    holdBody.Texture?.DisplayWidth ?? Width;
                float textureY = noteBodyStyle switch
                {
                    // Repeat the complete texture.
                    1 => 0,
                    // Keep the image at the bottom and extend its top row.
                    2 => bodyTextureHeight - holdBodyHeight,
                    // Keep the image at the top and extend its bottom row.
                    3 => 0,
                    // Keep the image centred and extend both edge rows.
                    4 => (bodyTextureHeight - holdBodyHeight) / 2,
                    _ => 0,
                };

                holdBody.TextureRelativeSizeAxes = Axes.None;
                holdBody.TextureRectangle = new RectangleF(
                    0,
                    textureY,
                    textureWidth,
                    holdBodyHeight);
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
            flipHoldTail ^ reverseHoldTailForScrollVelocity);
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
