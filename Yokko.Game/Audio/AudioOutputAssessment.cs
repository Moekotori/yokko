using Yokko.Audio;

namespace Yokko.Game.Audio;

internal enum AudioOutputAssessmentKind
{
    Verified,
    ExclusiveFallback,
    HighSharedLatency,
    HighLatency,
}

internal static class AudioOutputAssessment
{
    internal const double HighLatencyThresholdMilliseconds = 15;

    internal static AudioOutputAssessmentKind Assess(
        AudioBackendKind requestedBackend,
        AudioEngineStatus tested)
    {
        if (requestedBackend == AudioBackendKind.WasapiExclusive
            && tested.ActiveBackend == AudioBackendKind.SharedWasapi)
        {
            return AudioOutputAssessmentKind.ExclusiveFallback;
        }

        if (tested.EstimatedOutputLatencyMilliseconds
            <= HighLatencyThresholdMilliseconds)
        {
            return AudioOutputAssessmentKind.Verified;
        }

        return tested.ActiveBackend == AudioBackendKind.SharedWasapi
            ? AudioOutputAssessmentKind.HighSharedLatency
            : AudioOutputAssessmentKind.HighLatency;
    }
}
