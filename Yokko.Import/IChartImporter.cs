namespace Yokko.Import;

public interface IChartImporter
{
    ChartImportCapability Capability { get; }

    bool CanImport(string path);

    ValueTask<ChartImportResult> ImportAsync(ChartImportRequest request);

    async ValueTask<IReadOnlyList<ChartImportResult>> ImportAllAsync(
        ChartImportRequest request) =>
        [await ImportAsync(request)];
}
