using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Platform;
using osuTK.Input;
using Yokko.Core.Gameplay;
using Yokko.Game.Configuration;
using Yokko.Game.Gameplay;
using Yokko.Game.Screens.Gameplay;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class GameplaySettingsTest
{
    [Test]
    public void DefaultsMatchPlayableProfiles()
    {
        var settings = new YokkoGameplaySettings();

        Assert.That(
            settings.GetKeys(KeyMode.FourKey),
            Is.EqualTo(new[] { Key.D, Key.F, Key.J, Key.K }));
        Assert.That(
            settings.GetKeys(KeyMode.SevenKey),
            Is.EqualTo(new[]
            {
                Key.S,
                Key.D,
                Key.F,
                Key.Space,
                Key.J,
                Key.K,
                Key.L,
            }));
        Assert.That(
            settings.ScrollSpeed.Value,
            Is.EqualTo(OsuManiaScrollSpeed.Default));
        Assert.That(settings.ShowGameplayHud.Value, Is.True);
        Assert.That(settings.ShowHitError.Value, Is.True);
        Assert.That(settings.ShowLanePressFeedback.Value, Is.True);
    }

    [Test]
    public void DuplicateBindingSwapsLanes()
    {
        var settings = new YokkoGameplaySettings();

        settings.SetBinding(KeyMode.FourKey, 0, Key.F);

        Assert.That(
            settings.GetKeys(KeyMode.FourKey),
            Is.EqualTo(new[] { Key.F, Key.D, Key.J, Key.K }));
    }

    [Test]
    public void CompleteProfileCanBeReplacedAtomically()
    {
        var settings = new YokkoGameplaySettings();

        settings.SetBindings(
            KeyMode.FourKey,
            new[] { Key.Z, Key.X, Key.Period, Key.Slash });

        Assert.That(
            settings.GetKeys(KeyMode.FourKey),
            Is.EqualTo(new[] { Key.Z, Key.X, Key.Period, Key.Slash }));
    }

    [Test]
    public void CompleteProfileRejectsDuplicateKeys()
    {
        var settings = new YokkoGameplaySettings();

        Assert.That(
            () => settings.SetBindings(
                KeyMode.FourKey,
                new[] { Key.Z, Key.X, Key.X, Key.Slash }),
            Throws.ArgumentException);
        Assert.That(
            settings.GetKeys(KeyMode.FourKey),
            Is.EqualTo(new[] { Key.D, Key.F, Key.J, Key.K }));
    }

    [Test]
    public void ConfiguredBindingsDriveGameplayLookup()
    {
        var settings = new YokkoGameplaySettings();
        settings.SetBinding(KeyMode.FourKey, 0, Key.A);

        KeyModeBindings bindings = KeyModeBindings.ForMode(
            KeyMode.FourKey,
            settings.GetKeys(KeyMode.FourKey));

        Assert.That(bindings.GetLane(Key.A), Is.Zero);
        Assert.That(bindings.GetLane(Key.D), Is.EqualTo(-1));
    }

    [Test]
    public void GameplayPreferencesPersistAcrossConfigInstances()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "gameplay-settings-config",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var firstSettings = new YokkoGameplaySettings();
            using (var firstConfig =
                   new YokkoConfigManager(new NativeStorage(directory)))
            {
                firstConfig.BindGameplaySettings(firstSettings);
                firstSettings.SetBinding(KeyMode.FourKey, 0, Key.A);
                firstSettings.SetBinding(KeyMode.SevenKey, 3, Key.V);
                firstSettings.SetScrollSpeed(26.4);
                firstSettings.ShowGameplayHud.Value = false;
                firstSettings.ShowHitError.Value = false;
                firstSettings.ShowLanePressFeedback.Value = false;
                Assert.That(firstConfig.Save(), Is.True);
            }

            var restoredSettings = new YokkoGameplaySettings();
            using (var restoredConfig =
                   new YokkoConfigManager(new NativeStorage(directory)))
            {
                restoredConfig.BindGameplaySettings(restoredSettings);
                Assert.That(
                    restoredSettings.GetKeys(KeyMode.FourKey)[0],
                    Is.EqualTo(Key.A));
                Assert.That(
                    restoredSettings.GetKeys(KeyMode.SevenKey)[3],
                    Is.EqualTo(Key.V));
                Assert.That(
                    restoredSettings.ScrollSpeed.Value,
                    Is.EqualTo(26.4).Within(0.001));
                Assert.That(restoredSettings.ShowGameplayHud.Value, Is.False);
                Assert.That(restoredSettings.ShowHitError.Value, Is.False);
                Assert.That(
                    restoredSettings.ShowLanePressFeedback.Value,
                    Is.False);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Test]
    public void ScrollSpeedMatchesOsuManiaScaleAndTiming()
    {
        var settings = new YokkoGameplaySettings();

        Assert.That(
            OsuManiaScrollSpeed.ComputeScrollTime(1),
            Is.EqualTo(11485));
        Assert.That(
            OsuManiaScrollSpeed.ComputeScrollTime(40),
            Is.EqualTo(287.125));

        settings.SetScrollSpeed(20.04);
        Assert.That(settings.ScrollSpeed.Value, Is.EqualTo(20));

        settings.AdjustScrollSpeed(1);
        Assert.That(settings.ScrollSpeed.Value, Is.EqualTo(21));

        settings.SetScrollSpeed(100);
        Assert.That(
            settings.ScrollSpeed.Value,
            Is.EqualTo(OsuManiaScrollSpeed.Maximum));

        settings.SetScrollSpeed(-100);
        Assert.That(
            settings.ScrollSpeed.Value,
            Is.EqualTo(OsuManiaScrollSpeed.Minimum));
    }
}
