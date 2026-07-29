using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Yokko.Audio;
using Yokko.Audio.Decoding;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class DecodedAudioSourceTest
{
    // 200 ms mono sine encoded by FFmpeg/libmp3lame. Its Xing/Lavc tag
    // declares 576 encoder-delay and 972 end-padding samples.
    private const string gapless_mp3_data =
        "SUQzBAAAAAAAI1RTU0UAAAAPAAADTGF2ZjYyLjEyLjEwMgAAAAAAAAAAAAAA//tAwAAAAAAAAAAAAAAAAAAAAAAAWGluZwAAAA8AAAAJAAAH1QB0dHR0dHR0dHR0dIiIiIiIiIiIiIiIlZWVlZWVlZWVlZWmpqampqampqamprOzs7Ozs7Ozs7OzwMDAwMDAwMDAwMDR0dHR0dHR0dHR0eXl5eXl5eXl5eXl//////////////8AAAAATGF2YzYyLjI4AAAAAAAAAAAAAAAAJAPMAAAAAAAAB9WTn/WKAAAAAAD/+8DEAAAMpCdM9MCAKfAVaL85IEAAId+tt69evXr1792zypXBMAMAMA4jn6wAAAR4eH/8AAEf8cP//AHeZ/+Bv/45n/+f/+YA74AGH/0PABHgAGHnxw8AAGAADDx8cPAABwAjDz/HgAAAAAGHh4eHgAAAAAGHh4eHgA3xAYACWAyByE4YEAYFAACNwFAAgAoMAdcQAEwuKDCwxuGCU95gEjmrDQYDBRivRkAONYlQvWARoHShYkI/w1aOUM0LhICVOHxDki5RWpAi8fMfHKFzC5iZHNWylP8ipkXiaMS7/+RUyLxeMS6Xf4iCoKiI9/rBURBUFREqFAAAHH4wAAH/+8G1LygIAVHEKAFmBECqYQwRZhEIJmS4JcYJoCIKApBAAa1AJAIh8fVP2PmEsKOFE/uaPAugA3H44//9sAlFE4CDQDGMiXoBcYiXufkC4AgtUufm08LDndlzIn76C0U8U/hdoQjKA+hBYHoAAAH//ouAKqIQWKkMAhIom0xgsDkmkgBcGAijQAzUYvAceq5f9EqKakCZoBf/jjxdC/C8AaVpSdSFpQB0YLwCg9NCUAkJRt5D8Nyi7p0XpesogVJrT+XYZakDbABcfDAAAeGTAFmBAAIqHCBjiPANxMGYjI1KAPwcCogeyR33Lhinwqv7spBJ9FP6pigHWFAAN3/96bEJXMBDOdtE92AAV5iBhh9cMJgsACYbXH/Z27kvwp63oqC3I/1zNQBGUAIoD8cAACaIjqwJnq9FBsIEcTBbRDgQaQAARddcjW4fllvg1naJrP9SlNSBNAE+PcGVWlCJXIUAFbRUBUqb4YIDiVAEQ7swfeH5BR8p/fjU0AJIAZx8OAABNkUmmqbIOgA6aQWyYO+McXj6YAgMlS6U3C3lkVke3QKiFiVo/UKG2IA3gBvH44//9sYeIu6YTJEgAKmQIWYa1weSimBgeR9d6zEndh6zRf647W0flZqmA//7MMTyAEeYSym95QAgxwlldY6JVGhAvHw4AACnYQJI0QgUBLSphGgpiO4R/WFoYGSvYVOOg8MSqr6YsxH+Wv2gzKgABQH445qJpvKypLK2l2WbDwBJgvgmB00ZQCUlg38OQxN3NsifsmMFyFjvlY5iKhv8AFx+MAABzRdAUBGUUUoqPJaeCiGCyNaaTYFQYCOlw0+HHYjdRotxdf/7EMT+AEXISSuseEqgwwkldR8I5BJT7/x1yOgAAADx/qO1GGMQZiLAMDAAAGAQRsBQB5gRg3mEqDaYNqPZsCihGCSEiYK4E5gTgQoxkbAcy5uzodJ9Rwi3mmh92w5EMZFWaB0RGdXp//sgxPgAxmBJKal4SqC5iSV5jolU9nc4D+6QRWFnsIKQCmoy0AsQtQpAtDnQVjQaCMIgyoyM+SAoBpIhUAYeBiAz+ZeIw43bBETuPhHEzAHMBHJboBxmQoqqg7EbAQYZpZkjpiyq1HYjYDgwUQpo6nxJhyw3cv01JTBgheNAO0ldq//7EMT9gMXELS3gY6LglwklkR6IrLZVf///2TqaO5LLUMY1o0/0zGf///34Xezt12WSyxDOW8a0ajX////1oYljlv/F34sYVaWl/eOVNTf/////6qWKent2OfrDHHH/x1lW2d+Aw+ch//sQxP0ARhAtK6BjouC5CSW1jolUgHzXEVVMQU1FMy4xMDBVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVX/+yDE9wBFjC0toGOi4MOJJXwfCHRVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV//swxP6ARewrK6DjwuFri+R+vPAFVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVV//tQxPyAFsUnPfm8gAAAADSDgAAEVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVQ==";

    private static readonly string gapless_mp3 = expandSilenceRuns(
        gapless_mp3_data);

    private static string expandSilenceRuns(string encoded)
    {
        int index = 0;
        int[] lengths = { 91, 145, 180, 249 };
        return Regex.Replace(
            encoded,
            "V{10,}",
            _ => new string('V', lengths[index++]));
    }

    [Test]
    public void Mp3GaplessMetadataDefinesLogicalStartAndDuration()
    {
        string path = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"{TestContext.CurrentContext.Test.ID}.mp3");
        File.WriteAllBytes(path, Convert.FromBase64String(gapless_mp3));

        try
        {
            using DecodedAudioSource source = DecodedAudioSource.Open(path);
            var samples = new float[4096];
            int read = source.Read(samples);
            int firstSignal = Array.FindIndex(
                samples,
                0,
                read,
                sample => Math.Abs(sample) >= 0.02f);

            Assert.That(source.SampleRate, Is.EqualTo(44100));
            Assert.That(source.TotalTime.TotalMilliseconds, Is.EqualTo(200).Within(0.001));
            Assert.That(
                firstSignal / 2,
                Is.InRange(0, 4),
                "Encoder and decoder priming must not appear on the logical timeline.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void DoubleTimePreservesPitchWhileNightcoreRaisesIt()
    {
        string path = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"{TestContext.CurrentContext.Test.ID}.wav");
        createSineWave(path, 48000, 440, 1500);

        try
        {
            using DecodedAudioSource doubleTime = DecodedAudioSource.Open(
                path,
                1.5,
                AudioPitchMode.Preserve);
            using DecodedAudioSource nightcore = DecodedAudioSource.Open(
                path,
                1.5,
                AudioPitchMode.ScaleWithRate);

            float[] doubleTimeSamples = readAll(doubleTime);
            float[] nightcoreSamples = readAll(nightcore);

            Assert.Multiple(() =>
            {
                Assert.That(doubleTime.SampleRate, Is.EqualTo(48000));
                Assert.That(nightcore.SampleRate, Is.EqualTo(48000));
                Assert.That(
                    doubleTimeSamples.Length / 2,
                    Is.InRange(43000, 53000),
                    "A 1.5 second source should become about one second.");
                Assert.That(
                    nightcoreSamples.Length / 2,
                    Is.InRange(43000, 53000));
                Assert.That(
                    estimateFrequency(doubleTimeSamples, 48000),
                    Is.InRange(420, 460),
                    "DT must preserve the original pitch.");
                Assert.That(
                    estimateFrequency(nightcoreSamples, 48000),
                    Is.InRange(630, 690),
                    "NC must raise pitch together with playback rate.");
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void HalfTimePreservesPitchWhileDaycoreLowersIt()
    {
        string path = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"{TestContext.CurrentContext.Test.ID}.wav");
        createSineWave(path, 48000, 440, 1500);

        try
        {
            using DecodedAudioSource halfTime = DecodedAudioSource.Open(
                path,
                0.75,
                AudioPitchMode.Preserve);
            using DecodedAudioSource daycore = DecodedAudioSource.Open(
                path,
                0.75,
                AudioPitchMode.ScaleWithRate);

            float[] halfTimeSamples = readAll(halfTime);
            float[] daycoreSamples = readAll(daycore);

            Assert.Multiple(() =>
            {
                Assert.That(
                    halfTimeSamples.Length / 2,
                    Is.InRange(88000, 104000),
                    "A 1.5 second source should become about two seconds.");
                Assert.That(
                    daycoreSamples.Length / 2,
                    Is.InRange(88000, 104000));
                Assert.That(
                    estimateFrequency(halfTimeSamples, 48000),
                    Is.InRange(420, 460),
                    "HT must preserve the original pitch.");
                Assert.That(
                    estimateFrequency(daycoreSamples, 48000),
                    Is.InRange(310, 350),
                    "DC must lower pitch together with playback rate.");
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static float[] readAll(DecodedAudioSource source)
    {
        var samples = new List<float>();
        var block = new float[8192];

        while (true)
        {
            int read = source.Read(block);
            if (read == 0)
                break;

            samples.AddRange(block.AsSpan(0, read).ToArray());
        }

        return samples.ToArray();
    }

    private static double estimateFrequency(
        float[] interleavedStereo,
        int sampleRate)
    {
        int startFrame = Math.Min(
            interleavedStereo.Length / 4,
            sampleRate / 4);
        int endFrame = Math.Min(
            interleavedStereo.Length / 2,
            startFrame + sampleRate / 2);
        int crossings = 0;
        float previous = interleavedStereo[startFrame * 2];

        for (int frame = startFrame + 1; frame < endFrame; frame++)
        {
            float current = interleavedStereo[frame * 2];
            if (previous <= 0 && current > 0)
                crossings++;

            previous = current;
        }

        double seconds = (endFrame - startFrame) / (double)sampleRate;
        return crossings / seconds;
    }

    private static void createSineWave(
        string path,
        int sampleRate,
        double frequency,
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
            short sample = (short)Math.Round(
                Math.Sin(frame * Math.PI * 2 * frequency / sampleRate)
                * short.MaxValue
                * 0.5);
            writer.Write(sample);
            writer.Write(sample);
        }
    }
}
