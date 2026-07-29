using System;
using System.Collections.Generic;
using System.Linq;
using Yokko.Core.Timing;

namespace Yokko.Game.Screens.Gameplay;

/// <summary>
/// Finds note paths intersecting a visible scroll-position range without
/// walking every earlier hold note.
/// </summary>
internal sealed class ScrollRangeIndex
{
    private readonly Entry[] entries;
    private readonly double[] subtreeMaximums;

    internal ScrollRangeIndex(
        IEnumerable<(int Index, ScrollPositionRange Range)> ranges)
    {
        entries = ranges
                  .Select(static item => new Entry(
                      item.Index,
                      Math.Min(item.Range.Minimum, item.Range.Maximum),
                      Math.Max(item.Range.Minimum, item.Range.Maximum)))
                  .OrderBy(static entry => entry.Minimum)
                  .ThenBy(static entry => entry.Maximum)
                  .ToArray();
        subtreeMaximums = new double[Math.Max(1, entries.Length * 4)];

        if (entries.Length > 0)
            build(1, 0, entries.Length);
    }

    /// <summary>
    /// Adds every indexed range intersecting <paramref name="minimum"/> to
    /// <paramref name="maximum"/> and returns the number of tree nodes visited.
    /// </summary>
    internal int CollectOverlapping(
        double minimum,
        double maximum,
        List<int> destination)
    {
        if (minimum > maximum)
            (minimum, maximum) = (maximum, minimum);

        int visitedNodes = 0;

        if (entries.Length > 0)
        {
            collectOverlapping(
                1,
                0,
                entries.Length,
                minimum,
                maximum,
                destination,
                ref visitedNodes);
        }

        return visitedNodes;
    }

    private double build(int node, int start, int end)
    {
        if (end - start == 1)
            return subtreeMaximums[node] = entries[start].Maximum;

        int middle = start + (end - start) / 2;
        return subtreeMaximums[node] = Math.Max(
            build(node * 2, start, middle),
            build(node * 2 + 1, middle, end));
    }

    private void collectOverlapping(
        int node,
        int start,
        int end,
        double minimum,
        double maximum,
        List<int> destination,
        ref int visitedNodes)
    {
        visitedNodes++;

        // Entries are ordered by minimum, so the first entry is also the
        // smallest minimum in this subtree. The maximum tree handles the
        // opposite edge and lets whole groups of past holds be skipped.
        if (entries[start].Minimum > maximum
            || subtreeMaximums[node] < minimum)
        {
            return;
        }

        if (end - start == 1)
        {
            destination.Add(entries[start].Index);
            return;
        }

        int middle = start + (end - start) / 2;
        collectOverlapping(
            node * 2,
            start,
            middle,
            minimum,
            maximum,
            destination,
            ref visitedNodes);
        collectOverlapping(
            node * 2 + 1,
            middle,
            end,
            minimum,
            maximum,
            destination,
            ref visitedNodes);
    }

    private readonly record struct Entry(
        int Index,
        double Minimum,
        double Maximum);
}
