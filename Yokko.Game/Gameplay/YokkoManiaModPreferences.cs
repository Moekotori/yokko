using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using osu.Framework.Bindables;
using Yokko.Core.Mods;

namespace Yokko.Game.Gameplay;

/// <summary>
/// Global Mania Mod memory. Keeps the active selection across song changes in
/// the current session, as well as each configurable Mod's last valid settings.
/// </summary>
internal sealed class YokkoManiaModPreferences
{
    // Deliberately allowlisted: newly added Mods must be reviewed before they
    // can unexpectedly carry into another chart or game restart.
    private static readonly HashSet<ManiaModId> persistent_active_mods =
    [
        ManiaModId.Easy,
        ManiaModId.NoFail,
        ManiaModId.HalfTime,
        ManiaModId.Daycore,
        ManiaModId.NoRelease,
        ManiaModId.HardRock,
        ManiaModId.DoubleTime,
        ManiaModId.Nightcore,
        ManiaModId.FadeIn,
        ManiaModId.Hidden,
        ManiaModId.Cover,
        ManiaModId.Flashlight,
        ManiaModId.Random,
        ManiaModId.DualStages,
        ManiaModId.Mirror,
        ManiaModId.DifficultyAdjust,
        ManiaModId.Classic,
        ManiaModId.Invert,
        ManiaModId.ConstantSpeed,
        ManiaModId.HoldOff,
        ManiaModId.Key1,
        ManiaModId.Key2,
        ManiaModId.Key3,
        ManiaModId.Key4,
        ManiaModId.Key5,
        ManiaModId.Key6,
        ManiaModId.Key7,
        ManiaModId.Key8,
        ManiaModId.Key9,
        ManiaModId.Key10,
        ManiaModId.WindUp,
        ManiaModId.WindDown,
        ManiaModId.Muted,
        ManiaModId.AdaptiveSpeed,
        ManiaModId.NoPause,
        ManiaModId.IidxHardGauge,
        ManiaModId.Lr2HardGauge,
        ManiaModId.BeatorajaHardGauge,
    ];

    private static readonly HashSet<ManiaModId> configurable_mods =
    [
        ManiaModId.Perfect,
        ManiaModId.Cover,
        ManiaModId.Flashlight,
        ManiaModId.AccuracyChallenge,
        ManiaModId.DifficultyAdjust,
        ManiaModId.Muted,
        ManiaModId.WindUp,
        ManiaModId.WindDown,
        ManiaModId.AdaptiveSpeed,
        ManiaModId.NoPause,
    ];

    // A fixed-rate Mod name promises its canonical rate when switched on:
    // HT/DC start at 0.75x and DT/NC at 1.50x. A custom slider value remains
    // part of the active loadout, but must not leak into a later activation.

    private static readonly JsonSerializerOptions json_options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Dictionary<
        ManiaModId,
        ManiaModConfigurationEntry> entries = [];
    private string loadedValue;
    private string loadedActiveModsValue;
    private ManiaModSet activeMods = ManiaModSet.Empty;

    public Bindable<string> SerializedConfiguration { get; } =
        new(string.Empty);
    public Bindable<string> SerializedActiveMods { get; } =
        new(string.Empty);

    public ManiaModSet RestoreActiveMods()
    {
        ensureActiveModsLoaded();
        return activeMods;
    }

    public void RememberActiveMods(ManiaModSet mods)
    {
        ensureActiveModsLoaded();
        mods ??= ManiaModSet.Empty;
        ManiaModSet persistentMods = SelectPersistentActiveMods(mods);
        if (activeMods.Equals(persistentMods))
            return;

        activeMods = persistentMods;
        persistActiveMods();
    }

    private void persistActiveMods()
    {
        string serialized = activeMods.IsEmpty
            ? string.Empty
            : JsonSerializer.Serialize(
                ManiaModConfigurationCodec.Capture(activeMods),
                json_options);
        loadedActiveModsValue = serialized;
        SerializedActiveMods.Value = serialized;
    }

    public void Remember(ManiaModSet mods)
    {
        ensureLoaded();
        mods ??= ManiaModSet.Empty;
        ManiaModConfigurationEnvelope captured =
            ManiaModConfigurationCodec.Capture(mods);
        bool changed = false;
        foreach (ManiaModConfigurationEntry entry in captured.Mods)
        {
            if (!OsuManiaModParityCatalog.TryGet(
                    entry.Key,
                    out ManiaModDefinition definition)
                || definition is null
                || !configurable_mods.Contains(definition.Id))
            {
                continue;
            }

            if (!entries.TryGetValue(
                    definition.Id,
                    out ManiaModConfigurationEntry existing)
                || existing != entry)
            {
                entries[definition.Id] = entry;
                changed = true;
            }
        }

        if (changed)
            persist();
    }

    public ManiaModSet Apply(
        ManiaModSet selectedMods,
        ManiaModId mod)
    {
        ArgumentNullException.ThrowIfNull(selectedMods);
        ensureLoaded();
        if (!selectedMods.Contains(mod)
            || !entries.TryGetValue(
                mod,
                out ManiaModConfigurationEntry entry))
        {
            return selectedMods;
        }

        ManiaModSet preferred;
        try
        {
            preferred = ManiaModConfigurationCodec.Restore(
                new ManiaModConfigurationEnvelope(
                    ManiaModConfigurationEnvelope.CurrentSchemaVersion,
                    [entry]));
        }
        catch
        {
            entries.Remove(mod);
            persist();
            return selectedMods;
        }

        return mod switch
        {
            ManiaModId.Perfect =>
                selectedMods.WithPerfect(
                    preferred.PerfectRequirePerfectHits),
            ManiaModId.Cover =>
                selectedMods.WithCover(
                    preferred.CoverCoverage,
                    preferred.CoverDirection),
            ManiaModId.Flashlight =>
                selectedMods.WithFlashlight(
                    preferred.FlashlightSizeMultiplier,
                    preferred.FlashlightComboBasedSize),
            ManiaModId.AccuracyChallenge =>
                selectedMods.WithAccuracyChallenge(
                    preferred.AccuracyChallengeMinimum,
                    preferred.AccuracyChallengeMode),
            ManiaModId.DifficultyAdjust =>
                selectedMods.WithDifficultyAdjust(
                    preferred.DifficultyAdjustDrainRate,
                    preferred.DifficultyAdjustOverallDifficulty,
                    preferred.DifficultyAdjustExtendedLimits),
            ManiaModId.Muted =>
                selectedMods.WithMuted(
                    preferred.MutedInverse,
                    preferred.MutedMetronome,
                    preferred.MutedComboCount,
                    preferred.MutedAffectsHitSounds),
            ManiaModId.WindUp or ManiaModId.WindDown =>
                selectedMods.WithTimeRamp(
                    mod,
                    preferred.TimeRampInitialRate,
                    preferred.TimeRampFinalRate,
                    preferred.TimeRampAdjustPitch),
            ManiaModId.AdaptiveSpeed =>
                selectedMods.WithAdaptiveSpeed(
                    preferred.AdaptiveInitialRate,
                    preferred.AdaptiveAdjustPitch),
            ManiaModId.NoPause =>
                selectedMods.WithNoPause(
                    preferred.NoPauseAllowedPauses),
            _ => selectedMods,
        };
    }

    private void ensureLoaded()
    {
        string current = SerializedConfiguration.Value
                         ?? string.Empty;
        if (string.Equals(
                current,
                loadedValue,
                StringComparison.Ordinal))
        {
            return;
        }

        entries.Clear();
        loadedValue = current;
        if (string.IsNullOrWhiteSpace(current))
            return;

        try
        {
            ManiaModConfigurationEnvelope envelope =
                JsonSerializer.Deserialize<
                    ManiaModConfigurationEnvelope>(
                    current,
                    json_options)
                ?? throw new JsonException();
            if (envelope.SchemaVersion
                != ManiaModConfigurationEnvelope.CurrentSchemaVersion)
            {
                throw new NotSupportedException();
            }

            foreach (ManiaModConfigurationEntry entry in envelope.Mods)
            {
                if (!OsuManiaModParityCatalog.TryGet(
                        entry.Key,
                        out ManiaModDefinition definition)
                    || definition is null)
                {
                    throw new InvalidDataException(
                        $"Unknown Mania Mod preference '{entry.Key}'.");
                }

                if (!configurable_mods.Contains(definition.Id))
                {
                    continue;
                }

                _ = ManiaModConfigurationCodec.Restore(
                    new ManiaModConfigurationEnvelope(
                        envelope.SchemaVersion,
                        [entry]));
                entries[definition.Id] = entry;
            }
        }
        catch
        {
            entries.Clear();
            SerializedConfiguration.Value = string.Empty;
            loadedValue = string.Empty;
        }
    }

    private void ensureActiveModsLoaded()
    {
        string current = SerializedActiveMods.Value
                         ?? string.Empty;
        if (string.Equals(
                current,
                loadedActiveModsValue,
                StringComparison.Ordinal))
        {
            return;
        }

        loadedActiveModsValue = current;
        activeMods = ManiaModSet.Empty;
        if (string.IsNullOrWhiteSpace(current))
            return;

        try
        {
            ManiaModConfigurationEnvelope envelope =
                JsonSerializer.Deserialize<
                    ManiaModConfigurationEnvelope>(
                    current,
                    json_options)
                ?? throw new JsonException();
            ManiaModSet restored =
                ManiaModConfigurationCodec.Restore(envelope);
            activeMods = SelectPersistentActiveMods(restored);
            if (!activeMods.Equals(restored))
                persistActiveMods();
        }
        catch
        {
            SerializedActiveMods.Value = string.Empty;
            loadedActiveModsValue = string.Empty;
        }
    }

    internal static ManiaModSet SelectPersistentActiveMods(ManiaModSet mods)
    {
        ManiaModConfigurationEnvelope captured =
            ManiaModConfigurationCodec.Capture(mods);
        ManiaModConfigurationEntry[] persistentEntries = captured.Mods
            .Where(static entry =>
                OsuManiaModParityCatalog.TryGet(
                    entry.Key,
                    out ManiaModDefinition definition)
                && definition is not null
                && persistent_active_mods.Contains(definition.Id))
            .ToArray();
        return ManiaModConfigurationCodec.Restore(
            new ManiaModConfigurationEnvelope(
                captured.SchemaVersion,
                persistentEntries));
    }

    private void persist()
    {
        var envelope = new ManiaModConfigurationEnvelope(
            ManiaModConfigurationEnvelope.CurrentSchemaVersion,
            entries.OrderBy(static item => item.Key)
                   .Select(static item => item.Value)
                   .ToArray());
        string serialized = JsonSerializer.Serialize(
            envelope,
            json_options);
        loadedValue = serialized;
        SerializedConfiguration.Value = serialized;
    }
}
