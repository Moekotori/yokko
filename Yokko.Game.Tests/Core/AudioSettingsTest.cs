using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Platform;
using osuTK.Input;
using Yokko.Audio;
using Yokko.Game.Audio;
using Yokko.Game.Configuration;
using Yokko.Game.Screens.Settings;

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
        Assert.That(settings.MusicVolume.Value, Is.EqualTo(1));
        Assert.That(settings.HitSoundVolume.Value, Is.EqualTo(1));
        Assert.That(settings.DeviceId.Value, Is.Empty);
        Assert.That(settings.AsioDeviceId.Value, Is.Empty);
        Assert.That(settings.PreferredBufferSize.Value, Is.EqualTo(64));
        Assert.That(settings.UserOffsetMilliseconds.Value, Is.Zero);
    }

    [Test]
    public void AsioUsesItsOwnRememberedDevice()
    {
        var settings = new YokkoAudioSettings();
        settings.DeviceId.Value = "wasapi-endpoint";
        settings.AsioDeviceId.Value = "asio:{driver}";
        settings.PreferredBackend.Value = AudioBackendKind.Asio;

        AudioEngineStartRequest asio =
            settings.CreateStartRequest("song.wav");
        settings.PreferredBackend.Value =
            AudioBackendKind.WasapiExclusive;
        AudioEngineStartRequest wasapi =
            settings.CreateStartRequest("song.wav");

        Assert.Multiple(() =>
        {
            Assert.That(asio.DeviceId, Is.EqualTo("asio:{driver}"));
            Assert.That(
                wasapi.DeviceId,
                Is.EqualTo("wasapi-endpoint"));
        });
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
                firstSettings.AsioDeviceId.Value =
                    "asio:{persisted-driver}";
                firstSettings.PreferredBufferSize.Value = 256;
                firstSettings.MasterVolume.Value = 0.65;
                firstSettings.MusicVolume.Value = 0.8;
                firstSettings.HitSoundVolume.Value = 0.55;
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
                    restoredSettings.AsioDeviceId.Value,
                    Is.EqualTo("asio:{persisted-driver}"));
                Assert.That(
                    restoredSettings.PreferredBufferSize.Value,
                    Is.EqualTo(256));
                Assert.That(
                    restoredSettings.MasterVolume.Value,
                    Is.EqualTo(0.65));
                Assert.That(
                    restoredSettings.MusicVolume.Value,
                    Is.EqualTo(0.8));
                Assert.That(
                    restoredSettings.HitSoundVolume.Value,
                    Is.EqualTo(0.55));
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
        settings.MusicVolume.Value = 0.8;
        settings.HitSoundVolume.Value = 0.4;

        settings.ApplyMixSettings(audio, false);

        Assert.Multiple(() =>
        {
            Assert.That(audio.MusicVolume, Is.EqualTo(0.52).Within(0.0001));
            Assert.That(audio.HitSoundVolume, Is.Zero);
            Assert.That(audio.MetronomeVolume, Is.Zero);
        });

        settings.ApplyMixSettings(audio, true);
        Assert.That(audio.HitSoundVolume, Is.EqualTo(0.26).Within(0.0001));
    }

    [Test]
    public void VolumeSliderUsesOnePercentDragAndFivePercentWheelSteps()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                SettingsVolumeSlider.ValueFromProgress(0.326),
                Is.EqualTo(0.33));
            Assert.That(
                SettingsVolumeSlider.ValueFromProgress(-1),
                Is.Zero);
            Assert.That(
                SettingsVolumeSlider.ValueFromProgress(2),
                Is.EqualTo(1));
            Assert.That(
                SettingsVolumeSlider.AdjustForScroll(0.5, 1),
                Is.EqualTo(0.55));
            Assert.That(
                SettingsVolumeSlider.AdjustForScroll(0.5, -1),
                Is.EqualTo(0.45));
            Assert.That(
                SettingsVolumeSlider.AdjustForScroll(0.98, 1),
                Is.EqualTo(1));
            Assert.That(
                SettingsVolumeSlider.AdjustForScroll(0.02, -1),
                Is.Zero);
            Assert.That(
                SettingsVolumeSlider.AcceptsWheelAt(36),
                Is.True);
            Assert.That(
                SettingsVolumeSlider.AcceptsWheelAt(12),
                Is.False);
            Assert.That(
                SettingsVolumeSlider.AdjustForKey(0.5, Key.Right),
                Is.EqualTo(0.51));
            Assert.That(
                SettingsVolumeSlider.AdjustForKey(
                    0.5,
                    Key.Left,
                    true),
                Is.EqualTo(0.45));
            Assert.That(
                SettingsVolumeSlider.AdjustForKey(0.5, Key.Home),
                Is.Zero);
            Assert.That(
                SettingsOffsetStepper.AdjustForKey(12, Key.Left),
                Is.EqualTo(11));
            Assert.That(
                SettingsOffsetStepper.AdjustForKey(
                    12,
                    Key.Right,
                    true),
                Is.EqualTo(22));
            Assert.That(
                SettingsOffsetStepper.AdjustForKey(12, Key.Home),
                Is.Zero);
        });
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
