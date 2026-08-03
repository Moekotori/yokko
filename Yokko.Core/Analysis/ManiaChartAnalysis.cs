using System;
using System.Collections.Generic;
using System.Linq;
using Yokko.Core.Beatmaps;

namespace Yokko.Core.Analysis;

public sealed record ManiaChartAnalysisResult(
    int NoteCount,
    int HoldCount,
    double AverageKps,
    double PeakKps,
    double HoldRatio,
    IReadOnlyList<int> LaneNoteCounts,
    int BusiestLane,
    double LaneImbalance);

public static class ManiaChartAnalysis
{
    public static ManiaChartAnalysisResult Analyse(
        YokkoBeatmap beatmap,
        double playbackRate = 1)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        if (!double.IsFinite(playbackRate) || playbackRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(playbackRate));

        YokkoHitObject[] notes = beatmap.HitObjects
            .Where(static hitObject =>
                hitObject.Kind is HitObjectKind.Tap or HitObjectKind.Hold)
            .OrderBy(static hitObject => hitObject.StartTimeMilliseconds)
            .ToArray();
        int laneCount = Math.Max(1, (int)beatmap.KeyMode);
        var laneNoteCounts = new int[laneCount];
        int holdCount = 0;
        foreach (YokkoHitObject note in notes)
        {
            if ((uint)note.Lane < (uint)laneNoteCounts.Length)
                laneNoteCounts[note.Lane]++;
            if (note.Kind == HitObjectKind.Hold)
                holdCount++;
        }

        if (notes.Length == 0)
        {
            return new ManiaChartAnalysisResult(
                0,
                0,
                0,
                0,
                0,
                laneNoteCounts,
                0,
                0);
        }

        double firstTime = notes[0].StartTimeMilliseconds / playbackRate;
        double lastTime = notes[^1].StartTimeMilliseconds / playbackRate;
        double activeSeconds = Math.Max(1, (lastTime - firstTime) / 1000d);
        double averageKps = notes.Length / activeSeconds;

        int windowStart = 0;
        int peakCount = 0;
        for (int windowEnd = 0; windowEnd < notes.Length; windowEnd++)
        {
            double endTime = notes[windowEnd].StartTimeMilliseconds
                             / playbackRate;
            while (windowStart <= windowEnd
                   && endTime
                      - notes[windowStart].StartTimeMilliseconds
                      / playbackRate
                      >= 1000)
            {
                windowStart++;
            }

            peakCount = Math.Max(peakCount, windowEnd - windowStart + 1);
        }

        int busiestLane = 0;
        for (int lane = 1; lane < laneNoteCounts.Length; lane++)
        {
            if (laneNoteCounts[lane] > laneNoteCounts[busiestLane])
                busiestLane = lane;
        }

        double meanLaneCount = notes.Length / (double)laneNoteCounts.Length;
        double laneImbalance = meanLaneCount <= 0
            ? 0
            : laneNoteCounts.Max(count => Math.Abs(count - meanLaneCount))
              / meanLaneCount;

        return new ManiaChartAnalysisResult(
            notes.Length,
            holdCount,
            averageKps,
            peakCount,
            holdCount / (double)notes.Length,
            laneNoteCounts,
            busiestLane,
            laneImbalance);
    }
}
