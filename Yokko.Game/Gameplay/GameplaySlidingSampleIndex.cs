using System;
using System.Collections.Generic;
using Yokko.Core.Beatmaps;

namespace Yokko.Game.Gameplay;

/// <summary>
/// Pre-indexes the small subset of objects which can own sliding samples so
/// input edges never scan the full beatmap.
/// </summary>
internal sealed class GameplaySlidingSampleIndex
{
    private readonly int[][] objectIndicesByLane;

    internal GameplaySlidingSampleIndex(
        YokkoBeatmap beatmap,
        int laneCount)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        if (laneCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(laneCount));

        var indices = new List<int>[laneCount];
        for (int lane = 0; lane < laneCount; lane++)
            indices[lane] = [];

        for (int index = 0; index < beatmap.HitObjects.Count; index++)
        {
            YokkoHitObject hitObject = beatmap.HitObjects[index];
            if (hitObject.PlaySlidingSamples
                && (uint)hitObject.Lane < indices.Length)
            {
                indices[hitObject.Lane].Add(index);
            }
        }

        objectIndicesByLane = new int[laneCount][];
        for (int lane = 0; lane < laneCount; lane++)
            objectIndicesByLane[lane] = indices[lane].ToArray();
    }

    internal int[] GetObjectIndices(int lane) =>
        (uint)lane < objectIndicesByLane.Length
            ? objectIndicesByLane[lane]
            : [];
}
