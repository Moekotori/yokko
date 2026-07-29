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
/// Global per-Mod configuration memory. Active Mod selection remains
/// session-owned; only each configurable Mod's last valid settings are kept.
/// </summary>
internal sealed class YokkoManiaModPreferences
{
    private static readonly HashSet<ManiaModId> configurable_mods =
    [
        ManiaModId.HalfTime,
        ManiaModId.Daycore,
        ManiaModId.DoubleTime,
        ManiaModId.Nightcore,
        ManiaModId.Perfect,
        ManiaModId.Cover,
        ManiaModId.Flashlight,
        ManiaModId.AccuracyChallenge,
        ManiaModId.DifficultyAdjust,
        ManiaModId.Muted,
        ManiaModId.WindUp,
        ManiaModId.WindDown,
        ManiaModId.AdaptiveSpeed,
    ];

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

    public Bindable<string> SerializedConfiguration { get; } =
        new(string.Empty);

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
            ManiaModId.HalfTime
                or ManiaModId.Daycore
                or ManiaModId.DoubleTime
                or ManiaModId.Nightcore =>
                selectedMods.WithFixedRate(
                    mod,
                    preferred.FixedRateSpeedChange,
                    preferred.FixedRateAdjustPitch),
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
