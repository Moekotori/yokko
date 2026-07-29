using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Platform;
using Yokko.Audio;
using Yokko.Game.Audio;
using Yokko.Game.Configuration;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class AudioSettingsTest
{
    [Test]
    public void DefaultsPreferLowestLatencyNativePath()
    {
        var settings = new YokkoAudioSettings();

        Assert.That(
            settings.PreferredBackend.Value,
            Is.EqualTo(AudioBackendKind.WasapiExclusive));
        Assert.That(settings.HomeMusicEnabled.Value, Is.True);
        Assert.That(settings.MasterVolume.Value, Is.EqualTo(1));
        Assert.That(settings.DeviceId.Value, Is.Empty);
        Assert.That(settings.PreferredBufferSize.Value, Is.EqualTo(64));
        Assert.That(settings.UserOffsetMilliseconds.Value, Is.Zero);
    }

    [Test]
    public void GameplayRequestUsesCurrentSettingsTruth()
    {
        var settings = new YokkoAudioSettings();
        settings.PreferredBackend.Value = AudioBackendKind.SharedWasapi;
        settings.DeviceId.Value = "test-endpoint";
        settings.PreferredBufferSize.Value = 256;
        settings.UserOffsetMilliseconds.Value = 12;

        AudioEngineStartRequest request =
            settings.CreateStartRequest("song.wav");

        Assert.That(request.AudioPath, Is.EqualTo("song.wav"));
        Assert.That(
            request.PreferredBackend,
            Is.EqualTo(AudioBackendKind.SharedWasapi));
        Assert.That(request.DeviceId, Is.EqualTo("test-endpoint"));
        Assert.That(request.PreferredBufferSize, Is.EqualTo(256));
        Assert.That(request.UserOffsetMilliseconds, Is.EqualTo(12));
        Assert.That(request.PlaybackRate, Is.EqualTo(1));
        Assert.That(request.PitchMode, Is.EqualTo(AudioPitchMode.Preserve));
    }

    [Test]
    public void GameplayRequestCarriesModAudioPolicy()
    {
        var settings = new YokkoAudioSettings();

        AudioEngineStartRequest request = settings.CreateStartRequest(
            "song.wav",
            1.25,
            AudioPitchMode.ScaleWithRate,
            1.5);

        Assert.Multiple(() =>
        {
            Assert.That(request.PlaybackRate, Is.EqualTo(1.25));
            Assert.That(
                request.PitchMode,
                Is.EqualTo(AudioPitchMode.ScaleWithRate));
            Assert.That(
                request.FixedFrequencyScale,
                Is.EqualTo(1.5));
        });
    }

    [Test]
    public void AudioPreferencesPersistAcrossConfigInstances()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "audio-settings-config",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var firstSettings = new YokkoAudioSettings();
            using (var firstConfig =
                   new YokkoConfigManager(new NativeStorage(directory)))
            {
                firstConfig.BindAudioSettings(firstSettings);
                firstSettings.HomeMusicEnabled.Value = false;
                firstSettings.PreferredBackend.Value =
                    AudioBackendKind.SharedWasapi;
                firstSettings.DeviceId.Value = "persisted-endpoint";
                firstSettings.PreferredBufferSize.Value = 256;
                firstSettings.MasterVolume.Value = 0.65;
                firstSettings.UserOffsetMilliseconds.Value = -8;
                Assert.That(firstConfig.Save(), Is.True);
            }

            var restoredSettings = new YokkoAudioSettings();
            using (var restoredConfig =
                   new YokkoConfigManager(new NativeStorage(directory)))
            {
                restoredConfig.BindAudioSettings(restoredSettings);
                Assert.That(
                    restoredSettings.HomeMusicEnabled.Value,
                    Is.False);
                Assert.That(
                    restoredSettings.PreferredBackend.Value,
                    Is.EqualTo(AudioBackendKind.SharedWasapi));
                Assert.That(
                    restoredSettings.DeviceId.Value,
                    Is.EqualTo("persisted-endpoint"));
                Assert.That(
                    restoredSettings.PreferredBufferSize.Value,
                    Is.EqualTo(256));
                Assert.That(
                    restoredSettings.MasterVolume.Value,
                    Is.EqualTo(0.65));
                Assert.That(
                    restoredSettings.UserOffsetMilliseconds.Value,
                    Is.EqualTo(-8));
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Test]
    public void MasterVolumeAppliesToMusicAndOptionalHitSounds()
    {
        var settings = new YokkoAudioSettings();
        var audio = new NullAudioEngine();
        settings.MasterVolume.Value = 0.65;

        settings.ApplyMixSettings(audio, false);

        Assert.Multiple(() =>
        {
            Assert.That(audio.MusicVolume, Is.EqualTo(0.65));
            Assert.That(audio.HitSoundVolume, Is.Zero);
            Assert.That(audio.MetronomeVolume, Is.Zero);
        });

        settings.ApplyMixSettings(audio, true);
        Assert.That(audio.HitSoundVolume, Is.EqualTo(0.65));
    }

    [Test]
    public void PausedHomeMusicPersistsWhenConfigIsDisposed()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "home-music-config",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var firstSettings = new YokkoAudioSettings();
            using (var firstConfig =
                   new YokkoConfigManager(new NativeStorage(directory)))
            {
                firstConfig.BindAudioSettings(firstSettings);
                firstSettings.HomeMusicEnabled.Value = false;
            }

            var restoredSettings = new YokkoAudioSettings();
            using (var restoredConfig =
                   new YokkoConfigManager(new NativeStorage(directory)))
            {
                restoredConfig.BindAudioSettings(restoredSettings);
                Assert.That(
                    restoredSettings.HomeMusicEnabled.Value,
                    Is.False);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
