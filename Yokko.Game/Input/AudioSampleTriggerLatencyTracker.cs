using System;
using Yokko.Audio;

namespace Yokko.Game.Input;

internal readonly record struct AudioSampleTriggerLatencyStatistics(
    PipelineStageLatencyStatistics CaptureToEnqueue,
    PipelineStageLatencyStatistics EnqueueToCallback,
    PipelineStageLatencyStatistics CaptureToCallback,
    PipelineStageLatencyStatistics EstimatedCaptureToPresentation);

internal sealed class AudioSampleTriggerLatencyTracker
{
    private const int capacity = 512;
    private readonly double[] captureToEnqueue = new double[capacity];
    private readonly double[] enqueueToCallback = new double[capacity];
    private readonly double[] captureToCallback = new double[capacity];
    private readonly double[] estimatedCaptureToPresentation =
        new double[capacity];
    private int count;
    private int nextIndex;

    internal void Record(AudioSampleTriggerTelemetry telemetry)
    {
        if (!valid(telemetry.CaptureToEnqueueMilliseconds)
            || !valid(telemetry.EnqueueToCallbackMilliseconds)
            || !valid(telemetry.CaptureToCallbackMilliseconds)
            || !valid(telemetry.EstimatedCaptureToPresentationMilliseconds))
        {
            return;
        }

        captureToEnqueue[nextIndex] =
            telemetry.CaptureToEnqueueMilliseconds;
        enqueueToCallback[nextIndex] =
            telemetry.EnqueueToCallbackMilliseconds;
        captureToCallback[nextIndex] =
            telemetry.CaptureToCallbackMilliseconds;
        estimatedCaptureToPresentation[nextIndex] =
            telemetry.EstimatedCaptureToPresentationMilliseconds;
        nextIndex = (nextIndex + 1) % capacity;
        count = Math.Min(count + 1, capacity);
    }

    internal AudioSampleTriggerLatencyStatistics Snapshot() => new(
        snapshot(captureToEnqueue),
        snapshot(enqueueToCallback),
        snapshot(captureToCallback),
        snapshot(estimatedCaptureToPresentation));

    private PipelineStageLatencyStatistics snapshot(double[] source)
    {
        if (count == 0)
            return default;

        var sorted = new double[count];
        Array.Copy(source, sorted, count);
        Array.Sort(sorted);
        return new PipelineStageLatencyStatistics(
            count,
            percentile(sorted, 0.50),
            percentile(sorted, 0.95),
            percentile(sorted, 0.99),
            sorted[^1]);
    }

    private static double percentile(double[] sorted, double percentile)
    {
        int index = Math.Clamp(
            (int)Math.Ceiling(sorted.Length * percentile) - 1,
            0,
            sorted.Length - 1);
        return sorted[index];
    }

    private static bool valid(double value) =>
        double.IsFinite(value) && value >= 0;
}
