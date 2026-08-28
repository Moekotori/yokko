using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Yokko.Game.Gameplay;

internal sealed class GameplayNamedKeyProfile
{
    public string Name { get; set; } = "Default";
    public string Payload { get; set; } = string.Empty;
}

/// <summary>
/// Named key profile storage layered on top of <see cref="GameplayKeyProfileCodec"/>.
/// </summary>
internal static class GameplayNamedKeyProfileStore
{
    private static readonly JsonSerializerOptions json_options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal static IReadOnlyList<GameplayNamedKeyProfile> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [new GameplayNamedKeyProfile()];

        try
        {
            return JsonSerializer.Deserialize<GameplayNamedKeyProfile[]>(
                       json,
                       json_options)
                   ?? [new GameplayNamedKeyProfile()];
        }
        catch
        {
            return
            [
                new GameplayNamedKeyProfile
                {
                    Name = "Default",
                    Payload = json.Trim(),
                },
            ];
        }
    }

    internal static string Serialize(IEnumerable<GameplayNamedKeyProfile> profiles) =>
        JsonSerializer.Serialize(profiles.ToArray(), json_options);

    internal static string EnsureDefaultProfile(string legacyPayload)
    {
        IReadOnlyList<GameplayNamedKeyProfile> profiles = Parse(legacyPayload);
        if (profiles.Count > 0 && !string.IsNullOrWhiteSpace(profiles[0].Payload))
            return legacyPayload;

        return Serialize(
        [
            new GameplayNamedKeyProfile
            {
                Name = "Default",
                Payload = legacyPayload ?? string.Empty,
            },
        ]);
    }
}
