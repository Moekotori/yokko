using System;
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
using Yokko.Game.Presentation;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Screens.Gameplay;

public partial class GameplayPlayfield : CompositeDrawable
{
    private const double approachTimeMilliseconds = 1800;
    private readonly LaneColumn[] laneColumns;
    private readonly DrawableNote[] noteDrawables;
    private readonly float topY;
    private readonly float judgementY;

    internal GameplayPlayfield(
        YokkoBeatmap beatmap,
        KeyModeBindings keyBindings,
        OsuManiaSkin skin = null)
    {
        int keyCount = keyBindings.KeyCount;
        OsuManiaSkinConfiguration configuration = skin?.Configuration;
        float playfieldWidth = configuration?.PlayfieldWidth ?? (keyCount == 4 ? 424 : 658);
        float laneWidth = skin == null ? playfieldWidth / keyCount : 0;

        topY = skin == null ? 28 : 0;
        judgementY = skin == null ? 528 : Math.Clamp(configuration.HitPosition, 0, 480);
        Size = new Vector2(playfieldWidth, skin == null ? 620 : 480);
        Masking = true;

        laneColumns = Enumerable.Range(0, keyCount)
                                .Select(lane =>
                                {
                                    float width = configuration?.ColumnWidths[lane] ?? laneWidth;
                                    return new LaneColumn(lane, keyBindings.GetDisplayKey(lane), width, skin)
                                    {
                                        X = configuration?.GetLaneX(lane) ?? lane * laneWidth,
                                        Width = width,
                                    };
                                })
                                .ToArray();

        noteDrawables = beatmap.HitObjects.Select((hitObject, index) =>
        {
            float width = configuration?.ColumnWidths[hitObject.Lane] ?? laneWidth - 16;
            return new DrawableNote(index, hitObject, width, skin)
            {
                X = configuration?.GetLaneX(hitObject.Lane) ?? hitObject.Lane * laneWidth + 8,
            };
        }).ToArray();

        var children = new System.Collections.Generic.List<Drawable>
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.018f, 0.022f, 0.032f, 0.94f),
            },
            new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = laneColumns,
            },
            new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = noteDrawables,
            },
        };

        Texture stageHint = skin?.GetTexture(configuration.StageHint);

        if (stageHint != null)
        {
            children.Add(new Sprite
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.X,
                Width = 1,
                Height = stageHint.DisplayWidth > 0
                    ? stageHint.DisplayHeight * playfieldWidth / stageHint.DisplayWidth
                    : 1,
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
        if ((uint)judgement.HitObjectIndex >= noteDrawables.Length)
            return;

        noteDrawables[judgement.HitObjectIndex].ApplyJudgement(judgement);
    }

    public void UpdateGameplayTime(double gameplayTimeMilliseconds, BeatmapJudgementState state)
    {
        for (int i = 0; i < noteDrawables.Length; i++)
            noteDrawables[i].UpdatePosition(
                gameplayTimeMilliseconds,
                state.IsResolved(i),
                state.IsHoldActive(i),
                topY,
                judgementY,
                approachTimeMilliseconds);
    }
}
