using NUnit.Framework;
using Yokko.Core.Scoring;
using Yokko.Game.Scoring;
using Yokko.Import.Malody;
using Yokko.Import.Osu;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class ExternalReplayScoreConverterTest
{
    [Test]
    public void ConvertsOsuManiaHeaderStatistics()
    {
        var replay = new OsuReplay(
            20260801,
            "hash",
            "OtherPlayer",
            OsuLegacyMods.Hidden,
            [],
            new OsuReplayScore(
                987_654,
                321,
                90,
                5,
                3,
                1,
                1,
                0));

        ManiaScoreResult result = ExternalReplayScoreConverter.FromOsu(replay);

        Assert.Multiple(() =>
        {
            Assert.That(result.Score, Is.EqualTo(987_654));
            Assert.That(result.MaxCombo, Is.EqualTo(321));
            Assert.That(result.Accuracy,
                Is.EqualTo((300d * 95 + 200d * 3 + 100d + 50d)
                           / (300d * 100)));
            Assert.That(result.Perfect, Is.EqualTo(90));
            Assert.That(result.Great, Is.EqualTo(5));
        });
    }

    [Test]
    public void ConvertsMalodyAccuracyWeights()
    {
        var replay = new MalodyReplay(
            "0123456789abcdef0123456789abcdef",
            "4K",
            "Song",
            "Artist",
            765_432,
            98,
            80,
            10,
            5,
            5,
            2,
            MalodyReplayMods.None,
            2,
            null,
            []);

        ManiaScoreResult result = ExternalReplayScoreConverter.FromMalody(replay);

        Assert.Multiple(() =>
        {
            Assert.That(result.Score, Is.EqualTo(765_432));
            Assert.That(result.Accuracy, Is.EqualTo(0.895));
            Assert.That(result.Rank, Is.EqualTo(ScoreRank.B));
            Assert.That(result.ComboBreaks, Is.EqualTo(2));
        });
    }
}
