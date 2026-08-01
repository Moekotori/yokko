using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Yokko.Core.Mods;
using Yokko.Game.Gameplay;
using Yokko.Import;
using Yokko.Import.Malody;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class MalodyReplayIOTest
{
    [Test]
    public void ReadsKeyReplayMetadataAndOrderedInputs()
    {
        using Stream stream = createReplay(
        [
            (200, 1, 1),
            (100, 1, 0),
            (200, 2, 1),
            (-20, 1, 3),
        ]);

        MalodyReplay replay = MalodyReplayIO.Read(stream);
        GameplayReplay gameplay = GameplayReplay.FromMalodyReplay(replay, 4);

        Assert.Multiple(() =>
        {
            Assert.That(replay.BeatmapHash,
                Is.EqualTo("0123456789abcdef0123456789abcdef"));
            Assert.That(replay.DifficultyName, Is.EqualTo("4K Hard"));
            Assert.That(replay.SongTitle, Is.EqualTo("Replay test"));
            Assert.That(replay.SongArtist, Is.EqualTo("Yokko"));
            Assert.That(replay.Score, Is.EqualTo(1_234_567));
            Assert.That(replay.MaxCombo, Is.EqualTo(432));
            Assert.That(replay.Best, Is.EqualTo(400));
            Assert.That(replay.Cool, Is.EqualTo(20));
            Assert.That(replay.Good, Is.EqualTo(10));
            Assert.That(replay.Miss, Is.EqualTo(2));
            Assert.That(replay.HoldBreaks, Is.EqualTo(1));
            Assert.That(replay.Judge, Is.EqualTo(2));
            Assert.That(replay.PlayedAt,
                Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000)));
            Assert.That(gameplay.Mods.Contains(ManiaModId.Mirror), Is.True);
            Assert.That(gameplay.Mods.Contains(ManiaModId.ConstantSpeed), Is.True);
            Assert.That(gameplay.Mods.Contains(ManiaModId.DoubleTime), Is.True);
            Assert.That(gameplay.Mods.PlaybackRate, Is.EqualTo(1.2));
            Assert.That(gameplay.Inputs, Is.EqualTo(new[]
            {
                new GameplayReplayInput(0, true, 100),
                new GameplayReplayInput(1, false, 200),
                new GameplayReplayInput(1, true, 200),
            }));
        });
    }

    [Test]
    public void RejectsReplayLaneOutsideBeatmap()
    {
        using Stream stream = createReplay([(100, 1, 4)]);
        MalodyReplay replay = MalodyReplayIO.Read(stream);

        Assert.That(
            () => GameplayReplay.FromMalodyReplay(replay, 4),
            Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void MalodyChartImportExposesReplayBeatmapHash()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "malody-replay-import",
            TestContext.CurrentContext.Test.ID);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "hash-test.mc");
        File.WriteAllText(path, """
            {
              "meta": {
                "mode": 0,
                "mode_ext": { "column": 4 },
                "song": { "title": "Hash test", "artist": "Yokko" },
                "creator": "Yokko",
                "version": "4K"
              },
              "time": [ { "beat": [0,0,1], "bpm": 120 } ],
              "note": [ { "beat": [1,0,1], "column": 0 } ]
            }
            """, new UTF8Encoding(false));

        ChartImportResult result = new MalodyChartImporter()
                                   .ImportAsync(new ChartImportRequest(
                                       path,
                                       false,
                                       false))
                                   .AsTask()
                                   .GetAwaiter()
                                   .GetResult();

        using FileStream source = File.OpenRead(path);
        string expected = Convert.ToHexString(MD5.HashData(source))
                                 .ToLowerInvariant();
        Assert.That(result.SourceHash, Is.EqualTo(expected));
    }

    private static Stream createReplay(
        IReadOnlyList<(int Time, byte Action, byte Lane)> events)
    {
        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(
                   stream,
                   Encoding.UTF8,
                   leaveOpen: true))
        {
            writeString(writer, "mr format head");
            writer.Write(new byte[] { 7, 3, 4, 0 });
            writeString(writer, "0123456789abcdef0123456789abcdef");
            writeString(writer, "4K Hard");
            writeString(writer, "Replay test");
            writeString(writer, "Yokko");
            writer.Write(1_234_567);
            writer.Write(432);
            writer.Write(400);
            writer.Write(20);
            writer.Write(10);
            writer.Write(2);
            writer.Write(1);
            writer.Write((int)(
                MalodyReplayMods.Flip
                | MalodyReplayMods.Constant
                | MalodyReplayMods.Dash));
            writer.Write(2);
            writeString(writer, "mr data");
            writer.Write(new byte[] { 7, 3, 4, 0 });
            writer.Write(events.Count);
            writer.Write((byte)0);
            writer.Write(1_700_000_000);
            writer.Write(0);
            foreach ((int time, byte action, byte lane) in events)
            {
                writer.Write(time);
                writer.Write(action);
                writer.Write(lane);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static void writeString(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}
