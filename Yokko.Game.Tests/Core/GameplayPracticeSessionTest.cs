using NUnit.Framework;
using Yokko.Core.Gameplay;
using Yokko.Core.Scoring;
using Yokko.Game.Gameplay;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class GameplayPracticeSessionTest
{
    [Test]
    public void TracksIterationsAndSummarisesAverageAndBest()
    {
        var session = new GameplayPracticeSession(
            new GameplayPracticePlan(1000, 3000, 2));
        session.Record(result(0.90));

        Assert.That(session.HasRemainingIterations, Is.True);

        session.Record(result(0.98));

        Assert.Multiple(() =>
        {
            Assert.That(session.CompletedIterations, Is.EqualTo(2));
            Assert.That(session.HasRemainingIterations, Is.False);
            Assert.That(session.Summary, Does.Contain("94.00%"));
            Assert.That(session.Summary, Does.Contain("98.00%"));
        });
    }

    private static ManiaScoreResult result(double accuracy) => new(
        900_000,
        accuracy,
        100,
        ScoreRank.A,
        1,
        0,
        0,
        0,
        0,
        0);
}
