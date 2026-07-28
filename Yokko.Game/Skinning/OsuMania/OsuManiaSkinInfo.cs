using System.Collections.Generic;

namespace Yokko.Game.Skinning.OsuMania;

internal sealed record OsuManiaSkinInfo(
    string Name,
    string Author,
    string Version,
    IReadOnlyDictionary<int, OsuManiaSkinConfiguration> ManiaConfigurations)
{
    public OsuManiaSkinConfiguration GetConfiguration(int keys) =>
        ManiaConfigurations.TryGetValue(keys, out OsuManiaSkinConfiguration configuration)
            ? configuration
            : OsuManiaSkinConfiguration.CreateDefault(keys);
}
