using Yokko.Import.Bms;
using Yokko.Import.Etterna;
using Yokko.Import.Malody;
using Yokko.Import.Osu;
using Yokko.Import.Quaver;

namespace Yokko.Import;

public static class KnownChartImporters
{
    public static IReadOnlyList<IChartImporter> Importers { get; } =
    [
        new OsuManiaChartImporter(),
        new QuaverChartImporter(),
        new MalodyChartImporter(),
        new EtternaChartImporter(),
        new BmsChartImporter(),
    ];

    public static IReadOnlyList<ChartImportCapability> Capabilities { get; } =
        Importers.Select(static importer => importer.Capability).ToArray();

    public static string[] FileExtensions { get; } =
        Capabilities.SelectMany(static capability => capability.FileExtensions)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

    public static bool CanImport(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && Importers.Any(importer => importer.CanImport(path));

    public static async ValueTask<ChartImportResult> ImportAsync(ChartImportRequest request)
    {
        IChartImporter? importer = Importers.FirstOrDefault(candidate => candidate.CanImport(request.Path));

        if (importer == null)
            throw new NotSupportedException($"Unsupported chart format: {Path.GetExtension(request.Path)}");

        return await importer.ImportAsync(request);
    }
}
