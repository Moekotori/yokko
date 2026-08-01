using NUnit.Framework;
using Yokko.Game.Gameplay;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class GameplayReplayTimelineTest
{
    [Test]
    public void ReplayBuildsStableFullStateFrames()
    {
        var replay = new GameplayReplay(
        [
            new GameplayReplayInput(0, true, 100),
            new GameplayReplayInput(1, true, 100),
            new GameplayReplayInput(0, false, 150),
            new GameplayReplayInput(1, false, 200),
            new GameplayReplayInput(1, false, 210),
        ]);

        Assert.That(replay.Frames, Is.EqualTo(new[]
        {
            new GameplayReplayFrame(100, 0b0001),
            new GameplayReplayFrame(100, 0b0011),
            new GameplayReplayFrame(150, 0b0010),
            new GameplayReplayFrame(200, 0b0000),
        }));
    }

    [Test]
    public void TimelinePreservesSameTimeOrderAndSupportsRewind()
    {
        var timeline = new GameplayReplayTimeline(
        [
            new GameplayReplayFrame(100, 0b0001),
            new GameplayReplayFrame(100, 0b0011),
            new GameplayReplayFrame(150, 0b0010),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(timeline.MoveNext(100, out var first), Is.True);
            Assert.That(first.PressedLanes, Is.EqualTo(0b0001));
            Assert.That(timeline.MoveNext(100, out var second), Is.True);
            Assert.That(second.PressedLanes, Is.EqualTo(0b0011));
            Assert.That(timeline.MoveNext(100, out _), Is.False);
            Assert.That(timeline.Seek(149), Is.EqualTo(0b0011));
            Assert.That(timeline.CurrentFrameIndex, Is.EqualTo(1));
            Assert.That(timeline.Seek(99), Is.Zero);
            Assert.That(timeline.CurrentFrame, Is.Null);
        });
    }
}
