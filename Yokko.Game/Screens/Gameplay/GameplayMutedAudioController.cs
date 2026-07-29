using System;
using Yokko.Audio;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Core.Timing;

namespace Yokko.Game.Screens.Gameplay;

internal sealed class GameplayMutedAudioController
{
    private const double transitionDurationMilliseconds = 500;
    private readonly ManiaModSet mods;
    private readonly IAudioMixControl audio;
    private readonly BeatTimingMap timing;
    private ManiaMutedMix current;
    private ManiaMutedMix transitionStart;
    private ManiaMutedMix target;
    private double transitionElapsed;
    private int observedCombo;
    private double lastGameplayTime = double.NaN;
    private int nextBeatRow;

    internal GameplayMutedAudioController(
        YokkoBeatmap beatmap,
        ManiaModSet mods,
        IAudioMixControl audio)
    {
        this.mods = mods;
        this.audio = audio;
        timing = new BeatTimingMap(beatmap.TimingPoints, 1);
        current = transitionStart = target =
            ManiaMutedPolicy.Resolve(mods, 0);
        observedCombo = 0;
        apply(current);
    }

    internal ManiaMutedMix Current => current;

    internal void Update(
        double elapsedMilliseconds,
        int combo,
        double gameplayTimeMilliseconds)
    {
        if (combo != observedCombo)
        {
            observedCombo = Math.Max(0, combo);
            transitionStart = current;
            target = ManiaMutedPolicy.Resolve(mods, observedCombo);
            transitionElapsed = 0;
        }

        transitionElapsed = Math.Min(
            transitionDurationMilliseconds,
            transitionElapsed + Math.Max(0, elapsedMilliseconds));
        double progress = transitionDurationMilliseconds == 0
            ? 1
            : transitionElapsed / transitionDurationMilliseconds;
        double eased = 1 - Math.Pow(1 - progress, 5);
        current = new ManiaMutedMix(
            lerp(transitionStart.MusicVolume, target.MusicVolume, eased),
            lerp(transitionStart.HitSoundVolume, target.HitSoundVolume, eased),
            lerp(transitionStart.MetronomeVolume, target.MetronomeVolume, eased));
        apply(current);
        updateMetronome(gameplayTimeMilliseconds);
    }

    internal void Restore()
    {
        current = new ManiaMutedMix(1, 1, 0);
        apply(current);
    }

    private void updateMetronome(double gameplayTime)
    {
        if (!mods.MutedMetronome || gameplayTime < 0)
        {
            lastGameplayTime = gameplayTime;
            return;
        }

        if (!double.IsFinite(lastGameplayTime)
            || gameplayTime < lastGameplayTime
            || gameplayTime - lastGameplayTime > 2000)
        {
            nextBeatRow = timing.ClosestRowAt(gameplayTime);
            while (timing.TimeAtRow(nextBeatRow) <= gameplayTime)
                nextBeatRow++;
            lastGameplayTime = gameplayTime;
            return;
        }

        int triggered = 0;
        while (timing.TimeAtRow(nextBeatRow) <= gameplayTime && triggered < 4)
        {
            if (timing.TimeAtRow(nextBeatRow) > lastGameplayTime)
            {
                audio.TriggerMetronome();
                triggered++;
            }
            nextBeatRow++;
        }

        lastGameplayTime = gameplayTime;
    }

    private void apply(ManiaMutedMix mix) =>
        audio.SetMixVolumes(
            mix.MusicVolume,
            mix.HitSoundVolume,
            mix.MetronomeVolume);

    private static double lerp(double start, double end, double amount) =>
        start + (end - start) * amount;
}
