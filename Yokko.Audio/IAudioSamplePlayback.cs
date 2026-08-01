namespace Yokko.Audio;

public enum AudioSampleBus
{
    HitSound = 0,
    Music = 1,
}

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

/// <summary>
/// Optional per-trigger gain used by beatmap hit sample volumes. The gain is
/// applied in addition to the global hit-sound bus volume.
/// </summary>
public interface IAudioSamplePlaybackWithGain : IAudioSamplePlayback
{
    bool TriggerSample(string samplePath, double gain);
}

/// <summary>
/// Optional per-trigger routing for prepared chart samples. Existing sample
/// playback remains on the hit-sound bus unless a caller explicitly selects
/// another bus.
/// </summary>
public interface IAudioBusSamplePlayback : IAudioSamplePlaybackWithGain
{
    bool TriggerSample(
        string samplePath,
        double gain,
        AudioSampleBus bus);
}

/// <summary>
/// Optional callback-owned looping sample capability used by lazer-style
/// sliding hold samples. A zero handle means that the loop could not start.
/// </summary>
public interface IAudioLoopingSamplePlayback : IAudioSamplePlaybackWithGain
{
    uint StartLoopingSample(string samplePath, double gain);

    bool StopLoopingSample(uint loopId);
}

/// <summary>
/// Optional prepared-sample fast path. Path lookup happens once while binding
/// the handle rather than for every gameplay input edge.
/// </summary>
public interface IPreparedAudioSamplePlayback : IAudioLoopingSamplePlayback
{
    bool TryGetPreparedSampleHandle(
        string samplePath,
        out PreparedAudioSampleHandle handle);

    bool TriggerPreparedSample(
        PreparedAudioSampleHandle handle,
        double gain);

    uint StartLoopingPreparedSample(
        PreparedAudioSampleHandle handle,
        double gain);
}

public interface IPreparedAudioBusSamplePlayback :
    IPreparedAudioSamplePlayback,
    IAudioBusSamplePlayback
{
    bool TriggerPreparedSample(
        PreparedAudioSampleHandle handle,
        double gain,
        AudioSampleBus bus);
}
