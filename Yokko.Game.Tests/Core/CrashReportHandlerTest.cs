using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Yokko.Desktop.Diagnostics;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class CrashReportHandlerTest
{
    private string testRoot;

    [SetUp]
    public void SetUp()
    {
        testRoot = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "crash-reports",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(testRoot))
            Directory.Delete(testRoot, true);
    }

    [Test]
    public void ReportContainsExceptionChainAndDiagnosticContext()
    {
        string logDirectory = Path.Combine(testRoot, "logs");
        string reportDirectory = Path.Combine(testRoot, "reports");
        var inner = new InvalidOperationException("inner failure");
        var exception = new ApplicationException("outer failure", inner);
        exception.Data["chart"] = "example.osu";

        using var handler = new CrashReportHandler(
            Assembly.GetExecutingAssembly(),
            reportDirectory);
        handler.SetStoragePaths(reportDirectory, logDirectory);

        string reportPath = handler.TryWrite(
            exception,
            "focused test");

        Assert.That(reportPath, Is.Not.Null);
        Assert.That(File.Exists(reportPath), Is.True);

        string report = File.ReadAllText(reportPath);

        Assert.Multiple(() =>
        {
            Assert.That(report, Does.Contain("Yokko crash report"));
            Assert.That(report, Does.Contain("Source: focused test"));
            Assert.That(report, Does.Contain("Crash reason: System.ApplicationException: outer failure"));
            Assert.That(report, Does.Contain("Root type: System.ApplicationException"));
            Assert.That(report, Does.Contain("Root message: outer failure"));
            Assert.That(report, Does.Contain("chart = example.osu"));
            Assert.That(report, Does.Contain("Inner 1 type: System.InvalidOperationException"));
            Assert.That(report, Does.Contain("Inner 1 message: inner failure"));
            Assert.That(report, Does.Contain($"Framework logs: {logDirectory}"));
            Assert.That(report, Does.Contain("Operating system:"));
            Assert.That(report, Does.Contain("Managed thread ID:"));
        });
    }

    [Test]
    public void MultipleReportsDoNotOverwriteEachOther()
    {
        using var handler = new CrashReportHandler(
            Assembly.GetExecutingAssembly(),
            testRoot);

        string first = handler.TryWrite(new Exception("first"), "test");
        string second = handler.TryWrite(new Exception("second"), "test");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(File.ReadAllText(first), Does.Contain("Root message: first"));
            Assert.That(File.ReadAllText(second), Does.Contain("Root message: second"));
        });
    }

    [Test]
    public void ReportingFailureDoesNotEscape()
    {
        string filePath = Path.Combine(testRoot, "not-a-directory");
        File.WriteAllText(filePath, "occupied");

        using var handler = new CrashReportHandler(
            Assembly.GetExecutingAssembly(),
            filePath);

        Assert.That(
            handler.TryWrite(new Exception("original failure"), "test"),
            Is.Null);
    }
}
