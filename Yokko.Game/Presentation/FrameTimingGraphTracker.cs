using System;

namespace Yokko.Game.Presentation;

internal readonly record struct FrameTimingGraphSnapshot(
    double[] FrameTimes,
    double StutterThresholdMilliseconds);

internal sealed class FrameTimingGraphTracker
{
    private const int bar_count = 5;
    private const double minimum_stutter_threshold_milliseconds = 25;
    private const double baseline_stutter_multiplier = 2;
    private const double minimum_visible_height_change_ratio = 2.0 / 17;

    private readonly double[] buckets = new double[bar_count];
    private double currentBucketMaximum;
    private bool initialised;

    public void Record(double frameTimeMilliseconds)
    {
        if (!double.IsFinite(frameTimeMilliseconds)
            || frameTimeMilliseconds <= 0)
            return;

        currentBucketMaximum = Math.Max(
            currentBucketMaximum,
            frameTimeMilliseconds);
    }

    public FrameTimingGraphSnapshot CompleteBucket(
        double baselineFrameTimeMilliseconds)
    {
        if (!double.IsFinite(baselineFrameTimeMilliseconds)
            || baselineFrameTimeMilliseconds <= 0)
            return default;

        double sample = currentBucketMaximum > 0
            ? currentBucketMaximum
            : baselineFrameTimeMilliseconds;
        currentBucketMaximum = 0;

        if (!initialised)
        {
            Array.Fill(buckets, sample);
            initialised = true;
        }
        else
        {
            Array.Copy(
                buckets,
                1,
                buckets,
                0,
                buckets.Length - 1);
            buckets[^1] = sample;
        }

        return Snapshot(baselineFrameTimeMilliseconds);
    }

    public FrameTimingGraphSnapshot Snapshot(
        double baselineFrameTimeMilliseconds)
    {
        if (!initialised
            || !double.IsFinite(baselineFrameTimeMilliseconds)
            || baselineFrameTimeMilliseconds <= 0)
            return default;

        return new FrameTimingGraphSnapshot(
            (double[])buckets.Clone(),
            StutterThreshold(baselineFrameTimeMilliseconds));
    }

    public static double StutterThreshold(
        double baselineFrameTimeMilliseconds) =>
        Math.Max(
            minimum_stutter_threshold_milliseconds,
            baselineFrameTimeMilliseconds
            * baseline_stutter_multiplier);

    public static bool IsStutter(
        double frameTimeMilliseconds,
        double thresholdMilliseconds) =>
        frameTimeMilliseconds >= thresholdMilliseconds;

    public static double HeightRatio(
        double frameTimeMilliseconds,
        double thresholdMilliseconds)
    {
        if (!double.IsFinite(frameTimeMilliseconds)
            || !double.IsFinite(thresholdMilliseconds)
            || frameTimeMilliseconds <= 0
            || thresholdMilliseconds <= 0)
            return 0;

        return Math.Clamp(
            frameTimeMilliseconds / thresholdMilliseconds,
            0,
            1);
    }

    public static bool ShouldUpdateBar(
        double displayedHeightRatio,
        double targetHeightRatio,
        bool displayedAsStutter,
        bool targetIsStutter) =>
        displayedAsStutter != targetIsStutter
        || Math.Abs(targetHeightRatio - displayedHeightRatio)
        >= minimum_visible_height_change_ratio;
}
