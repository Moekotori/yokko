namespace Yokko.Audio;

public sealed record AudioEngineStartRequest(
    string AudioPath,
    AudioBackendKind PreferredBackend,
    string? DeviceId,
    int PreferredSampleRate,
    int PreferredBufferSize,
    double UserOffsetMilliseconds,
    double PlaybackRate = 1,
    AudioPitchMode PitchMode = AudioPitchMode.Preserve,
    bool DynamicPlaybackRate = false,
    double? FixedFrequencyScale = null);
