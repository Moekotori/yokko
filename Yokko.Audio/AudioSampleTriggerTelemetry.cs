namespace Yokko.Audio;

public readonly record struct AudioSampleTriggerTelemetry(
    ulong TraceId,
    uint SampleId,
    double CaptureToEnqueueMilliseconds,
    double EnqueueToCallbackMilliseconds,
    double CaptureToCallbackMilliseconds,
    double EstimatedCaptureToPresentationMilliseconds,
    ulong FirstOutputFramePosition);

public readonly record struct AudioSampleTriggerTelemetryStatus(
    uint Capacity,
    uint PendingCount,
    ulong DroppedCount);

/// <summary>
/// Optional prepared-sample path which correlates an input capture timestamp
/// with native queue consumption and the first output buffer containing it.
/// </summary>
public interface ITimestampedPreparedAudioSamplePlayback :
    IPreparedAudioSamplePlayback
{
    bool SupportsSampleTriggerTelemetry { get; }

    bool TriggerPreparedSample(
        PreparedAudioSampleHandle handle,
        double gain,
        long captureTimestamp,
        long timestampFrequency,
        out ulong traceId);

    bool TryDequeueSampleTriggerTelemetry(
        out AudioSampleTriggerTelemetry telemetry);

    AudioSampleTriggerTelemetryStatus SampleTriggerTelemetryStatus { get; }
}
