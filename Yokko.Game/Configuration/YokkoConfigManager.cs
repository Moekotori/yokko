using osu.Framework.Configuration;
using osu.Framework.Platform;
using Yokko.Audio;
using Yokko.Game.Audio;
using Yokko.Game.Gameplay;
using Yokko.Game.Importing;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Configuration;

internal enum YokkoSetting
{
    AudioBackend,
    AudioDeviceId,
    AudioBufferSize,
    AudioOffsetMilliseconds,
    ImportPreferKeysounds,
    ImportPreferSscSimfiles,
    ImportShowCompatibilityWarnings,
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
    GameplayScrollSpeed,
    GameplayShowHud,
    GameplayShowHitError,
    GameplayShowLanePressFeedback,
    SkinSelectedId,
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
        SetDefault(YokkoSetting.GameplayScrollSpeed, 1.0, 0.5, 2.0, 0.05);
        SetDefault(YokkoSetting.GameplayShowHud, true);
        SetDefault(YokkoSetting.GameplayShowHitError, true);
        SetDefault(YokkoSetting.GameplayShowLanePressFeedback, true);
        SetDefault(YokkoSetting.SkinSelectedId, string.Empty);
    }

    public void BindAudioSettings(YokkoAudioSettings settings)
    {
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
        BindWith(YokkoSetting.GameplayScrollSpeed, settings.ScrollSpeed);
        BindWith(YokkoSetting.GameplayShowHud, settings.ShowGameplayHud);
        BindWith(YokkoSetting.GameplayShowHitError, settings.ShowHitError);
        BindWith(
            YokkoSetting.GameplayShowLanePressFeedback,
            settings.ShowLanePressFeedback);
    }

    public void BindSkinSettings(YokkoSkinSettings settings)
    {
        BindWith(YokkoSetting.SkinSelectedId, settings.SelectedSkinId);
    }
}
