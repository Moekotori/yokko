using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Yokko.Audio.Decoding;

internal sealed class DecodedAudioSample
{
    private const int channels = 2;
    private const int readBlockSamples = 8192;
    private const int maximumDurationSeconds = 60;

    private readonly Dictionary<int, float[]> resampled = new();

    private DecodedAudioSample(int sampleRate, float[] samples)
    {
        SampleRate = sampleRate;
        Samples = samples;
        resampled[sampleRate] = samples;
    }

    internal int SampleRate { get; }

    internal float[] Samples { get; }

    internal static DecodedAudioSample Decode(string path)
    {
        using DecodedAudioSource source = DecodedAudioSource.Open(path);
        int maximumSamples =
            checked(source.SampleRate * channels * maximumDurationSeconds);
        var decoded = new List<float>(Math.Min(
            maximumSamples,
            Math.Max(readBlockSamples, (int)Math.Min(
                int.MaxValue,
                source.TotalTime.TotalSeconds
                * source.SampleRate
                * channels))));
        var buffer = new float[readBlockSamples];

        while (true)
        {
            int read = source.Read(buffer);
            if (read == 0)
                break;
            if (decoded.Count > maximumSamples - read)
            {
                throw new InvalidDataException(
                    $"Keysound '{path}' exceeds the {maximumDurationSeconds} second safety limit.");
            }

            for (int index = 0; index < read; index++)
                decoded.Add(buffer[index]);
        }

        if (decoded.Count == 0)
            throw new InvalidDataException($"Keysound '{path}' contains no audio.");

        return new DecodedAudioSample(source.SampleRate, decoded.ToArray());
    }

    internal float[] GetSamplesAt(int targetSampleRate)
    {
        if (resampled.TryGetValue(targetSampleRate, out float[]? cached))
            return cached;

        var provider = new ArraySampleProvider(Samples, SampleRate);
        var resampler = new WdlResamplingSampleProvider(
            provider,
            targetSampleRate);
        int estimatedSamples = checked(
            (int)Math.Ceiling(
                Samples.Length * (double)targetSampleRate / SampleRate));
        var output = new List<float>(estimatedSamples + readBlockSamples);
        var buffer = new float[readBlockSamples];

        while (true)
        {
            int read = resampler.Read(buffer, 0, buffer.Length);
            if (read == 0)
                break;

            for (int index = 0; index < read; index++)
                output.Add(buffer[index]);
        }

        float[] result = output.ToArray();
        resampled[targetSampleRate] = result;
        return result;
    }

    private sealed class ArraySampleProvider : ISampleProvider
    {
        private readonly float[] samples;
        private int position;

        internal ArraySampleProvider(float[] samples, int sampleRate)
        {
            this.samples = samples;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
                sampleRate,
                channels);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int available = Math.Min(count, samples.Length - position);
            Array.Copy(samples, position, buffer, offset, available);
            position += available;
            return available;
        }
    }
}
