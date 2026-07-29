using System;
using osu.Framework.Configuration;
using osu.Framework.Platform;
using Yokko.Audio;
using Yokko.Game.Audio;
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
    AudioBackend,
    AudioDeviceId,
    AudioBufferSize,
    AudioOffsetMilliseconds,
    ImportPreferKeysounds,
    ImportPreferSscSimfiles,
    ImportShowCompatibilityWarnings,
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
    QuaverScrollRateNormalization,
    GameplayShowLanePressFeedback,
    GameplayKeysoundsEnabled,
    GameplayPauseWhenUnfocused,
    ManiaModConfiguration,
    DisplayUiScale,
    DisplayFrameLimit,
    DisplayShowPerformanceReadout,
    SkinSelectedId,
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
            YokkoSetting.AudioBackend,
            AudioBackendKind.WasapiExclusive);
        SetDefault(YokkoSetting.AudioDeviceId, string.Empty);
        SetDefault(YokkoSetting.AudioBufferSize, 64, 64, 2048);
        SetDefault(
            YokkoSetting.AudioOffsetMilliseconds,
            0.0,
            -200.0,
            200.0,
            1.0);
        SetDefault(YokkoSetting.ImportPreferKeysounds, true);
        SetDefault(YokkoSetting.ImportPreferSscSimfiles, true);
        SetDefault(YokkoSetting.ImportShowCompatibilityWarnings, true);
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
            YokkoSetting.QuaverScrollRateNormalization,
            0.0,
            0.0,
            100.0,
            10.0);
        SetDefault(YokkoSetting.GameplayShowLanePressFeedback, true);
        SetDefault(YokkoSetting.GameplayKeysoundsEnabled, true);
        SetDefault(YokkoSetting.GameplayPauseWhenUnfocused, true);
        SetDefault(YokkoSetting.ManiaModConfiguration, string.Empty);
        SetDefault(YokkoSetting.DisplayUiScale, YokkoUiScale.Comfortable);
        SetDefault(
            YokkoSetting.DisplayFrameLimit,
            YokkoFrameLimit.Limit2x);
        SetDefault(YokkoSetting.DisplayShowPerformanceReadout, false);
        SetDefault(YokkoSetting.SkinSelectedId, string.Empty);
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
        BindWith(YokkoSetting.AudioBackend, settings.PreferredBackend);
        BindWith(YokkoSetting.AudioDeviceId, settings.DeviceId);
        BindWith(
            YokkoSetting.AudioBufferSize,
            settings.PreferredBufferSize);
        BindWith(
            YokkoSetting.AudioOffsetMilliseconds,
            settings.UserOffsetMilliseconds);
    }

    public void BindImportSettings(YokkoImportSettings settings)
    {
        BindWith(YokkoSetting.ImportPreferKeysounds, settings.PreferKeysounds);
        BindWith(YokkoSetting.ImportPreferSscSimfiles, settings.PreferSscSimfiles);
        BindWith(
            YokkoSetting.ImportShowCompatibilityWarnings,
            settings.ShowCompatibilityWarnings);
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
            YokkoSetting.QuaverScrollRateNormalization,
            settings.QuaverScrollRateNormalization);
        BindWith(
            YokkoSetting.GameplayShowLanePressFeedback,
            settings.ShowLanePressFeedback);
        BindWith(
            YokkoSetting.GameplayKeysoundsEnabled,
            settings.KeysoundsEnabled);
        BindWith(
            YokkoSetting.GameplayPauseWhenUnfocused,
            settings.PauseWhenUnfocused);
    }

    public void BindDisplaySettings(YokkoDisplaySettings settings)
    {
        BindWith(YokkoSetting.DisplayUiScale, settings.UiScale);
        BindWith(YokkoSetting.DisplayFrameLimit, settings.FrameLimit);
        BindWith(
            YokkoSetting.DisplayShowPerformanceReadout,
            settings.ShowPerformanceReadout);
    }

    public void BindModPreferences(
        YokkoManiaModPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        BindWith(
            YokkoSetting.ManiaModConfiguration,
            preferences.SerializedConfiguration);
    }

    public void BindSkinSettings(YokkoSkinSettings settings)
    {
        BindWith(YokkoSetting.SkinSelectedId, settings.SelectedSkinId);
    }

    public string GetLastSettingsPage() =>
        Get<string>(YokkoSetting.SettingsLastPage);

    public void SetLastSettingsPage(string page)
    {
        SetValue(YokkoSetting.SettingsLastPage, page);
        Save();
    }
}
