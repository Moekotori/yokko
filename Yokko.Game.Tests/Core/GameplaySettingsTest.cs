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
using Yokko.Game.Screens.Settings;

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
            settings.ScrollSpeedAdjustmentMode.Value,
            Is.EqualTo(ScrollSpeedAdjustmentMode.OsuManiaScale));
        Assert.That(
            settings.ScrollDirection.Value,
            Is.EqualTo(ManiaScrollDirection.Downscroll));
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
        Assert.That(
            settings.JudgementDisplayDurationMilliseconds.Value,
            Is.EqualTo(
                YokkoGameplaySettings
                    .DefaultJudgementDisplayDurationMilliseconds));
        Assert.That(
            settings.JudgementOpacity.Value,
            Is.EqualTo(YokkoGameplaySettings.MaximumJudgementOpacity));
        Assert.That(settings.ShowJudgementHitError.Value, Is.True);
        Assert.That(settings.LayoutPlayfieldOffsetX.Value, Is.Zero);
        Assert.That(settings.LayoutPlayfieldOffsetY.Value, Is.Zero);
        Assert.That(settings.LayoutHudOffsetX.Value, Is.Zero);
        Assert.That(settings.LayoutHudOffsetY.Value, Is.Zero);
        Assert.That(settings.LayoutPlayfieldWidthScale.Value, Is.EqualTo(1));
        Assert.That(settings.LayoutPlayfieldHeightScale.Value, Is.EqualTo(1));
        Assert.That(settings.LayoutHudScaleX.Value, Is.EqualTo(1));
        Assert.That(settings.LayoutHudScaleY.Value, Is.EqualTo(1));
        Assert.That(settings.LayoutAccuracyOffsetX.Value, Is.Zero);
        Assert.That(settings.LayoutAccuracyOffsetY.Value, Is.Zero);
        Assert.That(settings.LayoutAccuracyScaleX.Value, Is.EqualTo(1));
        Assert.That(settings.LayoutAccuracyScaleY.Value, Is.EqualTo(1));
        Assert.That(settings.LayoutProgressOffsetX.Value, Is.Zero);
        Assert.That(settings.LayoutProgressOffsetY.Value, Is.Zero);
        Assert.That(settings.LayoutProgressScaleX.Value, Is.EqualTo(1));
        Assert.That(settings.LayoutProgressScaleY.Value, Is.EqualTo(1));
        Assert.That(settings.LayoutTimingBarOffsetX.Value, Is.Zero);
        Assert.That(settings.LayoutTimingBarOffsetY.Value, Is.Zero);
        Assert.That(settings.LayoutTimingBarScaleX.Value, Is.EqualTo(1));
        Assert.That(settings.LayoutTimingBarScaleY.Value, Is.EqualTo(1));
        Assert.That(settings.LayoutComboOffsetX.Value, Is.Zero);
        Assert.That(settings.LayoutComboOffsetY.Value, Is.Zero);
        Assert.That(settings.LayoutComboScaleX.Value, Is.EqualTo(1));
        Assert.That(settings.LayoutComboScaleY.Value, Is.EqualTo(1));
        Assert.That(settings.LayoutJudgementOffsetX.Value, Is.Zero);
        Assert.That(settings.LayoutJudgementOffsetY.Value, Is.Zero);
        Assert.That(settings.LayoutJudgementScaleX.Value, Is.EqualTo(1));
        Assert.That(settings.LayoutJudgementScaleY.Value, Is.EqualTo(1));
        Assert.That(settings.LayoutTopCoverRatio.Value, Is.Zero);
        Assert.That(settings.LayoutBottomCoverRatio.Value, Is.Zero);
        Assert.That(
            settings.BackgroundDim.Value,
            Is.EqualTo(YokkoGameplaySettings.MaximumBackgroundDim));
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
        Assert.That(
            settings.ToggleLayoutEditorUiKey.Value,
            Is.EqualTo(Key.BackSlash));
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
        Assert.That(settings.SupportedShortcutActions, Has.Count.EqualTo(14));
    }

    [Test]
    public void JudgementFeedbackSettingsClampAndSnap()
    {
        var settings = new YokkoGameplaySettings();

        settings.SetJudgementDisplayDuration(874);
        settings.SetJudgementOpacity(0.64);

        Assert.Multiple(() =>
        {
            Assert.That(
                settings.JudgementDisplayDurationMilliseconds.Value,
                Is.EqualTo(850));
            Assert.That(
                settings.JudgementOpacity.Value,
                Is.EqualTo(0.6).Within(0.001));
        });

        settings.SetJudgementDisplayDuration(double.MaxValue);
        settings.SetJudgementOpacity(double.MinValue);

        Assert.Multiple(() =>
        {
            Assert.That(
                settings.JudgementDisplayDurationMilliseconds.Value,
                Is.EqualTo(
                    YokkoGameplaySettings
                        .MaximumJudgementDisplayDurationMilliseconds));
            Assert.That(
                settings.JudgementOpacity.Value,
                Is.EqualTo(YokkoGameplaySettings.MinimumJudgementOpacity));
        });
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

        settings.SetShortcutBinding(
            ManiaShortcutAction.PauseOrBack,
            Key.BackSlash);
        Assert.That(settings.PauseOrBackKey.Value, Is.EqualTo(Key.BackSlash));
        Assert.That(
            settings.ToggleLayoutEditorUiKey.Value,
            Is.EqualTo(Key.BackSlash));
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
    public void GameplayLayoutCanResetWithoutChangingGameplayRules()
    {
        var settings = new YokkoGameplaySettings();
        settings.LayoutPlayfieldOffsetX.Value = 0.25;
        settings.LayoutPlayfieldOffsetY.Value = -0.1;
        settings.LayoutHudOffsetX.Value = -0.2;
        settings.LayoutHudOffsetY.Value = 0.3;
        settings.LayoutPlayfieldWidthScale.Value = 1.45;
        settings.LayoutPlayfieldHeightScale.Value = 0.8;
        settings.LayoutHudScaleX.Value = 1.2;
        settings.LayoutHudScaleY.Value = 0.75;
        settings.LayoutAccuracyOffsetX.Value = 0.14;
        settings.LayoutAccuracyOffsetY.Value = -0.08;
        settings.LayoutAccuracyScaleX.Value = 1.1;
        settings.LayoutAccuracyScaleY.Value = 0.9;
        settings.LayoutProgressOffsetX.Value = -0.16;
        settings.LayoutProgressOffsetY.Value = 0.12;
        settings.LayoutProgressScaleX.Value = 0.8;
        settings.LayoutProgressScaleY.Value = 1.25;
        settings.LayoutTimingBarOffsetX.Value = 0.18;
        settings.LayoutTimingBarOffsetY.Value = -0.12;
        settings.LayoutTimingBarScaleX.Value = 1.4;
        settings.LayoutTimingBarScaleY.Value = 0.65;
        settings.LayoutComboOffsetX.Value = 0.21;
        settings.LayoutComboOffsetY.Value = -0.17;
        settings.LayoutComboScaleX.Value = 1.25;
        settings.LayoutComboScaleY.Value = 0.8;
        settings.LayoutJudgementOffsetX.Value = -0.23;
        settings.LayoutJudgementOffsetY.Value = 0.14;
        settings.LayoutJudgementScaleX.Value = 1.3;
        settings.LayoutJudgementScaleY.Value = 0.7;
        settings.ReplayControlsOffsetX.Value = 0.2;
        settings.ReplayControlsOffsetY.Value = 0.35;
        settings.LayoutTopCoverRatio.Value = 0.32;
        settings.LayoutBottomCoverRatio.Value = 0.18;
        settings.BackgroundDim.Value = 0.75;
        settings.ScrollSpeed.Value = 12;

        settings.ResetGameplayLayout();

        Assert.Multiple(() =>
        {
            Assert.That(settings.LayoutPlayfieldOffsetX.Value, Is.Zero);
            Assert.That(settings.LayoutPlayfieldOffsetY.Value, Is.Zero);
            Assert.That(settings.LayoutHudOffsetX.Value, Is.Zero);
            Assert.That(settings.LayoutHudOffsetY.Value, Is.Zero);
            Assert.That(
                settings.LayoutPlayfieldWidthScale.Value,
                Is.EqualTo(1));
            Assert.That(
                settings.LayoutPlayfieldHeightScale.Value,
                Is.EqualTo(1));
            Assert.That(settings.LayoutHudScaleX.Value, Is.EqualTo(1));
            Assert.That(settings.LayoutHudScaleY.Value, Is.EqualTo(1));
            Assert.That(settings.LayoutAccuracyOffsetX.Value, Is.Zero);
            Assert.That(settings.LayoutAccuracyOffsetY.Value, Is.Zero);
            Assert.That(settings.LayoutAccuracyScaleX.Value, Is.EqualTo(1));
            Assert.That(settings.LayoutAccuracyScaleY.Value, Is.EqualTo(1));
            Assert.That(settings.LayoutProgressOffsetX.Value, Is.Zero);
            Assert.That(settings.LayoutProgressOffsetY.Value, Is.Zero);
            Assert.That(settings.LayoutProgressScaleX.Value, Is.EqualTo(1));
            Assert.That(settings.LayoutProgressScaleY.Value, Is.EqualTo(1));
            Assert.That(settings.LayoutTimingBarOffsetX.Value, Is.Zero);
            Assert.That(settings.LayoutTimingBarOffsetY.Value, Is.Zero);
            Assert.That(settings.LayoutTimingBarScaleX.Value, Is.EqualTo(1));
            Assert.That(settings.LayoutTimingBarScaleY.Value, Is.EqualTo(1));
            Assert.That(settings.LayoutComboOffsetX.Value, Is.Zero);
            Assert.That(settings.LayoutComboOffsetY.Value, Is.Zero);
            Assert.That(settings.LayoutComboScaleX.Value, Is.EqualTo(1));
            Assert.That(settings.LayoutComboScaleY.Value, Is.EqualTo(1));
            Assert.That(settings.LayoutJudgementOffsetX.Value, Is.Zero);
            Assert.That(settings.LayoutJudgementOffsetY.Value, Is.Zero);
            Assert.That(settings.LayoutJudgementScaleX.Value, Is.EqualTo(1));
            Assert.That(settings.LayoutJudgementScaleY.Value, Is.EqualTo(1));
            Assert.That(settings.ReplayControlsOffsetX.Value, Is.Zero);
            Assert.That(settings.ReplayControlsOffsetY.Value, Is.Zero);
            Assert.That(settings.LayoutTopCoverRatio.Value, Is.Zero);
            Assert.That(settings.LayoutBottomCoverRatio.Value, Is.Zero);
            Assert.That(
                settings.BackgroundDim.Value,
                Is.EqualTo(YokkoGameplaySettings.DefaultBackgroundDim));
            Assert.That(settings.ScrollSpeed.Value, Is.EqualTo(12));
        });
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
                firstSettings.SetShortcutBinding(
                    ManiaShortcutAction.ToggleLayoutEditorUi,
                    Key.H);
                firstSettings.SetScrollTimeMilliseconds(700);
                firstSettings.ScrollSpeedAdjustmentMode.Value =
                    ScrollSpeedAdjustmentMode.Milliseconds;
                firstSettings.ScrollDirection.Value =
                    ManiaScrollDirection.Upscroll;
                firstSettings.QuaverScrollRateNormalization.Value = 60;
                firstSettings.JudgementMode.Value =
                    JudgementMode.Etterna;
                firstSettings.SetEtternaJustice(8);
                firstSettings.ShowLanePressFeedback.Value = false;
                firstSettings.ShowTimingBar.Value = false;
                firstSettings.SetJudgementDisplayDuration(850);
                firstSettings.SetJudgementOpacity(0.6);
                firstSettings.ShowJudgementHitError.Value = false;
                firstSettings.LayoutPlayfieldOffsetX.Value = 0.22;
                firstSettings.LayoutPlayfieldOffsetY.Value = -0.14;
                firstSettings.LayoutHudOffsetX.Value = -0.18;
                firstSettings.LayoutHudOffsetY.Value = 0.2;
                firstSettings.LayoutPlayfieldWidthScale.Value = 1.35;
                firstSettings.LayoutPlayfieldHeightScale.Value = 0.85;
                firstSettings.LayoutHudScaleX.Value = 1.2;
                firstSettings.LayoutHudScaleY.Value = 0.75;
                firstSettings.LayoutAccuracyOffsetX.Value = 0.17;
                firstSettings.LayoutAccuracyOffsetY.Value = -0.09;
                firstSettings.LayoutAccuracyScaleX.Value = 1.15;
                firstSettings.LayoutAccuracyScaleY.Value = 0.9;
                firstSettings.LayoutProgressOffsetX.Value = -0.12;
                firstSettings.LayoutProgressOffsetY.Value = 0.16;
                firstSettings.LayoutProgressScaleX.Value = 0.85;
                firstSettings.LayoutProgressScaleY.Value = 1.3;
                firstSettings.LayoutTimingBarOffsetX.Value = 0.15;
                firstSettings.LayoutTimingBarOffsetY.Value = -0.11;
                firstSettings.LayoutTimingBarScaleX.Value = 1.4;
                firstSettings.LayoutTimingBarScaleY.Value = 0.7;
                firstSettings.LayoutComboOffsetX.Value = 0.19;
                firstSettings.LayoutComboOffsetY.Value = -0.13;
                firstSettings.LayoutComboScaleX.Value = 1.35;
                firstSettings.LayoutComboScaleY.Value = 0.8;
                firstSettings.LayoutJudgementOffsetX.Value = -0.16;
                firstSettings.LayoutJudgementOffsetY.Value = 0.24;
                firstSettings.LayoutJudgementScaleX.Value = 1.45;
                firstSettings.LayoutJudgementScaleY.Value = 0.75;
                firstSettings.ReplayControlsOffsetX.Value = 0.2;
                firstSettings.ReplayControlsOffsetY.Value = 0.35;
                firstSettings.LayoutTopCoverRatio.Value = 0.28;
                firstSettings.LayoutBottomCoverRatio.Value = 0.12;
                firstSettings.BackgroundDim.Value = 0.65;
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
                    restoredSettings.ToggleLayoutEditorUiKey.Value,
                    Is.EqualTo(Key.H));
                Assert.That(
                    OsuManiaScrollSpeed.ComputeScrollTime(
                        restoredSettings.ScrollSpeed.Value),
                    Is.EqualTo(700).Within(0.02));
                Assert.That(
                    restoredSettings.ScrollSpeedAdjustmentMode.Value,
                    Is.EqualTo(ScrollSpeedAdjustmentMode.Milliseconds));
                Assert.That(
                    restoredSettings.ScrollDirection.Value,
                    Is.EqualTo(ManiaScrollDirection.Upscroll));
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
                    restoredSettings
                        .JudgementDisplayDurationMilliseconds.Value,
                    Is.EqualTo(850));
                Assert.That(
                    restoredSettings.JudgementOpacity.Value,
                    Is.EqualTo(0.6).Within(0.001));
                Assert.That(
                    restoredSettings.ShowJudgementHitError.Value,
                    Is.False);
                Assert.That(
                    restoredSettings.LayoutPlayfieldOffsetX.Value,
                    Is.EqualTo(0.22).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutPlayfieldOffsetY.Value,
                    Is.EqualTo(-0.14).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutHudOffsetX.Value,
                    Is.EqualTo(-0.18).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutHudOffsetY.Value,
                    Is.EqualTo(0.2).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutPlayfieldWidthScale.Value,
                    Is.EqualTo(1.35).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutPlayfieldHeightScale.Value,
                    Is.EqualTo(0.85).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutHudScaleX.Value,
                    Is.EqualTo(1.2).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutHudScaleY.Value,
                    Is.EqualTo(0.75).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutAccuracyOffsetX.Value,
                    Is.EqualTo(0.17).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutAccuracyOffsetY.Value,
                    Is.EqualTo(-0.09).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutAccuracyScaleX.Value,
                    Is.EqualTo(1.15).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutAccuracyScaleY.Value,
                    Is.EqualTo(0.9).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutProgressOffsetX.Value,
                    Is.EqualTo(-0.12).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutProgressOffsetY.Value,
                    Is.EqualTo(0.16).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutProgressScaleX.Value,
                    Is.EqualTo(0.85).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutProgressScaleY.Value,
                    Is.EqualTo(1.3).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutTimingBarOffsetX.Value,
                    Is.EqualTo(0.15).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutTimingBarOffsetY.Value,
                    Is.EqualTo(-0.11).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutTimingBarScaleX.Value,
                    Is.EqualTo(1.4).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutTimingBarScaleY.Value,
                    Is.EqualTo(0.7).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutComboOffsetX.Value,
                    Is.EqualTo(0.19).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutComboOffsetY.Value,
                    Is.EqualTo(-0.13).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutComboScaleX.Value,
                    Is.EqualTo(1.35).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutComboScaleY.Value,
                    Is.EqualTo(0.8).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutJudgementOffsetX.Value,
                    Is.EqualTo(-0.16).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutJudgementOffsetY.Value,
                    Is.EqualTo(0.24).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutJudgementScaleX.Value,
                    Is.EqualTo(1.45).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutJudgementScaleY.Value,
                    Is.EqualTo(0.75).Within(0.001));
                Assert.That(
                    restoredSettings.ReplayControlsOffsetX.Value,
                    Is.EqualTo(0.2).Within(0.001));
                Assert.That(
                    restoredSettings.ReplayControlsOffsetY.Value,
                    Is.EqualTo(0.35).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutTopCoverRatio.Value,
                    Is.EqualTo(0.28).Within(0.001));
                Assert.That(
                    restoredSettings.LayoutBottomCoverRatio.Value,
                    Is.EqualTo(0.12).Within(0.001));
                Assert.That(
                    restoredSettings.BackgroundDim.Value,
                    Is.EqualTo(0.65).Within(0.001));
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
    public void KeyProfileBeforeLayoutEditorShortcutStillImports()
    {
        var source = new YokkoGameplaySettings();
        source.SetBinding(KeyMode.FourKey, 0, Key.Z);
        string legacyV3 = GameplayKeyProfileCodec.Encode(source)
            .Replace(
                ",ToggleLayoutEditorUi:BackSlash",
                string.Empty,
                StringComparison.Ordinal);

        var restored = new YokkoGameplaySettings();
        GameplayKeyProfileCodec.DecodeAndApply(legacyV3, restored);

        Assert.That(restored.GetKeys(KeyMode.FourKey)[0], Is.EqualTo(Key.Z));
        Assert.That(
            restored.ToggleLayoutEditorUiKey.Value,
            Is.EqualTo(Key.BackSlash));
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

        settings.SetScrollTimeMilliseconds(700);
        Assert.That(
            OsuManiaScrollSpeed.ComputeScrollTime(
                settings.ScrollSpeed.Value),
            Is.EqualTo(700).Within(0.02));

        settings.AdjustScrollTimeMilliseconds(-1);
        Assert.That(
            OsuManiaScrollSpeed.ComputeScrollTime(
                settings.ScrollSpeed.Value),
            Is.EqualTo(699).Within(0.02));

        settings.AdjustScrollTimeMilliseconds(1);
        Assert.That(
            OsuManiaScrollSpeed.ComputeScrollTime(
                settings.ScrollSpeed.Value),
            Is.EqualTo(700).Within(0.02));

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
    public void ScrollSpeedSliderSeparatesWholeAndFineAdjustment()
    {
        Assert.That(
            GameplayScrollSpeedSlider.ValueFromProgress(
                0,
                ScrollSpeedAdjustmentMode.OsuManiaScale),
            Is.EqualTo(OsuManiaScrollSpeed.Minimum));
        Assert.That(
            GameplayScrollSpeedSlider.ValueFromProgress(
                1,
                ScrollSpeedAdjustmentMode.OsuManiaScale),
            Is.EqualTo(OsuManiaScrollSpeed.Maximum));
        Assert.That(
            GameplayScrollSpeedSlider.ValueFromProgress(
                0.5,
                ScrollSpeedAdjustmentMode.OsuManiaScale),
            Is.EqualTo(21));
        Assert.That(
            GameplayScrollSpeedSlider.ValueFromProgress(
                0.5,
                ScrollSpeedAdjustmentMode.Milliseconds),
            Is.EqualTo(20.5));
        Assert.That(
            GameplayScrollSpeedSlider.AdjustForScroll(34, 1),
            Is.EqualTo(35));
        Assert.That(
            GameplayScrollSpeedSlider.AdjustForScroll(34, -1),
            Is.EqualTo(33));
        Assert.That(
            GameplayScrollSpeedSlider.AdjustForScroll(34.4, 1),
            Is.EqualTo(35));
        Assert.That(
            GameplayScrollSpeedSlider.AdjustForScroll(40, 1),
            Is.EqualTo(40));
        Assert.That(
            OsuManiaScrollSpeed.SnapToWholeStep(25.3),
            Is.EqualTo(25));
        Assert.That(
            GameplayScrollSpeedSlider.FineScrollTimeDeltaForDirection(1),
            Is.EqualTo(-1),
            "scrolling up or pressing right/up should make notes faster");
        Assert.That(
            GameplayScrollSpeedSlider.FineScrollTimeDeltaForDirection(-1),
            Is.EqualTo(1),
            "scrolling down or pressing left/down should make notes slower");
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
