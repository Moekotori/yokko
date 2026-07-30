namespace Yokko.Audio;

internal sealed class PlaybackRateTimeline
{
    private readonly List<Segment> segments = [];

    internal PlaybackRateTimeline() => Reset(0, 1);

    internal void Reset(
        double sourceTimeMilliseconds,
        double playbackRate)
    {
        validate(sourceTimeMilliseconds, playbackRate);
        segments.Clear();
        segments.Add(new Segment(
            0,
            sourceTimeMilliseconds,
            playbackRate));
    }

    internal void SetRate(
        double outputTimeMilliseconds,
        double playbackRate)
    {
        validate(outputTimeMilliseconds, playbackRate);

        Segment current = segments[^1];
        outputTimeMilliseconds = Math.Max(
            outputTimeMilliseconds,
            current.OutputTimeMilliseconds);
        double sourceTimeMilliseconds = Map(outputTimeMilliseconds);

        if (outputTimeMilliseconds == current.OutputTimeMilliseconds)
        {
            segments[^1] = current with
            {
                SourceTimeMilliseconds = sourceTimeMilliseconds,
                PlaybackRate = playbackRate,
            };
            return;
        }

        segments.Add(new Segment(
            outputTimeMilliseconds,
            sourceTimeMilliseconds,
            playbackRate));
    }

    internal double Map(double outputTimeMilliseconds)
    {
        if (!double.IsFinite(outputTimeMilliseconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outputTimeMilliseconds));
        }

        int low = 0;
        int high = segments.Count - 1;
        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            if (segments[middle].OutputTimeMilliseconds
                <= outputTimeMilliseconds)
            {
                low = middle;
            }
            else
                high = middle - 1;
        }

        Segment segment = segments[low];
        return segment.SourceTimeMilliseconds
               + (outputTimeMilliseconds
                  - segment.OutputTimeMilliseconds)
               * segment.PlaybackRate;
    }

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
