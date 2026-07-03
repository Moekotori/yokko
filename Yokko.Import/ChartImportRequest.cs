namespace Yokko.Import;

public sealed record ChartImportRequest(
    string Path,
    bool PreferKeysounds,
    CancellationToken CancellationToken = default);
