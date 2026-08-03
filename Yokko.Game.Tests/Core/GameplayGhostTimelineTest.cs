using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Core.Timing;
using Yokko.Game.Gameplay;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class GameplayGhostTimelineTest
{
    [Test]
    public void TimelineMatchesReplayRebuilderAndSupportsRewindQueries()
    {
        var chart = new YokkoBeatmap(
            "PB ghost test",
            "Yokko",
            "Yokko",
            "4K",
            KeyMode.FourKey,
            ChartSourceFormat.Yokko,
            [YokkoTimingPoint.Default],
            null,
            [
                new YokkoHitObject(0, 1000, null, HitObjectKind.Tap),
                new YokkoHitObject(1, 3000, null, HitObjectKind.Tap),
            ]);
        var replay = new GameplayReplay(
        [
            new GameplayReplayInput(0, true, 1000),
            new GameplayReplayInput(0, false, 1020),
        ]);
        JudgementConfiguration configuration =
            JudgementConfiguration.YokkoDefault;
        var windows = new JudgementWindows(
            chart.OverallDifficulty,
            configuration: configuration);

        GameplayGhostTimeline ghost = GameplayGhostTimeline.Build(
            chart,
            replay,
            ManiaModSet.Empty,
            windows,
            configuration,
            minesEnabled: true);
        GameplayReplayRestoredState rebuilt =
            GameplayReplayStateRebuilder.Rebuild(
                chart,
                replay,
                ManiaModSet.Empty,
                windows,
                configuration,
                minesEnabled: true,
                5000);

        int cachedIndex = -1;
        Assert.That(ghost.TryQuery(5000, ref cachedIndex, out var final), Is.True);
        Assert.That(ghost.TryQuery(1100, ref cachedIndex, out var rewound), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(final.Score, Is.EqualTo(rebuilt.JudgementState.Score));
            Assert.That(
                final.Accuracy,
                Is.EqualTo(rebuilt.JudgementState.Accuracy).Within(0.000000001));
            Assert.That(final.Combo, Is.EqualTo(rebuilt.JudgementState.Combo));
            Assert.That(
                final.MissCount,
                Is.EqualTo(rebuilt.JudgementState.Counts.Miss));
            Assert.That(rewound.MissCount, Is.Zero);
            Assert.That(rewound.Combo, Is.EqualTo(1));
            Assert.That(cachedIndex, Is.GreaterThanOrEqualTo(0));
        });
    }

    [Test]
    public void LongSilentSpanUsesEventDrivenSimulation()
    {
        const double lateObjectTime = 10 * 60 * 1000;
        var chart = new YokkoBeatmap(
            "Event driven ghost test",
            "Yokko",
            "Yokko",
            "4K",
            KeyMode.FourKey,
            ChartSourceFormat.Yokko,
            [YokkoTimingPoint.Default],
            null,
            [
                new YokkoHitObject(
                    0,
                    1000,
                    null,
                    HitObjectKind.Mine),
                new YokkoHitObject(
                    1,
                    lateObjectTime,
                    null,
                    HitObjectKind.Tap),
            ]);
        var replay = new GameplayReplay(
        [
            new GameplayReplayInput(0, true, 0),
            new GameplayReplayInput(0, false, 2000),
        ]);
        JudgementConfiguration configuration =
            JudgementConfiguration.YokkoDefault;
        var windows = new JudgementWindows(
            chart.OverallDifficulty,
            configuration: configuration);

        GameplayGhostTimeline ghost = GameplayGhostTimeline.Build(
            chart,
            replay,
            ManiaModSet.Empty,
            windows,
            configuration,
            minesEnabled: true);
        GameplayReplayRestoredState rebuilt =
            GameplayReplayStateRebuilder.Rebuild(
                chart,
                replay,
                ManiaModSet.Empty,
                windows,
                configuration,
                minesEnabled: true,
                lateObjectTime + 3000);

        int cachedIndex = -1;
        Assert.That(
            ghost.TryQuery(
                lateObjectTime + 3000,
                ref cachedIndex,
                out GameplayGhostSnapshot final),
            Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(
                final.Score,
                Is.EqualTo(rebuilt.JudgementState.Score));
            Assert.That(
                final.MissCount,
                Is.EqualTo(rebuilt.JudgementState.Counts.Miss));
            Assert.That(
                ghost.SimulationStepCount,
                Is.LessThan(16),
                "A ten-minute silent span must not be sampled every 25ms.");
        });
    }
}
