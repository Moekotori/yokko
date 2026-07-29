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
}
