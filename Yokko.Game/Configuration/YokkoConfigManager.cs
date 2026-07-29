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
    ManiaScrollSpeed,
    GameplayShowLanePressFeedback,
    GameplayKeysoundsEnabled,
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
        SetDefault(
            YokkoSetting.ManiaScrollSpeed,
            OsuManiaScrollSpeed.Default,
            OsuManiaScrollSpeed.Minimum,
            OsuManiaScrollSpeed.Maximum,
            OsuManiaScrollSpeed.SettingsPrecision);
        SetDefault(YokkoSetting.GameplayShowLanePressFeedback, true);
        SetDefault(YokkoSetting.GameplayKeysoundsEnabled, true);
        SetDefault(YokkoSetting.DisplayUiScale, YokkoUiScale.Comfortable);
        SetDefault(
            YokkoSetting.DisplayFrameLimit,
            YokkoFrameLimit.Limit8x);
        SetDefault(YokkoSetting.DisplayShowPerformanceReadout, false);
        SetDefault(YokkoSetting.SkinSelectedId, string.Empty);
        SetDefault(YokkoSetting.SettingsLastPage, "Display");
    }

    public void BindAudioSettings(YokkoAudioSettings settings)
    {
        BindWith(
            YokkoSetting.HomeMusicEnabled,
            settings.HomeMusicEnabled);
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
        BindWith(YokkoSetting.ManiaScrollSpeed, settings.ScrollSpeed);
        BindWith(
            YokkoSetting.GameplayShowLanePressFeedback,
            settings.ShowLanePressFeedback);
        BindWith(
            YokkoSetting.GameplayKeysoundsEnabled,
            settings.KeysoundsEnabled);
    }

    public void BindDisplaySettings(YokkoDisplaySettings settings)
    {
        BindWith(YokkoSetting.DisplayUiScale, settings.UiScale);
        BindWith(YokkoSetting.DisplayFrameLimit, settings.FrameLimit);
        BindWith(
            YokkoSetting.DisplayShowPerformanceReadout,
            settings.ShowPerformanceReadout);
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
