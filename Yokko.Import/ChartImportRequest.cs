namespace Yokko.Import;

public sealed record ChartImportRequest(
    string Path,
    bool PreferKeysounds,
    bool PreferSscSimfiles = true,
    CancellationToken CancellationToken = default);
