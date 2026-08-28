using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using osu.Framework.Bindables;

namespace Yokko.Game.Gameplay;

internal sealed class GameplayLayoutPreset
{
    public string Name { get; set; } = "Default";
    public Dictionary<string, double> Values { get; set; } = new();
}

/// <summary>
/// Captures and restores HUD / playfield layout bindables as named presets.
/// </summary>
internal static class GameplayLayoutPresetStore
{
    private static readonly JsonSerializerOptions json_options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal static readonly (string Id, string DisplayName)[] BuiltInPresets =
    [
        ("default", "Default"),
        ("compact", "Compact HUD"),
        ("stream", "Stream"),
    ];

    internal static string Capture(YokkoGameplaySettings settings)
    {
        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach ((string key, Bindable<double> bindable) in enumerateLayoutBindables(settings))
            values[key] = bindable.Value;

        return JsonSerializer.Serialize(values, json_options);
    }

    internal static void Apply(YokkoGameplaySettings settings, string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        Dictionary<string, double>? values = JsonSerializer.Deserialize<
            Dictionary<string, double>>(json, json_options);
        if (values == null)
            return;

        foreach ((string key, Bindable<double> bindable) in enumerateLayoutBindables(settings))
        {
            if (values.TryGetValue(key, out double value))
                bindable.Value = value;
        }
    }

    internal static string SerializePresets(
        IEnumerable<GameplayLayoutPreset> presets) =>
        JsonSerializer.Serialize(presets.ToArray(), json_options);

    internal static IReadOnlyList<GameplayLayoutPreset> ParsePresets(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<GameplayLayoutPreset[]>(
                       json,
                       json_options)
                   ?? [];
        }
        catch
        {
            return [];
        }
    }

    internal static IEnumerable<(string Key, Bindable<double> Bindable)>
        enumerateLayoutBindables(YokkoGameplaySettings settings)
    {
        yield return ("playfieldOffsetX", settings.LayoutPlayfieldOffsetX);
        yield return ("playfieldOffsetY", settings.LayoutPlayfieldOffsetY);
        yield return ("hudOffsetX", settings.LayoutHudOffsetX);
        yield return ("hudOffsetY", settings.LayoutHudOffsetY);
        yield return ("playfieldWidthScale", settings.LayoutPlayfieldWidthScale);
        yield return ("playfieldHeightScale", settings.LayoutPlayfieldHeightScale);
        yield return ("hudScaleX", settings.LayoutHudScaleX);
        yield return ("hudScaleY", settings.LayoutHudScaleY);
        yield return ("accuracyOffsetX", settings.LayoutAccuracyOffsetX);
        yield return ("accuracyOffsetY", settings.LayoutAccuracyOffsetY);
        yield return ("accuracyScaleX", settings.LayoutAccuracyScaleX);
        yield return ("accuracyScaleY", settings.LayoutAccuracyScaleY);
        yield return ("progressOffsetX", settings.LayoutProgressOffsetX);
        yield return ("progressOffsetY", settings.LayoutProgressOffsetY);
        yield return ("progressScaleX", settings.LayoutProgressScaleX);
        yield return ("progressScaleY", settings.LayoutProgressScaleY);
        yield return ("timingBarOffsetX", settings.LayoutTimingBarOffsetX);
        yield return ("timingBarOffsetY", settings.LayoutTimingBarOffsetY);
        yield return ("timingBarScaleX", settings.LayoutTimingBarScaleX);
        yield return ("timingBarScaleY", settings.LayoutTimingBarScaleY);
        yield return ("comboOffsetX", settings.LayoutComboOffsetX);
        yield return ("comboOffsetY", settings.LayoutComboOffsetY);
        yield return ("comboScaleX", settings.LayoutComboScaleX);
        yield return ("comboScaleY", settings.LayoutComboScaleY);
        yield return ("judgementOffsetX", settings.LayoutJudgementOffsetX);
        yield return ("judgementOffsetY", settings.LayoutJudgementOffsetY);
        yield return ("judgementScaleX", settings.LayoutJudgementScaleX);
        yield return ("judgementScaleY", settings.LayoutJudgementScaleY);
        yield return ("performanceReadoutOffsetX", settings.LayoutPerformanceReadoutOffsetX);
        yield return ("performanceReadoutOffsetY", settings.LayoutPerformanceReadoutOffsetY);
        yield return ("replayControlsOffsetX", settings.ReplayControlsOffsetX);
        yield return ("replayControlsOffsetY", settings.ReplayControlsOffsetY);
        yield return ("topCoverRatio", settings.LayoutTopCoverRatio);
        yield return ("bottomCoverRatio", settings.LayoutBottomCoverRatio);
        yield return ("backgroundDim", settings.BackgroundDim);
    }
}
