using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Game.Gameplay;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class YokkoReplayIOTest
{
    private string directory;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "yokko-replays",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    [Test]
    public void NativeReplayRoundTripsInputsAndExactModConfiguration()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        ManiaModSet mods = ManiaModSet.Empty
            .WithRandomSeed(123456)
            .WithCover(0.61, ManiaCoverDirection.AgainstScroll)
            .WithDifficultyAdjust(10.5, -2, true)
            .WithMuted(true, false, 220, false)
            .WithFixedRate(ManiaModId.DoubleTime, 1.73, true);
        var replay = new GameplayReplay(
            [
                new GameplayReplayInput(0, true, 100),
                new GameplayReplayInput(0, false, 150),
                new GameplayReplayInput(3, true, 200),
            ],
            mods,
            new JudgementConfiguration(
                JudgementMode.Etterna,
                8));
        var recordedAt = new DateTimeOffset(
            2026,
            7,
            29,
            10,
            20,
            30,
            TimeSpan.Zero);
        using var stream = new MemoryStream();

        YokkoReplayIO.Write(
            stream,
            beatmap,
            beatmap,
            replay,
            "ABCDEF",
            recordedAt);
        string json = Encoding.UTF8.GetString(stream.ToArray());
        stream.Position = 0;
        YokkoReplayLoadResult restored = YokkoReplayIO.Read(stream);

        Assert.Multiple(() =>
        {
            Assert.That(restored.SourceHash, Is.EqualTo("ABCDEF"));
            Assert.That(
                restored.Replay.JudgementConfiguration,
                Is.EqualTo(new JudgementConfiguration(
                    JudgementMode.Etterna,
                    8)));
            Assert.That(restored.KeyCount, Is.EqualTo(4));
            Assert.That(restored.RecordedAt, Is.EqualTo(recordedAt));
            Assert.That(
                restored.BeatmapFingerprint,
                Is.EqualTo(YokkoBeatmapFingerprint.Compute(beatmap)));
            Assert.That(restored.Replay.Mods, Is.EqualTo(mods));
            Assert.That(restored.Replay.Inputs, Is.EqualTo(replay.Inputs));
            Assert.That(json, Does.Contain("\"schemaVersion\":3"));
            Assert.That(json, Does.Contain("\"frames\":"));
            Assert.That(json, Does.Contain("\"replayChecksum\":"));
            Assert.That(json, Does.Not.Contain("\"inputs\":"));
            Assert.That(json, Does.Contain("\"key\":\"random\""));
            Assert.That(json, Does.Contain("\"seed\":123456"));
            Assert.That(json, Does.Not.Contain("\"Random\""));
        });
    }

    [Test]
    public void NativeReplayRoundTripsOsuStableJudgementMode()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var replay = new GameplayReplay(
            [],
            ManiaModSet.Empty,
            JudgementConfiguration.OsuStableDefault);
        using var stream = new MemoryStream();

        YokkoReplayIO.Write(stream, beatmap, beatmap, replay);
        stream.Position = 0;

        Assert.That(
            YokkoReplayIO.Read(stream).Replay.JudgementConfiguration,
            Is.EqualTo(JudgementConfiguration.OsuStableDefault));
    }

    [Test]
    public void ReplayRejectsLaneOutsideAppliedKeyMode()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var replay = new GameplayReplay(
            [new GameplayReplayInput(4, true, 100)]);

        Assert.That(
            () => YokkoReplayIO.Write(
                new MemoryStream(),
                beatmap,
                beatmap,
                replay),
            Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void UnknownPersistedModCannotSilentlyBecomeNoMod()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var replay = new GameplayReplay(
            [],
            ManiaModSet.Empty.With(ManiaModId.Hidden, true));
        using var source = new MemoryStream();
        YokkoReplayIO.Write(source, beatmap, beatmap, replay);
        string json = Encoding.UTF8.GetString(source.ToArray())
            .Replace(
                "\"key\":\"hidden\"",
                "\"key\":\"future-mod\"",
                StringComparison.Ordinal);
        using var changed = new MemoryStream(
            Encoding.UTF8.GetBytes(json));

        Assert.That(
            () => YokkoReplayIO.Read(changed),
            Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void VersionOneReplayDefaultsToOriginalYokkoJudgement()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var replay = new GameplayReplay(
            [],
            ManiaModSet.Empty,
            new JudgementConfiguration(
                JudgementMode.Etterna,
                9));
        using var source = new MemoryStream();
        YokkoReplayIO.Write(source, beatmap, beatmap, replay);
        JsonObject versionOneDocument = JsonNode.Parse(
            Encoding.UTF8.GetString(source.ToArray()))!.AsObject();
        versionOneDocument["schemaVersion"] = 1;
        versionOneDocument["inputs"] = new JsonArray();
        versionOneDocument.Remove("frames");
        versionOneDocument.Remove("clientVersion");
        versionOneDocument.Remove("replayChecksum");
        string versionOneJson = versionOneDocument.ToJsonString();
        using var versionOne = new MemoryStream(
            Encoding.UTF8.GetBytes(versionOneJson));

        YokkoReplayLoadResult restored =
            YokkoReplayIO.Read(versionOne);

        Assert.That(
            restored.Replay.JudgementConfiguration,
            Is.EqualTo(JudgementConfiguration.YokkoDefault));
    }

    [Test]
    public void VersionTwoReplayRestoresLegacyInputEdges()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var replay = new GameplayReplay(
        [
            new GameplayReplayInput(0, true, 100),
            new GameplayReplayInput(0, false, 150),
        ]);
        using var source = new MemoryStream();
        YokkoReplayIO.Write(source, beatmap, beatmap, replay);
        JsonObject document = JsonNode.Parse(
            Encoding.UTF8.GetString(source.ToArray()))!.AsObject();
        document["schemaVersion"] = 2;
        document["inputs"] = new JsonArray(
            new JsonObject
            {
                ["lane"] = 0,
                ["isPressed"] = true,
                ["timeMilliseconds"] = 100,
            },
            new JsonObject
            {
                ["lane"] = 0,
                ["isPressed"] = false,
                ["timeMilliseconds"] = 150,
            });
        document.Remove("frames");
        document.Remove("clientVersion");
        document.Remove("replayChecksum");
        using var legacy = new MemoryStream(
            Encoding.UTF8.GetBytes(document.ToJsonString()));

        YokkoReplayLoadResult restored = YokkoReplayIO.Read(legacy);

        Assert.That(restored.Replay.Inputs, Is.EqualTo(replay.Inputs));
    }

    [Test]
    public void VersionThreeReplayRejectsChangedFrameState()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var replay = new GameplayReplay(
            [new GameplayReplayInput(0, true, 100)]);
        using var source = new MemoryStream();
        YokkoReplayIO.Write(source, beatmap, beatmap, replay);
        string changed = Encoding.UTF8.GetString(source.ToArray())
            .Replace(
                "\"pressedLanes\":1",
                "\"pressedLanes\":2",
                StringComparison.Ordinal);
        using var tampered = new MemoryStream(
            Encoding.UTF8.GetBytes(changed));

        Assert.That(
            () => YokkoReplayIO.Read(tampered),
            Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void ReplayStoreUsesAtomicUniqueNativeFiles()
    {
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var replay = new GameplayReplay([]);
        var store = new GameplayReplayStore();
        store.Initialise(directory);
        var timestamp = new DateTimeOffset(
            2026,
            7,
            29,
            10,
            20,
            30,
            TimeSpan.Zero);

        string first = store.Save(
            beatmap,
            beatmap,
            replay,
            recordedAt: timestamp);
        string second = store.Save(
            beatmap,
            beatmap,
            replay,
            recordedAt: timestamp);

        Assert.Multiple(() =>
        {
            Assert.That(first, Does.EndWith(YokkoReplayIO.FileExtension));
            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(File.Exists(first), Is.True);
            Assert.That(File.Exists(second), Is.True);
            Assert.That(
                Directory.EnumerateFiles(
                    directory,
                    "*.tmp",
                    SearchOption.AllDirectories),
                Is.Empty);
            Assert.That(
                YokkoReplayIO.ReadFromFile(first).Replay.Mods,
                Is.EqualTo(ManiaModSet.Empty));
        });
    }
}
