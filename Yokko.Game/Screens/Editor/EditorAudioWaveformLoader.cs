using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Audio.Track;

namespace Yokko.Game.Screens.Editor;

public static class EditorAudioWaveformLoader
{
    public const int DefaultPointCount = 16384;

    public static async Task<EditorAudioWaveform> LoadAsync(string audioPath, CancellationToken cancellationToken = default)
    {
        try
        {
            double durationMilliseconds = tryReadWaveDurationMilliseconds(audioPath);
            FileStream stream = File.OpenRead(audioPath);
            var waveform = new Waveform(stream);

            Waveform resampled = await waveform.GenerateResampledAsync(DefaultPointCount, cancellationToken).ConfigureAwait(false);
            Waveform.Point[] points = (await resampled.GetPointsAsync().ConfigureAwait(false)).ToArray();
            cancellationToken.ThrowIfCancellationRequested();

            if (points.Length == 0)
                return EditorAudioWaveform.Failed(audioPath, "Audio waveform empty");

            float normaliser = Math.Max(0.0001f, points.Max(point => Math.Max(Math.Abs(point.AmplitudeLeft), Math.Abs(point.AmplitudeRight))));
            float lowNormaliser = Math.Max(0.0001f, points.Max(point => point.LowIntensity));
            float midNormaliser = Math.Max(0.0001f, points.Max(point => point.MidIntensity));
            float highNormaliser = Math.Max(0.0001f, points.Max(point => point.HighIntensity));

            float[] peaks = new float[points.Length];
            float[] lows = new float[points.Length];
            float[] mids = new float[points.Length];
            float[] highs = new float[points.Length];

            for (int i = 0; i < points.Length; i++)
            {
                Waveform.Point point = points[i];
                peaks[i] = Math.Clamp(Math.Max(Math.Abs(point.AmplitudeLeft), Math.Abs(point.AmplitudeRight)) / normaliser, 0, 1);
                lows[i] = Math.Clamp(point.LowIntensity / lowNormaliser, 0, 1);
                mids[i] = Math.Clamp(point.MidIntensity / midNormaliser, 0, 1);
                highs[i] = Math.Clamp(point.HighIntensity / highNormaliser, 0, 1);
            }

            return EditorAudioWaveform.Ready(audioPath, durationMilliseconds, peaks, lows, mids, highs);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return EditorAudioWaveform.Failed(audioPath, ex.Message);
        }
    }

    private static double tryReadWaveDurationMilliseconds(string audioPath)
    {
        try
        {
            using var reader = new BinaryReader(File.OpenRead(audioPath));
            if (new string(reader.ReadChars(4)) != "RIFF")
                return 0;

            reader.ReadInt32();
            if (new string(reader.ReadChars(4)) != "WAVE")
                return 0;

            int byteRate = 0;
            int dataLength = 0;

            while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
            {
                string chunkId = new(reader.ReadChars(4));
                int chunkSize = reader.ReadInt32();
                long nextChunk = reader.BaseStream.Position + chunkSize;

                if (chunkId == "fmt " && chunkSize >= 16)
                {
                    reader.ReadInt16();
                    reader.ReadInt16();
                    reader.ReadInt32();
                    byteRate = reader.ReadInt32();
                }
                else if (chunkId == "data")
                {
                    dataLength = chunkSize;
                }

                reader.BaseStream.Position = Math.Min(nextChunk + chunkSize % 2, reader.BaseStream.Length);

                if (byteRate > 0 && dataLength > 0)
                    return dataLength * 1000d / byteRate;
            }

            return 0;
        }
        catch
        {
            return 0;
        }
    }
}
