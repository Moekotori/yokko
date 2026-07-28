namespace Yokko.Audio;

public sealed record AudioEngineStatus(
    AudioBackendKind ActiveBackend,
    string? DeviceName,
    int SampleRate,
    int BufferSize,
    double EstimatedOutputLatencyMilliseconds,
    bool IsExclusive,
    bool IsRunning,
    bool HasUnderrun,
    ulong CallbackCount,
    ulong CallbackDeadlineMissCount,
    double CallbackBudgetMilliseconds,
    double MaxCallbackDurationMilliseconds,
    int BackendError,
    uint BackendErrorStage);
