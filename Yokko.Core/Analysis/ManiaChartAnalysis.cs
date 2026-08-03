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
    double LaneImbalance,
    ManiaPatternProfile? PatternProfile = null);

public sealed record ManiaPatternPeak(
    string Pattern,
    double StartTimeMilliseconds,
    double EndTimeMilliseconds,
    double Intensity);

public sealed record ManiaPatternProfile(
    double Jack,
    double Chord,
    double Burst,
    double Anchor,
    double LongNote,
    double Release,
    IReadOnlyList<ManiaPatternPeak> Peaks)
{
    public static ManiaPatternProfile Empty { get; } =
        new(0, 0, 0, 0, 0, 0, []);
}

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
                0,
                ManiaPatternProfile.Empty);
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
        ManiaPatternProfile patternProfile = analysePatterns(
            notes,
            laneCount,
            playbackRate,
            averageKps);

        return new ManiaChartAnalysisResult(
            notes.Length,
            holdCount,
            averageKps,
            peakCount,
            holdCount / (double)notes.Length,
            laneNoteCounts,
            busiestLane,
            laneImbalance,
            patternProfile);
    }

    private static ManiaPatternProfile analysePatterns(
        IReadOnlyList<YokkoHitObject> notes,
        int laneCount,
        double playbackRate,
        double averageKps)
    {
        double[] times = notes
            .Select(note => note.StartTimeMilliseconds / playbackRate)
            .ToArray();
        double[] lastLaneTime = Enumerable.Repeat(
            double.NegativeInfinity,
            laneCount).ToArray();
        int jackCount = 0;
        int releasePressureCount = 0;
        var releaseTimes = new List<double>();
        foreach (YokkoHitObject note in notes)
        {
            double time = note.StartTimeMilliseconds / playbackRate;
            if ((uint)note.Lane < (uint)lastLaneTime.Length
                && time - lastLaneTime[note.Lane] <= 180)
            {
                jackCount++;
            }
            if ((uint)note.Lane < (uint)lastLaneTime.Length)
                lastLaneTime[note.Lane] = time;

            if (note.Kind == HitObjectKind.Hold
                && note.EndTimeMilliseconds is double end)
            {
                releaseTimes.Add(end / playbackRate);
            }
        }

        var chordGroups = notes
            .GroupBy(note => Math.Round(
                note.StartTimeMilliseconds / playbackRate,
                3))
            .Select(group => group.Select(note => note.Lane).Distinct().Count())
            .ToArray();
        int chordNotes = chordGroups.Where(count => count >= 2).Sum();

        releaseTimes.Sort();
        for (int i = 0; i < releaseTimes.Count; i++)
        {
            double release = releaseTimes[i];
            bool pressured = containsWithin(times, release, 90)
                              || i > 0
                              && release - releaseTimes[i - 1] <= 90
                              || i + 1 < releaseTimes.Count
                              && releaseTimes[i + 1] - release <= 90;
            if (pressured)
                releasePressureCount++;
        }

        const double segmentLength = 2000;
        var segmentPeaks = new List<ManiaPatternPeak>();
        double maxBurstRatio = 0;
        double maxAnchorRatio = 0;
        int segmentLeft = 0;
        int segmentRight = 0;
        int[] segmentLaneCounts = new int[laneCount];
        for (double start = times[0]; start <= times[^1]; start += 500)
        {
            double end = start + segmentLength;
            while (segmentLeft < segmentRight
                   && times[segmentLeft] < start)
            {
                int lane = notes[segmentLeft].Lane;
                if ((uint)lane < (uint)segmentLaneCounts.Length)
                    segmentLaneCounts[lane]--;
                segmentLeft++;
            }
            while (segmentRight < times.Length
                   && times[segmentRight] < end)
            {
                int lane = notes[segmentRight].Lane;
                if ((uint)lane < (uint)segmentLaneCounts.Length)
                    segmentLaneCounts[lane]++;
                segmentRight++;
            }

            int total = segmentRight - segmentLeft;
            if (total == 0)
                continue;

            double localKps = total / (segmentLength / 1000);
            double burstRatio = localKps / Math.Max(1, averageKps);
            double anchorRatio = segmentLaneCounts.Max() / (double)total;
            maxBurstRatio = Math.Max(maxBurstRatio, burstRatio);
            maxAnchorRatio = Math.Max(maxAnchorRatio, anchorRatio);
            if (burstRatio >= 1.35)
            {
                segmentPeaks.Add(new ManiaPatternPeak(
                    "BURST",
                    start * playbackRate,
                    end * playbackRate,
                    normalise((burstRatio - 1) / 1.5)));
            }
            if (anchorRatio >= Math.Max(0.45, 1d / laneCount * 1.8))
            {
                segmentPeaks.Add(new ManiaPatternPeak(
                    "ANCHOR",
                    start * playbackRate,
                    end * playbackRate,
                    normalise((anchorRatio - 1d / laneCount) * 1.8)));
            }
        }

        ManiaPatternPeak[] peaks = segmentPeaks
            .OrderByDescending(static peak => peak.Intensity)
            .Take(6)
            .OrderBy(static peak => peak.StartTimeMilliseconds)
            .ToArray();
        return new ManiaPatternProfile(
            normalise(jackCount / (double)Math.Max(1, notes.Count - 1) * 2.2),
            normalise(chordNotes / (double)notes.Count * 1.8),
            normalise((maxBurstRatio - 1) / 1.5),
            normalise((maxAnchorRatio - 1d / laneCount) * 1.8),
            normalise(releaseTimes.Count / (double)notes.Count * 1.6),
            normalise(releasePressureCount
                      / (double)Math.Max(1, releaseTimes.Count)),
            peaks);
    }

    private static double normalise(double value) =>
        Math.Clamp(value, 0, 1) * 100;

    private static bool containsWithin(
        IReadOnlyList<double> sortedValues,
        double target,
        double tolerance)
    {
        int low = 0;
        int high = sortedValues.Count;
        double minimum = target - tolerance;
        while (low < high)
        {
            int middle = low + (high - low) / 2;
            if (sortedValues[middle] < minimum)
                low = middle + 1;
            else
                high = middle;
        }

        return low < sortedValues.Count
               && sortedValues[low] <= target + tolerance;
    }
}
