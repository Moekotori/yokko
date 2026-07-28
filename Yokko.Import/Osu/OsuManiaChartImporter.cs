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
                [],
                OsuManiaBeatmapIO.ReadBackgroundPathFromFile(request.Path)));

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
                return ValueTask.FromResult(new ChartImportResult(
                    beatmap,
                    warnings,
                    OsuManiaBeatmapIO.ReadBackgroundPathFromFile(chart)));
            }
            catch (InvalidDataException ex)
            {
                failures.Add(ex);
            }
        }

        throw new InvalidDataException("The .osz package does not contain a supported 4K/7K osu!mania chart.", failures.FirstOrDefault());
    }

    public ValueTask<IReadOnlyList<ChartImportResult>> ImportAllAsync(
        ChartImportRequest request)
    {
        request.CancellationToken.ThrowIfCancellationRequested();

        if (Path.GetExtension(request.Path).Equals(".osu", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult<IReadOnlyList<ChartImportResult>>(
                [new ChartImportResult(
                    OsuManiaBeatmapIO.ReadBeatmapFromFile(request.Path),
                    [],
                    OsuManiaBeatmapIO.ReadBackgroundPathFromFile(request.Path))]);
        }

        IReadOnlyList<string> charts = ChartArchive.ExtractCharts(request.Path, ".osu");
        var results = new List<ChartImportResult>();
        var failures = new List<Exception>();

        foreach (string chart in charts)
        {
            request.CancellationToken.ThrowIfCancellationRequested();

            try
            {
                results.Add(new ChartImportResult(
                    OsuManiaBeatmapIO.ReadBeatmapFromFile(chart),
                    [],
                    OsuManiaBeatmapIO.ReadBackgroundPathFromFile(chart)));
            }
            catch (InvalidDataException ex)
            {
                failures.Add(ex);
            }
        }

        if (results.Count == 0)
        {
            throw new InvalidDataException(
                "The .osz package does not contain a supported 4K/7K osu!mania chart.",
                failures.FirstOrDefault());
        }

        if (failures.Count > 0)
        {
            string warning =
                $"Skipped {failures.Count} unsupported chart{(failures.Count == 1 ? string.Empty : "s")} in this .osz package.";
            for (int i = 0; i < results.Count; i++)
                results[i] = results[i] with { Warnings = [warning] };
        }

        return ValueTask.FromResult<IReadOnlyList<ChartImportResult>>(results);
    }
}
