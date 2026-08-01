using System.Linq;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Core.Timing;
using Yokko.Game.Gameplay;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class GameplayReplayStateRebuilderTest
{
    [Test]
    public void RewindAndFastForwardRestoreIdenticalModelState()
    {
        YokkoBeatmap chart = beatmap(
            new YokkoHitObject(0, 1000, null, HitObjectKind.Tap),
            new YokkoHitObject(1, 2000, 7000, HitObjectKind.Hold),
            new YokkoHitObject(2, 4000, null, HitObjectKind.Mine),
            new YokkoHitObject(3, 8000, null, HitObjectKind.Tap));
        GameplayReplay replay = GameplayAutoGenerator.Generate(chart);
        JudgementConfiguration configuration =
            JudgementConfiguration.YokkoDefault;
        var windows = new JudgementWindows(
            chart.OverallDifficulty,
            configuration: configuration);

        GameplayReplayRestoredState firstEnd = rebuild(9000);
        GameplayReplayRestoredState rewound = rebuild(5000);
        GameplayReplayRestoredState secondEnd = rebuild(9000);

        Assert.Multiple(() =>
        {
            Assert.That(rewound.PressedLanes[1], Is.True);
            Assert.That(rewound.JudgementState.IsHoldActive(1), Is.True);
            Assert.That(rewound.JudgementState.IsResolved(3), Is.False);
            Assert.That(
                secondEnd.JudgementState.CreateResult(),
                Is.EqualTo(firstEnd.JudgementState.CreateResult()));
            Assert.That(
                secondEnd.HealthState.Health,
                Is.EqualTo(firstEnd.HealthState.Health).Within(0.000001));
            Assert.That(secondEnd.JudgementState.IsComplete, Is.True);
            Assert.That(secondEnd.PressedLanes, Has.All.False);
        });

        GameplayReplayRestoredState rebuild(double target) =>
            GameplayReplayStateRebuilder.Rebuild(
                chart,
                replay,
                ManiaModSet.Empty,
                windows,
                configuration,
                minesEnabled: true,
                target);
    }

    [Test]
    public void SparseReplayExpiresMissBeforeLaterInput()
    {
        YokkoBeatmap chart = beatmap(
            new YokkoHitObject(0, 1000, null, HitObjectKind.Tap),
            new YokkoHitObject(0, 5000, null, HitObjectKind.Tap));
        var replay = new GameplayReplay(
        [
            new GameplayReplayInput(0, true, 5000),
            new GameplayReplayInput(0, false, 5050),
        ]);
        JudgementConfiguration configuration =
            JudgementConfiguration.YokkoDefault;
        var windows = new JudgementWindows(
            chart.OverallDifficulty,
            configuration: configuration);

        GameplayReplayRestoredState restored =
            GameplayReplayStateRebuilder.Rebuild(
                chart,
                replay,
                ManiaModSet.Empty,
                windows,
                configuration,
                minesEnabled: true,
                6000);

        Assert.Multiple(() =>
        {
            Assert.That(restored.JudgementState.Counts.Miss, Is.EqualTo(1));
            Assert.That(restored.JudgementState.Counts.Perfect, Is.EqualTo(1));
            Assert.That(restored.JudgementState.IsComplete, Is.True);
        });
    }

    private static YokkoBeatmap beatmap(
        params YokkoHitObject[] hitObjects) =>
        new(
            "Replay seek test",
            "Yokko",
            "Yokko",
            "4K",
            KeyMode.FourKey,
            ChartSourceFormat.Yokko,
            [YokkoTimingPoint.Default],
            null,
            hitObjects.OrderBy(static hitObject =>
                hitObject.StartTimeMilliseconds).ToArray());
}
