namespace Yokko.Import;

public interface IChartImporter
{
    ChartImportCapability Capability { get; }

    bool CanImport(string path);

    ValueTask<ChartImportResult> ImportAsync(ChartImportRequest request);
}
