using NUnit.Framework;
using osu.Framework.Bindables;
using Yokko.Audio;
using Yokko.Game.Audio;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public class AudioDeviceProfilesTest
{
    [Test]
    public void RoutesKeepTheirOwnMixBufferAndCalibrationAcrossRestart()
    {
        var serialized = new Bindable<string>("{}");
        var audio = new YokkoAudioSettings();
        audio.DeviceId.Value = "headphones";
        using (var profiles = new AudioDeviceProfiles(audio, serialized))
        {
            audio.UserOffsetMilliseconds.Value = -35;
            audio.MasterVolume.Value = 0.4;
            audio.PreferredBufferSize.Value = 128;
            audio.DeviceId.Value = "speakers";
            Assert.That(audio.UserOffsetMilliseconds.Value, Is.Zero);
            audio.UserOffsetMilliseconds.Value = 22;
            audio.MasterVolume.Value = 0.7;
            audio.PreferredBufferSize.Value = 512;
            audio.DeviceId.Value = "headphones";
            Assert.That(audio.UserOffsetMilliseconds.Value, Is.EqualTo(-35));
            Assert.That(audio.MasterVolume.Value, Is.EqualTo(0.4));
            Assert.That(audio.PreferredBufferSize.Value, Is.EqualTo(128));
            audio.PreferredBackend.Value = AudioBackendKind.SharedWasapi;
            Assert.That(audio.UserOffsetMilliseconds.Value, Is.Zero);
            audio.UserOffsetMilliseconds.Value = 7;
            audio.PreferredBackend.Value = AudioBackendKind.WasapiExclusive;
            Assert.That(audio.UserOffsetMilliseconds.Value, Is.EqualTo(-35));
        }
        var restored = new YokkoAudioSettings();
        restored.DeviceId.Value = "speakers";
        using var restoredProfiles = new AudioDeviceProfiles(restored, serialized);
        Assert.That(restored.UserOffsetMilliseconds.Value, Is.EqualTo(22));
        Assert.That(restored.MasterVolume.Value, Is.EqualTo(0.7));
        Assert.That(restored.PreferredBufferSize.Value, Is.EqualTo(512));
    }

    [TestCase("broken")]
    [TestCase("{\"WasapiExclusive:\":null}")]
    [TestCase("{\"WasapiExclusive:\":{\"Master\":2,\"Music\":1,\"HitSound\":1,\"Buffer\":64,\"Offset\":999}}")]
    public void InvalidProfileDoesNotOverwriteMigratedSettings(string json)
    {
        var audio = new YokkoAudioSettings();
        audio.UserOffsetMilliseconds.Value = 32;
        using var profiles = new AudioDeviceProfiles(audio, new Bindable<string>(json));
        Assert.That(audio.UserOffsetMilliseconds.Value, Is.EqualTo(32));
    }
}
