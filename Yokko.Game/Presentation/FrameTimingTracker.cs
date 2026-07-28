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
}
