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
    private readonly NoteUpdateState[] noteUpdateStates;
    private readonly NoteUpdateGroup[] noteUpdateGroups;
    private readonly bool[] noteAttached;
    private readonly bool[] visibleThisFrame;
    private List<int> visibleNoteIndices = new();
    private List<int> nextVisibleNoteIndices = new();
    private Container noteLayer;
    private bool activeNoteLayerReady;
    private readonly float topY;
    private readonly float judgementY;
    private readonly float basePlayfieldWidth;
    private readonly float[] baseLaneXs;
    private readonly float[] baseNoteXs;
    private readonly Sprite stageHintSprite;
    private readonly float baseStageHintHeight;
    private readonly OsuManiaSkinOverlay skinOverlay;
    private readonly ManiaModSet mods;
    private readonly bool receptorAtBottom;
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

    internal bool UsesSkinJudgementOverlay => skinOverlay != null;

    internal bool ConstantSpeedEnabled =>
        mods.Contains(ManiaModId.ConstantSpeed);

    internal GameplayPlayfield(
        YokkoBeatmap beatmap,
        KeyModeBindings keyBindings,
        OsuManiaSkin skin = null,
        double approachTimeMilliseconds = 1800,
        bool showLanePressFeedback = true,
        ManiaModSet mods = null)
    {
        this.mods = mods ?? ManiaModSet.Empty;
        this.approachTimeMilliseconds = approachTimeMilliseconds;
        bool constantSpeed =
            this.mods.Contains(ManiaModId.ConstantSpeed);
        var defaultScrollVelocityMap = constantSpeed
            ? new ScrollVelocityMap(null)
            : new ScrollVelocityMap(
                beatmap.ScrollVelocities,
                beatmap.InitialScrollVelocity);
        var defaultScrollSpeedFactorMap = new ScrollSpeedFactorMap(
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
        OsuManiaSkin activeSkin = beatmap.StageCount == 1
            ? skin
            : null;
        OsuManiaSkinConfiguration configuration =
            activeSkin?.Configuration;
        const float dualStageGap = 24;
        int keysPerStage = beatmap.KeysPerStage;
        float defaultStageWidth = keysPerStage switch
        {
            4 => 424,
            7 => 658,
            _ => 94 * keysPerStage,
        };
        float playfieldWidth = configuration?.PlayfieldWidth
                               ?? defaultStageWidth
                               * beatmap.StageCount
                               + (beatmap.StageCount - 1)
                               * dualStageGap;
        float laneWidth = activeSkin == null
            ? defaultStageWidth / keysPerStage
            : 0;
        basePlayfieldWidth = playfieldWidth;
        baseLaneXs = new float[keyCount];
        baseNoteXs = new float[beatmap.HitObjects.Count];

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
        Masking = true;

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
                               int[] holdIndices = pair.Value
                                   .Where(index =>
                                       beatmap.HitObjects[index].Kind
                                       == HitObjectKind.Hold)
                                   .ToArray();
                               return new NoteUpdateGroup(
                                   pair.Key.Velocity,
                                   pair.Key.Factor,
                                   pair.Value.ToArray(),
                                   pair.Value
                                       .Where(index =>
                                           beatmap.HitObjects[index].Kind
                                           == HitObjectKind.Tap)
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
        var receptorLayer = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = laneColumns.Select(column => column.ReceptorLayer).ToArray(),
        };
        noteLayer = new Container
        {
            RelativeSizeAxes = Axes.Both,
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
            laneBackgroundLayer,
        };
        if (beatmap.StageCount == 2)
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

        Texture stageHint =
            activeSkin?.GetTexture(configuration.StageHint);

        if (stageHint != null)
        {
            baseStageHintHeight = stageHint.DisplayWidth > 0
                ? stageHint.DisplayHeight * playfieldWidth / stageHint.DisplayWidth
                : 1;
            children.Add(stageHintSprite = new Sprite
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.X,
                Width = 1,
                Height = baseStageHintHeight,
                Y = judgementY,
                Texture = stageHint,
            });
        }
        else
        {
            children.Add(new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = activeSkin == null ? 4 : 1,
                Y = judgementY,
                Colour = configuration?.ColumnLineColour ?? YokkoPalette.Rose,
            });
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
            children.Add(
                skinOverlay = new OsuManiaSkinOverlay(activeSkin));
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

        skinOverlay?.ShowJudgement(judgement);

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

        if (stageHintSprite != null)
            stageHintSprite.Height = baseStageHintHeight * value;

        skinOverlay?.SetPlayfieldScale(value);
    }

    public void UpdateGameplayTime(double gameplayTimeMilliseconds, BeatmapJudgementState state)
    {
        skinOverlay?.SetCombo(state.Combo);
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
