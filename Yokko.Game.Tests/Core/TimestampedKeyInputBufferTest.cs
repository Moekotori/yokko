using NUnit.Framework;
using osuTK.Input;
using Yokko.Desktop.Input;
using Yokko.Game.Input;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class TimestampedKeyInputBufferTest
{
    [Test]
    public void OverflowRetainsNewestEdgesAndReportsLoss()
    {
        var buffer = new TimestampedKeyInputBuffer(2);

        buffer.Enqueue(new TimestampedKeyInput(Key.D, true, 10));
        buffer.Enqueue(new TimestampedKeyInput(Key.D, false, 20));
        buffer.Enqueue(new TimestampedKeyInput(Key.F, true, 30));

        Assert.That(buffer.CapturedEdgeCount, Is.EqualTo(3));
        Assert.That(buffer.DroppedEdgeCount, Is.EqualTo(1));
        Assert.That(buffer.Count, Is.EqualTo(2));
        Assert.That(buffer.TryDequeue(out TimestampedKeyInput first), Is.True);
        Assert.That(first.Timestamp, Is.EqualTo(20));
        Assert.That(buffer.TryDequeue(out TimestampedKeyInput second), Is.True);
        Assert.That(second.Timestamp, Is.EqualTo(30));
    }

    [Test]
    public void ResetStartsNewSessionMetrics()
    {
        var buffer = new TimestampedKeyInputBuffer(1);
        buffer.Enqueue(new TimestampedKeyInput(Key.D, true, 10));
        buffer.Enqueue(new TimestampedKeyInput(Key.F, true, 20));

        buffer.Reset();

        Assert.That(buffer.Count, Is.Zero);
        Assert.That(buffer.CapturedEdgeCount, Is.Zero);
        Assert.That(buffer.DroppedEdgeCount, Is.Zero);
    }
}
