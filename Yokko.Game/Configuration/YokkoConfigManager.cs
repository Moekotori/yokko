using System;
using osu.Framework.Configuration;
using osu.Framework.Platform;
using Yokko.Audio;
using Yokko.Game.Audio;
using Yokko.Game.Diagnostics;
using Yokko.Game.Gameplay;
using Yokko.Game.Importing;
using Yokko.Game.Presentation;
using Yokko.Game.Resources;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Configuration;

internal enum YokkoSetting
{
    HomeMusicEnabled,
    AudioMasterVolume,
    AudioMusicVolume,
    AudioHitSoundVolume,
    AudioBackgroundMode,
    AudioBackend,
    AudioDeviceId,
    AudioAsioDeviceId,
    AudioBufferSize,
    AudioOffsetMilliseconds,
    AudioManualPlaybackRatePitchMode,
    ImportPreferKeysounds,
    ImportPreferSscSimfiles,
    ImportEnableBmsScratch,
    ImportShowCompatibilityWarnings,
    ExternalOsuSongsPath,
    ResourceRootPath,
    GameplayFourKeyLane1,
    GameplayFourKeyLane2,
    GameplayFourKeyLane3,
    GameplayFourKeyLane4,
    GameplaySevenKeyLane1,
    GameplaySevenKeyLane2,
    GameplaySevenKeyLane3,
    GameplaySevenKeyLane4,
    GameplaySevenKeyLane5,
    GameplaySevenKeyLane6,
    GameplaySevenKeyLane7,
    GameplayKeyProfiles,
    ManiaPauseOrBackKey,
    ManiaToggleLayoutEditorUiKey,
    ManiaSkipIntroKey,
    ManiaQuickRetryKey,
    ManiaDecreaseScrollSpeedKey,
    ManiaIncreaseScrollSpeedKey,
    ManiaMenuPreviousKey,
    ManiaMenuPreviousAlternateKey,
    ManiaMenuNextKey,
    ManiaMenuNextAlternateKey,
    ManiaConfirmKey,
    ManiaConfirmAlternateKey,
    ManiaRetryKey,
    ManiaWatchReplayKey,
    ManiaScrollSpeed,
    ManiaScrollSpeedAdjustmentMode,
    ManiaScrollDirection,
    QuaverScrollRateNormalization,
    GameplayJudgementMode,
    GameplayEtternaJustice,
    GameplayShowLanePressFeedback,
    GameplayShowTimingBar,
    GameplayJudgementDisplayDuration,
    GameplayJudgementOpacity,
    GameplayShowJudgementHitError,
    GameplayLayoutPlayfieldOffsetX,
    GameplayLayoutPlayfieldOffsetY,
    GameplayLayoutHudOffsetX,
    GameplayLayoutHudOffsetY,
    GameplayLayoutPlayfieldWidthScale,
    GameplayLayoutPlayfieldHeightScale,
    GameplayLayoutHudScaleX,
    GameplayLayoutHudScaleY,
    GameplayLayoutTimingBarOffsetX,
    GameplayLayoutTimingBarOffsetY,
    GameplayLayoutTimingBarScaleX,
    GameplayLayoutTimingBarScaleY,
    GameplayLayoutComboOffsetX,
    GameplayLayoutComboOffsetY,
    GameplayLayoutComboScaleX,
    GameplayLayoutComboScaleY,
    GameplayLayoutJudgementOffsetX,
    GameplayLayoutJudgementOffsetY,
    GameplayLayoutJudgementScaleX,
    GameplayLayoutJudgementScaleY,
    GameplayReplayControlsOffsetX,
    GameplayReplayControlsOffsetY,
    GameplayLayoutTopCoverRatio,
    GameplayLayoutBottomCoverRatio,
    GameplayBackgroundDim,
    GameplayKeysoundsEnabled,
    GameplayMinesEnabled,
    GameplayPauseWhenUnfocused,
    GameplayResumeCountdownEnabled,
    GameplayResumeCountdownMilliseconds,
    ManiaModConfiguration,
    ManiaActiveMods,
    DisplayUiScale,
    DisplayFrameLimit,
    DisplayShowPerformanceReadout,
    DisplayFastAltTab,
    DisplayBackgroundFrameRate,
    DisplayFullscreenRefreshRate,
    DisplayDifficultyRatingMode,
    WindowMaximised,
    DebugConsoleVisible,
    SkinSelectedId,
    SkinShowComboBursts,
    SkinLongNoteCutEnabled,
    SkinLongNoteCutAmount,
    SettingsLastPage,
}

internal sealed class YokkoConfigManager : IniConfigManager<YokkoSetting>
{
    protected override string Filename => "yokko.ini";

    public YokkoConfigManager(Storage storage)
        : base(storage)
    {
    }

    protected override void InitialiseDefaults()
    {
        SetDefault(YokkoSetting.HomeMusicEnabled, true);
        SetDefault(YokkoSetting.AudioMasterVolume, 1.0, 0.0, 1.0, 0.01);
        SetDefault(YokkoSetting.AudioMusicVolume, 1.0, 0.0, 1.0, 0.01);
        SetDefault(YokkoSetting.AudioHitSoundVolume, 1.0, 0.0, 1.0, 0.01);
        SetDefault(
            YokkoSetting.AudioBackgroundMode,
            BackgroundAudioMode.KeepPlaying);
        SetDefault(
            YokkoSetting.AudioBackend,
            AudioBackendKind.WasapiExclusive);
        SetDefault(YokkoSetting.AudioDeviceId, string.Empty);
        SetDefault(YokkoSetting.AudioAsioDeviceId, string.Empty);
        SetDefault(YokkoSetting.AudioBufferSize, 64, 64, 2048);
        SetDefault(
            YokkoSetting.AudioOffsetMilliseconds,
            0.0,
            -200.0,
            200.0,
            1.0);
        SetDefault(
            YokkoSetting.AudioManualPlaybackRatePitchMode,
            AudioPitchMode.Preserve);
        SetDefault(YokkoSetting.ImportPreferKeysounds, true);
        SetDefault(YokkoSetting.ImportPreferSscSimfiles, true);
        SetDefault(YokkoSetting.ImportEnableBmsScratch, false);
        SetDefault(YokkoSetting.ImportShowCompatibilityWarnings, true);
        SetDefault(YokkoSetting.ExternalOsuSongsPath, string.Empty);
        SetDefault(YokkoSetting.ResourceRootPath, string.Empty);
        SetDefault(YokkoSetting.GameplayFourKeyLane1, osuTK.Input.Key.D);
        SetDefault(YokkoSetting.GameplayFourKeyLane2, osuTK.Input.Key.F);
        SetDefault(YokkoSetting.GameplayFourKeyLane3, osuTK.Input.Key.J);
        SetDefault(YokkoSetting.GameplayFourKeyLane4, osuTK.Input.Key.K);
        SetDefault(YokkoSetting.GameplaySevenKeyLane1, osuTK.Input.Key.S);
        SetDefault(YokkoSetting.GameplaySevenKeyLane2, osuTK.Input.Key.D);
        SetDefault(YokkoSetting.GameplaySevenKeyLane3, osuTK.Input.Key.F);
        SetDefault(YokkoSetting.GameplaySevenKeyLane4, osuTK.Input.Key.Space);
        SetDefault(YokkoSetting.GameplaySevenKeyLane5, osuTK.Input.Key.J);
        SetDefault(YokkoSetting.GameplaySevenKeyLane6, osuTK.Input.Key.K);
        SetDefault(YokkoSetting.GameplaySevenKeyLane7, osuTK.Input.Key.L);
        SetDefault(YokkoSetting.GameplayKeyProfiles, string.Empty);
        SetDefault(YokkoSetting.ManiaPauseOrBackKey, osuTK.Input.Key.Escape);
        SetDefault(
            YokkoSetting.ManiaToggleLayoutEditorUiKey,
            osuTK.Input.Key.BackSlash);
        SetDefault(YokkoSetting.ManiaSkipIntroKey, osuTK.Input.Key.Space);
        SetDefault(YokkoSetting.ManiaQuickRetryKey, osuTK.Input.Key.Tilde);
        SetDefault(YokkoSetting.ManiaDecreaseScrollSpeedKey, osuTK.Input.Key.F3);
        SetDefault(YokkoSetting.ManiaIncreaseScrollSpeedKey, osuTK.Input.Key.F4);
        SetDefault(YokkoSetting.ManiaMenuPreviousKey, osuTK.Input.Key.Up);
        SetDefault(
            YokkoSetting.ManiaMenuPreviousAlternateKey,
            osuTK.Input.Key.W);
        SetDefault(YokkoSetting.ManiaMenuNextKey, osuTK.Input.Key.Down);
        SetDefault(
            YokkoSetting.ManiaMenuNextAlternateKey,
            osuTK.Input.Key.S);
        SetDefault(YokkoSetting.ManiaConfirmKey, osuTK.Input.Key.Enter);
        SetDefault(
            YokkoSetting.ManiaConfirmAlternateKey,
            osuTK.Input.Key.Space);
        SetDefault(YokkoSetting.ManiaRetryKey, osuTK.Input.Key.R);
        SetDefault(YokkoSetting.ManiaWatchReplayKey, osuTK.Input.Key.V);
        SetDefault(
            YokkoSetting.ManiaScrollSpeed,
            OsuManiaScrollSpeed.Default,
            OsuManiaScrollSpeed.Minimum,
            OsuManiaScrollSpeed.Maximum,
            OsuManiaScrollSpeed.SettingsPrecision);
        SetDefault(
            YokkoSetting.ManiaScrollSpeedAdjustmentMode,
            ScrollSpeedAdjustmentMode.OsuManiaScale);
        SetDefault(
            YokkoSetting.ManiaScrollDirection,
            ManiaScrollDirection.Downscroll);
        SetDefault(
            YokkoSetting.QuaverScrollRateNormalization,
            0.0,
            0.0,
            100.0,
            10.0);
        SetDefault(
            YokkoSetting.GameplayJudgementMode,
            Yokko.Core.Scoring.JudgementMode.Yokko);
        SetDefault(
            YokkoSetting.GameplayEtternaJustice,
            (double)Yokko.Core.Scoring.JudgementConfiguration
                .DefaultEtternaJustice,
            Yokko.Core.Scoring.JudgementConfiguration
                .MinimumEtternaJustice,
            Yokko.Core.Scoring.JudgementConfiguration
                .MaximumEtternaJustice,
            1.0);
        SetDefault(YokkoSetting.GameplayShowLanePressFeedback, true);
        SetDefault(YokkoSetting.GameplayShowTimingBar, true);
        SetDefault(
            YokkoSetting.GameplayJudgementDisplayDuration,
            YokkoGameplaySettings
                .DefaultJudgementDisplayDurationMilliseconds,
            YokkoGameplaySettings
                .MinimumJudgementDisplayDurationMilliseconds,
            YokkoGameplaySettings
                .MaximumJudgementDisplayDurationMilliseconds,
            YokkoGameplaySettings
                .JudgementDisplayDurationStepMilliseconds);
        SetDefault(
            YokkoSetting.GameplayJudgementOpacity,
            YokkoGameplaySettings.MaximumJudgementOpacity,
            YokkoGameplaySettings.MinimumJudgementOpacity,
            YokkoGameplaySettings.MaximumJudgementOpacity,
            YokkoGameplaySettings.JudgementOpacityStep);
        SetDefault(YokkoSetting.GameplayShowJudgementHitError, true);
        SetDefault(
            YokkoSetting.GameplayLayoutPlayfieldOffsetX,
            0.0,
            YokkoGameplaySettings.MinimumLayoutOffset,
            YokkoGameplaySettings.MaximumLayoutOffset,
            0.005);
        SetDefault(
            YokkoSetting.GameplayLayoutPlayfieldOffsetY,
            0.0,
            YokkoGameplaySettings.MinimumLayoutOffset,
            YokkoGameplaySettings.MaximumLayoutOffset,
            0.005);
        SetDefault(
            YokkoSetting.GameplayLayoutHudOffsetX,
            0.0,
            YokkoGameplaySettings.MinimumLayoutOffset,
            YokkoGameplaySettings.MaximumLayoutOffset,
            0.005);
        SetDefault(
            YokkoSetting.GameplayLayoutHudOffsetY,
            0.0,
            YokkoGameplaySettings.MinimumLayoutOffset,
            YokkoGameplaySettings.MaximumLayoutOffset,
            0.005);
        SetDefault(
            YokkoSetting.GameplayLayoutPlayfieldWidthScale,
            1.0,
            YokkoGameplaySettings.MinimumPlayfieldWidthScale,
            YokkoGameplaySettings.MaximumPlayfieldWidthScale,
            0.05);
        SetDefault(
            YokkoSetting.GameplayLayoutPlayfieldHeightScale,
            1.0,
            YokkoGameplaySettings.MinimumLayoutScale,
            YokkoGameplaySettings.MaximumLayoutScale,
            0.05);
        SetDefault(
            YokkoSetting.GameplayLayoutHudScaleX,
            1.0,
            YokkoGameplaySettings.MinimumLayoutScale,
            YokkoGameplaySettings.MaximumLayoutScale,
            0.05);
        SetDefault(
            YokkoSetting.GameplayLayoutHudScaleY,
            1.0,
            YokkoGameplaySettings.MinimumLayoutScale,
            YokkoGameplaySettings.MaximumLayoutScale,
            0.05);
        SetDefault(
            YokkoSetting.GameplayLayoutTimingBarOffsetX,
            0.0,
            YokkoGameplaySettings.MinimumLayoutOffset,
            YokkoGameplaySettings.MaximumLayoutOffset,
            0.005);
        SetDefault(
            YokkoSetting.GameplayLayoutTimingBarOffsetY,
            0.0,
            YokkoGameplaySettings.MinimumLayoutOffset,
            YokkoGameplaySettings.MaximumLayoutOffset,
            0.005);
        SetDefault(
            YokkoSetting.GameplayLayoutTimingBarScaleX,
            1.0,
            YokkoGameplaySettings.MinimumLayoutScale,
            YokkoGameplaySettings.MaximumLayoutScale,
            0.05);
        SetDefault(
            YokkoSetting.GameplayLayoutTimingBarScaleY,
            1.0,
            YokkoGameplaySettings.MinimumLayoutScale,
            YokkoGameplaySettings.MaximumLayoutScale,
            0.05);
        SetDefault(
            YokkoSetting.GameplayLayoutComboOffsetX,
            0.0,
            YokkoGameplaySettings.MinimumLayoutOffset,
            YokkoGameplaySettings.MaximumLayoutOffset,
            0.005);
        SetDefault(
            YokkoSetting.GameplayLayoutComboOffsetY,
            0.0,
            YokkoGameplaySettings.MinimumLayoutOffset,
            YokkoGameplaySettings.MaximumLayoutOffset,
            0.005);
        SetDefault(
            YokkoSetting.GameplayLayoutComboScaleX,
            1.0,
            YokkoGameplaySettings.MinimumLayoutScale,
            YokkoGameplaySettings.MaximumLayoutScale,
            0.05);
        SetDefault(
            YokkoSetting.GameplayLayoutComboScaleY,
            1.0,
            YokkoGameplaySettings.MinimumLayoutScale,
            YokkoGameplaySettings.MaximumLayoutScale,
            0.05);
        SetDefault(
            YokkoSetting.GameplayLayoutJudgementOffsetX,
            0.0,
            YokkoGameplaySettings.MinimumLayoutOffset,
            YokkoGameplaySettings.MaximumLayoutOffset,
            0.005);
        SetDefault(
            YokkoSetting.GameplayLayoutJudgementOffsetY,
            0.0,
            YokkoGameplaySettings.MinimumLayoutOffset,
            YokkoGameplaySettings.MaximumLayoutOffset,
            0.005);
        SetDefault(
            YokkoSetting.GameplayLayoutJudgementScaleX,
            1.0,
            YokkoGameplaySettings.MinimumLayoutScale,
            YokkoGameplaySettings.MaximumLayoutScale,
            0.05);
        SetDefault(
            YokkoSetting.GameplayLayoutJudgementScaleY,
            1.0,
            YokkoGameplaySettings.MinimumLayoutScale,
            YokkoGameplaySettings.MaximumLayoutScale,
            0.05);
        SetDefault(
            YokkoSetting.GameplayReplayControlsOffsetX,
            0.0,
            YokkoGameplaySettings.MinimumLayoutOffset,
            YokkoGameplaySettings.MaximumLayoutOffset,
            0.005);
        SetDefault(
            YokkoSetting.GameplayReplayControlsOffsetY,
            0.0,
            YokkoGameplaySettings.MinimumLayoutOffset,
            YokkoGameplaySettings.MaximumLayoutOffset,
            0.005);
        SetDefault(
            YokkoSetting.GameplayLayoutTopCoverRatio,
            0.0,
            0.0,
            YokkoGameplaySettings.MaximumTopCoverRatio,
            0.01);
        SetDefault(
            YokkoSetting.GameplayLayoutBottomCoverRatio,
            0.0,
            0.0,
            YokkoGameplaySettings.MaximumBottomCoverRatio,
            0.01);
        SetDefault(
            YokkoSetting.GameplayBackgroundDim,
            YokkoGameplaySettings.DefaultBackgroundDim,
            YokkoGameplaySettings.MinimumBackgroundDim,
            YokkoGameplaySettings.MaximumBackgroundDim,
            YokkoGameplaySettings.BackgroundDimStep);
        SetDefault(YokkoSetting.GameplayKeysoundsEnabled, false);
        SetDefault(YokkoSetting.GameplayMinesEnabled, true);
        SetDefault(YokkoSetting.GameplayPauseWhenUnfocused, true);
        SetDefault(YokkoSetting.GameplayResumeCountdownEnabled, true);
        SetDefault(
            YokkoSetting.GameplayResumeCountdownMilliseconds,
            YokkoGameplaySettings.DefaultResumeCountdownMilliseconds,
            YokkoGameplaySettings.MinimumResumeCountdownMilliseconds,
            YokkoGameplaySettings.MaximumResumeCountdownMilliseconds,
            YokkoGameplaySettings.ResumeCountdownStepMilliseconds);
        SetDefault(YokkoSetting.ManiaModConfiguration, string.Empty);
        SetDefault(YokkoSetting.ManiaActiveMods, string.Empty);
        SetDefault(YokkoSetting.DisplayUiScale, YokkoUiScale.Comfortable);
        SetDefault(
            YokkoSetting.DisplayFrameLimit,
            YokkoFrameRateLimits.LowLatencyDefault);
        SetDefault(YokkoSetting.DisplayShowPerformanceReadout, false);
        SetDefault(YokkoSetting.DisplayFastAltTab, true);
        SetDefault(
            YokkoSetting.DisplayBackgroundFrameRate,
            YokkoBackgroundFrameRate.Fps30);
        SetDefault(
            YokkoSetting.DisplayFullscreenRefreshRate,
            0,
            0,
            1000);
        SetDefault(
            YokkoSetting.DisplayDifficultyRatingMode,
            Yokko.Core.Difficulty.ManiaDifficultyRatingMode
                .EtternaMsd);
        SetDefault(YokkoSetting.WindowMaximised, false);
        SetDefault(YokkoSetting.DebugConsoleVisible, false);
        SetDefault(YokkoSetting.SkinSelectedId, string.Empty);
        SetDefault(YokkoSetting.SkinShowComboBursts, true);
        SetDefault(
            YokkoSetting.SkinLongNoteCutEnabled,
            YokkoSkinSettings.DefaultLongNoteCutEnabled);
        SetDefault(
            YokkoSetting.SkinLongNoteCutAmount,
            YokkoSkinSettings.DefaultLongNoteCutAmount,
            YokkoSkinSettings.MinimumLongNoteCutAmount,
            YokkoSkinSettings.MaximumLongNoteCutAmount,
            YokkoSkinSettings.LongNoteCutAmountStep);
        SetDefault(YokkoSetting.SettingsLastPage, "Display");
    }

    public void BindAudioSettings(YokkoAudioSettings settings)
    {
        BindWith(
            YokkoSetting.HomeMusicEnabled,
            settings.HomeMusicEnabled);
        BindWith(YokkoSetting.AudioMasterVolume, settings.MasterVolume);
        BindWith(YokkoSetting.AudioMusicVolume, settings.MusicVolume);
        BindWith(
            YokkoSetting.AudioHitSoundVolume,
            settings.HitSoundVolume);
        BindWith(
            YokkoSetting.AudioBackgroundMode,
            settings.BackgroundAudio);
        BindWith(YokkoSetting.AudioBackend, settings.PreferredBackend);
        BindWith(YokkoSetting.AudioDeviceId, settings.DeviceId);
        BindWith(
            YokkoSetting.AudioAsioDeviceId,
            settings.AsioDeviceId);
        BindWith(
            YokkoSetting.AudioBufferSize,
            settings.PreferredBufferSize);
        BindWith(
            YokkoSetting.AudioOffsetMilliseconds,
            settings.UserOffsetMilliseconds);
        BindWith(
            YokkoSetting.AudioManualPlaybackRatePitchMode,
            settings.ManualPlaybackRatePitchMode);
    }

    public void BindImportSettings(YokkoImportSettings settings)
    {
        BindWith(YokkoSetting.ImportPreferKeysounds, settings.PreferKeysounds);
        BindWith(YokkoSetting.ImportPreferSscSimfiles, settings.PreferSscSimfiles);
        BindWith(
            YokkoSetting.ImportEnableBmsScratch,
            settings.EnableBmsScratch);
        BindWith(
            YokkoSetting.ImportShowCompatibilityWarnings,
            settings.ShowCompatibilityWarnings);
    }

    public void BindExternalOsuSettings(YokkoExternalOsuSettings settings)
    {
        BindWith(YokkoSetting.ExternalOsuSongsPath, settings.SongsPath);
    }

    public void BindResourceSettings(YokkoResourceSettings settings)
    {
        BindWith(YokkoSetting.ResourceRootPath, settings.RootPath);
    }

    public void BindGameplaySettings(YokkoGameplaySettings settings)
    {
        BindWith(YokkoSetting.GameplayFourKeyLane1, settings.FourKeyBindings[0]);
        BindWith(YokkoSetting.GameplayFourKeyLane2, settings.FourKeyBindings[1]);
        BindWith(YokkoSetting.GameplayFourKeyLane3, settings.FourKeyBindings[2]);
        BindWith(YokkoSetting.GameplayFourKeyLane4, settings.FourKeyBindings[3]);
        BindWith(YokkoSetting.GameplaySevenKeyLane1, settings.SevenKeyBindings[0]);
        BindWith(YokkoSetting.GameplaySevenKeyLane2, settings.SevenKeyBindings[1]);
        BindWith(YokkoSetting.GameplaySevenKeyLane3, settings.SevenKeyBindings[2]);
        BindWith(YokkoSetting.GameplaySevenKeyLane4, settings.SevenKeyBindings[3]);
        BindWith(YokkoSetting.GameplaySevenKeyLane5, settings.SevenKeyBindings[4]);
        BindWith(YokkoSetting.GameplaySevenKeyLane6, settings.SevenKeyBindings[5]);
        BindWith(YokkoSetting.GameplaySevenKeyLane7, settings.SevenKeyBindings[6]);

        string storedProfiles = Get<string>(YokkoSetting.GameplayKeyProfiles);
        if (!string.IsNullOrWhiteSpace(storedProfiles))
        {
            try
            {
                GameplayKeyProfileCodec.DecodeAndApply(
                    storedProfiles,
                    settings);
            }
            catch (FormatException)
            {
                // Keep the valid legacy 4K/7K values and lazer defaults for all
                // other modes. The normalized value below repairs the config.
            }
            catch (ArgumentException)
            {
                // See above. Invalid profiles must never block application load.
            }
        }

        void persistKeyProfiles() => SetValue(
            YokkoSetting.GameplayKeyProfiles,
            GameplayKeyProfileCodec.Encode(settings));

        settings.BindingsChanged += persistKeyProfiles;
        persistKeyProfiles();

        BindWith(
            YokkoSetting.ManiaPauseOrBackKey,
            settings.PauseOrBackKey);
        BindWith(
            YokkoSetting.ManiaToggleLayoutEditorUiKey,
            settings.ToggleLayoutEditorUiKey);
        BindWith(YokkoSetting.ManiaSkipIntroKey, settings.SkipIntroKey);
        BindWith(YokkoSetting.ManiaQuickRetryKey, settings.QuickRetryKey);
        BindWith(
            YokkoSetting.ManiaDecreaseScrollSpeedKey,
            settings.DecreaseScrollSpeedKey);
        BindWith(
            YokkoSetting.ManiaIncreaseScrollSpeedKey,
            settings.IncreaseScrollSpeedKey);
        BindWith(
            YokkoSetting.ManiaMenuPreviousKey,
            settings.MenuPreviousKey);
        BindWith(
            YokkoSetting.ManiaMenuPreviousAlternateKey,
            settings.MenuPreviousAlternateKey);
        BindWith(YokkoSetting.ManiaMenuNextKey, settings.MenuNextKey);
        BindWith(
            YokkoSetting.ManiaMenuNextAlternateKey,
            settings.MenuNextAlternateKey);
        BindWith(YokkoSetting.ManiaConfirmKey, settings.ConfirmKey);
        BindWith(
            YokkoSetting.ManiaConfirmAlternateKey,
            settings.ConfirmAlternateKey);
        BindWith(YokkoSetting.ManiaRetryKey, settings.RetryKey);
        BindWith(
            YokkoSetting.ManiaWatchReplayKey,
            settings.WatchReplayKey);
        BindWith(YokkoSetting.ManiaScrollSpeed, settings.ScrollSpeed);
        BindWith(
            YokkoSetting.ManiaScrollSpeedAdjustmentMode,
            settings.ScrollSpeedAdjustmentMode);
        BindWith(
            YokkoSetting.ManiaScrollDirection,
            settings.ScrollDirection);
        BindWith(
            YokkoSetting.QuaverScrollRateNormalization,
            settings.QuaverScrollRateNormalization);
        BindWith(
            YokkoSetting.GameplayJudgementMode,
            settings.JudgementMode);
        BindWith(
            YokkoSetting.GameplayEtternaJustice,
            settings.EtternaJustice);
        BindWith(
            YokkoSetting.GameplayShowLanePressFeedback,
            settings.ShowLanePressFeedback);
        BindWith(
            YokkoSetting.GameplayShowTimingBar,
            settings.ShowTimingBar);
        BindWith(
            YokkoSetting.GameplayJudgementDisplayDuration,
            settings.JudgementDisplayDurationMilliseconds);
        BindWith(
            YokkoSetting.GameplayJudgementOpacity,
            settings.JudgementOpacity);
        BindWith(
            YokkoSetting.GameplayShowJudgementHitError,
            settings.ShowJudgementHitError);
        BindWith(
            YokkoSetting.GameplayLayoutPlayfieldOffsetX,
            settings.LayoutPlayfieldOffsetX);
        BindWith(
            YokkoSetting.GameplayLayoutPlayfieldOffsetY,
            settings.LayoutPlayfieldOffsetY);
        BindWith(
            YokkoSetting.GameplayLayoutHudOffsetX,
            settings.LayoutHudOffsetX);
        BindWith(
            YokkoSetting.GameplayLayoutHudOffsetY,
            settings.LayoutHudOffsetY);
        BindWith(
            YokkoSetting.GameplayLayoutPlayfieldWidthScale,
            settings.LayoutPlayfieldWidthScale);
        BindWith(
            YokkoSetting.GameplayLayoutPlayfieldHeightScale,
            settings.LayoutPlayfieldHeightScale);
        BindWith(
            YokkoSetting.GameplayLayoutHudScaleX,
            settings.LayoutHudScaleX);
        BindWith(
            YokkoSetting.GameplayLayoutHudScaleY,
            settings.LayoutHudScaleY);
        BindWith(
            YokkoSetting.GameplayLayoutTimingBarOffsetX,
            settings.LayoutTimingBarOffsetX);
        BindWith(
            YokkoSetting.GameplayLayoutTimingBarOffsetY,
            settings.LayoutTimingBarOffsetY);
        BindWith(
            YokkoSetting.GameplayLayoutTimingBarScaleX,
            settings.LayoutTimingBarScaleX);
        BindWith(
            YokkoSetting.GameplayLayoutTimingBarScaleY,
            settings.LayoutTimingBarScaleY);
        BindWith(
            YokkoSetting.GameplayLayoutComboOffsetX,
            settings.LayoutComboOffsetX);
        BindWith(
            YokkoSetting.GameplayLayoutComboOffsetY,
            settings.LayoutComboOffsetY);
        BindWith(
            YokkoSetting.GameplayLayoutComboScaleX,
            settings.LayoutComboScaleX);
        BindWith(
            YokkoSetting.GameplayLayoutComboScaleY,
            settings.LayoutComboScaleY);
        BindWith(
            YokkoSetting.GameplayLayoutJudgementOffsetX,
            settings.LayoutJudgementOffsetX);
        BindWith(
            YokkoSetting.GameplayLayoutJudgementOffsetY,
            settings.LayoutJudgementOffsetY);
        BindWith(
            YokkoSetting.GameplayLayoutJudgementScaleX,
            settings.LayoutJudgementScaleX);
        BindWith(
            YokkoSetting.GameplayLayoutJudgementScaleY,
            settings.LayoutJudgementScaleY);
        BindWith(
            YokkoSetting.GameplayReplayControlsOffsetX,
            settings.ReplayControlsOffsetX);
        BindWith(
            YokkoSetting.GameplayReplayControlsOffsetY,
            settings.ReplayControlsOffsetY);
        BindWith(
            YokkoSetting.GameplayLayoutTopCoverRatio,
            settings.LayoutTopCoverRatio);
        BindWith(
            YokkoSetting.GameplayLayoutBottomCoverRatio,
            settings.LayoutBottomCoverRatio);
        BindWith(
            YokkoSetting.GameplayBackgroundDim,
            settings.BackgroundDim);
        BindWith(
            YokkoSetting.GameplayKeysoundsEnabled,
            settings.KeysoundsEnabled);
        settings.KeysoundsEnabled.BindValueChanged(_ => Save());
        BindWith(
            YokkoSetting.GameplayMinesEnabled,
            settings.MinesEnabled);
        BindWith(
            YokkoSetting.GameplayPauseWhenUnfocused,
            settings.PauseWhenUnfocused);
        BindWith(
            YokkoSetting.GameplayResumeCountdownEnabled,
            settings.ResumeCountdownEnabled);
        BindWith(
            YokkoSetting.GameplayResumeCountdownMilliseconds,
            settings.ResumeCountdownMilliseconds);
    }

    public void BindDisplaySettings(YokkoDisplaySettings settings)
    {
        BindWith(YokkoSetting.DisplayUiScale, settings.UiScale);
        BindWith(YokkoSetting.DisplayFrameLimit, settings.FrameLimit);
        BindWith(
            YokkoSetting.DisplayShowPerformanceReadout,
            settings.ShowPerformanceReadout);
        BindWith(
            YokkoSetting.DisplayFastAltTab,
            settings.FastAltTab);
        BindWith(
            YokkoSetting.DisplayBackgroundFrameRate,
            settings.BackgroundFrameRate);
        BindWith(
            YokkoSetting.DisplayFullscreenRefreshRate,
            settings.FullscreenRefreshRate);
        BindWith(
            YokkoSetting.DisplayDifficultyRatingMode,
            settings.DifficultyRatingMode);
    }

    public void BindDiagnosticSettings(YokkoDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        BindWith(
            YokkoSetting.DebugConsoleVisible,
            diagnostics.ConsoleVisible);
    }

    public bool GetWindowMaximised() =>
        Get<bool>(YokkoSetting.WindowMaximised);

    public void SetWindowMaximised(bool maximised)
    {
        SetValue(YokkoSetting.WindowMaximised, maximised);
        Save();
    }

    public void BindModPreferences(
        YokkoManiaModPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        BindWith(
            YokkoSetting.ManiaModConfiguration,
            preferences.SerializedConfiguration);
        BindWith(
            YokkoSetting.ManiaActiveMods,
            preferences.SerializedActiveMods);
    }

    public void BindSkinSettings(YokkoSkinSettings settings)
    {
        BindWith(YokkoSetting.SkinSelectedId, settings.SelectedSkinId);
        BindWith(YokkoSetting.SkinShowComboBursts, settings.ShowComboBursts);
        BindWith(
            YokkoSetting.SkinLongNoteCutEnabled,
            settings.LongNoteCutEnabled);
        BindWith(
            YokkoSetting.SkinLongNoteCutAmount,
            settings.LongNoteCutAmount);
    }

    public string GetLastSettingsPage() =>
        Get<string>(YokkoSetting.SettingsLastPage);

    public void SetLastSettingsPage(string page)
    {
        SetValue(YokkoSetting.SettingsLastPage, page);
        Save();
    }
}
