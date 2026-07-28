using NUnit.Framework;
using osuTK.Input;
using Yokko.Game.Gameplay;
using Yokko.Game.Input;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class GameplayInputClockTest
{
    [Test]
    public void EventAgeIsRemovedFromObservedGameplayTime()
    {
        double eventTime = GameplayInputClock.AtEventTimestamp(
            1200,
            10_000_000,
            10_025_000,
            10_000_000);

        Assert.That(eventTime, Is.EqualTo(1197.5).Within(0.0001));
    }

    [Test]
    public void EventBeforeSongStartIsClamped()
    {
        double eventTime = GameplayInputClock.AtEventTimestamp(
            1,
            10_000_000,
            10_100_000,
            10_000_000);

        Assert.That(eventTime, Is.Zero);
    }

    [Test]
    public void MissingOrFutureTimestampUsesObservedTime()
    {
        Assert.That(
            GameplayInputClock.AtEventTimestamp(250, 0, 10, 1000),
            Is.EqualTo(250));
        Assert.That(
            GameplayInputClock.AtEventTimestamp(250, 11, 10, 1000),
            Is.EqualTo(250));
    }

    [Test]
    public void TimestampSourceCapturesEachPhysicalEdgeOnce()
    {
        using var source = new KeyInputTimestampSource();
        source.BeginCapture();

        source.Record(Key.D, true, 100);
        source.Record(Key.D, true, 110);
        source.Record(Key.D, false, 120);

        Assert.That(source.TryTake(Key.D, true, out long pressed), Is.True);
        Assert.That(pressed, Is.EqualTo(100));
        Assert.That(source.TryTake(Key.D, true, out _), Is.False);
        Assert.That(source.TryTake(Key.D, false, out long released), Is.True);
        Assert.That(released, Is.EqualTo(120));
    }

    [Test]
    public void TimestampSourceDoesNotLeakEdgesAcrossGameplaySessions()
    {
        using var source = new KeyInputTimestampSource();
        source.BeginCapture();
        source.Record(Key.F, true, 100);

        source.EndCapture();
        source.BeginCapture();

        Assert.That(source.TryTake(Key.F, true, out _), Is.False);
    }
}
