using System;
using System.Collections.Generic;
using System.Linq;
using Yokko.Import;

namespace Yokko.Game.Importing;

internal sealed record ImportedChart(
    string Id,
    string SourcePath,
    ChartImportResult Result);

/// <summary>
/// Holds charts imported during the current Yokko session and notifies views
/// which present the playable chart library.
/// </summary>
internal sealed class ImportedChartLibrary
{
    private readonly List<ImportedChart> charts = [];
    private readonly object syncRoot = new();

    public event Action LibraryChanged;

    public IReadOnlyList<ImportedChart> GetCharts()
    {
        lock (syncRoot)
            return charts.ToArray();
    }

    public void AddOrReplace(ChartImportResult result, string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(result);
        AddOrReplace([result], sourcePath);
    }

    public void AddOrReplace(
        IReadOnlyList<ChartImportResult> results,
        string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        if (results.Count == 0)
            throw new ArgumentException("At least one imported chart is required.", nameof(results));

        ImportedChart[] imported = results
                                   .Select((result, index) => new ImportedChart(
                                       $"{sourcePath}\u001f{index}",
                                       sourcePath,
                                       result))
                                   .ToArray();

        lock (syncRoot)
        {
            charts.RemoveAll(chart =>
                chart.SourcePath.Equals(sourcePath, StringComparison.OrdinalIgnoreCase));
            charts.AddRange(imported);
        }

        LibraryChanged?.Invoke();
    }
}
