using NUnit.Framework;
using Yokko.Import;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class KnownChartImportersTest
{
    [TestCase("chart.osu")]
    [TestCase("pack.OSZ")]
    [TestCase("chart.qua")]
    [TestCase("pack.QP")]
    [TestCase("chart.mc")]
    [TestCase("pack.mcz")]
    [TestCase("chart.sm")]
    [TestCase("chart.ssc")]
    [TestCase("pack.zip")]
    [TestCase("pack.smzip")]
    [TestCase("chart.bms")]
    public void RecognisesSupportedDroppedCharts(string path)
    {
        Assert.That(KnownChartImporters.CanImport(path), Is.True);
    }

    [TestCase("")]
    [TestCase("skin.osk")]
    [TestCase("audio.mp3")]
    public void RejectsOtherDroppedFiles(string path)
    {
        Assert.That(KnownChartImporters.CanImport(path), Is.False);
    }
}
