using Yokko.Core.Beatmaps;

namespace Yokko.Import.Osu;

public sealed class OsuManiaChartImporter : IChartImporter
{
    public ChartImportCapability Capability { get; } =
        new(ChartSourceFormat.OsuMania, "osu!mania", [".osu"], true, false);

    public bool CanImport(string path)
        => string.Equals(Path.GetExtension(path), ".osu", StringComparison.OrdinalIgnoreCase);

    public ValueTask<ChartImportResult> ImportAsync(ChartImportRequest request)
    {
        request.CancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new ChartImportResult(
            OsuManiaBeatmapIO.ReadBeatmapFromFile(request.Path),
            []));
    }
}
