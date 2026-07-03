using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Yokko.Game.Screens.Editor;

namespace Yokko.Game.Tests.Core
{
    [TestFixture]
    public sealed class EditorAudioWaveformLoaderTest
    {
        [Test]
        public async Task LoadsWaveformFromPcmWav()
        {
            string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "waveform", TestContext.CurrentContext.Test.ID);
            Directory.CreateDirectory(directory);
            string audioPath = Path.Combine(directory, "tone.wav");
            writeSineWave(audioPath);

            EditorAudioWaveform waveform = await EditorAudioWaveformLoader.LoadAsync(audioPath);

            Assert.That(waveform.HasAudio, Is.True, waveform.Label);
            Assert.That(waveform.Peaks, Has.Count.EqualTo(EditorAudioWaveformLoader.DefaultPointCount));
            Assert.That(waveform.DurationMilliseconds, Is.GreaterThan(100));
        }

        private static void writeSineWave(string path)
        {
            const int sampleRate = 44100;
            const short channels = 1;
            const short bitsPerSample = 16;
            int sampleCount = sampleRate / 4;
            short blockAlign = (short)(channels * bitsPerSample / 8);
            int byteRate = sampleRate * blockAlign;
            int dataLength = sampleCount * blockAlign;

            using var writer = new BinaryWriter(File.Create(path), Encoding.ASCII);
            writer.Write("RIFF"u8.ToArray());
            writer.Write(36 + dataLength);
            writer.Write("WAVE"u8.ToArray());
            writer.Write("fmt "u8.ToArray());
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);
            writer.Write("data"u8.ToArray());
            writer.Write(dataLength);

            for (int i = 0; i < sampleCount; i++)
            {
                double sample = Math.Sin(i / (double)sampleRate * Math.Tau * 440);
                writer.Write((short)(sample * short.MaxValue * 0.45));
            }
        }
    }
}
