using System.Collections.Generic;

namespace Yokko.Game.Skinning.OsuMania;

internal sealed record OsuManiaSkinInfo(
    string Name,
    string Author,
    string Version,
    int AnimationFrameRate,
    bool LayeredHitSounds,
    bool ComboBurstRandom,
    string ComboPrefix,
    int ComboOverlap,
    IReadOnlyDictionary<int, OsuManiaSkinConfiguration> ManiaConfigurations)
{
    public string ScorePrefix { get; init; } = "score";

    public int ScoreOverlap { get; init; }

    public OsuManiaSkinConfiguration GetConfiguration(
        int keys,
        bool? splitStages = null) =>
        ManiaConfigurations.TryGetValue(keys, out OsuManiaSkinConfiguration configuration)
            ? configuration
            : OsuManiaSkinConfiguration.CreateDefault(
                keys,
                Version,
                splitStages);
}
