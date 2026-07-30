using System;
using System.Threading;
using Yokko.Game.Input;

namespace Yokko.Desktop.Input;

internal sealed class TimestampedKeyInputBuffer
{
    private readonly int capacity;
    private readonly Cell[] cells;
    private long enqueuePosition;
    private long dequeuePosition;
    private long capturedEdgeCount;
    private long droppedEdgeCount;

    public TimestampedKeyInputBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        this.capacity = capacity;
        cells = new Cell[capacity];
        for (int index = 0; index < cells.Length; index++)
            cells[index].Sequence = index;
    }

    public int Count
    {
        get
        {
            long count = Volatile.Read(ref enqueuePosition)
                         - Volatile.Read(ref dequeuePosition);
            return (int)Math.Clamp(count, 0, capacity);
        }
    }

    public long CapturedEdgeCount => Interlocked.Read(ref capturedEdgeCount);

    public long DroppedEdgeCount => Interlocked.Read(ref droppedEdgeCount);

    public void Enqueue(TimestampedKeyInput input)
    {
        var spinner = new SpinWait();
        while (true)
        {
            long enqueue = Volatile.Read(ref enqueuePosition);
            long dequeue = Volatile.Read(ref dequeuePosition);
            if (enqueue - dequeue >= capacity)
            {
                if (TryDequeue(out _))
                    Interlocked.Increment(ref droppedEdgeCount);
                else
                    spinner.SpinOnce();
                continue;
            }

            if (tryEnqueue(input))
                break;

            if (enqueue - Volatile.Read(ref dequeuePosition) >= capacity
                && TryDequeue(out _))
                Interlocked.Increment(ref droppedEdgeCount);
            else
                spinner.SpinOnce();
        }

        Interlocked.Increment(ref capturedEdgeCount);
    }

    public bool TryDequeue(out TimestampedKeyInput input)
    {
        var spinner = new SpinWait();
        while (true)
        {
            long position = Volatile.Read(ref dequeuePosition);
            ref Cell cell = ref cells[position % capacity];
            long sequence = Volatile.Read(ref cell.Sequence);
            long difference = sequence - (position + 1);

            if (difference == 0)
            {
                if (Interlocked.CompareExchange(
                        ref dequeuePosition,
                        position + 1,
                        position) != position)
                {
                    spinner.SpinOnce();
                    continue;
                }

                input = cell.Input;
                Volatile.Write(ref cell.Sequence, position + capacity);
                return true;
            }

            if (difference < 0)
            {
                input = default;
                return false;
            }

            spinner.SpinOnce();
        }
    }

    public void Clear()
    {
        while (TryDequeue(out _))
        {
        }
    }

    public void Reset()
    {
        Clear();
        Interlocked.Exchange(ref capturedEdgeCount, 0);
        Interlocked.Exchange(ref droppedEdgeCount, 0);
    }

    private bool tryEnqueue(TimestampedKeyInput input)
    {
        long position = Volatile.Read(ref enqueuePosition);
        ref Cell cell = ref cells[position % capacity];
        long sequence = Volatile.Read(ref cell.Sequence);
        if (sequence - position != 0)
            return false;

        cell.Input = input;
        Volatile.Write(ref cell.Sequence, position + 1);
        Volatile.Write(ref enqueuePosition, position + 1);
        return true;
    }

    private struct Cell
    {
        internal long Sequence;
        internal TimestampedKeyInput Input;
    }
}
