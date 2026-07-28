using Yokko.Audio.Decoding;

namespace Yokko.Audio;

public static class AudioWaveformAnalyzer
{
    public static Task<AudioWaveformAnalysis> AnalyzeAsync(
        string audioPath,
        int pointCount,
        CancellationToken cancellationToken = default)
    {
        if (pointCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(pointCount));

        return Task.Run(
            () => analyze(audioPath, pointCount, cancellationToken),
            cancellationToken);
    }

    private static AudioWaveformAnalysis analyze(
        string audioPath,
        int pointCount,
        CancellationToken cancellationToken)
    {
        using DecodedAudioSource source = DecodedAudioSource.Open(audioPath);
        long totalFrames = Math.Max(
            1,
            (long)Math.Ceiling(
                source.TotalTime.TotalSeconds * source.SampleRate));
        long framesPerPoint = Math.Max(
            1,
            (long)Math.Ceiling(totalFrames / (double)pointCount));

        var peaks = new float[pointCount];
        var lows = new float[pointCount];
        var mids = new float[pointCount];
        var highs = new float[pointCount];
        var samples = new float[8192];
        double lowState = 0;
        double midState = 0;
        double lowAlpha = onePoleAlpha(220, source.SampleRate);
        double midAlpha = onePoleAlpha(2200, source.SampleRate);
        long frameIndex = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int sampleCount = source.Read(samples);
            if (sampleCount == 0)
                break;

            int frames = sampleCount / 2;
            for (int frame = 0; frame < frames; frame++, frameIndex++)
            {
                float left = samples[frame * 2];
                float right = samples[frame * 2 + 1];
                double mono = (left + right) * 0.5;
                lowState += lowAlpha * (mono - lowState);
                midState += midAlpha * (mono - midState);

                int point = (int)Math.Min(
                    pointCount - 1,
                    frameIndex / framesPerPoint);
                peaks[point] = Math.Max(
                    peaks[point],
                    Math.Max(Math.Abs(left), Math.Abs(right)));
                lows[point] = Math.Max(lows[point], (float)Math.Abs(lowState));
                mids[point] = Math.Max(
                    mids[point],
                    (float)Math.Abs(midState - lowState));
                highs[point] = Math.Max(
                    highs[point],
                    (float)Math.Abs(mono - midState));
            }
        }

        normalise(peaks);
        normalise(lows);
        normalise(mids);
        normalise(highs);
        return new AudioWaveformAnalysis(
            source.TotalTime.TotalMilliseconds,
            peaks,
            lows,
            mids,
            highs);
    }

    private static double onePoleAlpha(double cutoff, int sampleRate)
        => 1 - Math.Exp(-2 * Math.PI * cutoff / sampleRate);

    private static void normalise(float[] values)
    {
        float maximum = Math.Max(0.0001f, values.Max());
        for (int index = 0; index < values.Length; index++)
            values[index] = Math.Clamp(values[index] / maximum, 0, 1);
    }
}
