using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Yokko.Import;
using Yokko.Import.Bms;
using Yokko.Core.Beatmaps;
using Yokko.Core.Scoring;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class BmsChartImporterRegressionTest
{
    [Test]
    public void ResolvesDeclaredAudioByStemWhenExtensionDiffers()
    {
        string directory = createTestDirectory();
        string chartPath = writeChart(directory, """
#TITLE Extension fallback
#BPM 120
#WAV01 sample.wav
#00111:01
#00112:01
#00113:01
#00114:01
""");
        File.WriteAllBytes(Path.Combine(directory, "sample.ogg"), []);

        ChartImportResult result = import(chartPath);

        Assert.That(
            result.Beatmap.HitObjects.Select(static note => note.SampleKey),
            Is.All.EndsWith("sample.ogg"));
    }

    [Test]
    public void RejectsAmbiguousOrOutOfDirectoryAudioFallbacks()
    {
        string directory = createTestDirectory();
        string ambiguousChart = writeChart(directory, """
#TITLE Ambiguous fallback
#BPM 120
#WAV01 ambiguous.wav
#00111:01
""", "ambiguous.bms");
        File.WriteAllBytes(Path.Combine(directory, "ambiguous.ogg"), []);
        File.WriteAllBytes(Path.Combine(directory, "ambiguous.mp3"), []);

        string subdirectoryChart = writeChart(directory, """
#TITLE Directory fallback
#BPM 120
#WAV01 sub/root-only.wav
#00111:01
""", "subdirectory.bms");
        File.WriteAllBytes(Path.Combine(directory, "root-only.wav"), []);

        ChartImportResult ambiguous = import(ambiguousChart);
        ChartImportResult subdirectory = import(subdirectoryChart);

        Assert.Multiple(() =>
        {
            Assert.That(ambiguous.Beatmap.HitObjects.Single().SampleKey, Is.Null);
            Assert.That(
                ambiguous.Warnings,
                Has.Some.Contains("multiple alternate file extensions"));
            Assert.That(subdirectory.Beatmap.HitObjects.Single().SampleKey, Is.Null);
        });
    }

    [Test]
    public void MergesDuplicatePlayableChannelsButStacksBackgroundChannels()
    {
        string directory = createTestDirectory();
        string chartPath = writeChart(directory, """
#TITLE Duplicate channels
#BPM 120
#WAV01 one.wav
#WAV02 two.wav
#WAV03 three.wav
#00001:01
#00001:02
#00111:0102
#00111:0003
#00212:01
#00213:01
#00214:01
""");
        foreach (string file in new[] { "one.wav", "two.wav", "three.wav" })
            File.WriteAllBytes(Path.Combine(directory, file), []);

        ChartImportResult result = import(chartPath);
        var laneObjects = result.Beatmap.HitObjects
                                .Where(static note => note.Lane == 0)
                                .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(laneObjects, Has.Length.EqualTo(2));
            Assert.That(laneObjects[0].SampleKey, Does.EndWith("one.wav"));
            Assert.That(laneObjects[1].SampleKey, Does.EndWith("three.wav"));
            Assert.That(result.Beatmap.ScheduledSamples, Has.Count.EqualTo(2));
            Assert.That(
                result.Beatmap.ScheduledSamples,
                Is.All.Matches<Yokko.Core.Beatmaps.YokkoScheduledSample>(
                    static sample => sample.UseMusicBus));
        });
    }

    [Test]
    public void CombinesBpmScrollSpeedWithStopAtTheSameBeat()
    {
        string directory = createTestDirectory();
        string chartPath = writeChart(directory, """
#TITLE BPM and STOP
#BPM 120
#BPM01 240
#STOP01 48
#00108:01
#00109:01
#00111:01
#00212:01
#00213:01
#00214:01
""");

        ChartImportResult result = import(chartPath);

        Assert.Multiple(() =>
        {
            Assert.That(result.Beatmap.InitialScrollVelocity, Is.EqualTo(1));
            Assert.That(result.Beatmap.ScrollVelocities, Has.Count.EqualTo(2));
            Assert.That(
                result.Beatmap.ScrollVelocities[0].TimeMilliseconds,
                Is.EqualTo(2000).Within(0.001));
            Assert.That(result.Beatmap.ScrollVelocities[0].Multiplier, Is.Zero);
            Assert.That(
                result.Beatmap.ScrollVelocities[1].TimeMilliseconds,
                Is.EqualTo(2250).Within(0.001));
            Assert.That(result.Beatmap.ScrollVelocities[1].Multiplier, Is.EqualTo(2));
            Assert.That(
                result.Beatmap.HitObjects.Max(static note => note.StartTimeMilliseconds),
                Is.EqualTo(3250).Within(0.001));
        });
    }

    [Test]
    public void RetainsRankAndDefExRankWithBeatorajaLineOrder()
    {
        string directory = createTestDirectory();
        string defaultChart = writeChart(directory, """
#TITLE Default rank
#BPM 120
#00111:01
""", "default.bms");
        string defExLastChart = writeChart(directory, """
#TITLE DEFEX last
#BPM 120
#RANK 3
#DEFEXRANK 50
#00111:01
""", "defex-last.bms");
        string rankLastChart = writeChart(directory, """
#TITLE RANK last
#BPM 120
#DEFEXRANK 50
#RANK 4
#00111:01
""", "rank-last.bms");
        string sevenKeyChart = writeChart(directory, """
#TITLE Seven key profile
#BPM 120
#00118:01
""", "seven-key.bme");

        BmsJudgementMetadata defaultRank =
            import(defaultChart).Beatmap.BmsJudgement!.Value;
        BmsJudgementMetadata defExLast =
            import(defExLastChart).Beatmap.BmsJudgement!.Value;
        BmsJudgementMetadata rankLast =
            import(rankLastChart).Beatmap.BmsJudgement!.Value;
        BmsJudgementMetadata sevenKey =
            import(sevenKeyChart).Beatmap.BmsJudgement!.Value;

        Assert.Multiple(() =>
        {
            Assert.That(defaultRank.WindowMultiplier, Is.EqualTo(0.75));
            Assert.That(defaultRank.Source,
                Is.EqualTo(BmsJudgementRankSource.Default));
            Assert.That(defaultRank.RegularKeysPerStage, Is.EqualTo(5));
            Assert.That(defExLast.WindowMultiplier, Is.EqualTo(0.37));
            Assert.That(defExLast.Source,
                Is.EqualTo(BmsJudgementRankSource.DefExRank));
            Assert.That(rankLast.WindowMultiplier, Is.EqualTo(1.25));
            Assert.That(rankLast.Source,
                Is.EqualTo(BmsJudgementRankSource.Rank));
            Assert.That(sevenKey.RegularKeysPerStage, Is.EqualTo(7));
        });
    }

    [Test]
    public void DefExRankUsesBeatorajaIntegerNormalisation()
    {
        string directory = createTestDirectory();
        string chart = writeChart(directory, """
#TITLE Integer DEFEX rank
#BPM 120
#DEFEXRANK 101
#00111:01
""", "defex-integer.bms");

        BmsJudgementMetadata metadata =
            import(chart).Beatmap.BmsJudgement!.Value;

        Assert.Multiple(() =>
        {
            Assert.That(metadata.WindowMultiplier, Is.EqualTo(0.75));
            Assert.That(metadata.Value, Is.EqualTo(101));
            Assert.That(metadata.Source,
                Is.EqualTo(BmsJudgementRankSource.DefExRank));
        });
    }

    [Test]
    public void DefExRankOneRetainsLegalZeroPercentWindow()
    {
        string directory = createTestDirectory();
        string chart = writeChart(directory, """
#TITLE Zero percent DEFEX rank
#BPM 120
#DEFEXRANK 1
#00111:01
""", "defex-zero.bms");

        ChartImportResult result = import(chart);
        BmsJudgementMetadata metadata =
            result.Beatmap.BmsJudgement!.Value;

        Assert.Multiple(() =>
        {
            Assert.That(metadata.WindowMultiplier, Is.Zero);
            Assert.That(metadata.Value, Is.EqualTo(1));
            Assert.That(
                new BmsJudgementWindows(metadata).Judge(
                    0,
                    BmsJudgeObjectType.Note),
                Is.EqualTo(JudgementRating.Perfect));
            Assert.That(
                new BmsJudgementWindows(metadata).Judge(
                    0.001,
                    BmsJudgeObjectType.Note),
                Is.EqualTo(JudgementRating.None));
        });
    }

    [Test]
    public void WarnsWhenLongNoteSemanticsExceedTraditionalLnSupport()
    {
        string directory = createTestDirectory();
        string chart = writeChart(directory, """
#TITLE Unsupported BMS long notes
#BPM 120
#LNMODE 3
#00151:0101
#00156:0101
""", "unsupported-ln.bms");

        ChartImportResult result = import(chart);

        Assert.Multiple(() =>
        {
            Assert.That(result.Warnings,
                Has.Some.Contains("CN/HCN judgement"));
            Assert.That(result.Warnings,
                Has.Some.Contains("long-scratch/BSS"));
        });
    }

    private static ChartImportResult import(string path) =>
        new BmsChartImporter().ImportAsync(new ChartImportRequest(
                                  path,
                                  PreferKeysounds: true))
                              .AsTask()
                              .GetAwaiter()
                              .GetResult();

    private static string createTestDirectory()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "bms-regression",
            TestContext.CurrentContext.Test.ID,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string writeChart(
        string directory,
        string content,
        string fileName = "chart.bms")
    {
        string path = Path.Combine(directory, fileName);
        File.WriteAllText(path, content, Encoding.ASCII);
        return path;
    }
}
