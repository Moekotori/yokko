using System;
using System.IO;
using System.Text;
using Yokko.Core.Beatmaps;

namespace Yokko.Game.Gameplay;

/// <summary>
/// Yokko default-skin fallback samples. osu!lazer's resource pack is
/// CC-BY-NC and is intentionally not redistributed; beatmap-provided samples
/// still take precedence using the exact lazer lookup order.
/// </summary>
internal static class GameplayDefaultHitSampleStore
{
    private const int sampleRate = 44100;
    private static readonly object gate = new();
    private static readonly string root = Path.Combine(
        Path.GetTempPath(),
        "Yokko",
        "default-hitsounds-v1");

    internal static string Resolve(YokkoHitSample sample)
    {
        if (!isSupported(sample.Name))
            return null;

        string path = Path.Combine(
            root,
            $"{sample.Bank}-{sample.Name}.wav");
        if (File.Exists(path))
            return path;

        lock (gate)
        {
            if (File.Exists(path))
                return path;

            try
            {
                Directory.CreateDirectory(root);
                writeWave(path, sample.Bank, sample.Name);
                return path;
            }
            catch
            {
                return null;
            }
        }
    }

    private static bool isSupported(string name) =>
        name is YokkoHitSample.HitNormal
            or YokkoHitSample.HitWhistle
            or YokkoHitSample.HitFinish
            or YokkoHitSample.HitClap
            or YokkoHitSample.SliderSlide
            or YokkoHitSample.SliderWhistle;

    private static void writeWave(
        string path,
        string bank,
        string name)
    {
        double duration = name is YokkoHitSample.SliderSlide
            or YokkoHitSample.SliderWhistle
            ? 0.18
            : 0.065;
        int frames = (int)Math.Round(sampleRate * duration);
        short[] pcm = new short[frames];
        double bankFactor = bank switch
        {
            YokkoHitSample.BankSoft => 0.78,
            YokkoHitSample.BankDrum => 0.58,
            _ => 1,
        };
        double frequency = name switch
        {
            YokkoHitSample.HitWhistle
                or YokkoHitSample.SliderWhistle => 1760,
            YokkoHitSample.HitFinish => 330,
            YokkoHitSample.HitClap => 980,
            YokkoHitSample.SliderSlide => 520,
            _ => 720,
        };

        uint noise = 0x9e3779b9;
        for (int index = 0; index < frames; index++)
        {
            double time = (double)index / sampleRate;
            double progress = (double)index / Math.Max(1, frames - 1);
            double envelope = Math.Pow(1 - progress, 3);
            noise = noise * 1664525u + 1013904223u;
            double random = ((noise >> 8) / 8388607.5) - 1;
            double tonal = Math.Sin(2 * Math.PI * frequency * time);
            double noiseMix = name is YokkoHitSample.HitClap
                or YokkoHitSample.HitFinish
                ? 0.55
                : 0.18;
            double value =
                (tonal * (1 - noiseMix) + random * noiseMix)
                * envelope
                * bankFactor
                * 0.45;
            pcm[index] = (short)Math.Clamp(
                Math.Round(value * short.MaxValue),
                short.MinValue,
                short.MaxValue);
        }

        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read);
        using var writer = new BinaryWriter(stream, Encoding.ASCII);
        int dataBytes = pcm.Length * sizeof(short);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataBytes);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * sizeof(short));
        writer.Write((short)sizeof(short));
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataBytes);
        foreach (short value in pcm)
            writer.Write(value);
    }
}
