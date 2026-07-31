using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Platform;
using osuTK.Input;
using Yokko.Core.Gameplay;
using Yokko.Core.Scoring;
using Yokko.Game.Configuration;
using Yokko.Game.Gameplay;
using Yokko.Game.Importing;
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
        Assert.That(
            settings.QuaverScrollRateNormalization.Value,
            Is.Zero);
        Assert.That(
            settings.JudgementMode.Value,
            Is.EqualTo(JudgementMode.Yokko));
        Assert.That(
            settings.EtternaJustice.Value,
            Is.EqualTo(JudgementConfiguration.DefaultEtternaJustice));
        Assert.That(settings.ShowLanePressFeedback.Value, Is.True);
        Assert.That(settings.ShowTimingBar.Value, Is.True);
        Assert.That(settings.KeysoundsEnabled.Value, Is.False);
        Assert.That(settings.MinesEnabled.Value, Is.True);
        Assert.That(settings.PauseWhenUnfocused.Value, Is.True);
        Assert.That(settings.ResumeCountdownEnabled.Value, Is.True);
        Assert.That(
            settings.ResumeCountdownMilliseconds.Value,
            Is.EqualTo(
                YokkoGameplaySettings.DefaultResumeCountdownMilliseconds));
        Assert.That(settings.DecreaseScrollSpeedKey.Value, Is.EqualTo(Key.F3));
        Assert.That(settings.IncreaseScrollSpeedKey.Value, Is.EqualTo(Key.F4));
        Assert.That(settings.PauseOrBackKey.Value, Is.EqualTo(Key.Escape));
        Assert.That(settings.SkipIntroKey.Value, Is.EqualTo(Key.Space));
        Assert.That(settings.QuickRetryKey.Value, Is.EqualTo(Key.Tilde));
        Assert.That(settings.MenuPreviousKey.Value, Is.EqualTo(Key.Up));
        Assert.That(
            settings.MenuPreviousAlternateKey.Value,
            Is.EqualTo(Key.W));
        Assert.That(settings.MenuNextKey.Value, Is.EqualTo(Key.Down));
        Assert.That(settings.MenuNextAlternateKey.Value, Is.EqualTo(Key.S));
        Assert.That(settings.ConfirmKey.Value, Is.EqualTo(Key.Enter));
        Assert.That(settings.ConfirmAlternateKey.Value, Is.EqualTo(Key.Space));
        Assert.That(settings.RetryKey.Value, Is.EqualTo(Key.R));
        Assert.That(settings.WatchReplayKey.Value, Is.EqualTo(Key.V));
        Assert.That(settings.SupportedShortcutActions, Has.Count.EqualTo(13));
    }

    [Test]
    public void ExtendedKeyModesUseLazerDefaultLayout()
    {
        KeyModeBindings oneKey =
            KeyModeBindings.ForMode(KeyMode.OneKey);
        KeyModeBindings sixKey =
            KeyModeBindings.ForMode(KeyMode.SixKey);
        KeyModeBindings tenKey =
            KeyModeBindings.ForMode(KeyMode.TenKey);

        Assert.That(oneKey.GetLane(Key.Space), Is.Zero);
        Assert.That(sixKey.GetLane(Key.S), Is.Zero);
        Assert.That(sixKey.GetLane(Key.L), Is.EqualTo(5));
        Assert.That(tenKey.GetLane(Key.V), Is.EqualTo(4));
        Assert.That(tenKey.GetLane(Key.N), Is.EqualTo(5));
        Assert.That(tenKey.GetLane(Key.Semicolon), Is.EqualTo(9));
    }

    [Test]
    public void EveryLazerManiaLayoutCanBeCustomised()
    {
        var settings = new YokkoGameplaySettings();

        Assert.That(settings.SupportedKeyModes, Has.Count.EqualTo(15));
        foreach (KeyMode mode in settings.SupportedKeyModes)
        {
            Assert.That(
                settings.GetKeys(mode),
                Has.Count.EqualTo((int)mode),
                mode.ToString());
        }

        settings.SetBinding(KeyMode.TenKey, 0, Key.Z);
        settings.SetBinding(KeyMode.TwentyKey, 19, Key.Slash);

        Assert.That(settings.GetKeys(KeyMode.TenKey)[0], Is.EqualTo(Key.Z));
        Assert.That(
            settings.GetKeys(KeyMode.TwentyKey)[19],
            Is.EqualTo(Key.Slash));
    }

    [Test]
    public void DualStageModesUseLazerDefaultLayout()
    {
        KeyModeBindings bindings = KeyModeBindings.ForMode(
            KeyMode.FourteenKey,
            2);

        Assert.Multiple(() =>
        {
            Assert.That(bindings.KeyCount, Is.EqualTo(14));
            Assert.That(bindings.GetLane(Key.W), Is.Zero);
            Assert.That(bindings.GetLane(Key.V), Is.EqualTo(3));
            Assert.That(bindings.GetLane(Key.P), Is.EqualTo(6));
            Assert.That(bindings.GetLane(Key.D), Is.EqualTo(7));
            Assert.That(bindings.GetLane(Key.B), Is.EqualTo(10));
            Assert.That(bindings.GetLane(Key.L), Is.EqualTo(13));
        });
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
    public void DuplicateManiaShortcutSwapsActions()
    {
        var settings = new YokkoGameplaySettings();

        ManiaShortcutBindingChange change =
            settings.SetShortcutBindingWithResult(
            ManiaShortcutAction.DecreaseScrollSpeed,
            Key.F4);

        Assert.That(settings.DecreaseScrollSpeedKey.Value, Is.EqualTo(Key.F4));
        Assert.That(settings.IncreaseScrollSpeedKey.Value, Is.EqualTo(Key.F3));
        Assert.That(
            change.SwappedAction,
            Is.EqualTo(ManiaShortcutAction.IncreaseScrollSpeed));
        Assert.That(settings.ModifiedShortcutBindingCount, Is.EqualTo(2));
    }

    [Test]
    public void ShortcutConflictResolutionIsContextAware()
    {
        var settings = new YokkoGameplaySettings();

        settings.SetShortcutBinding(
            ManiaShortcutAction.MenuPrevious,
            Key.W);
        Assert.That(settings.MenuPreviousKey.Value, Is.EqualTo(Key.W));
        Assert.That(
            settings.MenuPreviousAlternateKey.Value,
            Is.EqualTo(Key.Up));

        settings.SetShortcutBinding(
            ManiaShortcutAction.SkipIntro,
            Key.F3);
        Assert.That(settings.SkipIntroKey.Value, Is.EqualTo(Key.F3));
        Assert.That(settings.DecreaseScrollSpeedKey.Value, Is.EqualTo(Key.F3));
    }

    [Test]
    public void EveryShortcutCanResetIndividuallyOrTogether()
    {
        var settings = new YokkoGameplaySettings();

        settings.SetShortcutBinding(
            ManiaShortcutAction.PauseOrBack,
            Key.F10);
        settings.SetShortcutBinding(
            ManiaShortcutAction.WatchReplay,
            Key.F11);
        settings.ResetShortcutBinding(ManiaShortcutAction.PauseOrBack);

        Assert.That(settings.PauseOrBackKey.Value, Is.EqualTo(Key.Escape));
        Assert.That(settings.WatchReplayKey.Value, Is.EqualTo(Key.F11));

        settings.ResetShortcutBindings();
        Assert.That(settings.WatchReplayKey.Value, Is.EqualTo(Key.V));
        Assert.That(settings.ModifiedShortcutBindingCount, Is.Zero);
        Assert.That(settings.SupportedShortcutActions.All(
            settings.IsShortcutBindingDefault), Is.True);
        Assert.That(settings.SupportedShortcutActions.All(action =>
            settings.GetShortcutBinding(action) != Key.Unknown), Is.True);
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
    public void PresetsAndCrossModeCopyKeepProfilesUnique()
    {
        var settings = new YokkoGameplaySettings();

        settings.ApplyBindingPreset(
            KeyMode.FourKey,
            GameplayKeyPreset.LeftHanded);
        Assert.That(
            settings.GetKeys(KeyMode.FourKey),
            Is.EqualTo(new[] { Key.A, Key.S, Key.D, Key.F }));

        settings.CopyBindingsToOtherMode(KeyMode.FourKey);
        IReadOnlyList<Key> sevenKeys =
            settings.GetKeys(KeyMode.SevenKey);
        Assert.Multiple(() =>
        {
            Assert.That(sevenKeys, Has.Count.EqualTo(7));
            Assert.That(sevenKeys.Distinct().Count(), Is.EqualTo(7));
            Assert.That(
                new[]
                {
                    sevenKeys[1],
                    sevenKeys[2],
                    sevenKeys[4],
                    sevenKeys[5],
                },
                Is.EqualTo(new[] { Key.A, Key.S, Key.D, Key.F }));
        });

        settings.CopyBindingsToOtherMode(KeyMode.SevenKey);
        Assert.That(
            settings.GetKeys(KeyMode.FourKey),
            Is.EqualTo(new[] { Key.A, Key.S, Key.D, Key.F }));
    }

    [Test]
    public void ClipboardProfileRoundTripsAtomically()
    {
        var source = new YokkoGameplaySettings();
        source.ApplyBindingPreset(
            KeyMode.FourKey,
            GameplayKeyPreset.Split);
        source.ApplyBindingPreset(
            KeyMode.SevenKey,
            GameplayKeyPreset.LeftHanded);
        string encoded = GameplayKeyProfileCodec.Encode(source);

        var restored = new YokkoGameplaySettings();
        GameplayKeyProfileCodec.DecodeAndApply(encoded, restored);

        Assert.Multiple(() =>
        {
            Assert.That(
                restored.GetKeys(KeyMode.FourKey),
                Is.EqualTo(source.GetKeys(KeyMode.FourKey)));
            Assert.That(
                restored.GetKeys(KeyMode.SevenKey),
                Is.EqualTo(source.GetKeys(KeyMode.SevenKey)));
        });

        Assert.That(
            () => GameplayKeyProfileCodec.DecodeAndApply(
                "YOKKO-KEYS-V1|4K=Z,X,X,Slash|7K=S,D,F,Space,J,K,L",
                restored),
            Throws.TypeOf<FormatException>());
        Assert.That(
            restored.GetKeys(KeyMode.FourKey),
            Is.EqualTo(source.GetKeys(KeyMode.FourKey)));
    }

    [Test]
    public void CalibrationSuggestsTheInverseMedianTapOffset()
    {
        var calibration = new GameplayCalibrationSession(10_000);

        for (int beat = 0; beat < 12; beat++)
        {
            double expected =
                10_000
                + GameplayCalibrationSession.LeadInMilliseconds
                + beat * GameplayCalibrationSession.BeatIntervalMilliseconds;
            Assert.That(
                calibration.TryRecordTap(expected + 18 + beat % 3),
                Is.True);
            Assert.That(
                calibration.TryRecordTap(expected + 24),
                Is.False,
                "Only the first key on each beat should be sampled.");
        }

        Assert.Multiple(() =>
        {
            Assert.That(calibration.SampleCount, Is.EqualTo(12));
            Assert.That(calibration.HasRecommendation, Is.True);
            Assert.That(
                calibration.SuggestedOffsetMilliseconds,
                Is.EqualTo(-19));
            Assert.That(
                calibration.IsComplete(
                    10_000
                    + GameplayCalibrationSession.DurationMilliseconds),
                Is.True);
        });
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
                firstSettings.SetBinding(KeyMode.TenKey, 0, Key.Z);
                firstSettings.SetBinding(KeyMode.TwentyKey, 19, Key.Slash);
                firstSettings.SetShortcutBinding(
                    ManiaShortcutAction.DecreaseScrollSpeed,
                    Key.F7);
                firstSettings.SetShortcutBinding(
                    ManiaShortcutAction.IncreaseScrollSpeed,
                    Key.F8);
                firstSettings.SetShortcutBinding(
                    ManiaShortcutAction.PauseOrBack,
                    Key.F10);
                firstSettings.SetShortcutBinding(
                    ManiaShortcutAction.QuickRetry,
                    Key.F11);
                firstSettings.SetScrollSpeed(26.4);
                firstSettings.QuaverScrollRateNormalization.Value = 60;
                firstSettings.JudgementMode.Value =
                    JudgementMode.Etterna;
                firstSettings.SetEtternaJustice(8);
                firstSettings.ShowLanePressFeedback.Value = false;
                firstSettings.ShowTimingBar.Value = false;
                firstSettings.KeysoundsEnabled.Value = false;
                firstSettings.MinesEnabled.Value = false;
                firstSettings.PauseWhenUnfocused.Value = false;
                firstSettings.ResumeCountdownEnabled.Value = false;
                firstSettings.ResumeCountdownMilliseconds.Value = 1500;
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
                    restoredSettings.GetKeys(KeyMode.TenKey)[0],
                    Is.EqualTo(Key.Z));
                Assert.That(
                    restoredSettings.GetKeys(KeyMode.TwentyKey)[19],
                    Is.EqualTo(Key.Slash));
                Assert.That(
                    restoredSettings.DecreaseScrollSpeedKey.Value,
                    Is.EqualTo(Key.F7));
                Assert.That(
                    restoredSettings.IncreaseScrollSpeedKey.Value,
                    Is.EqualTo(Key.F8));
                Assert.That(
                    restoredSettings.PauseOrBackKey.Value,
                    Is.EqualTo(Key.F10));
                Assert.That(
                    restoredSettings.QuickRetryKey.Value,
                    Is.EqualTo(Key.F11));
                Assert.That(
                    restoredSettings.ScrollSpeed.Value,
                    Is.EqualTo(26.4).Within(0.001));
                Assert.That(
                    restoredSettings.QuaverScrollRateNormalization.Value,
                    Is.EqualTo(60));
                Assert.That(
                    restoredSettings.JudgementMode.Value,
                    Is.EqualTo(JudgementMode.Etterna));
                Assert.That(
                    restoredSettings.EtternaJustice.Value,
                    Is.EqualTo(8));
                Assert.That(
                    restoredSettings.ShowLanePressFeedback.Value,
                    Is.False);
                Assert.That(
                    restoredSettings.ShowTimingBar.Value,
                    Is.False);
                Assert.That(
                    restoredSettings.KeysoundsEnabled.Value,
                    Is.False);
                Assert.That(
                    restoredSettings.MinesEnabled.Value,
                    Is.False);
                Assert.That(
                    restoredSettings.PauseWhenUnfocused.Value,
                    Is.False);
                Assert.That(
                    restoredSettings.ResumeCountdownEnabled.Value,
                    Is.False);
                Assert.That(
                    restoredSettings.ResumeCountdownMilliseconds.Value,
                    Is.EqualTo(1500).Within(0.001));
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Test]
    public void BmsScratchPreferencePersistsAcrossConfigInstances()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "bms-scratch-config",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var firstSettings = new YokkoImportSettings();
            using (var firstConfig =
                   new YokkoConfigManager(new NativeStorage(directory)))
            {
                firstConfig.BindImportSettings(firstSettings);
                firstSettings.EnableBmsScratch.Value = true;
                Assert.That(firstConfig.Save(), Is.True);
            }

            var restoredSettings = new YokkoImportSettings();
            using (var restoredConfig =
                   new YokkoConfigManager(new NativeStorage(directory)))
            {
                restoredConfig.BindImportSettings(restoredSettings);
                Assert.That(
                    restoredSettings.EnableBmsScratch.Value,
                    Is.True);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Test]
    public void HitSoundPreferenceSavesImmediately()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "hitsound-settings-config",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            using (var firstConfig =
                   new YokkoConfigManager(new NativeStorage(directory)))
            {
                var firstSettings = new YokkoGameplaySettings();
                firstConfig.BindGameplaySettings(firstSettings);

                Assert.That(firstSettings.KeysoundsEnabled.Value, Is.False);
                firstSettings.KeysoundsEnabled.Value = true;

                using var restoredConfig =
                    new YokkoConfigManager(new NativeStorage(directory));
                var restoredSettings = new YokkoGameplaySettings();
                restoredConfig.BindGameplaySettings(restoredSettings);

                Assert.That(restoredSettings.KeysoundsEnabled.Value, Is.True);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Test]
    public void KeyProfileRoundTripIncludesEveryManiaMode()
    {
        var source = new YokkoGameplaySettings();
        source.SetBinding(KeyMode.TwoKey, 0, Key.Z);
        source.SetBinding(KeyMode.TwentyKey, 19, Key.Slash);
        source.SetShortcutBinding(
            ManiaShortcutAction.PauseOrBack,
            Key.F10);
        source.SetShortcutBinding(
            ManiaShortcutAction.WatchReplay,
            Key.F11);

        string encoded = GameplayKeyProfileCodec.Encode(source);
        var restored = new YokkoGameplaySettings();
        GameplayKeyProfileCodec.DecodeAndApply(encoded, restored);

        Assert.That(encoded, Does.StartWith("YOKKO-KEYS-V3|1K="));
        foreach (KeyMode mode in source.SupportedKeyModes)
        {
            Assert.That(
                restored.GetKeys(mode),
                Is.EqualTo(source.GetKeys(mode)),
                mode.ToString());
        }
        foreach (ManiaShortcutAction action in source.SupportedShortcutActions)
        {
            Assert.That(
                restored.GetShortcutBinding(action),
                Is.EqualTo(source.GetShortcutBinding(action)),
                action.ToString());
        }
    }

    [Test]
    public void LegacyFourAndSevenKeyProfileStillImports()
    {
        var settings = new YokkoGameplaySettings();

        GameplayKeyProfileCodec.DecodeAndApply(
            "YOKKO-KEYS-V1|4K=Z,X,Period,Slash|7K=A,S,D,Space,J,K,L",
            settings);

        Assert.That(
            settings.GetKeys(KeyMode.FourKey),
            Is.EqualTo(new[] { Key.Z, Key.X, Key.Period, Key.Slash }));
        Assert.That(
            settings.GetKeys(KeyMode.SevenKey),
            Is.EqualTo(new[]
            {
                Key.A,
                Key.S,
                Key.D,
                Key.Space,
                Key.J,
                Key.K,
                Key.L,
            }));
        Assert.That(
            settings.GetKeys(KeyMode.TenKey)[0],
            Is.EqualTo(Key.A));
        Assert.That(
            settings.GetKeys(KeyMode.TenKey)[9],
            Is.EqualTo(Key.Semicolon));
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

    [Test]
    public void LegacySkinHitPositionPreservesOsuManiaScrollVelocity()
    {
        double baseTimeRange = OsuManiaScrollSpeed.ComputeScrollTime(34);

        Assert.That(
            OsuManiaScrollSpeed.ComputeScrollTime(34, 402),
            Is.EqualTo(baseTimeRange).Within(0.001));
        Assert.That(
            OsuManiaScrollSpeed.ComputeScrollTime(34, 460),
            Is.EqualTo(baseTimeRange * 460 / 402).Within(0.001));
        Assert.That(
            OsuManiaScrollSpeed.ComputeScrollTime(34, 500),
            Is.EqualTo(baseTimeRange * 480 / 402).Within(0.001));
    }
}
