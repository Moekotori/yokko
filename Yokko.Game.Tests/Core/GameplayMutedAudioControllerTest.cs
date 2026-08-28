using NUnit.Framework;
using Yokko.Audio;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Game.Screens.Gameplay;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public class GameplayMutedAudioControllerTest
{
    [Test]
    public void ComboChangeUsesSmoothIndependentBusFade()
    {
        var audio = new NullAudioEngine();
        ManiaModSet mods = ManiaModSet.Empty.WithMuted(
            false,
            true,
            100,
            true);
        var controller = new GameplayMutedAudioController(
            DemoBeatmaps.CreateFourKeyDemo(),
            mods,
            audio);

        Assert.That(audio.MusicVolume, Is.EqualTo(1));
        Assert.That(audio.HitSoundVolume, Is.EqualTo(1));
        Assert.That(audio.MetronomeVolume, Is.EqualTo(0));

        controller.Update(250, 100, -1);
        Assert.That(audio.MusicVolume, Is.InRange(0, 0.1));
        Assert.That(audio.HitSoundVolume, Is.EqualTo(audio.MusicVolume));
        Assert.That(audio.MetronomeVolume, Is.InRange(0.9, 1));

        controller.Update(250, 100, 0);
        Assert.That(audio.MusicVolume, Is.EqualTo(0).Within(0.0001));
        Assert.That(audio.MetronomeVolume, Is.EqualTo(1).Within(0.0001));
        Assert.That(audio.MetronomeTriggerCount, Is.EqualTo(1));

        controller.Restore();
        Assert.That(audio.MusicVolume, Is.EqualTo(1));
        Assert.That(audio.HitSoundVolume, Is.EqualTo(1));
        Assert.That(audio.MetronomeVolume, Is.EqualTo(0));
    }

    [Test]
    public void SteadyMixSkipsRedundantEngineVolumeCalls()
    {
        var audio = new CountingMixControl();
        ManiaModSet mods = ManiaModSet.Empty.WithMuted(
            false,
            true,
            100,
            true);
        var controller = new GameplayMutedAudioController(
            DemoBeatmaps.CreateFourKeyDemo(),
            mods,
            audio);
        int callsAfterConstruction = audio.SetMixVolumesCallCount;

        // Let the combo transition finish, then keep updating steadily.
        controller.Update(1000, 100, -1);
        int callsAfterTransition = audio.SetMixVolumesCallCount;
        for (int frame = 0; frame < 10; frame++)
            controller.Update(16, 100, -1);

        Assert.That(callsAfterConstruction, Is.EqualTo(1));
        Assert.That(callsAfterTransition, Is.EqualTo(2));
        Assert.That(
            audio.SetMixVolumesCallCount,
            Is.EqualTo(callsAfterTransition),
            "An unchanged mix must not be re-sent to the audio engine.");

        controller.Update(16, 0, -1);
        Assert.That(
            audio.SetMixVolumesCallCount,
            Is.GreaterThan(callsAfterTransition),
            "A combo change must start applying volumes again.");
    }

    [Test]
    public void MutedMixPreservesMasterVolumeAndDisabledHitSounds()
    {
        var audio = new NullAudioEngine();
        ManiaModSet mods = ManiaModSet.Empty.WithMuted(
            false,
            true,
            100,
            true);
        var controller = new GameplayMutedAudioController(
            DemoBeatmaps.CreateFourKeyDemo(),
            mods,
            audio,
            0.6,
            0,
            0.6);

        Assert.That(audio.MusicVolume, Is.EqualTo(0.6));
        Assert.That(audio.HitSoundVolume, Is.Zero);

        controller.Update(500, 100, 0);
        Assert.That(audio.MusicVolume, Is.Zero.Within(0.0001));
        Assert.That(audio.HitSoundVolume, Is.Zero);
        Assert.That(audio.MetronomeVolume, Is.EqualTo(0.6).Within(0.0001));

        controller.Restore();
        Assert.That(audio.MusicVolume, Is.EqualTo(0.6));
        Assert.That(audio.HitSoundVolume, Is.Zero);
        Assert.That(audio.MetronomeVolume, Is.Zero);
    }

    private sealed class CountingMixControl : IAudioMixControl
    {
        public double MusicVolume { get; private set; } = 1;

        public double HitSoundVolume { get; private set; } = 1;

        public double MetronomeVolume { get; private set; }

        public int SetMixVolumesCallCount { get; private set; }

        public void SetMixVolumes(
            double musicVolume,
            double hitSoundVolume,
            double metronomeVolume)
        {
            SetMixVolumesCallCount++;
            MusicVolume = musicVolume;
            HitSoundVolume = hitSoundVolume;
            MetronomeVolume = metronomeVolume;
        }

        public bool TriggerMetronome() => true;
    }
}
