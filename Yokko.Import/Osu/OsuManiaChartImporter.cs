using Yokko.Core.Beatmaps;

namespace Yokko.Import.Osu;

public sealed class OsuManiaChartImporter : IChartImporter
{
    public ChartImportCapability Capability { get; } =
        new(ChartSourceFormat.OsuMania, "osu!mania", [".osu", ".osz"], false, false);

    public bool CanImport(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".osu", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".osz", StringComparison.OrdinalIgnoreCase);
    }

    public ValueTask<ChartImportResult> ImportAsync(ChartImportRequest request)
    {
        request.CancellationToken.ThrowIfCancellationRequested();

        if (Path.GetExtension(request.Path).Equals(".osu", StringComparison.OrdinalIgnoreCase))
            return ValueTask.FromResult(new ChartImportResult(
                OsuManiaBeatmapIO.ReadBeatmapFromFile(request.Path),
                []));

        IReadOnlyList<string> charts = ChartArchive.ExtractCharts(request.Path, ".osu");
        var failures = new List<Exception>();

        foreach (string chart in charts)
        {
            try
            {
                YokkoBeatmap beatmap = OsuManiaBeatmapIO.ReadBeatmapFromFile(chart);
                IReadOnlyList<string> warnings = charts.Count > 1
                    ? [$"This .osz contains {charts.Count} charts; imported {Path.GetFileName(chart)}."]
                    : [];
                return ValueTask.FromResult(new ChartImportResult(beatmap, warnings));
            }
            catch (InvalidDataException ex)
            {
                failures.Add(ex);
            }
        }

        throw new InvalidDataException("The .osz package does not contain a supported 4K/7K osu!mania chart.", failures.FirstOrDefault());
    }
}
