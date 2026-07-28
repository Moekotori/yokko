using System;
using System.Collections.Generic;
using Yokko.Import;

namespace Yokko.Game.Importing;

internal sealed record ImportedChart(
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

    public event Action<ImportedChart> ChartImported;

    public IReadOnlyList<ImportedChart> GetCharts()
    {
        lock (syncRoot)
            return charts.ToArray();
    }

    public void AddOrReplace(ChartImportResult result, string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var imported = new ImportedChart(sourcePath, result);

        lock (syncRoot)
        {
            int existingIndex = charts.FindIndex(chart =>
                chart.SourcePath.Equals(sourcePath, StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0)
                charts[existingIndex] = imported;
            else
                charts.Add(imported);
        }

        ChartImported?.Invoke(imported);
    }
}
