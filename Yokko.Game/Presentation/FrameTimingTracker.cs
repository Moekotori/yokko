using System;

namespace Yokko.Game.Presentation;

internal readonly record struct FrameTimingSnapshot(
    long Count,
    double FrameTimeMilliseconds,
    int FramesPerSecond,
    double P95FrameTimeMilliseconds,
    double P99FrameTimeMilliseconds,
    double MaximumFrameTimeMilliseconds,
    int BudgetMissCount,
    double BudgetMissRatio,
    double[] RecentFrameTimes);

internal sealed class FrameTimingTracker
{
    private const int recent_sample_count = 5;
    private const int analysis_sample_count = 2048;
    private const double analysis_window_milliseconds = 2000;
    private const double smoothing_window_milliseconds = 250;
    private const double minimum_visible_change_milliseconds = 0.2;
    private const double minimum_visible_change_ratio = 0.05;

    private readonly double[] recentFrameTimes =
        new double[recent_sample_count];
    private readonly double[] analysisFrameTimes =
        new double[analysis_sample_count];
    private int recentCount;
    private int nextRecentIndex;
    private int analysisCount;
    private int nextAnalysisIndex;
    private long count;
    private double smoothedFrameTime;

    public void Record(double frameTimeMilliseconds)
    {
        if (!double.IsFinite(frameTimeMilliseconds)
            || frameTimeMilliseconds <= 0
            || frameTimeMilliseconds > 10_000)
            return;

        if (count == 0)
        {
            smoothedFrameTime = frameTimeMilliseconds;
        }
        else
        {
            double blend = 1 - Math.Exp(
                -frameTimeMilliseconds
                / smoothing_window_milliseconds);
            smoothedFrameTime +=
                (frameTimeMilliseconds - smoothedFrameTime) * blend;
        }

        recentFrameTimes[nextRecentIndex] = frameTimeMilliseconds;
        nextRecentIndex =
            (nextRecentIndex + 1) % recent_sample_count;
        recentCount = Math.Min(recentCount + 1, recent_sample_count);

        analysisFrameTimes[nextAnalysisIndex] = frameTimeMilliseconds;
        nextAnalysisIndex =
            (nextAnalysisIndex + 1) % analysis_sample_count;
        analysisCount = Math.Min(
            analysisCount + 1,
            analysis_sample_count);
        count++;
    }

    public FrameTimingSnapshot Snapshot(
        double targetFrameTimeMilliseconds = double.PositiveInfinity)
    {
        if (count == 0)
            return default;

        var recent = new double[recentCount];
        int start = recentCount < recent_sample_count
            ? 0
            : nextRecentIndex;

        for (int index = 0; index < recentCount; index++)
        {
            recent[index] = recentFrameTimes[
                (start + index) % recent_sample_count];
        }

        var analysis = new double[analysisCount];
        int selectedAnalysisCount = 0;
        double selectedDuration = 0;

        for (int offset = 0; offset < analysisCount; offset++)
        {
            int index = (nextAnalysisIndex - 1 - offset
                         + analysis_sample_count)
                        % analysis_sample_count;
            double sample = analysisFrameTimes[index];
            analysis[selectedAnalysisCount++] = sample;
            selectedDuration += sample;
            if (selectedDuration >= analysis_window_milliseconds)
                break;
        }

        if (selectedAnalysisCount != analysis.Length)
            Array.Resize(ref analysis, selectedAnalysisCount);

        double maximum = 0;
        int budgetMissCount = 0;
        double budgetMissThreshold = double.IsFinite(
                targetFrameTimeMilliseconds)
            && targetFrameTimeMilliseconds > 0
                ? Math.Max(
                    targetFrameTimeMilliseconds * 1.5,
                    targetFrameTimeMilliseconds + 1)
                : double.PositiveInfinity;

        for (int index = 0; index < analysis.Length; index++)
        {
            double sample = analysis[index];
            maximum = Math.Max(maximum, sample);
            if (sample > budgetMissThreshold)
                budgetMissCount++;
        }

        Array.Sort(analysis);

        return new FrameTimingSnapshot(
            count,
            smoothedFrameTime,
            Math.Max(1, (int)Math.Round(1000 / smoothedFrameTime)),
            percentile(analysis, 0.95),
            percentile(analysis, 0.99),
            maximum,
            budgetMissCount,
            analysis.Length == 0
                ? 0
                : budgetMissCount / (double)analysis.Length,
            recent);
    }

    public void Reset()
    {
        recentCount = 0;
        nextRecentIndex = 0;
        analysisCount = 0;
        nextAnalysisIndex = 0;
        count = 0;
        smoothedFrameTime = 0;
    }

    public static bool ShouldUpdateDisplay(
        double displayedFrameTimeMilliseconds,
        double candidateFrameTimeMilliseconds)
    {
        if (!double.IsFinite(displayedFrameTimeMilliseconds)
            || displayedFrameTimeMilliseconds <= 0)
            return true;

        double threshold = Math.Max(
            minimum_visible_change_milliseconds,
            displayedFrameTimeMilliseconds
            * minimum_visible_change_ratio);
        return Math.Abs(
            candidateFrameTimeMilliseconds
            - displayedFrameTimeMilliseconds) >= threshold;
    }

    public static int QuantizeFramesPerSecond(int framesPerSecond)
    {
        int step = framesPerSecond >= 240
            ? 5
            : framesPerSecond >= 120
                ? 2
                : 1;
        return Math.Max(
            1,
            (int)Math.Round(framesPerSecond / (double)step) * step);
    }

    private static double percentile(
        double[] sortedValues,
        double percentile)
    {
        if (sortedValues.Length == 0)
            return 0;

        double rank = Math.Clamp(percentile, 0, 1)
                      * (sortedValues.Length - 1);
        int lower = (int)Math.Floor(rank);
        int upper = (int)Math.Ceiling(rank);
        if (lower == upper)
            return sortedValues[lower];

        double blend = rank - lower;
        return sortedValues[lower]
               + (sortedValues[upper] - sortedValues[lower]) * blend;
    }
}
