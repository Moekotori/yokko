using System;
using System.Collections.Generic;
using System.Text.Json;
using osu.Framework.Bindables;

namespace Yokko.Game.Audio;

/// <summary>One profile per backend and explicit endpoint. The default endpoint is a separate route.</summary>
internal sealed class AudioDeviceProfiles : IDisposable
{
    private readonly YokkoAudioSettings settings;
    private readonly Bindable<string> serialized;
    private Dictionary<string, Profile> profiles;
    private string activeKey;
    private bool applying;

    internal AudioDeviceProfiles(YokkoAudioSettings settings, Bindable<string> serialized)
    {
        this.settings = settings;
        this.serialized = serialized;
        try { profiles = JsonSerializer.Deserialize<Dictionary<string, Profile>>(serialized.Value) ?? new(); }
        catch (JsonException) { profiles = new(); }
        activeKey = RouteKey(settings);
        if (profiles.TryGetValue(activeKey, out Profile saved) && saved != null && saved.IsValid)
            restore(saved);
        settings.PreferredBackend.ValueChanged += routeChanged;
        settings.DeviceId.ValueChanged += deviceChanged;
        settings.AsioDeviceId.ValueChanged += deviceChanged;
        settings.MasterVolume.ValueChanged += volumeChanged;
        settings.MusicVolume.ValueChanged += volumeChanged;
        settings.HitSoundVolume.ValueChanged += volumeChanged;
        settings.UserOffsetMilliseconds.ValueChanged += volumeChanged;
        settings.PreferredBufferSize.ValueChanged += bufferChanged;
        save();
    }

    internal static string RouteKey(YokkoAudioSettings settings) =>
        $"{settings.PreferredBackend.Value}:{settings.SelectedDeviceId}";

    private void routeChanged(ValueChangedEvent<Yokko.Audio.AudioBackendKind> _) => switchRoute();
    private void deviceChanged(ValueChangedEvent<string> _) => switchRoute();
    private void volumeChanged(ValueChangedEvent<double> _) => save();
    private void bufferChanged(ValueChangedEvent<int> _) => save();

    private void switchRoute()
    {
        string key = RouteKey(settings);
        if (activeKey == key) return;
        save();
        activeKey = key;
        // A new route inherits loudness/buffer preferences, but never another device's calibration.
        restore(profiles.TryGetValue(key, out Profile profile) && profile != null && profile.IsValid
            ? profile : capture() with { Offset = 0 });
        save();
    }

    private Profile capture() => new(settings.MasterVolume.Value, settings.MusicVolume.Value,
        settings.HitSoundVolume.Value, settings.PreferredBufferSize.Value, settings.UserOffsetMilliseconds.Value);

    private void restore(Profile profile)
    {
        applying = true;
        try
        {
            settings.MasterVolume.Value = profile.Master;
            settings.MusicVolume.Value = profile.Music;
            settings.HitSoundVolume.Value = profile.HitSound;
            settings.PreferredBufferSize.Value = profile.Buffer;
            settings.UserOffsetMilliseconds.Value = profile.Offset;
        }
        finally { applying = false; }
    }

    private void save()
    {
        if (applying) return;
        profiles[activeKey] = capture();
        serialized.Value = JsonSerializer.Serialize(profiles);
    }

    public void Dispose()
    {
        settings.PreferredBackend.ValueChanged -= routeChanged;
        settings.DeviceId.ValueChanged -= deviceChanged;
        settings.AsioDeviceId.ValueChanged -= deviceChanged;
        settings.MasterVolume.ValueChanged -= volumeChanged;
        settings.MusicVolume.ValueChanged -= volumeChanged;
        settings.HitSoundVolume.ValueChanged -= volumeChanged;
        settings.UserOffsetMilliseconds.ValueChanged -= volumeChanged;
        settings.PreferredBufferSize.ValueChanged -= bufferChanged;
    }

    internal sealed record Profile(double Master, double Music, double HitSound, int Buffer, double Offset)
    {
        public bool IsValid => double.IsFinite(Master) && Master is >= 0 and <= 1
            && double.IsFinite(Music) && Music is >= 0 and <= 1
            && double.IsFinite(HitSound) && HitSound is >= 0 and <= 1
            && Buffer is 64 or 128 or 256 or 512 && double.IsFinite(Offset) && Math.Abs(Offset) <= 200;
    }
}
