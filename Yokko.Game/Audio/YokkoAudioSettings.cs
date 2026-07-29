using System;
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

    public readonly Bindable<double> MasterVolume = new(1);

    public readonly Bindable<double> MusicVolume = new(1);

    public readonly Bindable<double> HitSoundVolume = new(1);

    public readonly Bindable<AudioBackendKind> PreferredBackend =
        new(AudioBackendKind.WasapiExclusive);

    public readonly Bindable<string> DeviceId = new(string.Empty);

    public readonly Bindable<int> PreferredBufferSize = new(64);

    public readonly Bindable<double> UserOffsetMilliseconds = new(0);

    public double EffectiveMusicVolume =>
        clampVolume(MasterVolume.Value) * clampVolume(MusicVolume.Value);

    public double EffectiveHitSoundVolume =>
        clampVolume(MasterVolume.Value) * clampVolume(HitSoundVolume.Value);

    public void ApplyMixSettings(
        IAudioMixControl audio,
        bool hitSoundsEnabled = true)
    {
        audio.SetMixVolumes(
            EffectiveMusicVolume,
            hitSoundsEnabled ? EffectiveHitSoundVolume : 0,
            0);
    }

    private static double clampVolume(double volume) =>
        Math.Clamp(volume, 0, 1);

    public AudioEngineStartRequest CreateStartRequest(
        string audioPath,
        double playbackRate = 1,
        AudioPitchMode pitchMode = AudioPitchMode.Preserve,
        double? fixedFrequencyScale = null) =>
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
            pitchMode,
            FixedFrequencyScale: fixedFrequencyScale);
}
