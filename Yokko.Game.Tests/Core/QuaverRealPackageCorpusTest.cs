using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Yokko.Import;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class QuaverRealPackageCorpusTest
{
    [Test]
    [Category("Integration")]
    public void ImportsConfiguredRealQuaverSvPackage()
    {
        string path =
            Environment.GetEnvironmentVariable(
                "YOKKO_QUAVER_SV_PACKAGE");

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            Assert.Ignore(
                "Set YOKKO_QUAVER_SV_PACKAGE to a real .qp package.");
        }

        ChartImportResult[] results =
            KnownChartImporters.ImportAllAsync(
                                   new ChartImportRequest(path, true))
                               .AsTask()
                               .GetAwaiter()
                               .GetResult()
                               .ToArray();

        Assert.That(results, Is.Not.Empty);

        int totalZeroVelocityCount = 0;
        int totalNegativeVelocityCount = 0;

        foreach (ChartImportResult result in results)
        {
            Assert.Multiple(() =>
            {
                Assert.That(result.Beatmap.HitObjects, Is.Not.Empty);
                Assert.That(result.Beatmap.ScrollVelocities, Is.Not.Empty);
                Assert.That(
                    result.Beatmap.ScrollVelocities.Select(
                        velocity => velocity.TimeMilliseconds),
                    Is.Ordered);
                Assert.That(
                    result.Beatmap.ScrollVelocities.All(velocity =>
                        double.IsFinite(velocity.TimeMilliseconds)
                        && double.IsFinite(velocity.Multiplier)),
                    Is.True);
                Assert.That(
                    File.Exists(result.Beatmap.AudioPath),
                    Is.True);
                Assert.That(File.Exists(result.ArtworkPath), Is.True);
            });

            int zeroVelocityCount =
                result.Beatmap.ScrollVelocities.Count(
                    velocity => velocity.Multiplier == 0);
            int negativeVelocityCount =
                result.Beatmap.ScrollVelocities.Count(
                    velocity => velocity.Multiplier < 0);
            totalZeroVelocityCount += zeroVelocityCount;
            totalNegativeVelocityCount += negativeVelocityCount;

            TestContext.Progress.WriteLine(
                $"{result.Beatmap.Title} "
                + $"[{result.Beatmap.DifficultyName}] "
                + $"notes={result.Beatmap.HitObjects.Count} "
                + $"initialSV={result.Beatmap.InitialScrollVelocity} "
                + $"svs={result.Beatmap.ScrollVelocities.Count} "
                + $"zero={zeroVelocityCount} "
                + $"negative={negativeVelocityCount}");
        }

        Assert.Multiple(() =>
        {
            Assert.That(totalZeroVelocityCount, Is.GreaterThan(0));
            Assert.That(totalNegativeVelocityCount, Is.GreaterThan(0));
        });
    }
}
