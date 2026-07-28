using Yokko.Core.Beatmaps;

namespace Yokko.Import;

public sealed record ChartImportResult(
    YokkoBeatmap Beatmap,
    IReadOnlyList<string> Warnings,
    string? ArtworkPath = null,
    string? SourceHash = null);
