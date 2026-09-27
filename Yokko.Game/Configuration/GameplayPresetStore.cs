using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using osu.Framework.Bindables;
using Yokko.Core.Scoring;
using Yokko.Game.Gameplay;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Configuration;

/// <summary>Named snapshots of gameplay, bindings and skin. Hardware calibration stays with its device.</summary>
internal sealed class GameplayPresetStore
{
    private readonly YokkoGameplaySettings gameplay;
    private readonly YokkoSkinSettings skins;
    private readonly Bindable<string> serialized;
    private readonly Func<string, bool> skinExists;
    private readonly Dictionary<string, Bindable<double>> numbers;
    private readonly Dictionary<string, Bindable<bool>> switches;
    private readonly Dictionary<string, (double Min, double Max)> ranges = new();
    private Dictionary<int, Preset> presets;
    private Preset undo;
    internal bool CanUndo => undo != null;
    internal IReadOnlyDictionary<int, Preset> Presets => presets;

    internal GameplayPresetStore(YokkoGameplaySettings gameplay, YokkoSkinSettings skins,
        Bindable<string> serialized, Func<string, bool> skinExists)
    {
        this.gameplay = gameplay;
        this.skins = skins;
        this.serialized = serialized;
        this.skinExists = skinExists;
        // Discover scalar bindables only in these two owned models, not arbitrary types from JSON.
        numbers = new();
        switches = new();
        foreach ((string prefix, object model) in new[] { ("gameplay.", (object)gameplay), ("skin.", skins) })
        foreach (FieldInfo field in model.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            string key = prefix + field.Name;
            if (field.GetValue(model) is Bindable<double> number) numbers.Add(key, number);
            else if (field.GetValue(model) is Bindable<bool> toggle) switches.Add(key, toggle);
        }
        foreach (var field in numbers)
        {
            var hud = gameplay.HudLayoutSettings.FirstOrDefault(h => ReferenceEquals(h.Bindable, field.Value));
            ranges[field.Key] = hud.Bindable == null ? field.Key switch
            {
                "gameplay.ScrollSpeed" => (OsuManiaScrollSpeed.Minimum, OsuManiaScrollSpeed.Maximum),
                "gameplay.QuaverScrollRateNormalization" => (0, 100),
                "gameplay.EtternaJustice" => (1, 9),
                "gameplay.JudgementDisplayDurationMilliseconds" => (100, 2000),
                "gameplay.JudgementOpacity" => (0.2, 1),
                "gameplay.JudgementHitErrorScale" => (0.25, 2.5),
                "gameplay.ResumeCountdownMilliseconds" => (YokkoGameplaySettings.MinimumResumeCountdownMilliseconds, YokkoGameplaySettings.MaximumResumeCountdownMilliseconds),
                "skin.LongNoteCutAmount" => (0, 2),
                _ => throw new InvalidOperationException($"Missing preset validation range: {field.Key}"),
            } : (hud.Minimum, hud.Maximum);
        }
        try { presets = JsonSerializer.Deserialize<Dictionary<int, Preset>>(serialized.Value) ?? new(); }
        catch (JsonException) { presets = new(); }
        foreach (int slot in presets.Keys.ToArray())
        {
            try { validate(presets[slot]); if (slot is < 0 or >= 6) presets.Remove(slot); }
            catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException) { presets.Remove(slot); }
        }
    }

    internal void Save(int slot, string name)
    {
        requireSlot(slot);
        Preset preset = capture(name);
        validate(preset);
        presets[slot] = preset;
        persist();
    }
    internal void Delete(int slot) { requireSlot(slot); presets.Remove(slot); persist(); }
    internal void Load(int slot)
    {
        requireSlot(slot);
        if (!presets.TryGetValue(slot, out Preset preset)) throw new FormatException("Empty slot.");
        validate(preset);
        if (!string.IsNullOrEmpty(preset.SkinId) && !skinExists(preset.SkinId))
            throw new FormatException("The preset skin is missing. Import it before loading this preset.");
        undo = capture("Undo");
        apply(preset);
    }
    internal void Undo()
    {
        if (undo == null) return;
        if (!string.IsNullOrEmpty(undo.SkinId) && !skinExists(undo.SkinId)) throw new FormatException("Undo skin is missing.");
        apply(undo);
        undo = null;
    }
    internal string Export(int slot) => JsonSerializer.Serialize(presets[slot], new JsonSerializerOptions { WriteIndented = true });
    internal void Import(int slot, string json)
    {
        requireSlot(slot);
        if (string.IsNullOrWhiteSpace(json) || json.Length > 131072) throw new FormatException("Invalid preset size.");
        Preset preset = JsonSerializer.Deserialize<Preset>(json);
        validate(preset);
        presets[slot] = preset;
        persist();
    }
    private Preset capture(string name) => new(1, name?.Trim(), skins.SelectedSkinId.Value,
        numbers.ToDictionary(p => p.Key, p => p.Value.Value), switches.ToDictionary(p => p.Key, p => p.Value.Value),
        gameplay.ScrollSpeedAdjustmentMode.Value, gameplay.ScrollDirection.Value, gameplay.JudgementMode.Value,
        GameplayKeyProfileCodec.Encode(gameplay));

    private void validate(Preset preset)
    {
        if (preset == null || preset.Version != 1 || string.IsNullOrWhiteSpace(preset.Name)
            || preset.Name.Length > 32 || preset.Name.Any(char.IsControl) || preset.SkinId == null
            || preset.Numbers == null || preset.Switches == null
            || preset.Numbers.Count != numbers.Count || preset.Switches.Count != switches.Count
            || !Enum.IsDefined(preset.SpeedMode) || !Enum.IsDefined(preset.Direction) || !Enum.IsDefined(preset.Judgement))
            throw new FormatException("Unsupported or incomplete preset.");
        foreach (var pair in preset.Numbers)
        {
            if (!ranges.TryGetValue(pair.Key, out var range) || !double.IsFinite(pair.Value)
                || pair.Value < range.Min || pair.Value > range.Max) throw new FormatException("Invalid setting value.");
        }
        if (preset.Switches.Keys.Any(key => !switches.ContainsKey(key))) throw new FormatException("Unknown setting.");
        // Decode into a disposable value model first: malformed keys cannot partially change live state.
        GameplayKeyProfileCodec.DecodeAndApply(preset.Keys, new YokkoGameplaySettings());
    }
    private void apply(Preset preset)
    {
        // Selecting the skin loads its normal HUD layout synchronously; the preset then overrides it.
        skins.SelectedSkinId.Value = preset.SkinId;
        foreach (var pair in preset.Numbers) numbers[pair.Key].Value = pair.Value;
        foreach (var pair in preset.Switches) switches[pair.Key].Value = pair.Value;
        gameplay.ScrollSpeedAdjustmentMode.Value = preset.SpeedMode;
        gameplay.ScrollDirection.Value = preset.Direction;
        gameplay.JudgementMode.Value = preset.Judgement;
        GameplayKeyProfileCodec.DecodeAndApply(preset.Keys, gameplay);
    }
    private void persist() => serialized.Value = JsonSerializer.Serialize(presets);
    private static void requireSlot(int slot) { if (slot is < 0 or >= 6) throw new ArgumentOutOfRangeException(nameof(slot)); }

    internal sealed record Preset(int Version, string Name, string SkinId, Dictionary<string, double> Numbers,
        Dictionary<string, bool> Switches, ScrollSpeedAdjustmentMode SpeedMode, ManiaScrollDirection Direction,
        JudgementMode Judgement, string Keys);
}
