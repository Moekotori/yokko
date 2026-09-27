using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Platform;
using Yokko.Game.Audio;
using Yokko.Game.Configuration;
using Yokko.Game.Gameplay;
using Yokko.Game.Presentation;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public class SettingsFeaturePersistenceTest
{
    [Test]
    public void DeviceProfilesPresetsAndAccessibilitySurviveConfigReload()
    {
        string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "feature-config", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using (var config = new YokkoConfigManager(new NativeStorage(directory)))
            {
                var accessibility = new YokkoAccessibilitySettings();
                config.BindAccessibilitySettings(accessibility);
                accessibility.ReduceMotion.Value = accessibility.ReduceFlashes.Value = true;
                accessibility.HideComboBursts.Value = accessibility.HighContrastText.Value = true;
                var audio = new YokkoAudioSettings();
                config.BindAudioSettings(audio);
                using var profiles = new AudioDeviceProfiles(audio, config.GetBindable<string>(YokkoSetting.AudioDeviceProfiles));
                audio.DeviceId.Value = "headphones";
                audio.UserOffsetMilliseconds.Value = -24;
                audio.DeviceId.Value = "speakers";
                var gameplay = new YokkoGameplaySettings();
                gameplay.ScrollSpeed.Value = 16;
                var presets = new GameplayPresetStore(gameplay, new YokkoSkinSettings(),
                    config.GetBindable<string>(YokkoSetting.GameplayPresets), _ => true);
                presets.Save(0, "4K");
                Assert.That(config.Save(), Is.True);
            }
            using (var config = new YokkoConfigManager(new NativeStorage(directory)))
            {
                var accessibility = new YokkoAccessibilitySettings();
                config.BindAccessibilitySettings(accessibility);
                Assert.That(accessibility.ReduceMotion.Value && accessibility.ReduceFlashes.Value
                    && accessibility.HideComboBursts.Value && accessibility.HighContrastText.Value, Is.True);
                var audio = new YokkoAudioSettings();
                config.BindAudioSettings(audio);
                using var profiles = new AudioDeviceProfiles(audio, config.GetBindable<string>(YokkoSetting.AudioDeviceProfiles));
                audio.DeviceId.Value = "headphones";
                Assert.That(audio.UserOffsetMilliseconds.Value, Is.EqualTo(-24));
                var gameplay = new YokkoGameplaySettings();
                var presets = new GameplayPresetStore(gameplay, new YokkoSkinSettings(),
                    config.GetBindable<string>(YokkoSetting.GameplayPresets), _ => true);
                presets.Load(0);
                Assert.That(gameplay.ScrollSpeed.Value, Is.EqualTo(16));
            }
        }
        finally { Directory.Delete(directory, true); }
    }
}
