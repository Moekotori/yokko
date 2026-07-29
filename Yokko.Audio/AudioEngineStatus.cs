namespace Yokko.Audio;

public readonly record struct AudioEngineStatus(
    AudioBackendKind ActiveBackend,
    string? DeviceName,
    int SampleRate,
    int BufferSize,
    double EstimatedOutputLatencyMilliseconds,
    bool IsExclusive,
    bool IsRunning,
    bool IsFaulted,
    bool HasUnderrun,
    ulong CallbackCount,
    ulong CallbackDeadlineMissCount,
    double CallbackBudgetMilliseconds,
    double MaxCallbackDurationMilliseconds,
    ulong CallbackCadenceMissCount,
    double MaxCallbackIntervalMilliseconds,
    int BackendError,
    uint BackendErrorStage)
{
    public int DevicePeriodFrames { get; init; }

    public bool UsesWasapiSharedExplicitPeriod { get; init; }

    public int WasapiSharedExplicitPeriodError { get; init; }

    public ulong BackendOverloadCount { get; init; }
}
