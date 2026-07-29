namespace Yokko.Core.Mods;

public readonly record struct ManiaMutedMix(
    double MusicVolume,
    double HitSoundVolume,
    double MetronomeVolume);

/// <summary>
/// Pure osu!lazer-style Muted volume policy. Runtime interpolation remains
/// audio-owned; this type resolves the target mix for a combo value.
/// </summary>
public static class ManiaMutedPolicy
{
    public static ManiaMutedMix Resolve(
        ManiaModSet mods,
        int combo)
    {
        ArgumentNullException.ThrowIfNull(mods);
        if (!mods.Contains(ManiaModId.Muted))
            return new ManiaMutedMix(1, 1, 0);

        double dimFactor = mods.MutedComboCount == 0
            ? 1
            : Math.Clamp(
                (double)Math.Max(0, combo)
                / mods.MutedComboCount,
                0,
                1);
        if (mods.MutedInverse)
            dimFactor = 1 - dimFactor;

        double music = 1 - dimFactor;
        double hitSounds = mods.MutedAffectsHitSounds
            ? music
            : 1;
        double metronome = mods.MutedMetronome
            ? dimFactor
            : 0;
        return new ManiaMutedMix(
            music,
            hitSounds,
            metronome);
    }
}
