using System;
using osu.Framework.Bindables;
using Yokko.Audio;

namespace Yokko.Game.Audio;

public enum BackgroundAudioMode
{
    KeepPlaying,
    Dim,
    Mute,
}

/// <summary>
/// Application-owned audio preferences shared by settings and gameplay.
/// Device playback state remains owned by <see cref="IAudioEngine"/>.
/// </summary>
public sealed class YokkoAudioSettings
{
    public event Action MixChanged;

    public readonly Bindable<bool> HomeMusicEnabled = new(true);

    public readonly Bindable<double> MasterVolume = new(1);

    public readonly Bindable<double> MusicVolume = new(1);

    public readonly Bindable<double> HitSoundVolume = new(1);

    public readonly Bindable<BackgroundAudioMode> BackgroundAudio =
        new(BackgroundAudioMode.KeepPlaying);

    public readonly Bindable<AudioBackendKind> PreferredBackend =
        new(AudioBackendKind.WasapiExclusive);

    public readonly Bindable<string> DeviceId = new(string.Empty);

    public readonly Bindable<string> AsioDeviceId = new(string.Empty);

    public readonly Bindable<int> PreferredBufferSize = new(64);

    public readonly Bindable<double> UserOffsetMilliseconds = new(0);

    public readonly Bindable<AudioPitchMode> ManualPlaybackRatePitchMode =
        new(AudioPitchMode.Preserve);

    private bool applicationActive = true;

    public YokkoAudioSettings()
    {
        MasterVolume.BindValueChanged(_ => MixChanged?.Invoke());
        MusicVolume.BindValueChanged(_ => MixChanged?.Invoke());
        HitSoundVolume.BindValueChanged(_ => MixChanged?.Invoke());
        BackgroundAudio.BindValueChanged(_ => MixChanged?.Invoke());
    }

    public double EffectiveMusicVolume =>
        clampVolume(MasterVolume.Value)
        * clampVolume(MusicVolume.Value)
        * backgroundVolumeScale;

    public double EffectiveHitSoundVolume =>
        clampVolume(MasterVolume.Value)
        * clampVolume(HitSoundVolume.Value)
        * backgroundVolumeScale;

    public double EffectiveMasterVolume =>
        clampVolume(MasterVolume.Value) * backgroundVolumeScale;

    public bool IsApplicationActive => applicationActive;

    public void SetApplicationActive(bool active)
    {
        if (applicationActive == active)
            return;

        applicationActive = active;
        MixChanged?.Invoke();
    }

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

    private double backgroundVolumeScale => applicationActive
        ? 1
        : BackgroundAudio.Value switch
        {
            BackgroundAudioMode.Dim => 0.2,
            BackgroundAudioMode.Mute => 0,
            _ => 1,
        };

    public AudioEngineStartRequest CreateStartRequest(
        string audioPath,
        double playbackRate = 1,
        AudioPitchMode pitchMode = AudioPitchMode.Preserve,
        double? fixedFrequencyScale = null) =>
        new(
            audioPath,
            PreferredBackend.Value,
            string.IsNullOrWhiteSpace(SelectedDeviceId)
                ? null
                : SelectedDeviceId,
            48000,
            PreferredBufferSize.Value,
            UserOffsetMilliseconds.Value,
            playbackRate,
            pitchMode,
            FixedFrequencyScale: fixedFrequencyScale);

    public string SelectedDeviceId =>
        PreferredBackend.Value == AudioBackendKind.Asio
            ? AsioDeviceId.Value
            : DeviceId.Value;
}
