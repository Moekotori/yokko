using Yokko.Core.Beatmaps;

namespace Yokko.Import;

public static class KnownChartImporters
{
    public static IReadOnlyList<ChartImportCapability> Capabilities { get; } =
    [
        new(ChartSourceFormat.OsuMania, "osu!mania", [".osu", ".osz"], false, true),
        new(ChartSourceFormat.Malody, "Malody", [".mc", ".mcz"], false, true),
        new(ChartSourceFormat.Bms, "BMS", [".bms", ".bme", ".bml", ".pms"], true, true),
        new(ChartSourceFormat.Lr2Bms, "LR2 library", [".bms", ".bme", ".bml"], true, true),
    ];
}
