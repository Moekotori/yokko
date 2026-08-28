using NUnit.Framework;
using System;
using System.IO;
using Yokko.Import;
using Yokko.Import.Osu;

namespace Yokko.Game.Tests.Core;

/// <summary>
/// 非 osu 导入器（Etterna/BMS/Quaver/Malody）读取谱面前必须应用与
/// <see cref="OsuManiaBeatmapIO.MaximumFileBytes"/> 一致的文件大小防线。
/// </summary>
[TestFixture]
public sealed class ChartFileSizeLimitTest
{
    [TestCase(".sm")]
    [TestCase(".ssc")]
    [TestCase(".bms")]
    [TestCase(".qua")]
    [TestCase(".mc")]
    public void OversizedChartFileIsRejectedBeforeReading(string extension)
    {
        string path = createChartFile(
            extension,
            OsuManiaBeatmapIO.MaximumFileBytes + 1);

        try
        {
            Assert.That(
                () => import(path),
                Throws.TypeOf<InvalidDataException>()
                      .With.Message.Contains("safety limit"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestCase(".sm")]
    [TestCase(".bms")]
    [TestCase(".qua")]
    [TestCase(".mc")]
    public void ChartFileAtTheSizeLimitPassesTheGuard(string extension)
    {
        string path = createChartFile(
            extension,
            OsuManiaBeatmapIO.MaximumFileBytes);

        try
        {
            // 恰好等于上限的文件必须进入正常解析流程；允许因内容无效
            // 而失败，但绝不能是大小防线的拒绝。
            try
            {
                import(path);
            }
            catch (Exception exception)
            {
                Assert.That(
                    exception.Message,
                    Does.Not.Contain("safety limit"));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static ChartImportResult import(string path)
        => KnownChartImporters.ImportAsync(new ChartImportRequest(path, true))
                              .AsTask()
                              .GetAwaiter()
                              .GetResult();

    private static string createChartFile(string extension, long length)
    {
        string path = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "chart-size-limit",
            TestContext.CurrentContext.Test.ID,
            $"chart-{Guid.NewGuid():N}{extension}");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using (FileStream stream = File.Create(path))
            stream.SetLength(length);

        return path;
    }
}
