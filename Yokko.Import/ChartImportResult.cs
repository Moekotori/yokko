using Yokko.Core.Beatmaps;

namespace Yokko.Import;

/// <summary>
/// 单张谱面的导入结果。当结果来自压缩包（.osz/.qp/.mcz/.zip 等）时，
/// <c>ExtractedArchiveRoot</c> 指向本次解压产生的临时 GUID 目录；
/// <c>Beatmap</c> 中的音频、键音与背景路径可能位于该目录内。目录由
/// 调用方负责生命周期：结果不再被使用后，通过
/// <see cref="ChartArchive.TryDeleteExtraction"/> 删除。
/// </summary>
public sealed record ChartImportResult(
    YokkoBeatmap Beatmap,
    IReadOnlyList<string> Warnings,
    string? ArtworkPath = null,
    string? SourceHash = null,
    string? ExtractedArchiveRoot = null);
