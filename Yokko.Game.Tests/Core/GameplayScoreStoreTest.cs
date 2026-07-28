using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Platform;
using Yokko.Core.Beatmaps;
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
