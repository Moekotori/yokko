using System;
using System.Collections.Generic;
using System.Linq;
using Yokko.Core.Scoring;
using Yokko.Core.Timing;

namespace Yokko.Game.Screens.Gameplay;

/// <summary>
/// Selects the front-most unresolved object in each lane while a Quaver
/// scroll-speed factor collapses every object onto the same screen position.
/// </summary>
internal sealed class ZeroScrollVisibilityIndex
{
    internal const double FactorThreshold = 0.000001;

    private readonly Entry[][] laneEntries;
    private readonly int[] candidateByLane;
    private readonly Dictionary<int, Entry> entriesByIndex;
    private readonly ScrollRangeIndex holdTimeIndex;
    private readonly List<int> overlappingHolds = new();

    internal ZeroScrollVisibilityIndex(
        IEnumerable<Entry> source,
        int laneCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(laneCount, 1);

        Entry[] entries = source.ToArray();
        laneEntries = Enumerable.Range(0, laneCount)
                                .Select(lane => entries
                                    .Where(entry => entry.Lane == lane)
                                    .OrderBy(entry => entry.StartTime)
                                    .ThenBy(entry => entry.Index)
                                    .ToArray())
                                .ToArray();
        candidateByLane = new int[laneCount];
        entriesByIndex = entries.ToDictionary(entry => entry.Index);

        holdTimeIndex = new ScrollRangeIndex(
            entries.Where(entry => entry.EndTime.HasValue)
                   .Select(entry => (
                       entry.Index,
                       new ScrollPositionRange(
                           entry.StartTime,
                           entry.EndTime!.Value))));
    }

    internal void Collect(
        double gameplayTime,
        double lookBehindMilliseconds,
        BeatmapJudgementState state,
        List<int> destination)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(destination);

        Array.Fill(candidateByLane, -1);
        overlappingHolds.Clear();
        holdTimeIndex.CollectOverlapping(
            gameplayTime,
            gameplayTime,
            overlappingHolds);

        // A hold which began before the normal look-behind window can still
        // be active. Prefer it because earlier objects are front-most in the
        // lane when every object shares the same collapsed position.
        foreach (int index in overlappingHolds)
        {
            if (state.IsResolved(index))
                continue;

            Entry entry = entriesByIndex[index];
            int lane = entry.Lane;
            int current = candidateByLane[lane];
            if (current < 0
                || entry.StartTime < entriesByIndex[current].StartTime)
            {
                candidateByLane[lane] = index;
            }
        }

        double earliestRelevantTime = gameplayTime
                                      - Math.Max(0, lookBehindMilliseconds);
        for (int lane = 0; lane < laneEntries.Length; lane++)
        {
            if (candidateByLane[lane] >= 0)
                continue;

            Entry[] entries = laneEntries[lane];
            int start = lowerBound(entries, earliestRelevantTime);
            for (int i = start; i < entries.Length; i++)
            {
                if (state.IsResolved(entries[i].Index))
                    continue;

                candidateByLane[lane] = entries[i].Index;
                break;
            }
        }

        foreach (int index in candidateByLane)
        {
            if (index >= 0)
                destination.Add(index);
        }
    }

    private static int lowerBound(Entry[] entries, double minimumTime)
    {
        int low = 0;
        int high = entries.Length;

        while (low < high)
        {
            int middle = low + (high - low) / 2;
            if (entries[middle].StartTime < minimumTime)
                low = middle + 1;
            else
                high = middle;
        }

        return low;
    }

    internal readonly record struct Entry(
        int Index,
        int Lane,
        double StartTime,
        double? EndTime);
}
