using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Logging;
using osu.Framework.Platform;
using Yokko.Game.Configuration;
using Yokko.Game.Diagnostics;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class YokkoDiagnosticsTest
{
    [Test]
    public void BoundedBufferRetainsNewestEntriesInSequence()
    {
        var buffer = new YokkoDiagnosticBuffer(2);
        buffer.Add(LogLevel.Verbose, "one", "first");
        YokkoDiagnosticEntry second = buffer.Add(
            LogLevel.Important,
            "two",
            "second");
        YokkoDiagnosticEntry third = buffer.Add(
            LogLevel.Error,
            "three",
            "third");

        Assert.Multiple(() =>
        {
            Assert.That(buffer.Count, Is.EqualTo(2));
            Assert.That(
                buffer.Snapshot(),
                Is.EqualTo(new[] { second, third }));
            Assert.That(
                buffer.GetAfter(second.Sequence),
                Is.EqualTo(new[] { third }));
            Assert.That(third.ToDisplayText(), Does.Contain("ERR"));
            Assert.That(third.ToExportText(), Does.Contain("[three] third"));
        });
    }

    [Test]
    public void ClearKeepsSequenceMonotonicForLiveReaders()
    {
        var buffer = new YokkoDiagnosticBuffer(4);
        YokkoDiagnosticEntry before = buffer.Add(
            LogLevel.Verbose,
            "test",
            "before");
        buffer.Clear();
        YokkoDiagnosticEntry after = buffer.Add(
            LogLevel.Verbose,
            "test",
            "after");

        Assert.Multiple(() =>
        {
            Assert.That(after.Sequence, Is.GreaterThan(before.Sequence));
            Assert.That(buffer.GetAfter(before.Sequence), Is.EqualTo(new[] { after }));
        });
    }

    [Test]
    public void FrameworkLoggerEntryIsCapturedImmediately()
    {
        string marker = $"live-marker-{Guid.NewGuid():N}";
        using var diagnostics = new YokkoDiagnostics();

        Logger.Log(marker, LoggingTarget.Runtime, LogLevel.Important);

        Assert.That(
            diagnostics.Snapshot().Any(entry => entry.Message == marker),
            Is.True);
    }

    [Test]
    public void ConsoleVisibilityDefaultsOffAndPersists()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "diagnostics-config",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            using (var firstDiagnostics = new YokkoDiagnostics())
            using (var firstConfig = new YokkoConfigManager(
                       new NativeStorage(directory)))
            {
                firstConfig.BindDiagnosticSettings(firstDiagnostics);
                Assert.That(firstDiagnostics.ConsoleVisible.Value, Is.False);

                firstDiagnostics.ConsoleVisible.Value = true;
                Assert.That(firstConfig.Save(), Is.True);
            }

            using var restoredDiagnostics = new YokkoDiagnostics();
            using var restoredConfig = new YokkoConfigManager(
                new NativeStorage(directory));
            restoredConfig.BindDiagnosticSettings(restoredDiagnostics);
            Assert.That(restoredDiagnostics.ConsoleVisible.Value, Is.True);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Test]
    public void PerformanceSamplesAreRetainedAndAlertsAreLogged()
    {
        using var diagnostics = new YokkoDiagnostics();
        diagnostics.ConsoleVisible.Value = true;
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        diagnostics.ReportPerformance(performanceSnapshot(
            timestamp,
            YokkoPerformanceHealth.Stable));
        diagnostics.ReportPerformance(performanceSnapshot(
            timestamp.AddMilliseconds(100),
            YokkoPerformanceHealth.Critical));
        diagnostics.ReportPerformance(performanceSnapshot(
            timestamp.AddMilliseconds(200),
            YokkoPerformanceHealth.Stable));

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.TryGetLatestPerformance(out var latest),
                Is.True);
            Assert.That(latest.Health, Is.EqualTo(
                YokkoPerformanceHealth.Stable));
            Assert.That(
                diagnostics.Snapshot().Any(entry =>
                    entry.Message.Contains(
                        "[PERFORMANCE] frame-pacing-alert",
                        StringComparison.Ordinal)
                    && entry.Level == LogLevel.Important),
                Is.True);
            Assert.That(
                diagnostics.Snapshot().Any(entry =>
                    entry.Message.Contains(
                        "[PERFORMANCE] frame-pacing-recovered",
                        StringComparison.Ordinal)
                    && entry.Level == LogLevel.Important),
                Is.True);
        });
    }

    [Test]
    public void ExportBundleContainsLiveLogManifestAndPerformance()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "diagnostics-export",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string marker = $"export-marker-{Guid.NewGuid():N}";
            using var diagnostics = new YokkoDiagnostics();
            diagnostics.InitialiseForTesting(new NativeStorage(directory));
            string loggerFilename = Logger.GetLogger(
                "diagnostics").Filename;
            string sessionPrefix = loggerFilename[..loggerFilename.IndexOf('.')];
            string rawLogName = $"{sessionPrefix}.runtime.log";
            string rawLogDirectory = Path.Combine(directory, "logs");
            Directory.CreateDirectory(rawLogDirectory);
            File.WriteAllText(
                Path.Combine(rawLogDirectory, rawLogName),
                "raw-session-marker");
            diagnostics.ReportPerformance(performanceSnapshot(
                DateTimeOffset.UtcNow,
                YokkoPerformanceHealth.Warning));
            Logger.Log(marker, LoggingTarget.Runtime, LogLevel.Important);

            string path = diagnostics.ExportBundle();

            Assert.That(File.Exists(path), Is.True);
            using ZipArchive archive = ZipFile.OpenRead(path);
            ZipArchiveEntry liveLog = archive.GetEntry(
                "diagnostics-live.log");
            ZipArchiveEntry manifest = archive.GetEntry("manifest.txt");
            ZipArchiveEntry rawLog = archive.GetEntry(
                $"logs/{rawLogName}");

            Assert.Multiple(() =>
            {
                Assert.That(liveLog, Is.Not.Null);
                Assert.That(manifest, Is.Not.Null);
                Assert.That(rawLog, Is.Not.Null);
                Assert.That(readEntry(liveLog), Does.Contain(marker));
                Assert.That(
                    readEntry(rawLog),
                    Does.Contain("raw-session-marker"));
                Assert.That(
                    readEntry(manifest),
                    Does.Contain("LatestPerformance:"));
            });
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private static string readEntry(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static YokkoPerformanceSnapshot performanceSnapshot(
        DateTimeOffset timestamp,
        YokkoPerformanceHealth health) => new(
            timestamp,
            480,
            2.08,
            2.2,
            3.1,
            8,
            2,
            0.01,
            1000,
            1,
            1.2,
            1.8,
            5,
            1,
            0.005,
            1000,
            12.5,
            256 * 1024 * 1024,
            64 * 1024 * 1024,
            4,
            1,
            0,
            health);
}
