using Yokko.Core.Beatmaps;

namespace Yokko.Import.Osu;

public sealed class OsuManiaChartImporter : IChartImporter
{
    public ChartImportCapability Capability { get; } =
        new(
            ChartSourceFormat.OsuMania,
            "osu! / osu!mania",
            [".osu", ".osz"],
            false,
            false);

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
            return ValueTask.FromResult(importChartFile(
                request.Path,
                [],
                extractedArchiveRoot: null,
                request.CancellationToken));

        ChartArchiveExtraction extraction = ChartArchive.ExtractCharts(request.Path, ".osu");
        IReadOnlyList<string> charts = extraction.ChartPaths;
        var failures = new List<Exception>();

        foreach (string chart in charts)
        {
            try
            {
                IReadOnlyList<string> warnings = charts.Count > 1
                    ? [$"This .osz contains {charts.Count} charts; imported {Path.GetFileName(chart)}."]
                    : [];
                return ValueTask.FromResult(importChartFile(
                    chart,
                    warnings,
                    extraction.RootPath,
                    request.CancellationToken));
            }
            catch (InvalidDataException ex)
            {
                failures.Add(ex);
            }
        }

        ChartArchive.TryDeleteExtraction(extraction.RootPath);
        throw new InvalidDataException(
            "The .osz package does not contain a supported osu!standard or osu!mania chart.",
            failures.FirstOrDefault());
    }

    public ValueTask<IReadOnlyList<ChartImportResult>> ImportAllAsync(
        ChartImportRequest request)
    {
        request.CancellationToken.ThrowIfCancellationRequested();

        if (Path.GetExtension(request.Path).Equals(".osu", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult<IReadOnlyList<ChartImportResult>>(
                [importChartFile(
                    request.Path,
                    [],
                    extractedArchiveRoot: null,
                    request.CancellationToken)]);
        }

        ChartArchiveExtraction extraction = ChartArchive.ExtractCharts(request.Path, ".osu");
        var results = new List<ChartImportResult>();
        var failures = new List<Exception>();

        foreach (string chart in extraction.ChartPaths)
        {
            request.CancellationToken.ThrowIfCancellationRequested();

            try
            {
                results.Add(importChartFile(
                    chart,
                    [],
                    extraction.RootPath,
                    request.CancellationToken));
            }
            catch (InvalidDataException ex)
            {
                failures.Add(ex);
            }
        }

        if (results.Count == 0)
        {
            ChartArchive.TryDeleteExtraction(extraction.RootPath);
            throw new InvalidDataException(
                "The .osz package does not contain a supported osu!standard or osu!mania chart.",
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

    private static ChartImportResult importChartFile(
        string path,
        IReadOnlyList<string> warnings,
        string? extractedArchiveRoot,
        CancellationToken cancellationToken)
    {
        OsuBeatmapFileImport import = OsuManiaBeatmapIO.ReadBeatmapForImport(
            path,
            cancellationToken);
        return new ChartImportResult(
            import.Beatmap,
            warnings,
            import.BackgroundPath,
            import.Md5Hash,
            extractedArchiveRoot);
    }
}
