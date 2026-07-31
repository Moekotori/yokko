namespace Yokko.Import;

public sealed record ChartImportRequest(
    string Path,
    bool PreferKeysounds,
    bool PreferSscSimfiles = true,
    bool EnableBmsScratch = false,
    CancellationToken CancellationToken = default);
