using Yokko.Core.Beatmaps;

namespace Yokko.Import;

public sealed record ChartImportCapability(
    ChartSourceFormat Format,
    string DisplayName,
    string[] FileExtensions,
    bool SupportsKeysounds,
    bool SupportsBackgroundAnimation);
