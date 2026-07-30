using System.Threading;

namespace Yokko.Audio;

internal sealed class PlaybackRateTimeline
{
    private const int initialCapacity = 16;
    private readonly object writeLock = new();
    private Segment[] segments = new Segment[initialCapacity];
    private int segmentCount;
    private int version;

    internal PlaybackRateTimeline() => Reset(0, 1);

    internal void Reset(
        double sourceTimeMilliseconds,
        double playbackRate)
    {
        validate(sourceTimeMilliseconds, playbackRate);
        lock (writeLock)
        {
            beginWrite();
            try
            {
                segments[0] = new Segment(
                    0,
                    sourceTimeMilliseconds,
                    playbackRate);
                segmentCount = 1;
            }
            finally
            {
                endWrite();
            }
        }
    }

    internal void SetRate(
        double outputTimeMilliseconds,
        double playbackRate)
    {
        validate(outputTimeMilliseconds, playbackRate);
        lock (writeLock)
        {
            beginWrite();
            try
            {
                Segment current = segments[segmentCount - 1];
                outputTimeMilliseconds = Math.Max(
                    outputTimeMilliseconds,
                    current.OutputTimeMilliseconds);
                double sourceTimeMilliseconds = mapUnsafe(
                    segments,
                    segmentCount,
                    outputTimeMilliseconds);

                var next = new Segment(
                    outputTimeMilliseconds,
                    sourceTimeMilliseconds,
                    playbackRate);
                if (outputTimeMilliseconds == current.OutputTimeMilliseconds)
                {
                    segments[segmentCount - 1] = next;
                    return;
                }

                if (segmentCount == segments.Length)
                    Array.Resize(ref segments, segments.Length * 2);
                segments[segmentCount++] = next;
            }
            finally
            {
                endWrite();
            }
        }
    }

    internal double Map(double outputTimeMilliseconds)
    {
        if (!double.IsFinite(outputTimeMilliseconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outputTimeMilliseconds));
        }

        var spinner = new SpinWait();
        while (true)
        {
            int before = Volatile.Read(ref version);
            if ((before & 1) != 0)
            {
                spinner.SpinOnce();
                continue;
            }

            int count = Volatile.Read(ref segmentCount);
            Segment[] currentSegments = Volatile.Read(ref segments);
            double mapped = mapUnsafe(
                currentSegments,
                count,
                outputTimeMilliseconds);
            if (before == Volatile.Read(ref version))
                return mapped;

            spinner.SpinOnce();
        }
    }

    internal double PlaybackRate
    {
        get
        {
            var spinner = new SpinWait();
            while (true)
            {
                int before = Volatile.Read(ref version);
                if ((before & 1) != 0)
                {
                    spinner.SpinOnce();
                    continue;
                }

                int count = Volatile.Read(ref segmentCount);
                Segment[] currentSegments = Volatile.Read(ref segments);
                double rate = currentSegments[count - 1].PlaybackRate;
                if (before == Volatile.Read(ref version))
                    return rate;

                spinner.SpinOnce();
            }
        }
    }

    private static double mapUnsafe(
        Segment[] currentSegments,
        int count,
        double outputTimeMilliseconds)
    {
        int low = 0;
        int high = count - 1;
        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            if (currentSegments[middle].OutputTimeMilliseconds
                <= outputTimeMilliseconds)
                low = middle;
            else
                high = middle - 1;
        }

        Segment segment = currentSegments[low];
        return segment.SourceTimeMilliseconds
               + (outputTimeMilliseconds
                  - segment.OutputTimeMilliseconds)
               * segment.PlaybackRate;
    }

    private void beginWrite() => Interlocked.Increment(ref version);

    private void endWrite() => Interlocked.Increment(ref version);

    private static void validate(
        double timeMilliseconds,
        double playbackRate)
    {
        if (!double.IsFinite(timeMilliseconds))
            throw new ArgumentOutOfRangeException(nameof(timeMilliseconds));
        if (!double.IsFinite(playbackRate) || playbackRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(playbackRate));
    }

    private readonly record struct Segment(
        double OutputTimeMilliseconds,
        double SourceTimeMilliseconds,
        double PlaybackRate);
}
