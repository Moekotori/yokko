using System;
using System.Collections.Generic;
using System.Linq;
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
    private bool activeNoteLayerReady;
    private readonly float topY;
    private readonly float judgementY;
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
    private readonly Sprite[] warningArrows = [];
    private readonly double firstObjectTime;
    private readonly OsuManiaSkinOverlay[] skinOverlays = [];
    private readonly bool separateSkinScore;
    private readonly ManiaModSet mods;
    private readonly bool receptorAtBottom;
    private readonly bool skinUpsideDown;
    private readonly ManiaNoteVisibilityCover noteVisibilityCover;
    private readonly ManiaFlashlightOverlay flashlightOverlay;
    private ManiaVisibilityPolicy visibilityPolicy;

    internal float ScrollOrigin => topY;

    internal float JudgementPosition => judgementY;

    internal double ApproachTimeMilliseconds => approachTimeMilliseconds;

    internal int VisibleNoteCount => visibleNoteIndices.Count;

    internal int ActiveDrawableNoteCount => noteLayer.Count;

    internal int LastHoldRangeNodeVisits { get; private set; }

    internal int KeyCount => laneColumns.Length;

    internal DrawableNote GetDrawableNote(int index) => noteDrawables[index];

    internal double GetNoteStartScrollPosition(int index) =>
        noteUpdateStates[index].StartPosition;

    internal ManiaVisibilityPolicy VisibilityPolicy => visibilityPolicy;

    internal bool UsesSkinJudgementOverlay => skinOverlays.Length > 0;

    internal bool ShowsSkinJudgementLine => skinJudgementLines.Length > 0;

    internal bool HasSkinStageBottom => stageBottomSprites.Length > 0;

    internal int SkinStageBottomCount => stageBottomSprites.Length;

    internal int SkinStageHintCount => stageHintSprites.Length;

    internal int SkinJudgementLineCount => skinJudgementLines.Length;

    internal int SkinWarningArrowCount => warningArrows.Length;

    internal int SkinBarLineCount => barLines?.Length ?? 0;

    internal bool ConstantSpeedEnabled =>
        mods.Contains(ManiaModId.ConstantSpeed);

    internal GameplayPlayfield(
        YokkoBeatmap beatmap,
        KeyModeBindings keyBindings,
        OsuManiaSkin skin = null,
        double approachTimeMilliseconds = 1800,
        bool showLanePressFeedback = true,
        ManiaModSet mods = null,
        bool showMines = true)
    {
        this.mods = mods ?? ManiaModSet.Empty;
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
        separateSkinScore = configuration?.SeparateScore ?? true;
        skinUpsideDown = configuration?.UpsideDown == true;
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

        topY = activeSkin == null
            ? 28
            : configuration.UpsideDown ? 480 : 0;
        judgementY = activeSkin == null
            ? 528
            : configuration.UpsideDown
                ? 480 - Math.Clamp(configuration.HitPosition, 0, 480)
                : Math.Clamp(configuration.HitPosition, 0, 480);
        receptorAtBottom = configuration?.UpsideDown != true;
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
                                        showLanePressFeedback)
                                    {
                                        X = x,
                                        Width = width,
                                    };
                                    column.ReceptorLayer.X = x;
                                    return column;
                                })
                                .ToArray();

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
                beatmap.LegacyLongNoteRendering)
            {
                X = x,
                Alpha = 0,
                // Preserve beatmap ordering when notes leave and re-enter
                // the active scene graph after seeks.
                Depth = -index,
            };
        }).ToArray();
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
        Array.Fill(noteAttached, true);
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
                                   activeIndices,
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
                                               .VisibilityRange))));
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
            Children = noteDrawables,
        };
        visibilityPolicy =
            ManiaVisibilityPolicyResolver.Resolve(this.mods, 0);
        Drawable noteContent = noteLayer;
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
                    float leftWidth = stageLeft.DisplayHeight > 0
                        ? stageLeft.DisplayWidth * 480 / stageLeft.DisplayHeight
                        : 1;
                    stageSides.Add(new Sprite
                    {
                        Origin = Anchor.TopRight,
                        X = stageX + 0.05f,
                        Size = new Vector2(leftWidth, 480),
                        Texture = stageLeft,
                    });
                }

                if (stageRight == null)
                    continue;

                float rightWidth =
                    stageRight.DisplayHeight > 0
                        ? stageRight.DisplayWidth * 480 / stageRight.DisplayHeight
                        : 1;
                stageSides.Add(new Sprite
                {
                    Origin = Anchor.TopLeft,
                    X = stageX + stageWidth - 0.05f,
                    Size = new Vector2(rightWidth, 480),
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
            children.Add(new Box
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
                        Anchor = configuration.UpsideDown
                            ? Anchor.TopLeft
                            : Anchor.BottomLeft,
                        Origin = configuration.UpsideDown
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
                                       stageHint.DisplayWidth > 0
                                           ? stageHint.DisplayHeight
                                             * segment.Width
                                             / stageHint.DisplayWidth
                                             * 0.9f
                                             * 1.6025f
                                           : 1)
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
            children.Add(new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 4,
                Y = judgementY,
                Colour = YokkoPalette.Rose,
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
        if (warningArrow != null && firstObjectTime >= 1000)
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
                    configuration.UpsideDown ? -1 : 1),
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
                Y = judgementY - 36,
                Colour = new Color4(1f, 1f, 1f, 0.18f),
            });
        }
        else
        {
            skinOverlays = stageSegments
                           .Select(segment => new OsuManiaSkinOverlay(
                               activeSkin)
                           {
                               X = segment.X,
                               Width = segment.Width,
                           })
                           .ToArray();
            children.AddRange(skinOverlays);
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

    protected override void LoadComplete()
    {
        base.LoadComplete();

        // Follow lazer's lifetime-managed hit object model: preload every
        // drawable, then keep only the current visibility window attached to
        // the live scene graph.
        noteLayer.Clear(false);
        Array.Fill(noteAttached, false);
        activeNoteLayerReady = true;

        foreach (int index in visibleNoteIndices)
            attachNote(index);
    }

    public void SetLanePressed(int lane, bool pressed)
    {
        if ((uint)lane >= laneColumns.Length)
            return;

        laneColumns[lane].SetPressed(pressed);
    }

    public void ApplyJudgement(JudgementEvent judgement)
    {
        if (judgement.Phase is JudgementPhase.Hold
            or JudgementPhase.HoldBody)
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

    public void SetApproachTime(double value) =>
        approachTimeMilliseconds = Math.Max(1, value);

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
                (skinUpsideDown ? -1 : 1) * value);
        }

        foreach (LegacyManiaBarLine barLine in barLines)
            barLine.SetWidthScale(value);

        for (int i = 0; i < skinOverlays.Length; i++)
        {
            skinOverlays[i].X = baseStageXs[i] * value;
            skinOverlays[i].Width = baseStageWidths[i] * value;
            skinOverlays[i].SetPlayfieldScale(value);
        }
    }

    public void UpdateGameplayTime(double gameplayTimeMilliseconds, BeatmapJudgementState state)
    {
        foreach (OsuManiaSkinOverlay overlay in skinOverlays)
            overlay.SetCombo(state.Combo);
        foreach (Sprite warningArrow in warningArrows)
            warningArrow.Alpha =
                gameplayTimeMilliseconds < firstObjectTime ? 1 : 0;
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

            if (Math.Abs(scrollSpeedFactor) < double.Epsilon)
            {
                foreach (int index in group.AllIndices)
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
        var timingMap = new BeatTimingMap(beatmap.TimingPoints);
        double lastObjectTime = beatmap.HitObjects.Count == 0
            ? 0
            : beatmap.HitObjects.Max(hitObject =>
                hitObject.EndTimeMilliseconds
                ?? hitObject.StartTimeMilliseconds);
        int finalRow = timingMap.ClosestRowAt(lastObjectTime)
                       + timingMap.BeatDivisor
                       * Math.Max(
                           1,
                           timingMap.TimingPointAt(lastObjectTime).Meter);
        var result = new List<LegacyManiaBarLine>();

        for (int row = 0; row <= finalRow; row++)
        {
            if (!timingMap.IsMeasureRow(row))
                continue;

            double time = timingMap.TimeAtRow(row);
            result.Add(new LegacyManiaBarLine(
                time,
                velocityMap,
                stageSegments,
                configuration.BarLineHeight
                / OsuManiaSkinConfiguration.LegacyPositionScaleFactor,
                configuration.BarLineColour));
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
        int[] allIndices,
        int[] tapIndices,
        ScrollRangeIndex holdRangeIndex)
    {
        internal ScrollVelocityMap Velocity { get; } = velocity;

        internal ScrollSpeedFactorMap Factor { get; } = factor;

        internal int[] AllIndices { get; } = allIndices;

        internal int[] TapIndices { get; } = tapIndices;

        internal ScrollRangeIndex HoldRangeIndex { get; } =
            holdRangeIndex;

        internal List<int> HoldCandidates { get; } = new();
    }
}
