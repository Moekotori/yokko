namespace Yokko.Audio;

public sealed record AudioEngineStartRequest(
    string AudioPath,
    AudioBackendKind PreferredBackend,
    string? DeviceId,
    int PreferredSampleRate,
    int PreferredBufferSize,
    double UserOffsetMilliseconds);
