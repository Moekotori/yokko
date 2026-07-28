using System;

namespace Yokko.Game.Presentation;

internal readonly record struct FrameTimingSnapshot(
    long Count,
    double FrameTimeMilliseconds,
    int FramesPerSecond,
    double[] RecentFrameTimes);

internal sealed class FrameTimingTracker
{
    private const int recent_sample_count = 5;
    private const double smoothing_window_milliseconds = 250;
    private const double minimum_visible_change_milliseconds = 0.2;
    private const double minimum_visible_change_ratio = 0.05;

    private readonly double[] recentFrameTimes =
        new double[recent_sample_count];
    private int recentCount;
    private int nextRecentIndex;
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
        count++;
    }

    public FrameTimingSnapshot Snapshot()
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

        return new FrameTimingSnapshot(
            count,
            smoothedFrameTime,
            Math.Max(1, (int)Math.Round(1000 / smoothedFrameTime)),
            recent);
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
}
