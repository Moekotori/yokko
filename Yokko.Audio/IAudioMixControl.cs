namespace Yokko.Audio;

public interface IAudioMixControl
{
    double MusicVolume { get; }

    double HitSoundVolume { get; }

    double MetronomeVolume { get; }

    void SetMixVolumes(
        double musicVolume,
        double hitSoundVolume,
        double metronomeVolume);

    bool TriggerMetronome();
}
