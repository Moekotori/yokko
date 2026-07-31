using System;
using System.IO;
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
}
