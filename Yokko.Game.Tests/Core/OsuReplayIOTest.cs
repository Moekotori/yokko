using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using SharpCompress.Compressors.LZMA;
using Yokko.Core.Mods;
using Yokko.Game.Gameplay;
using Yokko.Import;
using Yokko.Import.Osu;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class OsuReplayIOTest
{
    [Test]
    public void ReadsManiaReplayAndConvertsKeyTransitions()
    {
        using Stream stream = createReplay(
            "0123456789abcdef0123456789abcdef",
            "Yokko Player",
            """
            0|0|0|0,100|3|0|0,25|2|0|0,25|0|0|0,-12345|0|0|0
            """,
            OsuLegacyMods.Nightcore
            | OsuLegacyMods.DoubleTime
            | OsuLegacyMods.Perfect
            | OsuLegacyMods.SuddenDeath);

        OsuReplay replay = OsuReplayIO.Read(stream);
        GameplayReplay gameplayReplay =
            GameplayReplay.FromOsuReplay(replay, 4);

        Assert.Multiple(() =>
        {
            Assert.That(replay.GameVersion, Is.EqualTo(20260728));
            Assert.That(
                replay.BeatmapHash,
                Is.EqualTo("0123456789abcdef0123456789abcdef"));
            Assert.That(replay.PlayerName, Is.EqualTo("Yokko Player"));
            Assert.That(
                gameplayReplay.Mods.Mods,
                Is.EqualTo(new[]
                {
                    ManiaModId.Perfect,
                    ManiaModId.Nightcore,
                }));
            Assert.That(replay.Frames.Select(frame => frame.TimeMilliseconds),
                Is.EqualTo(new[] { 0, 100, 125, 150 }));
            Assert.That(
                gameplayReplay.Inputs,
                Is.EqualTo(new[]
                {
                    new GameplayReplayInput(0, true, 100),
                    new GameplayReplayInput(1, true, 100),
                    new GameplayReplayInput(0, false, 125),
                    new GameplayReplayInput(1, false, 150),
                }));
        });
    }

    [Test]
    public void RemovesStableIntroFrames()
    {
        using Stream stream = createReplay(
            "hash",
            "Player",
            "0|256|-500|0,499|256|-500|0,1|1|0|0");

        OsuReplay replay = OsuReplayIO.Read(stream);

        Assert.That(replay.Frames, Is.EqualTo(new[]
        {
            new OsuReplayFrame(500, 1),
        }));
    }

    [Test]
    public void RejectsReplayKeysOutsideBeatmap()
    {
        var replay = new OsuReplay(
            20260728,
            "hash",
            "Player",
            OsuLegacyMods.None,
            [new OsuReplayFrame(100, 1 << 4)]);

        Assert.That(
            () => GameplayReplay.FromOsuReplay(replay, 4),
            Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void RejectsNonManiaReplay()
    {
        using var stream = new MemoryStream([0]);

        Assert.That(
            () => OsuReplayIO.Read(stream),
            Throws.TypeOf<InvalidDataException>()
                  .With.Message.Contains("osu!mania"));
    }

    [Test]
    public void OsuChartImportExposesReplayBeatmapHash()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "replay-import",
            TestContext.CurrentContext.Test.ID);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "hash-test.osu");
        File.WriteAllText(path, """
            osu file format v14

            [General]
            Mode: 3

            [Metadata]
            Title:Hash test

            [Difficulty]
            CircleSize:4

            [HitObjects]
            64,192,500,1,0,0:0:0:0:
            """, new UTF8Encoding(false));

        ChartImportResult result = new OsuManiaChartImporter()
                                   .ImportAsync(new ChartImportRequest(
                                       path,
                                       false,
                                       false))
                                   .AsTask()
                                   .GetAwaiter()
                                   .GetResult();

        using FileStream stream = File.OpenRead(path);
        string expected =
            Convert.ToHexString(MD5.HashData(stream)).ToLowerInvariant();
        Assert.That(result.SourceHash, Is.EqualTo(expected));
    }

    private static Stream createReplay(
        string beatmapHash,
        string playerName,
        string replayFrames,
        OsuLegacyMods mods = OsuLegacyMods.None)
    {
        byte[] compressedFrames = compress(replayFrames);
        var stream = new MemoryStream();

        using (var writer = new BinaryWriter(
                   stream,
                   Encoding.UTF8,
                   leaveOpen: true))
        {
            writer.Write((byte)3);
            writer.Write(20260728);
            writeLegacyString(writer, beatmapHash);
            writeLegacyString(writer, playerName);
            writeLegacyString(writer, "replay hash");
            for (int i = 0; i < 6; i++)
                writer.Write((ushort)0);
            writer.Write(123456);
            writer.Write((ushort)42);
            writer.Write(false);
            writer.Write((int)mods);
            writeLegacyString(writer, string.Empty);
            writer.Write(DateTime.UnixEpoch.Ticks);
            writer.Write(compressedFrames.Length);
            writer.Write(compressedFrames);
        }

        stream.Position = 0;
        return stream;
    }

    private static byte[] compress(string value)
    {
        byte[] content = Encoding.UTF8.GetBytes(value);
        using var output = new MemoryStream();

        using (var lzma = LzmaStream.Create(
                   new LzmaEncoderProperties(false, 1 << 21, 255),
                   false,
                   output))
        {
            output.Write(lzma.Properties);

            long length = content.Length;
            for (int i = 0; i < sizeof(long); i++)
                output.WriteByte((byte)(length >> (8 * i)));

            lzma.Write(content);
        }

        return output.ToArray();
    }

    private static void writeLegacyString(
        BinaryWriter writer,
        string value)
    {
        writer.Write((byte)0x0b);
        writer.Write(value);
    }
}
