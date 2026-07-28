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
            var mp3 = new GaplessMpegWaveStream(audioPath);
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

    private sealed class GaplessMpegWaveStream : WaveStream
    {
        // ISO Layer III synthesis contributes 529 samples in addition to the
        // encoder delay recorded in a Xing/LAME-style gapless tag.
        private const int layer_3_decoder_delay = 529;

        private readonly Mp3FileReaderBase source;
        private readonly long startTrimBytes;
        private readonly long logicalLength;
        private long logicalPosition;

        internal GaplessMpegWaveStream(string path)
        {
            Mp3GaplessInfo gapless = readGaplessInfo(path);
            var builder = new Mp3FileReaderBase.FrameDecompressorBuilder(
                format => new Mp3FrameDecompressor(format));
            source = new Mp3FileReaderBase(path, builder);
            WaveFormat = source.WaveFormat;

            int decoderDelay = gapless.HasValue
                ? layer_3_decoder_delay
                : 0;
            long startTrimFrames =
                gapless.EncoderDelayFrames + decoderDelay;
            long endTrimFrames = Math.Max(
                0,
                gapless.EncoderPaddingFrames - decoderDelay);
            startTrimBytes = startTrimFrames * WaveFormat.BlockAlign;
            long endTrimBytes = endTrimFrames * WaveFormat.BlockAlign;
            logicalLength = Math.Max(
                0,
                source.Length - startTrimBytes - endTrimBytes);
            Position = 0;
        }

        public override WaveFormat WaveFormat { get; }

        public override long Length => logicalLength;

        public override long Position
        {
            get => logicalPosition;
            set
            {
                long aligned = Math.Clamp(value, 0, Length);
                aligned -= aligned % WaveFormat.BlockAlign;
                source.Position = startTrimBytes + aligned;
                logicalPosition = aligned;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long remaining = Length - logicalPosition;
            int requested = (int)Math.Min(count, remaining);
            requested -= requested % WaveFormat.BlockAlign;
            if (requested <= 0)
                return 0;

            int read = source.Read(buffer, offset, requested);
            logicalPosition += read;
            return read;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                source.Dispose();

            base.Dispose(disposing);
        }

        private static Mp3GaplessInfo readGaplessInfo(string path)
        {
            using FileStream stream = File.OpenRead(path);
            _ = Id3v2Tag.ReadTag(stream);
            Mp3Frame? frame = Mp3Frame.LoadFromStream(stream);
            if (frame?.RawData == null
                || frame.MpegLayer != MpegLayer.Layer3)
                return default;

            byte[] data = frame.RawData;
            int sideInfoSize = frame.MpegVersion switch
            {
                MpegVersion.Version1 when frame.ChannelMode == ChannelMode.Mono
                    => 17,
                MpegVersion.Version1 => 32,
                _ when frame.ChannelMode == ChannelMode.Mono => 9,
                _ => 17,
            };
            int cursor = 4 + (frame.CrcPresent ? 2 : 0) + sideInfoSize;
            if (!matches(data, cursor, "Xing")
                && !matches(data, cursor, "Info"))
                return default;

            cursor += 4;
            if (!tryReadBigEndianInt32(data, ref cursor, out int flags))
                return default;
            if ((flags & 0x1) != 0)
                cursor += 4;
            if ((flags & 0x2) != 0)
                cursor += 4;
            if ((flags & 0x4) != 0)
                cursor += 100;
            if ((flags & 0x8) != 0)
                cursor += 4;

            // LAME and Lavc both place the packed delay/padding values at
            // bytes 21-23 of their 36-byte encoder tag.
            if (cursor < 0 || cursor + 24 > data.Length)
                return default;
            if (!matches(data, cursor, "LAME")
                && !matches(data, cursor, "Lavc"))
                return default;

            int encoderDelay =
                (data[cursor + 21] << 4) | (data[cursor + 22] >> 4);
            int encoderPadding =
                ((data[cursor + 22] & 0x0f) << 8) | data[cursor + 23];
            return encoderDelay == 0 && encoderPadding == 0
                ? default
                : new Mp3GaplessInfo(encoderDelay, encoderPadding);
        }

        private static bool matches(byte[] data, int offset, string value)
        {
            if (offset < 0 || offset + value.Length > data.Length)
                return false;

            for (int index = 0; index < value.Length; index++)
            {
                if (data[offset + index] != value[index])
                    return false;
            }

            return true;
        }

        private static bool tryReadBigEndianInt32(
            byte[] data,
            ref int offset,
            out int value)
        {
            if (offset < 0 || offset + 4 > data.Length)
            {
                value = 0;
                return false;
            }

            value =
                data[offset] << 24
                | data[offset + 1] << 16
                | data[offset + 2] << 8
                | data[offset + 3];
            offset += 4;
            return true;
        }

        private readonly record struct Mp3GaplessInfo(
            int EncoderDelayFrames,
            int EncoderPaddingFrames)
        {
            internal bool HasValue =>
                EncoderDelayFrames > 0 || EncoderPaddingFrames > 0;
        }
    }

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
