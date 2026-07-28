namespace Yokko.Audio;

public static class AudioEngineFactory
{
    public static IAudioEngine CreateDefault()
        => NativeAudioEngine.IsAvailable
            ? new NativeAudioEngine()
            : new NullAudioEngine();

    public static IReadOnlyList<AudioBackendCapabilities> AvailableBackends
        => NativeAudioEngine.IsAvailable
            ? NativeAudioEngine.SupportedBackends
            :
            [
                new(
                    AudioBackendKind.Fallback,
                    false,
                    false,
                    false,
                    "Native audio library is unavailable.",
                    false),
            ];
}
