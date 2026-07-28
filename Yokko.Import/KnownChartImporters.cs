using Yokko.Core.Beatmaps;

namespace Yokko.Import;

public static class KnownChartImporters
{
    public static IReadOnlyList<ChartImportCapability> Capabilities { get; } =
    [
        new(ChartSourceFormat.OsuMania, "osu!mania", [".osu"], false, false),
    ];
}
