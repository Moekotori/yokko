using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    [Test]
    public async Task ConcurrentProducerAndConsumerPreserveOrder()
    {
        const int edgeCount = 50_000;
        var buffer = new TimestampedKeyInputBuffer(edgeCount);
        var consumed = new List<long>(edgeCount);

        Task producer = Task.Run(() =>
        {
            for (int index = 1; index <= edgeCount; index++)
            {
                buffer.Enqueue(new TimestampedKeyInput(
                    Key.D,
                    (index & 1) == 0,
                    index));
            }
        });

        var spinner = new SpinWait();
        while (consumed.Count < edgeCount)
        {
            if (buffer.TryDequeue(out TimestampedKeyInput input))
            {
                consumed.Add(input.Timestamp);
                continue;
            }

            if (producer.IsCompleted && buffer.Count == 0)
                break;
            spinner.SpinOnce();
        }
        await producer;

        Assert.Multiple(() =>
        {
            Assert.That(buffer.DroppedEdgeCount, Is.Zero);
            Assert.That(consumed, Has.Count.EqualTo(edgeCount));
            Assert.That(
                consumed,
                Is.EqualTo(Enumerable.Range(1, edgeCount)
                    .Select(static value => (long)value)));
        });
    }

    [Test]
    public void EnqueueAndDequeueDoNotAllocateAfterConstruction()
    {
        var buffer = new TimestampedKeyInputBuffer(8);
        for (int index = 0; index < 1000; index++)
        {
            buffer.Enqueue(new TimestampedKeyInput(Key.D, true, index + 1));
            buffer.TryDequeue(out _);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 10_000; index++)
        {
            buffer.Enqueue(new TimestampedKeyInput(Key.D, true, index + 1));
            buffer.TryDequeue(out _);
        }

        Assert.That(
            GC.GetAllocatedBytesForCurrentThread() - before,
            Is.Zero);
    }

    [Test]
    public void PowerOfTwoBufferPreservesEightKilohertzEquivalentStream()
    {
        const int edgeCount = 8_000 * 8;
        var buffer = new TimestampedKeyInputBuffer(1024);

        for (int edge = 1; edge <= edgeCount; edge++)
        {
            buffer.Enqueue(new TimestampedKeyInput(
                Key.D,
                (edge & 1) != 0,
                edge));
            if (!buffer.TryDequeue(out TimestampedKeyInput input)
                || input.Timestamp != edge)
            {
                Assert.Fail($"Input order diverged at edge {edge}.");
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(buffer.CapturedEdgeCount, Is.EqualTo(edgeCount));
            Assert.That(buffer.DroppedEdgeCount, Is.Zero);
            Assert.That(buffer.Count, Is.Zero);
        });
    }

    [Test]
    public void RawKeyStateSuppressesDuplicateEdgesWithoutHashing()
    {
        var state = new WindowsRawKeyboardTimestampBackend.RawKeyState();
        int regular = WindowsRawKeyboardTimestampBackend.RawKeyState
            .IdentityIndex(30, 0, 0x41);
        int extended = WindowsRawKeyboardTimestampBackend.RawKeyState
            .IdentityIndex(30, 0x0002, 0x41);

        Assert.Multiple(() =>
        {
            Assert.That(state.Set(regular, true), Is.True);
            Assert.That(state.Set(regular, true), Is.False);
            Assert.That(state.Set(extended, true), Is.True);
            Assert.That(state.Set(regular, false), Is.True);
            Assert.That(state.Set(regular, false), Is.False);
        });

        state.Clear();
        Assert.That(state.Set(extended, true), Is.True);
    }

    [Test]
    public void RawKeyStatePreservesRapidTriggerEdges()
    {
        const int cycles = 80_000;
        var state = new WindowsRawKeyboardTimestampBackend.RawKeyState();
        int identity = WindowsRawKeyboardTimestampBackend.RawKeyState
            .IdentityIndex(32, 0, 0x44);
        int acceptedEdges = 0;

        for (int cycle = 0; cycle < cycles; cycle++)
        {
            if (state.Set(identity, true))
                acceptedEdges++;
            if (state.Set(identity, false))
                acceptedEdges++;
        }

        Assert.That(acceptedEdges, Is.EqualTo(cycles * 2));
    }
}
