using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Core.Timing;
using Yokko.Game.Gameplay;
using Yokko.Game.Presentation;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Screens.Gameplay;

public partial class GameplayPlayfield : CompositeDrawable
{
    private double approachTimeMilliseconds;
    private readonly LaneColumn[] laneColumns;
    private readonly DrawableNote[] noteDrawables;
    private readonly LegacyManiaBarLine[] barLines;
    private readonly ScrollVelocityMap defaultScrollVelocityMap;
    private readonly ScrollSpeedFactorMap defaultScrollSpeedFactorMap;
    private readonly NoteUpdateState[] noteUpdateStates;
    private readonly NoteUpdateGroup[] noteUpdateGroups;
    private readonly bool[] noteAttached;
    private readonly bool[] visibleThisFrame;
    private readonly int[] noteLanes;
    private readonly bool[] holdActiveLanes;
    private List<int> visibleNoteIndices = new();
    private List<int> nextVisibleNoteIndices = new();
    private Container noteLayer;
    private readonly Container layoutAutoplayDemoNoteLayer;
    private readonly DrawableNote[] layoutAutoplayDemoNotes;
    private readonly float[] baseLayoutAutoplayDemoNoteXs;
    private bool activeNoteLayerReady;
    private readonly float topY;
    private readonly float baseJudgementY;
    private float judgementY;
    private readonly float basePlayfieldWidth;
    private readonly float[] baseLaneXs;
    private readonly float[] baseNoteXs;
    private readonly float[] baseStageXs;
    private readonly float[] baseStageWidths;
    private readonly Container stageSideLayer;
    private readonly LegacyManiaAnimatedSprite[] stageBottomSprites = [];
    private readonly Sprite[] stageHintSprites = [];
    private readonly float[] baseStageHintHeights = [];
    private readonly Box[] skinJudgementLines = [];
    private readonly Box builtInJudgementLine;
    private readonly Box builtInJudgementGuide;
    private readonly Sprite[] warningArrows = [];
    private readonly double firstObjectTime;
    private readonly double warningArrowStartTime =
        double.PositiveInfinity;
    private readonly OsuManiaSkinOverlay[] skinOverlays = [];
    private readonly Box layoutTopCover;
    private readonly Box layoutBottomCover;
    private readonly bool separateSkinScore;
    private readonly ManiaModSet mods;
    private readonly bool receptorAtBottom;
    private readonly bool upscroll;
    private readonly LegacyManiaHealthBar legacyHealthBar;
    private readonly IReadOnlyList<Texture> comboBurstTextures =
        Array.Empty<Texture>();
    private readonly Container comboBurstLayer;
    private readonly int comboBurstStyle = 1;
    private readonly bool comboBurstRandom;
    private int comboBurstTextureIndex;
    private int lastComboBurstMilestone;
    private bool focusModeActive;
    private readonly ManiaNoteVisibilityCover noteVisibilityCover;
    private readonly ManiaFlashlightOverlay flashlightOverlay;
    private ManiaVisibilityPolicy visibilityPolicy;
    private double longNoteCutAmount;
    private bool layoutAutoplayDemoActive;
    private double layoutAutoplayDemoStartTime;

    private const double layoutAutoplayDemoNoteStartTime = 1100;
    private const double layoutAutoplayDemoNoteEndTime = 2800;
    private const double layoutAutoplayDemoPeriod = 3200;

    internal float ScrollOrigin => topY;

    internal float JudgementPosition => judgementY;

    internal float BaseJudgementPosition => baseJudgementY;

    internal double JudgementTravelScale =>
        Math.Abs(judgementY - topY)
        / Math.Max(1, Math.Abs(baseJudgementY - topY));

    internal bool JudgementRegionAlignedForTest
    {
        get
        {
            float offset = judgementY - baseJudgementY;
            bool lanesAligned = laneColumns.All(lane =>
                Math.Abs(lane.ReceptorLayer.Y - offset) < 0.01f);
            bool hintsAligned = stageHintSprites.All(hint =>
                Math.Abs(hint.Y - judgementY) < 0.01f);
            bool skinLinesAligned = skinJudgementLines.All(line =>
                Math.Abs(line.Y - judgementY) < 0.01f);
            bool builtInLinesAligned =
                (builtInJudgementLine == null
                 || Math.Abs(
                     builtInJudgementLine.Y - (judgementY - 1)) < 0.01f)
                && (builtInJudgementGuide == null
                    || Math.Abs(
                        builtInJudgementGuide.Y
                        - (judgementY + (upscroll ? 36 : -36))) < 0.01f);
            return lanesAligned
                   && hintsAligned
                   && skinLinesAligned
                   && builtInLinesAligned;
        }
    }

    internal double ApproachTimeMilliseconds => approachTimeMilliseconds;

    internal int VisibleNoteCount => visibleNoteIndices.Count;

    internal int ActiveDrawableNoteCount => noteLayer.Count;

    internal bool RegularNoteLayerVisible => noteLayer.Alpha > 0.5f;

    internal int LastHoldRangeNodeVisits { get; private set; }

    internal int KeyCount => laneColumns.Length;

    internal DrawableNote GetDrawableNote(int index) => noteDrawables[index];

    internal LaneColumn GetLaneColumn(int lane) => laneColumns[lane];

    internal double GetNoteStartScrollPosition(int index) =>
        noteUpdateStates[index].StartPosition;

    internal ManiaVisibilityPolicy VisibilityPolicy => visibilityPolicy;

    internal double LongNoteCutAmount => longNoteCutAmount;

    internal int LayoutAutoplayDemoLongNoteCount =>
        layoutAutoplayDemoNotes.Length;

    internal int VisibleLayoutAutoplayDemoLongNoteCount =>
        layoutAutoplayDemoNotes.Count(static note => note.Alpha > 0.5f);

    internal float LayoutAutoplayDemoLongNoteCutDistance =>
        layoutAutoplayDemoNotes.Length == 0
            ? 0
            : layoutAutoplayDemoNotes.Max(static note =>
                note.AppliedLongNoteCutDistance);

    internal bool UsesSkinJudgementOverlay => skinOverlays.Length > 0;

    internal bool SkinJudgementEditorPreviewUsesTexture =>
        skinOverlays.Length > 0
        && skinOverlays.All(overlay => overlay.EditorPreviewUsesTexture);

    internal bool SkinComboEditorPreviewVisible =>
        skinOverlays.Length > 0
        && skinOverlays.All(overlay => overlay.EditorComboPreviewVisible);

    internal Drawable SkinComboLayoutDrawable =>
        skinOverlays.FirstOrDefault()?.ComboLayoutDrawable;

    internal Drawable SkinJudgementLayoutDrawable =>
        skinOverlays.FirstOrDefault()?.JudgementLayoutDrawable;

    internal bool SkinFeedbackRendersAboveLayoutCovers =>
        skinOverlays.Length == 0
        || skinOverlays.All(overlay =>
            overlay.Depth < layoutTopCover.Depth
            && overlay.Depth < layoutBottomCover.Depth);

    internal bool HitEffectsVisibleForTest =>
        laneColumns.All(column => column.HitEffectsVisibleForTest);

    internal bool HitEffectsHiddenForTest =>
        laneColumns.All(column =>
            !column.HitEffectsVisibleForTest
            && column.HitEffectsLayerHiddenForTest);

    internal bool HitEffectLayersHiddenForTest =>
        laneColumns.All(column => column.HitEffectsLayerHiddenForTest);

    internal float LayoutTopCoverHeightForTest => layoutTopCover.Height;

    internal bool ShowsSkinJudgementLine => skinJudgementLines.Length > 0;

    internal bool HasSkinStageBottom => stageBottomSprites.Length > 0;

    internal int SkinStageBottomCount => stageBottomSprites.Length;

    internal int SkinStageHintCount => stageHintSprites.Length;

    internal float SkinStageHintHeight =>
        stageHintSprites.FirstOrDefault()?.Height ?? 0;

    internal int SkinJudgementLineCount => skinJudgementLines.Length;

    internal int SkinWarningArrowCount => warningArrows.Length;

    internal double SkinWarningArrowStartTime => warningArrowStartTime;

    internal int SkinBarLineCount => barLines?.Length ?? 0;

    internal float? SkinColumnStart { get; }

    internal float? SkinColumnRight { get; }

    internal bool HasSkinHealthBar =>
        legacyHealthBar?.IsAvailable == true;

    internal int ComboBurstCount { get; private set; }

    internal bool? LastComboBurstRightSide { get; private set; }

    internal float? LastComboBurstStartX { get; private set; }

    internal float? LastComboBurstRestX { get; private set; }

    internal bool ConstantSpeedEnabled =>
        mods.Contains(ManiaModId.ConstantSpeed);

    internal GameplayPlayfield(
        YokkoBeatmap beatmap,
        KeyModeBindings keyBindings,
        OsuManiaSkin skin = null,
        double approachTimeMilliseconds = 1800,
        bool showLanePressFeedback = true,
        ManiaModSet mods = null,
        bool showMines = true,
        bool showComboBursts = true,
        double longNoteCutAmount = 0,
        ManiaScrollDirection scrollDirection =
            ManiaScrollDirection.Downscroll,
        JudgementConfiguration? judgementConfiguration = null)
    {
        this.mods = mods ?? ManiaModSet.Empty;
        this.longNoteCutAmount = Math.Max(0, longNoteCutAmount);
        upscroll = scrollDirection == ManiaScrollDirection.Upscroll;
        this.approachTimeMilliseconds = approachTimeMilliseconds;
        bool constantSpeed =
            this.mods.Contains(ManiaModId.ConstantSpeed);
        defaultScrollVelocityMap = constantSpeed
            ? new ScrollVelocityMap(null)
            : new ScrollVelocityMap(
                beatmap.ScrollVelocities,
                beatmap.InitialScrollVelocity);
        defaultScrollSpeedFactorMap = new ScrollSpeedFactorMap(
            constantSpeed ? null : beatmap.ScrollSpeedFactors);
        Dictionary<string, (ScrollVelocityMap Velocity, ScrollSpeedFactorMap Factor)>
            profileMaps = constantSpeed
                ? new Dictionary<
                    string,
                    (ScrollVelocityMap Velocity,
                        ScrollSpeedFactorMap Factor)>(
                    StringComparer.Ordinal)
                : beatmap.ScrollProfiles.ToDictionary(
                    static pair => pair.Key,
                    static pair => (
                        new ScrollVelocityMap(
                            pair.Value.ScrollVelocities,
                            pair.Value.InitialScrollVelocity),
                        new ScrollSpeedFactorMap(
                            pair.Value.ScrollSpeedFactors)),
                    StringComparer.Ordinal);
        int keyCount = keyBindings.KeyCount;
        OsuManiaSkin activeSkin = skin;
        OsuManiaSkinConfiguration configuration =
            activeSkin?.Configuration;
        OsuManiaSkinConfiguration fallbackConfiguration =
            activeSkin?.FallbackConfiguration;
        SkinColumnStart = configuration?.ColumnStart;
        SkinColumnRight = configuration?.ColumnRight;
        separateSkinScore = configuration?.SeparateScore ?? true;
        const float dualStageGap = 24;
        int keysPerStage = beatmap.KeysPerStage;
        bool splitSkinStages = configuration != null
                               && (configuration.SplitStages
                                   ?? beatmap.StageCount == 2);
        float skinStageGap = splitSkinStages
            ? configuration.StageSeparation
            : 0;
        float defaultStageWidth = keysPerStage switch
        {
            4 => 424,
            7 => 658,
            _ => 94 * keysPerStage,
        };
        float playfieldWidth = configuration == null
            ? defaultStageWidth
              * beatmap.StageCount
              + (beatmap.StageCount - 1)
              * dualStageGap
            : configuration.PlayfieldWidth + skinStageGap;
        IReadOnlyList<(float X, float Width)> stageSegments =
            createStageSegments(
                configuration,
                keyCount,
                splitSkinStages,
                skinStageGap,
                playfieldWidth);
        baseStageXs = stageSegments.Select(segment => segment.X).ToArray();
        baseStageWidths =
            stageSegments.Select(segment => segment.Width).ToArray();
        float laneWidth = activeSkin == null
            ? defaultStageWidth / keysPerStage
            : 0;
        basePlayfieldWidth = playfieldWidth;
        baseLaneXs = new float[keyCount];
        baseNoteXs = new float[beatmap.HitObjects.Count];
        firstObjectTime = beatmap.HitObjects.Count == 0
            ? double.NegativeInfinity
            : beatmap.HitObjects.Min(hitObject =>
                hitObject.StartTimeMilliseconds);

        if (activeSkin == null)
        {
            topY = upscroll ? 592 : 28;
            judgementY = upscroll ? 92 : 528;
        }
        else
        {
            topY = upscroll ? 480 : 0;
            float hitPosition = Math.Clamp(
                configuration.HitPosition,
                0,
                480);
            judgementY = upscroll
                ? 480 - hitPosition
                : hitPosition;
        }
        receptorAtBottom = !upscroll;
        baseJudgementY = judgementY;
        Size = new Vector2(
            playfieldWidth,
            activeSkin == null ? 620 : 480);
        // Legacy stage borders are anchored outside the lane bounds. Keep the
        // playfield itself unmasked and clip only scrolling note content.
        Masking = false;

        laneColumns = Enumerable.Range(0, keyCount)
                                .Select(lane =>
                                {
                                     float width = configuration?.ColumnWidths[lane] ?? laneWidth;
                                     float x = configuration?.GetLaneX(lane)
                                               ?? lane * laneWidth
                                               + (beatmap.StageCount == 2
                                                  && lane >= keysPerStage
                                                   ? dualStageGap
                                                   : 0);
                                     if (configuration != null
                                         && splitSkinStages
                                         && lane >= keyCount / 2)
                                     {
                                         x += skinStageGap;
                                     }
                                    baseLaneXs[lane] = x;
                                    var column = new LaneColumn(
                                        lane,
                                        keyBindings.GetDisplayKey(lane),
                                        width,
                                        activeSkin,
                                        showLanePressFeedback,
                                        lane == keyCount - 1
                                        || splitSkinStages
                                        && lane == keyCount / 2 - 1,
                                        beatmap.ScratchLanes.Contains(lane),
                                        upscroll)
                                    {
                                        X = x,
                                        Width = width,
                                    };
                                    column.ReceptorLayer.X = x;
                                    return column;
                                })
                                .ToArray();

        float[] noteDepths = computeNoteDepths(beatmap.HitObjects);
        noteDrawables = beatmap.HitObjects.Select((hitObject, index) =>
        {
            float width = configuration?.ColumnWidths[hitObject.Lane] ?? laneWidth - 16;
            float x = configuration?.GetLaneX(hitObject.Lane)
                      ?? hitObject.Lane * laneWidth
                      + (beatmap.StageCount == 2
                         && hitObject.Lane >= keysPerStage
                          ? dualStageGap
                          : 0)
                      + 8;
            if (configuration != null
                && splitSkinStages
                && hitObject.Lane >= keyCount / 2)
            {
                x += skinStageGap;
            }
            baseNoteXs[index] = x;
            return new DrawableNote(
                index,
                hitObject,
                width,
                activeSkin,
                beatmap.LegacyLongNoteRendering,
                beatmap.ScratchLanes.Contains(hitObject.Lane),
                this.longNoteCutAmount,
                upscroll)
            {
                X = x,
                Alpha = 0,
                // osu!mania draws earlier hit objects on top of later ones
                // within a column, so notes covered by a long note body (or
                // stacked at the same position by SV) stay hidden behind the
                // earlier object — the "hidden note" (藏键) behaviour.
                // Reference: ppy/osu HitObjectContainer.Compare — "Put
                // earlier hitobjects towards the end of the list".
                Depth = noteDepths[index],
            };
        }).ToArray();

        int layoutDemoNoteCount = Math.Min(4, keyCount);
        layoutAutoplayDemoNotes = new DrawableNote[layoutDemoNoteCount];
        baseLayoutAutoplayDemoNoteXs = new float[layoutDemoNoteCount];
        for (int index = 0; index < layoutDemoNoteCount; index++)
        {
            int lane = layoutDemoNoteCount == 1
                ? 0
                : (int)Math.Round(
                    index * (keyCount - 1d) / (layoutDemoNoteCount - 1));
            float width = configuration?.ColumnWidths[lane]
                          ?? laneWidth - 16;
            float x = baseLaneXs[lane] + (configuration == null ? 8 : 0);
            baseLayoutAutoplayDemoNoteXs[index] = x;
            layoutAutoplayDemoNotes[index] = new DrawableNote(
                -1,
                new YokkoHitObject(
                    lane,
                    layoutAutoplayDemoNoteStartTime,
                    layoutAutoplayDemoNoteEndTime,
                    HitObjectKind.Hold),
                width,
                activeSkin,
                beatmap.LegacyLongNoteRendering,
                beatmap.ScratchLanes.Contains(lane),
                this.longNoteCutAmount,
                upscroll)
            {
                X = x,
                Alpha = 0,
            };
        }
        ScrollVelocityMap[] noteScrollVelocityMaps =
            beatmap.HitObjects
                   .Select(hitObject =>
                   {
                       return hitObject.ScrollProfileId != null
                              && profileMaps.TryGetValue(
                                  hitObject.ScrollProfileId,
                                  out var profile)
                           ? profile.Velocity
                           : defaultScrollVelocityMap;
                   })
                   .ToArray();
        ScrollSpeedFactorMap[] noteScrollSpeedFactorMaps =
            beatmap.HitObjects
                   .Select(hitObject =>
                   {
                       return hitObject.ScrollProfileId != null
                              && profileMaps.TryGetValue(
                                  hitObject.ScrollProfileId,
                                  out var profile)
                           ? profile.Factor
                           : defaultScrollSpeedFactorMap;
                   })
                   .ToArray();
        noteUpdateStates = new NoteUpdateState[noteDrawables.Length];
        noteAttached = new bool[noteDrawables.Length];
        visibleThisFrame = new bool[noteDrawables.Length];
        noteLanes = beatmap.HitObjects.Select(static hitObject => hitObject.Lane).ToArray();
        holdActiveLanes = new bool[keyCount];
        var updateGroups =
            new Dictionary<
                (ScrollVelocityMap Velocity, ScrollSpeedFactorMap Factor),
                List<int>>();

        for (int i = 0; i < noteDrawables.Length; i++)
        {
            YokkoHitObject hitObject = beatmap.HitObjects[i];
            ScrollVelocityMap velocity = noteScrollVelocityMaps[i];
            ScrollSpeedFactorMap factor = noteScrollSpeedFactorMaps[i];
            double startPosition =
                velocity.PositionAt(hitObject.StartTimeMilliseconds);
            double endPosition = hitObject.EndTimeMilliseconds is double endTime
                ? velocity.PositionAt(endTime)
                : startPosition;
            ScrollPositionRange visibilityRange =
                hitObject.EndTimeMilliseconds is double holdEndTime
                    ? velocity.PositionRangeBetween(
                        hitObject.StartTimeMilliseconds,
                        holdEndTime)
                    : new ScrollPositionRange(
                        startPosition,
                        startPosition);
            ScrollPositionRange bodyRange =
                beatmap.LegacyLongNoteRendering
                    ? positionRange(startPosition, endPosition)
                    : visibilityRange;
            noteUpdateStates[i] = new NoteUpdateState(
                startPosition,
                endPosition,
                visibilityRange,
                bodyRange);

            var key = (velocity, factor);
            if (!updateGroups.TryGetValue(key, out List<int> indices))
                updateGroups.Add(key, indices = new List<int>());

            indices.Add(i);
        }

        noteUpdateGroups = updateGroups
                           .Select(pair =>
                           {
                               int[] activeIndices = pair.Value
                                   .Where(index =>
                                       showMines
                                       || beatmap.HitObjects[index].Kind
                                       != HitObjectKind.Mine)
                                   .ToArray();
                               int[] holdIndices = activeIndices
                                   .Where(index =>
                                       beatmap.HitObjects[index].Kind
                                       == HitObjectKind.Hold)
                                   .ToArray();
                               return new NoteUpdateGroup(
                                   pair.Key.Velocity,
                                   pair.Key.Factor,
                                   activeIndices
                                       .Where(index =>
                                           beatmap.HitObjects[index].Kind
                                           is HitObjectKind.Tap
                                           or HitObjectKind.Mine)
                                       .OrderBy(index =>
                                           noteUpdateStates[index]
                                               .StartPosition)
                                       .ToArray(),
                                   new ScrollRangeIndex(
                                       holdIndices.Select(index => (
                                           index,
                                           noteUpdateStates[index]
                                               .VisibilityRange))),
                                   new ZeroScrollVisibilityIndex(
                                       activeIndices.Select(index =>
                                       {
                                           YokkoHitObject hitObject =
                                               beatmap.HitObjects[index];
                                           return new ZeroScrollVisibilityIndex.Entry(
                                               index,
                                               hitObject.Lane,
                                               hitObject.StartTimeMilliseconds,
                                               hitObject.EndTimeMilliseconds);
                                       }),
                                       keyCount));
                           })
                           .ToArray();

        var laneBackgroundLayer = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = laneColumns,
        };
        barLines = activeSkin == null || configuration.BarLineHeight <= 0
            ? []
            : createBarLines(
                beatmap,
                defaultScrollVelocityMap,
                stageSegments,
                configuration);
        var barLineLayer = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = barLines,
        };
        var receptorLayer = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = laneColumns.Select(column => column.ReceptorLayer).ToArray(),
        };
        noteLayer = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Masking = true,
        };
        layoutAutoplayDemoNoteLayer = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Masking = true,
            Alpha = 0,
            Children = layoutAutoplayDemoNotes,
        };
        visibilityPolicy =
            ManiaVisibilityPolicyResolver.Resolve(this.mods, 0);
        Drawable noteContent = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                noteLayer,
                layoutAutoplayDemoNoteLayer,
            },
        };
        if (visibilityPolicy.Mode is ManiaVisibilityMode.FadeIn
            or ManiaVisibilityMode.Hidden
            or ManiaVisibilityMode.Cover)
        {
            noteContent = noteVisibilityCover =
                new ManiaNoteVisibilityCover(noteLayer);
            applyVisibilityPolicy();
        }
        var children = new System.Collections.Generic.List<Drawable>
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.018f, 0.022f, 0.032f, 0.94f),
            },
        };

        if (activeSkin != null)
        {
            Texture stageLeft = activeSkin.GetTexture(
                configuration.StageLeft,
                fallbackConfiguration.StageLeft);
            Texture stageRight = activeSkin.GetTexture(
                configuration.StageRight,
                fallbackConfiguration.StageRight);
            var stageSides = new List<Drawable>();

            foreach ((float stageX, float stageWidth) in stageSegments)
            {
                if (stageLeft != null)
                {
                    stageSides.Add(new Sprite
                    {
                        Origin = Anchor.TopRight,
                        X = stageX + 0.05f,
                        // Legacy mania stretches stage sides vertically to
                        // the field and preserves source width in its 768px
                        // render space. Convert that width back to Yokko's
                        // 480px legacy coordinate space.
                        Size = new Vector2(
                            stageLeft.DisplayWidth
                            / OsuManiaSkinConfiguration
                                .LegacyPositionScaleFactor,
                            480),
                        Texture = stageLeft,
                    });
                }

                if (stageRight == null)
                    continue;

                stageSides.Add(new Sprite
                {
                    Origin = Anchor.TopLeft,
                    X = stageX + stageWidth - 0.05f,
                    Size = new Vector2(
                        stageRight.DisplayWidth
                        / OsuManiaSkinConfiguration
                            .LegacyPositionScaleFactor,
                        480),
                    Texture = stageRight,
                });
            }

            if (stageSides.Count > 0)
            {
                children.Add(stageSideLayer = new Container
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = playfieldWidth,
                    Children = stageSides.ToArray(),
                });
            }
        }

        children.Add(laneBackgroundLayer);
        if (barLines.Length > 0)
            children.Add(barLineLayer);

        if (beatmap.StageCount == 2 && activeSkin == null)
        {
            children.Add(builtInJudgementLine = new Box
            {
                X = defaultStageWidth,
                Width = dualStageGap,
                RelativeSizeAxes = Axes.Y,
                Colour = new Color4(
                    YokkoPalette.Cyan.R,
                    YokkoPalette.Cyan.G,
                    YokkoPalette.Cyan.B,
                    0.18f),
            });
        }

        if (configuration?.KeysUnderNotes == true)
        {
            children.Add(receptorLayer);
            children.Add(noteContent);
        }
        else
        {
            children.Add(noteContent);
            children.Add(receptorLayer);
        }

        if (activeSkin != null)
        {
            IReadOnlyList<Texture> stageBottomFrames =
                activeSkin.GetAnimationFrames(
                    configuration.StageBottom,
                    fallbackConfiguration.StageBottom);
            Texture stageBottom = stageBottomFrames.FirstOrDefault();
            if (stageBottom != null)
            {
                stageBottomSprites = stageSegments.Select(segment =>
                {
                    var sprite = new LegacyManiaAnimatedSprite(
                        stageBottomFrames)
                    {
                        Anchor = upscroll
                            ? Anchor.TopLeft
                            : Anchor.BottomLeft,
                        Origin = upscroll
                            ? Anchor.TopCentre
                            : Anchor.BottomCentre,
                        X = segment.X + segment.Width / 2,
                        Size = new Vector2(
                            stageBottom.DisplayWidth,
                            stageBottom.DisplayHeight),
                    };
                    sprite.FrameChanged += texture =>
                    {
                        sprite.Size = new Vector2(
                            texture.DisplayWidth,
                            texture.DisplayHeight);
                    };
                    return sprite;
                }).ToArray();
                children.AddRange(stageBottomSprites);
            }
        }

        Texture stageHint =
            activeSkin?.GetTexture(
                configuration.StageHint,
                fallbackConfiguration.StageHint);

        if (stageHint != null)
        {
            baseStageHintHeights = stageSegments
                                   .Select(segment =>
                                       stageHint.DisplayHeight
                                       / OsuManiaSkinConfiguration
                                           .LegacyPositionScaleFactor
                                       * 0.9f
                                       * 1.6025f)
                                   .ToArray();
            stageHintSprites = stageSegments
                               .Select((segment, index) => new Sprite
                               {
                                   Anchor = Anchor.TopLeft,
                                   Origin = Anchor.Centre,
                                   X = segment.X + segment.Width / 2,
                                   Width = segment.Width,
                                   Height = baseStageHintHeights[index],
                                   Y = judgementY,
                                   Texture = stageHint,
                               })
                               .ToArray();
            children.AddRange(stageHintSprites);
        }
        else if (activeSkin == null)
        {
            children.Add(builtInJudgementGuide = new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 2,
                Y = judgementY - 1,
                Colour = new Color4(
                    YokkoPalette.Rose.R,
                    YokkoPalette.Rose.G,
                    YokkoPalette.Rose.B,
                    0.62f),
            });
        }

        if (activeSkin != null && configuration.ShowJudgementLine)
        {
            skinJudgementLines = stageSegments.Select(segment => new Box
            {
                X = segment.X,
                Width = segment.Width,
                Height =
                    1 / OsuManiaSkinConfiguration.LegacyPositionScaleFactor,
                Y = judgementY,
                Colour =
                    LegacyManiaColourCompatibility.DisallowZeroAlpha(
                        configuration.JudgementLineColour),
                Alpha = 0.9f,
            }).ToArray();
            children.AddRange(skinJudgementLines);
        }

        Texture warningArrow =
            activeSkin?.GetTexture(
                configuration.WarningArrow,
                fallbackConfiguration.WarningArrow);
        if (warningArrow != null
            && tryGetWarningArrowStartTime(
                beatmap,
                firstObjectTime,
                out warningArrowStartTime))
        {
            warningArrows = stageSegments.Select(segment => new Sprite
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                X = segment.X + segment.Width / 2,
                Y = 240,
                Size = new Vector2(
                    warningArrow.DisplayWidth,
                    warningArrow.DisplayHeight),
                Scale = new Vector2(
                    1,
                    upscroll ? -1 : 1),
                Texture = warningArrow,
                Alpha = 0,
            }).ToArray();
            children.AddRange(warningArrows);
        }

        if (activeSkin == null)
        {
            children.Add(new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Y = judgementY + (upscroll ? 36 : -36),
                Colour = new Color4(1f, 1f, 1f, 0.18f),
            });
        }

        // Layout blockers cover the lane presentation, but skin combo and
        // judgement feedback are deliberately added in front of them below.
        children.Add(layoutTopCover = createLayoutCover(false));
        children.Add(layoutBottomCover = createLayoutCover(true));

        if (activeSkin != null)
        {
            skinOverlays = stageSegments
                           .Select(segment => new OsuManiaSkinOverlay(
                               activeSkin,
                               upscroll,
                               judgementConfiguration)
                           {
                               X = segment.X,
                               Width = segment.Width,
                               Depth = -20,
                           })
                           .ToArray();
            children.AddRange(skinOverlays);
        }

        if (activeSkin?.HasLegacyHealthBar == true)
        {
            var healthBar = new LegacyManiaHealthBar(activeSkin)
            {
                X = playfieldWidth,
                Y = 480,
            };
            if (healthBar.IsAvailable)
            {
                legacyHealthBar = healthBar;
                children.Add(legacyHealthBar);
            }
        }

        if (activeSkin != null && showComboBursts)
        {
            comboBurstTextures =
                activeSkin.GetAnimationFrames("comboburst-mania");
            comboBurstStyle = configuration.ComboBurstStyle;
            comboBurstRandom = activeSkin.Info.ComboBurstRandom;
            if (comboBurstTextures.Count > 0)
            {
                children.Add(comboBurstLayer = new Container
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = basePlayfieldWidth,
                });
            }
        }

        if (visibilityPolicy.Mode == ManiaVisibilityMode.Flashlight)
        {
            children.Add(flashlightOverlay =
                new ManiaFlashlightOverlay(
                    visibilityPolicy.FlashlightSize)
                {
                    Depth = float.MinValue,
                });
        }

        InternalChildren = children.ToArray();
    }

    internal void SetLayoutCoverRatios(double topRatio, double bottomRatio)
    {
        float topHeight = Height * (float)Math.Clamp(
            topRatio,
            0,
            YokkoGameplaySettings.MaximumTopCoverRatio);
        float bottomHeight = Height * (float)Math.Clamp(
            bottomRatio,
            0,
            YokkoGameplaySettings.MaximumBottomCoverRatio);

        layoutTopCover.Height = topHeight;
        layoutTopCover.Alpha = topHeight > 0.5f ? 1 : 0;
        layoutBottomCover.Height = bottomHeight;
        layoutBottomCover.Alpha = bottomHeight > 0.5f ? 1 : 0;
    }

    private static Box createLayoutCover(bool bottom) => new()
    {
        RelativeSizeAxes = Axes.X,
        Anchor = bottom ? Anchor.BottomLeft : Anchor.TopLeft,
        Origin = bottom ? Anchor.BottomLeft : Anchor.TopLeft,
        Colour = Color4.Black,
        Depth = -10,
    };

    protected override void LoadComplete()
    {
        base.LoadComplete();

        // Follow lazer's lifetime-managed hit object model: preload every
        // drawable off-tree, then attach only the current visibility window to
        // the live scene graph. This avoids an O(N) first-frame attach/clear
        // pass for long charts.
        activeNoteLayerReady = true;

        foreach (int index in visibleNoteIndices)
            attachNote(index);
    }

    [BackgroundDependencyLoader]
    private void loadNotes() => LoadComponents(noteDrawables);

    public void SetLanePressed(int lane, bool pressed)
    {
        if ((uint)lane >= laneColumns.Length)
            return;

        laneColumns[lane].SetPressed(pressed);
    }

    internal void SetHitEffectsVisible(bool visible)
    {
        foreach (LaneColumn column in laneColumns)
            column.SetHitEffectsVisible(visible);
    }

    public void ApplyJudgement(JudgementEvent judgement)
    {
        // lazer's Hold parent result is internal, but stable's single combined
        // LN result is also represented by this phase and must stay visible.
        if (judgement.Phase == JudgementPhase.HoldBody
            || (judgement.Phase == JudgementPhase.Hold
                && !judgement.Rating.AffectsAccuracy()))
            return;

        if (skinOverlays.Length == 1 || !separateSkinScore)
        {
            foreach (OsuManiaSkinOverlay overlay in skinOverlays)
                overlay.ShowJudgement(judgement);
        }
        else if (skinOverlays.Length > 1
                 && (uint)judgement.Lane < laneColumns.Length)
        {
            int stageIndex = judgement.Lane < laneColumns.Length / 2
                ? 0
                : 1;
            skinOverlays[stageIndex].ShowJudgement(judgement);
        }

        if ((uint)judgement.Lane < laneColumns.Length
            && judgement.Phase == JudgementPhase.Mine
            && judgement.Rating == JudgementRating.IgnoreMiss)
        {
            laneColumns[judgement.Lane].ShowMineExplosion();
        }

        if ((uint)judgement.Lane < laneColumns.Length
            && judgement.Rating is JudgementRating.Meh
                or JudgementRating.Ok
                or JudgementRating.Good
                or JudgementRating.Great
                or JudgementRating.Perfect)
        {
            laneColumns[judgement.Lane].ShowHitExplosion();
        }

        if ((uint)judgement.HitObjectIndex >= noteDrawables.Length)
            return;

        noteDrawables[judgement.HitObjectIndex].ApplyJudgement(judgement);
    }

    internal void ConfigureSkinJudgementFeedback(
        double displayDuration,
        double opacity)
    {
        foreach (OsuManiaSkinOverlay overlay in skinOverlays)
            overlay.ConfigureJudgementFeedback(displayDuration, opacity);
    }

    internal void SetSkinJudgementEditorPreview(bool preview)
    {
        foreach (OsuManiaSkinOverlay overlay in skinOverlays)
            overlay.SetEditorPreview(preview);
    }

    internal void SetSkinComboEditorPreview(bool preview)
    {
        foreach (OsuManiaSkinOverlay overlay in skinOverlays)
            overlay.SetComboEditorPreview(preview);
    }

    internal void SetSkinComboVisible(bool visible)
    {
        foreach (OsuManiaSkinOverlay overlay in skinOverlays)
            overlay.SetComboVisible(visible);
    }

    internal void SetSkinJudgementVisible(bool visible)
    {
        foreach (OsuManiaSkinOverlay overlay in skinOverlays)
            overlay.SetJudgementVisible(visible);
    }

    internal bool SkinComboVisibleForTest =>
        skinOverlays.Length > 0 && skinOverlays.All(overlay => overlay.ComboVisibleForTest);

    internal bool SkinJudgementVisibleForTest =>
        skinOverlays.Length > 0 && skinOverlays.All(overlay => overlay.JudgementVisibleForTest);

    internal void SetFocusMode(bool active)
    {
        focusModeActive = active;

        foreach (OsuManiaSkinOverlay overlay in skinOverlays)
            overlay.Alpha = active ? 0 : 1;
        if (legacyHealthBar != null)
            legacyHealthBar.Alpha = active ? 0 : 1;
        if (comboBurstLayer != null)
            comboBurstLayer.Alpha = active ? 0 : 1;
    }

    internal void SetSkinFeedbackLayout(
        Vector2 comboOffset,
        Vector2 comboScale,
        Vector2 judgementOffset,
        Vector2 judgementScale)
    {
        foreach (OsuManiaSkinOverlay overlay in skinOverlays)
        {
            overlay.SetFeedbackLayout(
                comboOffset,
                comboScale,
                judgementOffset,
                judgementScale);
        }
    }

    public void SetApproachTime(double value) =>
        approachTimeMilliseconds = Math.Max(1, value);

    internal double SetJudgementLineOffset(double normalisedOffset)
    {
        double oldDistance = Math.Max(1, Math.Abs(judgementY - topY));
        float requested = baseJudgementY
                          + (float)(normalisedOffset * Height);
        judgementY = Math.Clamp(
            requested,
            0,
            Height);
        float realisedOffset = judgementY - baseJudgementY;

        foreach (LaneColumn lane in laneColumns)
            lane.SetJudgementLineOffset(realisedOffset);
        foreach (Sprite hint in stageHintSprites)
            hint.Y = judgementY;
        foreach (Box line in skinJudgementLines)
            line.Y = judgementY;
        if (builtInJudgementLine != null)
            builtInJudgementLine.Y = judgementY - 1;
        if (builtInJudgementGuide != null)
        {
            builtInJudgementGuide.Y =
                judgementY + (upscroll ? 36 : -36);
        }

        double newDistance = Math.Max(1, Math.Abs(judgementY - topY));
        approachTimeMilliseconds *= newDistance / oldDistance;
        return realisedOffset / Math.Max(1, Height);
    }

    internal void SetLongNoteCutAmount(double value)
    {
        longNoteCutAmount = Math.Max(0, value);
        foreach (DrawableNote note in noteDrawables)
            note.SetLongNoteCutAmount(longNoteCutAmount);
        foreach (DrawableNote note in layoutAutoplayDemoNotes)
            note.SetLongNoteCutAmount(longNoteCutAmount);
    }

    internal void SetLayoutAutoplayDemo(
        bool active,
        double gameplayTimeMilliseconds)
    {
        if (active && !layoutAutoplayDemoActive)
            layoutAutoplayDemoStartTime = gameplayTimeMilliseconds;

        layoutAutoplayDemoActive = active;
        noteLayer.Alpha = active ? 0 : 1;
        layoutAutoplayDemoNoteLayer.Alpha = active ? 1 : 0;
        if (active)
            return;

        foreach (DrawableNote note in layoutAutoplayDemoNotes)
            note.HideOutsideVisibleRange();
    }

    public void SetWidthScale(float value)
    {
        value = Math.Max(0.01f, value);
        Width = basePlayfieldWidth * value;

        for (int i = 0; i < laneColumns.Length; i++)
        {
            float x = baseLaneXs[i] * value;
            laneColumns[i].X = x;
            laneColumns[i].SetWidthScale(value);
            laneColumns[i].ReceptorLayer.X = x;
        }

        for (int i = 0; i < noteDrawables.Length; i++)
        {
            noteDrawables[i].X = baseNoteXs[i] * value;
            noteDrawables[i].SetColumnScale(value);
        }

        for (int i = 0; i < layoutAutoplayDemoNotes.Length; i++)
        {
            layoutAutoplayDemoNotes[i].X =
                baseLayoutAutoplayDemoNoteXs[i] * value;
            layoutAutoplayDemoNotes[i].SetColumnScale(value);
        }

        if (stageSideLayer != null)
            stageSideLayer.Scale = new Vector2(value, 1);

        for (int i = 0; i < stageHintSprites.Length; i++)
        {
            stageHintSprites[i].X =
                (baseStageXs[i] + baseStageWidths[i] / 2) * value;
            stageHintSprites[i].Width = baseStageWidths[i] * value;
            stageHintSprites[i].Height =
                baseStageHintHeights[i] * value;
        }

        for (int i = 0; i < stageBottomSprites.Length; i++)
        {
            stageBottomSprites[i].X =
                (baseStageXs[i] + baseStageWidths[i] / 2) * value;
            stageBottomSprites[i].Scale = new Vector2(value);
        }

        for (int i = 0; i < skinJudgementLines.Length; i++)
        {
            skinJudgementLines[i].X = baseStageXs[i] * value;
            skinJudgementLines[i].Width = baseStageWidths[i] * value;
        }

        for (int i = 0; i < warningArrows.Length; i++)
        {
            warningArrows[i].X =
                (baseStageXs[i] + baseStageWidths[i] / 2) * value;
            warningArrows[i].Scale = new Vector2(
                value,
                (upscroll ? -1 : 1) * value);
        }

        foreach (LegacyManiaBarLine barLine in barLines)
            barLine.SetWidthScale(value);

        for (int i = 0; i < skinOverlays.Length; i++)
        {
            skinOverlays[i].X = baseStageXs[i] * value;
            skinOverlays[i].Width = baseStageWidths[i] * value;
            skinOverlays[i].SetPlayfieldScale(value);
        }

        if (legacyHealthBar != null)
        {
            legacyHealthBar.X = basePlayfieldWidth * value;
            legacyHealthBar.SetPlayfieldWidthScale(value);
        }

        if (comboBurstLayer != null)
            comboBurstLayer.Scale = new Vector2(value, 1);
    }

    public void UpdateGameplayTime(
        double gameplayTimeMilliseconds,
        BeatmapJudgementState state,
        ManiaHealthState healthState = null)
    {
        updateLayoutAutoplayDemoNotes(gameplayTimeMilliseconds);
        if (healthState != null)
            legacyHealthBar?.SetHealth(healthState.Health);
        updateComboBurst(state.Combo);

        foreach (OsuManiaSkinOverlay overlay in skinOverlays)
            overlay.SetCombo(state.Combo);
        foreach (Sprite warningArrow in warningArrows)
            warningArrow.Alpha =
                !focusModeActive
                && gameplayTimeMilliseconds >= warningArrowStartTime
                && gameplayTimeMilliseconds < firstObjectTime
                    ? 1
                    : 0;
        foreach (LegacyManiaBarLine barLine in barLines)
        {
            barLine.UpdatePosition(
                gameplayTimeMilliseconds,
                topY,
                judgementY,
                approachTimeMilliseconds,
                defaultScrollVelocityMap,
                defaultScrollSpeedFactorMap);
        }
        visibilityPolicy =
            ManiaVisibilityPolicyResolver.Resolve(mods, state.Combo);
        applyVisibilityPolicy();
        LastHoldRangeNodeVisits = 0;

        foreach (int index in visibleNoteIndices)
            visibleThisFrame[index] = false;

        nextVisibleNoteIndices.Clear();

        foreach (NoteUpdateGroup group in noteUpdateGroups)
        {
            double scrollSpeedFactor =
                group.Factor.FactorAt(gameplayTimeMilliseconds);
            double currentPosition =
                group.Velocity.PositionAt(gameplayTimeMilliseconds);

            if (Math.Abs(scrollSpeedFactor)
                <= ZeroScrollVisibilityIndex.FactorThreshold)
            {
                group.ZeroFactorCandidates.Clear();
                group.ZeroFactorIndex.Collect(
                    gameplayTimeMilliseconds,
                    approachTimeMilliseconds,
                    state,
                    group.ZeroFactorCandidates);
                foreach (int index in group.ZeroFactorCandidates)
                {
                    updateVisibleNote(
                        index,
                        gameplayTimeMilliseconds,
                        state,
                        group,
                        scrollSpeedFactor,
                        currentPosition);
                }

                continue;
            }

            double positionAtLowerVisibility =
                currentPosition
                + (1 - DrawableNote.MinimumVisibleProgress)
                * approachTimeMilliseconds
                / scrollSpeedFactor;
            double positionAtUpperVisibility =
                currentPosition
                + (1 - DrawableNote.MaximumVisibleProgress)
                * approachTimeMilliseconds
                / scrollSpeedFactor;
            double minimumVisiblePosition = Math.Min(
                positionAtLowerVisibility,
                positionAtUpperVisibility);
            double maximumVisiblePosition = Math.Max(
                positionAtLowerVisibility,
                positionAtUpperVisibility);

            int firstTap = lowerBoundTap(
                group.TapIndices,
                minimumVisiblePosition);
            for (int i = firstTap; i < group.TapIndices.Length; i++)
            {
                int index = group.TapIndices[i];
                if (noteUpdateStates[index].StartPosition
                    > maximumVisiblePosition)
                {
                    break;
                }

                updateVisibleNote(
                    index,
                    gameplayTimeMilliseconds,
                    state,
                    group,
                    scrollSpeedFactor,
                    currentPosition);
            }

            group.HoldCandidates.Clear();
            LastHoldRangeNodeVisits +=
                group.HoldRangeIndex.CollectOverlapping(
                    minimumVisiblePosition,
                    maximumVisiblePosition,
                    group.HoldCandidates);

            foreach (int index in group.HoldCandidates)
            {
                updateVisibleNote(
                    index,
                    gameplayTimeMilliseconds,
                    state,
                    group,
                    scrollSpeedFactor,
                    currentPosition);
            }
        }

        foreach (int index in visibleNoteIndices)
        {
            if (visibleThisFrame[index])
                continue;

            noteDrawables[index].HideOutsideVisibleRange();
            detachNote(index);
        }

        (visibleNoteIndices, nextVisibleNoteIndices) =
            (nextVisibleNoteIndices, visibleNoteIndices);

        Array.Clear(holdActiveLanes);
        foreach (int index in visibleNoteIndices)
        {
            if (state.IsHoldActive(index))
                holdActiveLanes[noteLanes[index]] = true;
        }

        for (int lane = 0; lane < laneColumns.Length; lane++)
            laneColumns[lane].SetHoldActive(holdActiveLanes[lane]);

        if (skinOverlays.Length == 1)
        {
            skinOverlays[0].SetHoldActive(
                holdActiveLanes.Any(static active => active));
        }
        else
        {
            for (int stage = 0; stage < skinOverlays.Length; stage++)
            {
                int startLane =
                    stage * laneColumns.Length / skinOverlays.Length;
                int endLane =
                    (stage + 1) * laneColumns.Length
                    / skinOverlays.Length;
                bool active = false;

                for (int lane = startLane; lane < endLane; lane++)
                    active |= holdActiveLanes[lane];

                skinOverlays[stage].SetHoldActive(active);
            }
        }
    }

    private void updateLayoutAutoplayDemoNotes(
        double gameplayTimeMilliseconds)
    {
        if (!layoutAutoplayDemoActive
            || layoutAutoplayDemoNotes.Length == 0)
        {
            return;
        }

        double elapsed = gameplayTimeMilliseconds
                         - layoutAutoplayDemoStartTime;
        double phaseSpacing = layoutAutoplayDemoPeriod
                              / layoutAutoplayDemoNotes.Length;

        for (int index = 0;
             index < layoutAutoplayDemoNotes.Length;
             index++)
        {
            double localTime = (elapsed + index * phaseSpacing)
                               % layoutAutoplayDemoPeriod;
            if (localTime < 0)
                localTime += layoutAutoplayDemoPeriod;

            bool holdActive = localTime
                              is >= layoutAutoplayDemoNoteStartTime
                              and <= layoutAutoplayDemoNoteEndTime;
            layoutAutoplayDemoNotes[index].UpdatePosition(
                localTime,
                false,
                holdActive,
                topY,
                judgementY,
                approachTimeMilliseconds);
        }
    }

    internal void ResetForReplaySeek(
        double gameplayTimeMilliseconds,
        BeatmapJudgementState state,
        ManiaHealthState healthState)
    {
        foreach (DrawableNote note in noteDrawables)
            note.ResetForReplaySeek();
        foreach (LaneColumn lane in laneColumns)
            lane.ClearTransientFeedback();
        foreach (OsuManiaSkinOverlay overlay in skinOverlays)
            overlay.ClearTransientFeedback();
        comboBurstLayer?.Clear();
        lastComboBurstMilestone = 0;
        comboBurstTextureIndex = 0;

        UpdateGameplayTime(
            gameplayTimeMilliseconds,
            state,
            healthState);
    }

    private void updateComboBurst(int combo)
    {
        if (comboBurstLayer == null)
            return;

        int milestone = Math.Max(0, combo / 100 * 100);
        if (milestone < lastComboBurstMilestone)
            lastComboBurstMilestone = milestone;

        if (milestone < 100
            || milestone <= lastComboBurstMilestone)
            return;

        lastComboBurstMilestone = milestone;
        bool rightSide = comboBurstStyle switch
        {
            0 => false,
            1 => true,
            _ => Random.Shared.Next(2) == 1,
        };
        showComboBurst(rightSide);
    }

    private void showComboBurst(bool rightSide)
    {
        int textureIndex = comboBurstRandom
            ? Random.Shared.Next(comboBurstTextures.Count)
            : comboBurstTextureIndex++ % comboBurstTextures.Count;
        Texture texture = comboBurstTextures[textureIndex];
        int stageIndex = rightSide
            ? baseStageXs.Length - 1
            : 0;
        float stageLeft = baseStageXs[stageIndex];
        float stageRight =
            stageLeft + baseStageWidths[stageIndex];
        float width =
            texture.DisplayWidth
            / OsuManiaSkinConfiguration.LegacyPositionScaleFactor;
        // osu!stable anchors combobursts at the side of the stage, not across
        // it: the character slides in from outside the stage edge, rests with
        // only a small overlap onto the stage, then slides back out the same
        // side. Right-side bursts are horizontally flipped.
        const float restOverlap = 0.12f;
        float restX = rightSide
            ? stageRight + width * (1 - restOverlap)
            : stageLeft + width * restOverlap;
        float startX = rightSide
            ? restX + width
            : restX - width;
        var burst = new Sprite
        {
            Name = "Legacy mania combo burst",
            // Origin is always the bottom-right corner so the sprite occupies
            // [X - width, X] regardless of the right-side flip.
            Origin = Anchor.BottomRight,
            Position = new Vector2(startX, 480),
            Size = new Vector2(
                width,
                texture.DisplayHeight
                / OsuManiaSkinConfiguration
                    .LegacyPositionScaleFactor),
            Scale = new Vector2(rightSide ? -1 : 1, 1),
            Texture = texture,
            Alpha = 0,
        };
        comboBurstLayer.Add(burst);
        burst.FadeIn(200);
        burst.MoveToX(restX, 650, Easing.OutQuint)
             .Delay(450)
             .MoveToX(startX, 600, Easing.InQuint);
        burst.Delay(1350)
             .FadeOut(350)
             .Expire();
        ComboBurstCount++;
        LastComboBurstRightSide = rightSide;
        LastComboBurstStartX = startX;
        LastComboBurstRestX = restX;
    }

    private void applyVisibilityPolicy()
    {
        if (noteVisibilityCover != null)
        {
            bool againstScroll =
                visibilityPolicy.CoverDirection
                == ManiaCoverDirection.AgainstScroll;
            bool coversReceptor =
                againstScroll;
            bool coversBottom = coversReceptor
                ? receptorAtBottom
                : !receptorAtBottom;
            noteVisibilityCover.SetCoverage(
                visibilityPolicy.Coverage,
                coversBottom);
        }

        flashlightOverlay?.SetWindowSize(
            visibilityPolicy.FlashlightSize);
    }

    private int lowerBoundTap(
        int[] indices,
        double minimumPosition)
    {
        int low = 0;
        int high = indices.Length;

        while (low < high)
        {
            int middle = low + (high - low) / 2;
            if (noteUpdateStates[indices[middle]].StartPosition
                < minimumPosition)
            {
                low = middle + 1;
            }
            else
                high = middle;
        }

        return low;
    }

    private void updateVisibleNote(
        int index,
        double gameplayTimeMilliseconds,
        BeatmapJudgementState state,
        NoteUpdateGroup group,
        double scrollSpeedFactor,
        double currentPosition)
    {
        if (visibleThisFrame[index])
            return;

        visibleThisFrame[index] = true;
        nextVisibleNoteIndices.Add(index);
        attachNote(index);
        NoteUpdateState updateState = noteUpdateStates[index];
        noteDrawables[index].UpdatePosition(
            gameplayTimeMilliseconds,
            state.IsResolved(index),
            state.IsHoldActive(index),
            topY,
            judgementY,
            approachTimeMilliseconds,
            group.Velocity,
            scrollSpeedFactor,
            currentPosition,
            updateState.StartPosition,
            updateState.EndPosition,
            updateState.BodyRange);
    }

    private static ScrollPositionRange positionRange(
        double first,
        double second) =>
        new(Math.Min(first, second), Math.Max(first, second));

    /// <summary>
    /// Ranks hit objects by start time so earlier objects receive the lower
    /// (front-most) depth. osu!framework draws higher-depth children first
    /// (behind), mirroring osu!mania's per-column draw order.
    /// </summary>
    private static float[] computeNoteDepths(
        IReadOnlyList<YokkoHitObject> hitObjects)
    {
        int[] order = Enumerable.Range(0, hitObjects.Count)
                                .OrderBy(index =>
                                    hitObjects[index].StartTimeMilliseconds)
                                .ThenBy(static index => index)
                                .ToArray();
        float[] depths = new float[hitObjects.Count];

        for (int rank = 0; rank < order.Length; rank++)
            depths[order[rank]] = rank;

        return depths;
    }

    private static bool tryGetWarningArrowStartTime(
        YokkoBeatmap beatmap,
        double firstObjectTimeMilliseconds,
        out double startTimeMilliseconds)
    {
        startTimeMilliseconds = double.PositiveInfinity;

        if (!double.IsFinite(firstObjectTimeMilliseconds)
            || firstObjectTimeMilliseconds <= 0)
        {
            return false;
        }

        var timingMap = new BeatTimingMap(beatmap.TimingPoints);
        int row = timingMap.ClosestRowAt(firstObjectTimeMilliseconds);

        while (row >= 0
               && timingMap.TimeAtRow(row)
               >= firstObjectTimeMilliseconds - 0.0001)
        {
            row--;
        }

        int precedingMeasureLines = 0;
        for (; row >= 0; row--)
        {
            if (!timingMap.IsMeasureRow(row))
                continue;

            double lineTime = timingMap.TimeAtRow(row);
            if (lineTime < 0)
                continue;

            precedingMeasureLines++;
            if (precedingMeasureLines != 3)
                continue;

            startTimeMilliseconds = lineTime;
            return true;
        }

        return false;
    }

    private static IReadOnlyList<(float X, float Width)>
        createStageSegments(
            OsuManiaSkinConfiguration configuration,
            int keyCount,
            bool splitStages,
            float stageGap,
            float playfieldWidth)
    {
        if (configuration == null || !splitStages || keyCount < 2)
            return [(0, playfieldWidth)];

        int secondStageLane = keyCount / 2;
        float firstStageWidth =
            configuration.GetLaneX(secondStageLane - 1)
            + configuration.ColumnWidths[secondStageLane - 1];
        float secondStageX =
            configuration.GetLaneX(secondStageLane) + stageGap;
        return
        [
            (0, firstStageWidth),
            (secondStageX, playfieldWidth - secondStageX),
        ];
    }

    private static LegacyManiaBarLine[] createBarLines(
        YokkoBeatmap beatmap,
        ScrollVelocityMap velocityMap,
        IReadOnlyList<(float X, float Width)> stageSegments,
        OsuManiaSkinConfiguration configuration)
    {
        if (beatmap.HitObjects.Count == 0)
            return [];

        return GenerateBarLineTimes(beatmap)
            .Select(time => new LegacyManiaBarLine(
                time,
                velocityMap,
                stageSegments,
                configuration.BarLineHeight
                / OsuManiaSkinConfiguration.LegacyPositionScaleFactor,
                configuration.BarLineColour))
            .ToArray();
    }

    internal static double[] GenerateBarLineTimes(YokkoBeatmap beatmap)
    {
        if (beatmap.HitObjects.Count == 0)
            return [];

        YokkoTimingPoint[] timingPoints = beatmap.TimingPoints
            .Where(static point =>
                point.Uninherited
                && double.IsFinite(point.TimeMilliseconds)
                && double.IsFinite(point.BeatLengthMilliseconds)
                && point.BeatLengthMilliseconds > 0)
            .OrderBy(static point => point.TimeMilliseconds)
            .GroupBy(static point => point.TimeMilliseconds)
            .Select(static group => group.Last())
            .ToArray();
        if (timingPoints.Length == 0)
            return [];

        double firstObjectTime = beatmap.HitObjects.Min(static hitObject =>
            hitObject.StartTimeMilliseconds);
        double lastObjectTime = beatmap.HitObjects.Max(static hitObject =>
            hitObject.EndTimeMilliseconds
            ?? hitObject.StartTimeMilliseconds);
        double generationStartTime = Math.Min(0, firstObjectTime);
        var result = new List<double>();

        for (int index = 0; index < timingPoints.Length; index++)
        {
            YokkoTimingPoint point = timingPoints[index];
            double barLength = point.BeatLengthMilliseconds
                               * Math.Max(1, point.Meter);
            double endTime = index < timingPoints.Length - 1
                ? timingPoints[index + 1].TimeMilliseconds
                : lastObjectTime + 1 + barLength;
            double startTime = point.TimeMilliseconds > generationStartTime
                ? point.TimeMilliseconds
                : point.TimeMilliseconds
                  + Math.Ceiling(
                      (generationStartTime - point.TimeMilliseconds)
                      / barLength) * barLength;

            const int omitFirstBarLineFlag = 8;
            if ((point.Effects & omitFirstBarLineFlag) != 0)
                startTime += barLength;

            for (double time = startTime;
                 time < endTime - 0.000001;
                 time += barLength)
            {
                result.Add(time);
            }
        }

        return result.ToArray();
    }

    private void attachNote(int index)
    {
        if (!activeNoteLayerReady || noteAttached[index])
            return;

        noteLayer.Add(noteDrawables[index]);
        noteAttached[index] = true;
    }

    private void detachNote(int index)
    {
        if (!activeNoteLayerReady || !noteAttached[index])
            return;

        noteLayer.Remove(noteDrawables[index], false);
        noteAttached[index] = false;
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            for (int i = 0; i < noteDrawables.Length; i++)
            {
                if (!noteAttached[i])
                    noteDrawables[i].Dispose();
            }
        }

        base.Dispose(isDisposing);
    }

    private readonly record struct NoteUpdateState(
        double StartPosition,
        double EndPosition,
        ScrollPositionRange VisibilityRange,
        ScrollPositionRange BodyRange);

    private sealed class NoteUpdateGroup(
        ScrollVelocityMap velocity,
        ScrollSpeedFactorMap factor,
        int[] tapIndices,
        ScrollRangeIndex holdRangeIndex,
        ZeroScrollVisibilityIndex zeroFactorIndex)
    {
        internal ScrollVelocityMap Velocity { get; } = velocity;

        internal ScrollSpeedFactorMap Factor { get; } = factor;

        internal int[] TapIndices { get; } = tapIndices;

        internal ScrollRangeIndex HoldRangeIndex { get; } =
            holdRangeIndex;

        internal ZeroScrollVisibilityIndex ZeroFactorIndex { get; } =
            zeroFactorIndex;

        internal List<int> HoldCandidates { get; } = new();

        internal List<int> ZeroFactorCandidates { get; } = new();
    }
}
