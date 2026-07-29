using osu.Framework.Bindables;
using Yokko.Audio;

namespace Yokko.Game.Audio;

/// <summary>
/// Application-owned audio preferences shared by settings and gameplay.
/// Device playback state remains owned by <see cref="IAudioEngine"/>.
/// </summary>
public sealed class YokkoAudioSettings
{
    public readonly Bindable<bool> HomeMusicEnabled = new(true);

    public readonly Bindable<AudioBackendKind> PreferredBackend =
        new(AudioBackendKind.WasapiExclusive);

    public readonly Bindable<string> DeviceId = new(string.Empty);

    public readonly Bindable<int> PreferredBufferSize = new(64);

    public readonly Bindable<double> UserOffsetMilliseconds = new(0);

    public AudioEngineStartRequest CreateStartRequest(
        string audioPath,
        double playbackRate = 1,
        AudioPitchMode pitchMode = AudioPitchMode.Preserve) =>
        new(
            audioPath,
            PreferredBackend.Value,
            string.IsNullOrWhiteSpace(DeviceId.Value)
                ? null
                : DeviceId.Value,
            48000,
            PreferredBufferSize.Value,
            UserOffsetMilliseconds.Value,
            playbackRate,
            pitchMode);
}
