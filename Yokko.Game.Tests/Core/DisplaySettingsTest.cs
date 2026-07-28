using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Platform;
using Yokko.Game.Configuration;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Settings;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class DisplaySettingsTest
{
    [Test]
    public void LastSettingsPagePersistsAcrossConfigInstances()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "settings-page-config",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            using (var firstConfig = new YokkoConfigManager(new NativeStorage(directory)))
                firstConfig.SetLastSettingsPage(SettingsPageKind.Gameplay.ToString());

            using (var restoredConfig = new YokkoConfigManager(new NativeStorage(directory)))
            {
                Assert.That(
                    restoredConfig.GetLastSettingsPage(),
                    Is.EqualTo(SettingsPageKind.Gameplay.ToString()));
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Test]
    public void InterfaceScalePersistsAcrossConfigInstances()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "display-settings-config",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var firstSettings = new YokkoDisplaySettings();
            using (var firstConfig = new YokkoConfigManager(new NativeStorage(directory)))
            {
                firstConfig.BindDisplaySettings(firstSettings);
                firstSettings.UiScale.Value = YokkoUiScale.Large;
                Assert.That(firstConfig.Save(), Is.True);
            }

            var restoredSettings = new YokkoDisplaySettings();
            using (var restoredConfig = new YokkoConfigManager(new NativeStorage(directory)))
            {
                restoredConfig.BindDisplaySettings(restoredSettings);
                Assert.That(restoredSettings.UiScale.Value, Is.EqualTo(YokkoUiScale.Large));
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
