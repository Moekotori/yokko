using Yokko.Import.Osu;

namespace Yokko.Import;

/// <summary>
/// 非 osu 谱面导入器共用的文件大小防线，与
/// <see cref="OsuManiaBeatmapIO.MaximumFileBytes"/> 使用同一上限，
/// 在读取整份谱面内容前拒绝异常巨大的文件。
/// </summary>
internal static class ChartFileSizeGuard
{
    public const long MaximumFileBytes = OsuManiaBeatmapIO.MaximumFileBytes;

    /// <summary>
    /// 校验 <paramref name="path"/> 的文件大小；超过
    /// <see cref="MaximumFileBytes"/> 时抛出 <see cref="InvalidDataException"/>。
    /// </summary>
    public static void EnsureWithinLimit(string path, string formatName)
    {
        var info = new FileInfo(path);

        if (info.Length > MaximumFileBytes)
        {
            throw new InvalidDataException(
                $"{formatName} chart '{path}' is {info.Length:N0} bytes; "
                + $"the safety limit is {MaximumFileBytes:N0} bytes.");
        }
    }
}
