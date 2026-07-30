using System;

namespace Yokko.Game.Input;

internal readonly record struct PipelineStageLatencyStatistics(
    int Count,
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds,
    double MaximumMilliseconds);

internal readonly record struct InputPipelineLatencyStatistics(
    PipelineStageLatencyStatistics CaptureToDequeue,
    PipelineStageLatencyStatistics CaptureToAudioEnqueue,
    PipelineStageLatencyStatistics CaptureToCompletion,
    PipelineStageLatencyStatistics Processing);

internal sealed class InputPipelineLatencyTracker
{
    private const int capacity = 512;
    private readonly double[] captureToDequeue = new double[capacity];
    private readonly double[] captureToAudioEnqueue = new double[capacity];
    private readonly double[] captureToCompletion = new double[capacity];
    private readonly double[] processing = new double[capacity];
    private int count;
    private int nextIndex;

    internal void Record(
        long captureTimestamp,
        long dequeueTimestamp,
        long audioEnqueueTimestamp,
        long completionTimestamp,
        long timestampFrequency)
    {
        if (timestampFrequency <= 0
            || captureTimestamp <= 0
            || dequeueTimestamp < captureTimestamp
            || completionTimestamp < dequeueTimestamp)
        {
            return;
        }

        captureToDequeue[nextIndex] = milliseconds(
            dequeueTimestamp - captureTimestamp,
            timestampFrequency);
        captureToCompletion[nextIndex] = milliseconds(
            completionTimestamp - captureTimestamp,
            timestampFrequency);
        processing[nextIndex] = milliseconds(
            completionTimestamp - dequeueTimestamp,
            timestampFrequency);
        captureToAudioEnqueue[nextIndex] =
            audioEnqueueTimestamp >= dequeueTimestamp
            && audioEnqueueTimestamp <= completionTimestamp
                ? milliseconds(
                    audioEnqueueTimestamp - captureTimestamp,
                    timestampFrequency)
                : double.NaN;

        nextIndex = (nextIndex + 1) % capacity;
        count = Math.Min(count + 1, capacity);
    }

    internal InputPipelineLatencyStatistics Snapshot() =>
        new(
            snapshot(captureToDequeue),
            snapshot(captureToAudioEnqueue),
            snapshot(captureToCompletion),
            snapshot(processing));

    private PipelineStageLatencyStatistics snapshot(double[] source)
    {
        if (count == 0)
            return default;

        var sorted = new double[count];
        int sampleCount = 0;
        for (int index = 0; index < count; index++)
        {
            double value = source[index];
            if (double.IsFinite(value) && value >= 0)
                sorted[sampleCount++] = value;
        }

        if (sampleCount == 0)
            return default;

        Array.Sort(sorted, 0, sampleCount);
        return new PipelineStageLatencyStatistics(
            sampleCount,
            percentile(sorted, sampleCount, 0.50),
            percentile(sorted, sampleCount, 0.95),
            percentile(sorted, sampleCount, 0.99),
            sorted[sampleCount - 1]);
    }

    private static double percentile(
        double[] sorted,
        int count,
        double percentile)
    {
        int index = Math.Clamp(
            (int)Math.Ceiling(count * percentile) - 1,
            0,
            count - 1);
        return sorted[index];
    }

    private static double milliseconds(long ticks, long frequency) =>
        ticks * 1000d / frequency;
}
