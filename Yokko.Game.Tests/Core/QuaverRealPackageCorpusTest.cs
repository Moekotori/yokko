using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Yokko.Core.Timing;
using Yokko.Import;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class QuaverRealPackageCorpusTest
{
    [Test]
    public void MatchesPinnedQuaverApiSpecialBpmCorpus()
    {
        // Minimal parity vector derived from Quaver.API's MPL-2.0
        // Quaver.API.Tests/Quaver/Resources/cheat.qua at
        // a921d561b2ece7f6bf3682446696c06c17b81649.
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "quaver-parity-corpus",
            TestContext.CurrentContext.Test.ID);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "special-bpm.qua");
        File.WriteAllText(
            path,
            """
Mode: Keys4
BPMDoesNotAffectScrollVelocity: false
TimingPoints:
  - StartTime: 0
    Bpm: 142
  - StartTime: 1000
    Bpm: .inf
  - StartTime: 1001
    Bpm: 0.0005988000193610787
  - StartTime: 1002
    Bpm: 142
SliderVelocities: []
HitObjects:
  - StartTime: 0
    Lane: 1
  - StartTime: 2000
    Lane: 1
""");

        ChartImportResult result =
            KnownChartImporters.ImportAsync(
                                   new ChartImportRequest(path, true))
                               .AsTask()
                               .GetAwaiter()
                               .GetResult();
        var map = new ScrollVelocityMap(
            result.Beatmap.ScrollVelocities,
            result.Beatmap.InitialScrollVelocity);

        Assert.Multiple(() =>
        {
            Assert.That(map.MultiplierAt(999), Is.EqualTo(1));
            Assert.That(map.MultiplierAt(1000), Is.EqualTo(128));
            Assert.That(
                map.MultiplierAt(1001),
                Is.EqualTo(0.0005988000193610787 / 142)
                  .Within(0.000000000001));
            Assert.That(map.MultiplierAt(1002), Is.EqualTo(1));
        });
    }

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
            assertRuntimeSvSemantics(result);

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

    private static void assertRuntimeSvSemantics(
        ChartImportResult result)
    {
        var map = new ScrollVelocityMap(
            result.Beatmap.ScrollVelocities,
            result.Beatmap.InitialScrollVelocity);
        double chartEnd =
            result.Beatmap.HitObjects.Max(hitObject =>
                hitObject.EndTimeMilliseconds
                ?? hitObject.StartTimeMilliseconds);

        for (int i = 0; i < map.ScrollVelocities.Count; i++)
        {
            YokkoScrollVelocity velocity =
                map.ScrollVelocities[i];
            double segmentEnd =
                i + 1 < map.ScrollVelocities.Count
                    ? map.ScrollVelocities[i + 1].TimeMilliseconds
                    : chartEnd;

            if (segmentEnd <= velocity.TimeMilliseconds)
                continue;

            double distance = map.DistanceBetween(
                velocity.TimeMilliseconds,
                segmentEnd);

            if (velocity.Multiplier == 0)
            {
                Assert.That(
                    distance,
                    Is.EqualTo(0).Within(0.000001),
                    $"Zero SV moved between "
                    + $"{velocity.TimeMilliseconds:0.###} and "
                    + $"{segmentEnd:0.###} ms.");
            }
            else if (velocity.Multiplier < 0)
            {
                Assert.That(
                    distance,
                    Is.LessThan(0),
                    $"Negative SV did not reverse between "
                    + $"{velocity.TimeMilliseconds:0.###} and "
                    + $"{segmentEnd:0.###} ms.");
            }
        }
    }
}
