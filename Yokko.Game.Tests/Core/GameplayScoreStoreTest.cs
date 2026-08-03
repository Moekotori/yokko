using System;
using System.Collections.Generic;
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
    public void ReplayPathAndPlayTimePersistWithHistoryEntry()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var first = new GameplayScoreStore();
        first.Initialise(new NativeStorage(testRoot));
        string replayPath = Path.Combine(testRoot, "Replays", "play.ykr");
        DateTimeOffset playedAt = new(2026, 8, 1, 3, 4, 5, TimeSpan.Zero);

        Assert.That(
            first.SaveBest(
                beatmap,
                ManiaModSet.Empty,
                JudgementConfiguration.YokkoDefault,
                result(900_000, 0.95),
                replayPath,
                playedAt,
                "NYAFA",
                "10248631"),
            Is.True);

        var restored = new GameplayScoreStore();
        restored.Initialise(new NativeStorage(testRoot));
        StoredGameplayScore saved = restored.GetHistory(
            beatmap,
            JudgementConfiguration.YokkoDefault).Single();

        Assert.Multiple(() =>
        {
            Assert.That(saved.ReplayPath, Is.EqualTo(replayPath));
            Assert.That(saved.PlayedAt, Is.EqualTo(playedAt));
            Assert.That(saved.PlayerName, Is.EqualTo("NYAFA"));
            Assert.That(saved.PlayerId, Is.EqualTo("10248631"));
            Assert.That(saved.IsCurrentPlayer, Is.True);
            Assert.That(
                saved.JudgementConfiguration,
                Is.EqualTo(JudgementConfiguration.YokkoDefault));
        });
    }

    [Test]
    public void TimingStatisticsPersistWithHistoryEntry()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var first = new GameplayScoreStore();
        first.Initialise(new NativeStorage(testRoot));
        GameplayTimingStatistics timing =
            GameplayTimingStatistics.FromSamples(
            [
                new GameplayTimingSample(
                    0,
                    -12.5,
                    1234,
                    JudgementRating.Great),
                new GameplayTimingSample(0, -4),
                new GameplayTimingSample(1, 0),
                new GameplayTimingSample(1, 6),
                new GameplayTimingSample(1, 18),
            ])!;

        Assert.That(
            first.SaveBest(
                beatmap,
                ManiaModSet.Empty,
                JudgementConfiguration.YokkoDefault,
                result(900_000, 0.95),
                playerId: "10248631",
                timing: timing),
            Is.True);

        var restored = new GameplayScoreStore();
        restored.Initialise(new NativeStorage(testRoot));
        GameplayTimingStatistics saved = restored.GetHistory(
            beatmap,
            JudgementConfiguration.YokkoDefault).Single().Timing;

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved.SampleCount, Is.EqualTo(5));
            Assert.That(saved.EarlyCount, Is.EqualTo(2));
            Assert.That(saved.OnTimeCount, Is.EqualTo(1));
            Assert.That(saved.LateCount, Is.EqualTo(2));
            Assert.That(
                saved.EarlyAverageMilliseconds,
                Is.EqualTo(-8.25).Within(0.0001));
            Assert.That(
                saved.LateAverageMilliseconds,
                Is.EqualTo(12).Within(0.0001));
            Assert.That(saved.MeanMilliseconds, Is.EqualTo(1.5).Within(0.0001));
            Assert.That(saved.UnstableRate, Is.GreaterThan(0));
            Assert.That(saved.Lanes, Has.Count.EqualTo(2));
            Assert.That(saved.Lanes[0].SampleCount, Is.EqualTo(2));
            Assert.That(saved.Lanes[1].SampleCount, Is.EqualTo(3));
            Assert.That(saved.Samples, Has.Count.EqualTo(5));
            Assert.That(saved.Samples[0].TimeMilliseconds, Is.EqualTo(1234));
            Assert.That(saved.Samples[0].Rating, Is.EqualTo(JudgementRating.Great));
        });
    }

    [Test]
    public void ReloadedStoreCanAppendAnotherYokkoScore()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var first = new GameplayScoreStore();
        first.Initialise(new NativeStorage(testRoot));

        Assert.That(
            first.SaveBest(beatmap, result(700_000, 0.90)),
            Is.True);

        var restored = new GameplayScoreStore();
        restored.Initialise(new NativeStorage(testRoot));

        Assert.That(
            restored.SaveBest(beatmap, result(800_000, 0.95)),
            Is.True);
        Assert.That(
            restored.GetHistory(
                beatmap,
                JudgementConfiguration.YokkoDefault),
            Has.Count.EqualTo(2));
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
    public void ConfigurableModDetailsPersistForHistoryDisplay()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var first = new GameplayScoreStore();
        first.Initialise(new NativeStorage(testRoot));
        ManiaModSet configured = ManiaModSet.Empty
            .WithRandomSeed(123456)
            .WithFixedRate(ManiaModId.DoubleTime, 1.73, true);

        Assert.That(
            first.SaveBest(beatmap, configured, result(800_000, 0.90)),
            Is.True);

        var restored = new GameplayScoreStore();
        restored.Initialise(new NativeStorage(testRoot));
        StoredGameplayScore saved = restored.GetBest(beatmap, configured);

        Assert.Multiple(() =>
        {
            Assert.That(saved.ModConfiguration, Is.Not.Null);
            Assert.That(
                ManiaModConfigurationCodec.Restore(saved.ModConfiguration),
                Is.EqualTo(configured));
            Assert.That(saved.ModLabels, Does.Contain("RD #123456"));
            Assert.That(saved.ModLabels, Does.Contain("DT 1.73× PITCH"));
        });
    }

    [Test]
    public void CinemaDoesNotPersistScoresOrHistory()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var store = new GameplayScoreStore();
        store.Initialise(new NativeStorage(testRoot));
        ManiaModSet automation = ManiaModSet.Empty.With(
            ManiaModId.Cinema,
            true);

        Assert.That(
            store.SaveBest(beatmap, automation, result(1_000_000, 1)),
            Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(store.GetBest(beatmap, automation), Is.Null);
            Assert.That(
                store.GetHistory(
                    beatmap,
                    JudgementConfiguration.YokkoDefault),
                Is.Empty);
        });
    }

    [Test]
    public void DeveloperAutoplayPersistsScoreHistoryAndReplayPath()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var store = new GameplayScoreStore();
        store.Initialise(new NativeStorage(testRoot));
        ManiaModSet developerAutoplay = ManiaModSet.Empty.With(
            ManiaModId.Autoplay,
            true);
        string replayPath = Path.Combine(
            testRoot,
            "Replays",
            "developer-autoplay.ykr");

        Assert.That(
            store.SaveBest(
                beatmap,
                developerAutoplay,
                JudgementConfiguration.YokkoDefault,
                result(1_000_000, 1),
                replayPath),
            Is.True);

        var restored = new GameplayScoreStore();
        restored.Initialise(new NativeStorage(testRoot));
        StoredGameplayScore saved = restored.GetHistory(
            beatmap,
            JudgementConfiguration.YokkoDefault).Single();

        Assert.Multiple(() =>
        {
            Assert.That(saved.Mods, Is.EqualTo(new[] { "AD" }));
            Assert.That(saved.ReplayPath, Is.EqualTo(replayPath));
            Assert.That(
                restored.GetBest(beatmap, developerAutoplay),
                Is.Not.Null);
        });
    }

    [TestCase("AT")]
    [TestCase("CN")]
    public void LegacyAutomationScoresAreIgnoredWhenLoading(
        string legacyAcronym)
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var first = new GameplayScoreStore();
        first.Initialise(new NativeStorage(testRoot));
        ManiaModSet doubleTime =
            ManiaModSet.Empty.With(ManiaModId.DoubleTime, true);
        Assert.That(
            first.SaveBest(beatmap, doubleTime, result(1_000_000, 1)),
            Is.True);

        foreach (string relativePath in new[]
                 {
                     "Scores/scores.json",
                     "Scores/history.json",
                 })
        {
            string path = Path.Combine(testRoot, relativePath);
            string json = File.ReadAllText(path)
                              .Replace(
                                  "\"DT\"",
                                  $"\"{legacyAcronym}\"");
            File.WriteAllText(path, json);
        }

        var restored = new GameplayScoreStore();
        restored.Initialise(new NativeStorage(testRoot));

        Assert.Multiple(() =>
        {
            Assert.That(restored.GetBest(beatmap, doubleTime), Is.Null);
            Assert.That(
                restored.GetHistory(
                    beatmap,
                    JudgementConfiguration.YokkoDefault),
                Is.Empty);
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

    [Test]
    public void EtternaComboStatisticsPersist()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var first = new GameplayScoreStore();
        first.Initialise(new NativeStorage(testRoot));

        Assert.That(
            first.SaveBest(
                beatmap,
                ManiaModSet.Empty,
                JudgementConfiguration.EtternaDefault,
                result(
                    800_000,
                    0.90,
                    comboBreaks: 3,
                    maxMissCombo: 2)),
            Is.True);

        var restored = new GameplayScoreStore();
        restored.Initialise(new NativeStorage(testRoot));
        StoredGameplayScore saved = restored.GetBest(
            beatmap,
            ManiaModSet.Empty,
            JudgementConfiguration.EtternaDefault);

        Assert.Multiple(() =>
        {
            Assert.That(saved.ComboBreaks, Is.EqualTo(3));
            Assert.That(saved.MaxMissCombo, Is.EqualTo(2));
        });
    }

    [Test]
    public void ExternalReplayAppearsInRankingWithItsPlayerButNotPersonalHistory()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var first = new GameplayScoreStore();
        first.Initialise(new NativeStorage(testRoot));
        Assert.That(first.SaveBest(beatmap, result(700_000, 0.90)), Is.True);

        Assert.That(
            first.ImportExternalScore(
                beatmap,
                ManiaModSet.Empty,
                JudgementConfiguration.YokkoDefault,
                result(900_000, 0.98),
                "OtherPlayer",
                "OtherPlayer",
                "osu",
                "osu:replay-one",
                Path.Combine(testRoot, "Replays", "external.ykr"),
                new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)),
            Is.True);
        Assert.That(
            first.ImportExternalScore(
                beatmap,
                ManiaModSet.Empty,
                JudgementConfiguration.YokkoDefault,
                result(900_000, 0.98),
                "OtherPlayer",
                "OtherPlayer",
                "osu",
                "osu:replay-one",
                null),
            Is.False);

        var restored = new GameplayScoreStore();
        restored.Initialise(new NativeStorage(testRoot));
        IReadOnlyList<StoredGameplayScore> ranking = restored.GetRanking(
            beatmap,
            JudgementConfiguration.YokkoDefault);
        IReadOnlyList<StoredGameplayScore> personal = restored.GetHistory(
            beatmap,
            JudgementConfiguration.YokkoDefault);
        IReadOnlyList<StoredGameplayScore> etternaRanking = restored.GetRanking(
            beatmap,
            JudgementConfiguration.EtternaDefault);

        Assert.Multiple(() =>
        {
            Assert.That(ranking, Has.Count.EqualTo(2));
            Assert.That(ranking[0].PlayerName, Is.EqualTo("OtherPlayer"));
            Assert.That(ranking[0].PlayerId, Is.EqualTo("OtherPlayer"));
            Assert.That(ranking[0].Source, Is.EqualTo("osu"));
            Assert.That(ranking[0].IsCurrentPlayer, Is.False);
            Assert.That(personal, Has.Count.EqualTo(1));
            Assert.That(personal[0].Score, Is.EqualTo(700_000));
            Assert.That(etternaRanking, Has.Count.EqualTo(1));
            Assert.That(etternaRanking[0].PlayerName, Is.EqualTo("OtherPlayer"));
            Assert.That(restored.GetBest(beatmap).Score, Is.EqualTo(700_000));
        });
    }

    private static ManiaScoreResult result(
        long score,
        double accuracy,
        int comboBreaks = 0,
        int maxMissCombo = 0) => new(
        score,
        accuracy,
        123,
        ScoreRank.S,
        10,
        2,
        1,
        0,
        0,
        0,
        comboBreaks,
        maxMissCombo);
}
