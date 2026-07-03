using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Beatmaps;
using Yokko.Core.Scoring;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

public partial class GameplayPlayfield : CompositeDrawable
{
    private const double approachTimeMilliseconds = 1800;
    private const float topY = 28;
    private const float judgementY = 528;

    private readonly LaneColumn[] laneColumns;
    private readonly DrawableNote[] noteDrawables;

    public GameplayPlayfield(YokkoBeatmap beatmap, KeyModeBindings keyBindings)
    {
        int keyCount = keyBindings.KeyCount;
        float playfieldWidth = keyCount == 4 ? 424 : 658;
        float laneWidth = playfieldWidth / keyCount;

        Size = new Vector2(playfieldWidth, 620);
        Masking = true;

        laneColumns = Enumerable.Range(0, keyCount)
                                .Select(lane => new LaneColumn(keyBindings.GetDisplayKey(lane))
                                {
                                    X = lane * laneWidth,
                                    Width = laneWidth,
                                })
                                .ToArray();

        noteDrawables = beatmap.HitObjects.Select((hitObject, index) => new DrawableNote(index, hitObject)
        {
            X = hitObject.Lane * laneWidth + 8,
            Width = laneWidth - 16,
        }).ToArray();

        InternalChildren = new Drawable[]
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
            new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 4,
                Y = judgementY,
                Colour = YokkoPalette.Rose,
            },
            new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Y = judgementY - 36,
                Colour = new Color4(1f, 1f, 1f, 0.18f),
            },
        };
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

        noteDrawables[judgement.HitObjectIndex].Resolve(judgement.Rating);
    }

    public void UpdateGameplayTime(double gameplayTimeMilliseconds, BeatmapJudgementState state)
    {
        for (int i = 0; i < noteDrawables.Length; i++)
            noteDrawables[i].UpdatePosition(gameplayTimeMilliseconds, state.IsResolved(i), topY, judgementY, approachTimeMilliseconds);
    }
}
