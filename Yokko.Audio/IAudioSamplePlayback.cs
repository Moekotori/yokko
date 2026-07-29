namespace Yokko.Audio;

/// <summary>
/// Optional low-latency one-shot sample capability exposed by an audio engine.
/// Samples are prepared off the gameplay input path and triggered without
/// entering the prefetched music queue.
/// </summary>
public interface IAudioSamplePlayback
{
    ValueTask PrepareSamplesAsync(
        IReadOnlyCollection<string> samplePaths,
        CancellationToken cancellationToken = default);

    bool TriggerSample(string samplePath);
}
