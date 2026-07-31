using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Platform;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Game.Scoring;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public class GameplayScoreStoreTest
{
    private string testRoot;

    [SetUp]
    public void SetUp()
    {
        testRoot = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "score-store",
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
    public void BestScorePersistsAndLowerScoreDoesNotReplaceIt()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var first = new GameplayScoreStore();
        first.Initialise(new NativeStorage(testRoot));

        Assert.That(first.SaveBest(beatmap, result(900_000, 0.95)), Is.True);
        Assert.That(first.SaveBest(beatmap, result(800_000, 0.99)), Is.False);

        var restored = new GameplayScoreStore();
        restored.Initialise(new NativeStorage(testRoot));
        StoredGameplayScore saved = restored.GetBest(beatmap);

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved.Score, Is.EqualTo(900_000));
            Assert.That(saved.Accuracy, Is.EqualTo(0.95));
            Assert.That(saved.Rank, Is.EqualTo(ScoreRank.S));
        });
    }

    [Test]
    public void EveryPlayPersistsToHistoryAcrossModSets()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var first = new GameplayScoreStore();
        first.Initialise(new NativeStorage(testRoot));
        var doubleTime = new ManiaModSet([ManiaModId.DoubleTime]);

        Assert.That(first.SaveBest(beatmap, result(900_000, 0.95)), Is.True);
        Assert.That(first.SaveBest(beatmap, result(700_000, 0.91)), Is.False);
        Assert.That(first.SaveBest(beatmap, doubleTime, result(800_000, 0.93)), Is.True);

        var restored = new GameplayScoreStore();
        restored.Initialise(new NativeStorage(testRoot));
        var history = restored.GetHistory(
            beatmap,
            JudgementConfiguration.YokkoDefault);

        Assert.Multiple(() =>
        {
            Assert.That(history, Has.Count.EqualTo(3));
            Assert.That(history.Select(score => score.Score),
                Is.EqualTo(new long[] { 900_000, 800_000, 700_000 }));
            Assert.That(history[1].Mods, Is.EqualTo(new[] { "DT" }));
        });
    }

    [Test]
    public void DifferentModSetsKeepIndependentBestScores()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var store = new GameplayScoreStore();
        store.Initialise(new NativeStorage(testRoot));
        var doubleTime = new ManiaModSet([ManiaModId.DoubleTime]);
        var nightcore = new ManiaModSet([ManiaModId.Nightcore]);

        Assert.That(
            store.SaveBest(
                beatmap,
                doubleTime,
                result(900_000, 0.95)),
            Is.True);
        Assert.That(
            store.SaveBest(
                beatmap,
                nightcore,
                result(800_000, 0.90)),
            Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(store.GetBest(beatmap), Is.Null);
            Assert.That(
                store.GetBest(beatmap, doubleTime).Score,
                Is.EqualTo(900_000));
            Assert.That(
                store.GetBest(beatmap, nightcore).Score,
                Is.EqualTo(800_000));
            Assert.That(
                store.GetBest(beatmap, doubleTime).Mods,
                Is.EqualTo(new[] { "DT" }));
        });
    }

    [Test]
    public void RandomSeedsKeepIndependentBestScores()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var store = new GameplayScoreStore();
        store.Initialise(new NativeStorage(testRoot));
        var seedOne = new ManiaModSet([ManiaModId.Random], 111);
        var seedTwo = new ManiaModSet([ManiaModId.Random], 222);

        Assert.That(
            store.SaveBest(beatmap, seedOne, result(700_000, 0.8)),
            Is.True);
        Assert.That(
            store.SaveBest(beatmap, seedTwo, result(800_000, 0.9)),
            Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(
                store.GetBest(beatmap, seedOne).Score,
                Is.EqualTo(700_000));
            Assert.That(
                store.GetBest(beatmap, seedTwo).Score,
                Is.EqualTo(800_000));
        });
    }

    [Test]
    public void PerfectConfigurationKeepsIndependentBestScores()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var store = new GameplayScoreStore();
        store.Initialise(new NativeStorage(testRoot));
        ManiaModSet defaultPerfect =
            ManiaModSet.Empty.WithPerfect(false);
        ManiaModSet strictPerfect =
            ManiaModSet.Empty.WithPerfect(true);

        Assert.That(
            store.SaveBest(
                beatmap,
                defaultPerfect,
                result(900_000, 0.95)),
            Is.True);
        Assert.That(
            store.SaveBest(
                beatmap,
                strictPerfect,
                result(800_000, 0.90)),
            Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(
                store.GetBest(beatmap, defaultPerfect).Score,
                Is.EqualTo(900_000));
            Assert.That(
                store.GetBest(beatmap, strictPerfect).Score,
                Is.EqualTo(800_000));
        });
    }

    [Test]
    public void FixedRateConfigurationKeepsIndependentBestScores()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var store = new GameplayScoreStore();
        store.Initialise(new NativeStorage(testRoot));
        ManiaModSet defaultHalfTime =
            ManiaModSet.Empty.With(ManiaModId.HalfTime, true);
        ManiaModSet customHalfTime = ManiaModSet.Empty.WithFixedRate(
            ManiaModId.HalfTime,
            0.80);

        Assert.That(
            store.SaveBest(
                beatmap,
                defaultHalfTime,
                result(900_000, 0.95)),
            Is.True);
        Assert.That(
            store.SaveBest(
                beatmap,
                customHalfTime,
                result(800_000, 0.90)),
            Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(
                store.GetBest(beatmap, defaultHalfTime).Score,
                Is.EqualTo(900_000));
            Assert.That(
                store.GetBest(beatmap, customHalfTime).Score,
                Is.EqualTo(800_000));
        });
    }

    [Test]
    public void JudgementModesAndJusticeLevelsKeepIndependentBestScores()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var store = new GameplayScoreStore();
        store.Initialise(new NativeStorage(testRoot));
        var yokko = JudgementConfiguration.YokkoDefault;
        var etternaJ4 = new JudgementConfiguration(
            JudgementMode.Etterna,
            4);
        var etternaJustice = new JudgementConfiguration(
            JudgementMode.Etterna,
            9);

        Assert.That(
            store.SaveBest(
                beatmap,
                ManiaModSet.Empty,
                yokko,
                result(900_000, 0.95)),
            Is.True);
        Assert.That(
            store.SaveBest(
                beatmap,
                ManiaModSet.Empty,
                etternaJ4,
                result(800_000, 0.90)),
            Is.True);
        Assert.That(
            store.SaveBest(
                beatmap,
                ManiaModSet.Empty,
                etternaJustice,
                result(700_000, 0.85)),
            Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(
                store.GetBest(
                    beatmap,
                    ManiaModSet.Empty,
                    yokko).Score,
                Is.EqualTo(900_000));
            Assert.That(
                store.GetBest(
                    beatmap,
                    ManiaModSet.Empty,
                    etternaJ4).Score,
                Is.EqualTo(800_000));
            Assert.That(
                store.GetBest(
                    beatmap,
                    ManiaModSet.Empty,
                    etternaJustice).Score,
                Is.EqualTo(700_000));
        });
    }

    private static ManiaScoreResult result(long score, double accuracy) => new(
        score,
        accuracy,
        123,
        ScoreRank.S,
        10,
        2,
        1,
        0,
        0,
        0);
}
