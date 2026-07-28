using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLayer.NAudioSupport;

namespace Yokko.Audio.Decoding;

internal sealed class DecodedAudioSource : IDisposable
{
    private readonly WaveStream stream;
    private readonly ISampleProvider stereoProvider;

    private DecodedAudioSource(WaveStream stream, ISampleProvider stereoProvider)
    {
        this.stream = stream;
        this.stereoProvider = stereoProvider;
    }

    internal int SampleRate => stereoProvider.WaveFormat.SampleRate;

    internal TimeSpan TotalTime => stream.TotalTime;

    internal TimeSpan CurrentTime
    {
        get => stream.CurrentTime;
        set => stream.CurrentTime = value < TimeSpan.Zero
            ? TimeSpan.Zero
            : value > stream.TotalTime
                ? stream.TotalTime
                : value;
    }

    internal int Read(float[] samples)
        => stereoProvider.Read(samples, 0, samples.Length);

    internal static DecodedAudioSource Open(string audioPath)
    {
        string extension = Path.GetExtension(audioPath);
        WaveStream stream;
        ISampleProvider samples;

        if (string.Equals(extension, ".ogg", StringComparison.OrdinalIgnoreCase))
        {
            var vorbis = new VorbisWaveReader(audioPath);
            stream = vorbis;
            samples = vorbis.ToSampleProvider();
        }
        else if (string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase))
        {
            var builder = new Mp3FileReaderBase.FrameDecompressorBuilder(
                waveFormat => new Mp3FrameDecompressor(waveFormat));
            var mp3 = new Mp3FileReaderBase(audioPath, builder);
            stream = mp3;
            samples = mp3.ToSampleProvider();
        }
        else if (string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase))
        {
            var wave = new WaveFileReader(audioPath);
            stream = wave;
            samples = wave.ToSampleProvider();
        }
        else
        {
            throw new NotSupportedException(
                $"Yokko native audio currently supports WAV, MP3 and OGG files, not '{extension}'.");
        }

        try
        {
            ISampleProvider stereo = samples.WaveFormat.Channels switch
            {
                1 => new MonoToStereoSampleProvider(samples),
                2 => samples,
                _ => new FirstTwoChannelsSampleProvider(samples),
            };
            return new DecodedAudioSource(stream, stereo);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public void Dispose()
        => stream.Dispose();

    private sealed class FirstTwoChannelsSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private float[] scratch = [];

        internal FirstTwoChannelsSampleProvider(ISampleProvider source)
        {
            this.source = source;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
                source.WaveFormat.SampleRate,
                2);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int requestedFrames = count / 2;
            int requestedSourceSamples =
                requestedFrames * source.WaveFormat.Channels;
            if (scratch.Length < requestedSourceSamples)
                scratch = new float[requestedSourceSamples];

            int sourceSamples = source.Read(
                scratch,
                0,
                requestedSourceSamples);
            int frames = sourceSamples / source.WaveFormat.Channels;
            for (int frame = 0; frame < frames; frame++)
            {
                int sourceOffset = frame * source.WaveFormat.Channels;
                int targetOffset = offset + frame * 2;
                buffer[targetOffset] = scratch[sourceOffset];
                buffer[targetOffset + 1] = scratch[sourceOffset + 1];
            }

            return frames * 2;
        }
    }
}
