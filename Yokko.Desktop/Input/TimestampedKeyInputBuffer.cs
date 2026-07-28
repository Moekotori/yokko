using System;
using System.Collections.Generic;
using Yokko.Game.Input;

namespace Yokko.Desktop.Input;

internal sealed class TimestampedKeyInputBuffer
{
    private readonly int capacity;
    private readonly Queue<TimestampedKeyInput> pending = new();

    public TimestampedKeyInputBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        this.capacity = capacity;
    }

    public int Count => pending.Count;

    public long CapturedEdgeCount { get; private set; }

    public long DroppedEdgeCount { get; private set; }

    public void Enqueue(TimestampedKeyInput input)
    {
        if (pending.Count >= capacity)
        {
            pending.Dequeue();
            DroppedEdgeCount++;
        }

        pending.Enqueue(input);
        CapturedEdgeCount++;
    }

    public bool TryDequeue(out TimestampedKeyInput input)
    {
        if (pending.Count > 0)
        {
            input = pending.Dequeue();
            return true;
        }

        input = default;
        return false;
    }

    public void Clear() => pending.Clear();

    public void Reset()
    {
        pending.Clear();
        CapturedEdgeCount = 0;
        DroppedEdgeCount = 0;
    }
}
