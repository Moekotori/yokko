using System;
using System.IO;
using NUnit.Framework;
using Yokko.Audio.Decoding;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class DecodedAudioSampleTest
{
    [Test]
    public void KeysoundDecodesAndResamplesToOutputRate()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "decoded-keysounds",
            TestContext.CurrentContext.Test.ID);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "keysound.wav");
        writeWave(path, 44100, 100);

        DecodedAudioSample sample = DecodedAudioSample.Decode(path);
        float[] resampled = sample.GetSamplesAt(48000);

        Assert.That(sample.SampleRate, Is.EqualTo(44100));
        Assert.That(sample.Samples, Has.Length.EqualTo(8820));
        Assert.That(resampled.Length, Is.InRange(9500, 9700));
        Assert.That(resampled.Length % 2, Is.Zero);
        Assert.That(resampled, Has.Some.Not.Zero);
    }

    private static void writeWave(
        string path,
        int sampleRate,
        int durationMilliseconds)
    {
        const short channels = 2;
        const short bitsPerSample = 16;
        int frameCount = sampleRate * durationMilliseconds / 1000;
        int dataLength = frameCount * channels * bitsPerSample / 8;
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataLength);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8);
        writer.Write((short)(channels * bitsPerSample / 8));
        writer.Write(bitsPerSample);
        writer.Write("data"u8.ToArray());
        writer.Write(dataLength);

        for (int frame = 0; frame < frameCount; frame++)
        {
            short value = (short)(Math.Sin(
                frame * Math.PI * 2 * 880 / sampleRate) * short.MaxValue / 2);
            writer.Write(value);
            writer.Write(value);
        }
    }
}
