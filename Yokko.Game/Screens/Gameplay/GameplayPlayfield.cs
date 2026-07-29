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
    private readonly ScrollVelocityMap[] noteScrollVelocityMaps;
    private readonly ScrollSpeedFactorMap[] noteScrollSpeedFactorMaps;
    private readonly float topY;
    private readonly float judgementY;
    private readonly float basePlayfieldWidth;
    private readonly float[] baseLaneXs;
    private readonly float[] baseNoteXs;
    private readonly Sprite stageHintSprite;
    private readonly float baseStageHintHeight;
    private readonly OsuManiaSkinOverlay skinOverlay;

    internal float ScrollOrigin => topY;

    internal float JudgementPosition => judgementY;

    internal double ApproachTimeMilliseconds => approachTimeMilliseconds;

    internal GameplayPlayfield(
        YokkoBeatmap beatmap,
        KeyModeBindings keyBindings,
        OsuManiaSkin skin = null,
        double approachTimeMilliseconds = 1800,
        bool showLanePressFeedback = true)
    {
        this.approachTimeMilliseconds = approachTimeMilliseconds;
        var defaultScrollVelocityMap = new ScrollVelocityMap(
            beatmap.ScrollVelocities,
            beatmap.InitialScrollVelocity);
        var defaultScrollSpeedFactorMap = new ScrollSpeedFactorMap(
            beatmap.ScrollSpeedFactors);
        Dictionary<string, (ScrollVelocityMap Velocity, ScrollSpeedFactorMap Factor)>
            profileMaps = beatmap.ScrollProfiles.ToDictionary(
                static pair => pair.Key,
                static pair => (
                    new ScrollVelocityMap(
                        pair.Value.ScrollVelocities,
                        pair.Value.InitialScrollVelocity),
                    new ScrollSpeedFactorMap(
                        pair.Value.ScrollSpeedFactors)),
                StringComparer.Ordinal);
        int keyCount = keyBindings.KeyCount;
        OsuManiaSkinConfiguration configuration = skin?.Configuration;
        float playfieldWidth = configuration?.PlayfieldWidth ?? (keyCount == 4 ? 424 : 658);
        float laneWidth = skin == null ? playfieldWidth / keyCount : 0;
        basePlayfieldWidth = playfieldWidth;
        baseLaneXs = new float[keyCount];
        baseNoteXs = new float[beatmap.HitObjects.Count];

        topY = skin == null
            ? 28
            : configuration.UpsideDown ? 480 : 0;
        judgementY = skin == null
            ? 528
            : configuration.UpsideDown
                ? 480 - Math.Clamp(configuration.HitPosition, 0, 480)
                : Math.Clamp(configuration.HitPosition, 0, 480);
        Size = new Vector2(playfieldWidth, skin == null ? 620 : 480);
        Masking = true;

        laneColumns = Enumerable.Range(0, keyCount)
                                .Select(lane =>
                                {
                                    float width = configuration?.ColumnWidths[lane] ?? laneWidth;
                                    float x = configuration?.GetLaneX(lane) ?? lane * laneWidth;
                                    baseLaneXs[lane] = x;
                                    var column = new LaneColumn(
                                        lane,
                                        keyBindings.GetDisplayKey(lane),
                                        width,
                                        skin,
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
            float x = configuration?.GetLaneX(hitObject.Lane) ?? hitObject.Lane * laneWidth + 8;
            baseNoteXs[index] = x;
            return new DrawableNote(index, hitObject, width, skin)
            {
                X = x,
            };
        }).ToArray();
        noteScrollVelocityMaps = beatmap.HitObjects.Select(hitObject =>
        {
            return hitObject.ScrollProfileId != null
                   && profileMaps.TryGetValue(
                       hitObject.ScrollProfileId,
                       out var profile)
                ? profile.Velocity
                : defaultScrollVelocityMap;
        }).ToArray();
        noteScrollSpeedFactorMaps = beatmap.HitObjects.Select(hitObject =>
        {
            return hitObject.ScrollProfileId != null
                   && profileMaps.TryGetValue(
                       hitObject.ScrollProfileId,
                       out var profile)
                ? profile.Factor
                : defaultScrollSpeedFactorMap;
        }).ToArray();

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
        var noteLayer = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = noteDrawables,
        };
        var children = new System.Collections.Generic.List<Drawable>
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.018f, 0.022f, 0.032f, 0.94f),
            },
            laneBackgroundLayer,
        };

        if (configuration?.KeysUnderNotes == true)
        {
            children.Add(receptorLayer);
            children.Add(noteLayer);
        }
        else
        {
            children.Add(noteLayer);
            children.Add(receptorLayer);
        }

        Texture stageHint = skin?.GetTexture(configuration.StageHint);

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
                Height = skin == null ? 4 : 1,
                Y = judgementY,
                Colour = configuration?.ColumnLineColour ?? YokkoPalette.Rose,
            });
        }

        if (skin == null)
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
            children.Add(skinOverlay = new OsuManiaSkinOverlay(skin));
        }

        InternalChildren = children.ToArray();
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

        for (int i = 0; i < noteDrawables.Length; i++)
            noteDrawables[i].UpdatePosition(
                gameplayTimeMilliseconds,
                state.IsResolved(i),
                state.IsHoldActive(i),
                topY,
                judgementY,
                approachTimeMilliseconds,
                noteScrollVelocityMaps[i],
                noteScrollSpeedFactorMaps[i]);
    }
}
