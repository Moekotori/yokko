using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Configuration;
using osu.Framework.Platform;
using Yokko.Game.Configuration;
using Yokko.Game.Importing;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class SettingsPersistenceCoverageTest
{
    [Test]
    public void ImportSkinAndSafetyPreferencesPersistAcrossConfigInstances()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "settings-persistence",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var firstImport = new YokkoImportSettings();
            var firstSkin = new YokkoSkinSettings();
            using (var firstConfig =
                   new YokkoConfigManager(new NativeStorage(directory)))
            {
                firstConfig.BindImportSettings(firstImport);
                firstConfig.BindSkinSettings(firstSkin);
                firstImport.PreferKeysounds.Value = false;
                firstImport.PreferSscSimfiles.Value = false;
                firstImport.EnableBmsScratch.Value = true;
                firstImport.ShowCompatibilityWarnings.Value = false;
                firstSkin.ShowComboBursts.Value = false;
                firstConfig.GetBindable<double>(
                    YokkoSetting.HomeExitHoldDurationMilliseconds).Value = 2200;
                Assert.That(firstConfig.Save(), Is.True);
            }

            var restoredImport = new YokkoImportSettings();
            var restoredSkin = new YokkoSkinSettings();
            using (var restoredConfig =
                   new YokkoConfigManager(new NativeStorage(directory)))
            {
                restoredConfig.BindImportSettings(restoredImport);
                restoredConfig.BindSkinSettings(restoredSkin);

                Assert.Multiple(() =>
                {
                    Assert.That(restoredImport.PreferKeysounds.Value, Is.False);
                    Assert.That(restoredImport.PreferSscSimfiles.Value, Is.False);
                    Assert.That(restoredImport.EnableBmsScratch.Value, Is.True);
                    Assert.That(
                        restoredImport.ShowCompatibilityWarnings.Value,
                        Is.False);
                    Assert.That(restoredSkin.ShowComboBursts.Value, Is.False);
                    Assert.That(
                        restoredConfig.Get<double>(
                            YokkoSetting.HomeExitHoldDurationMilliseconds),
                        Is.EqualTo(2200));
                });
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
