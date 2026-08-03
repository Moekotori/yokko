using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Core.Timing;
using Yokko.Game.Gameplay;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class GameplayReplayRejudgeTest
{
    [Test]
    public void OffsetPreviewRejudgesWithoutMutatingReplay()
    {
        var beatmap = new YokkoBeatmap(
            "Offset preview",
            "Yokko",
            "Tests",
            "4K",
            KeyMode.FourKey,
            ChartSourceFormat.Yokko,
            [YokkoTimingPoint.Default],
            null,
            [new YokkoHitObject(0, 1000, null, HitObjectKind.Tap)]);
        var replay = new GameplayReplay([
            new GameplayReplayInput(0, true, 1060),
            new GameplayReplayInput(0, false, 1080),
        ]);
        JudgementConfiguration configuration =
            JudgementConfiguration.YokkoDefault;
        var windows = new JudgementWindows(
            beatmap.OverallDifficulty,
            configuration: configuration);

        ManiaScoreResult original = GameplayReplayRejudge.Preview(
            beatmap,
            replay,
            ManiaModSet.Empty,
            windows,
            configuration,
            minesEnabled: true,
            completionTimeMilliseconds: 2000,
            offsetMilliseconds: 0);
        ManiaScoreResult corrected = GameplayReplayRejudge.Preview(
            beatmap,
            replay,
            ManiaModSet.Empty,
            windows,
            configuration,
            minesEnabled: true,
            completionTimeMilliseconds: 2000,
            offsetMilliseconds: -60);

        Assert.Multiple(() =>
        {
            Assert.That(corrected.Accuracy, Is.GreaterThan(original.Accuracy));
            Assert.That(corrected.Perfect, Is.EqualTo(1));
            Assert.That(replay.Inputs[0].TimeMilliseconds, Is.EqualTo(1060));
        });
    }
}
