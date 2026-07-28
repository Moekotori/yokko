using NUnit.Framework;
using System.IO;
using Yokko.Core.Beatmaps;
using Yokko.Core.Editing;
using Yokko.Core.Gameplay;
using Yokko.Import.Osu;

namespace Yokko.Game.Tests.Core
{
    [TestFixture]
    public sealed class OsuManiaBeatmapIOTest
    {
        [Test]
        public void ReadsOsuManiaTapAndHoldObjects()
        {
            var beatmap = OsuManiaBeatmapIO.ReadBeatmap(sampleOsu);

            Assert.That(beatmap.Title, Is.EqualTo("Test Song"));
            Assert.That(beatmap.Artist, Is.EqualTo("Test Artist"));
            Assert.That(beatmap.Creator, Is.EqualTo("Mapper"));
            Assert.That(beatmap.DifficultyName, Is.EqualTo("4K"));
            Assert.That(beatmap.KeyMode, Is.EqualTo(KeyMode.FourKey));
            Assert.That(beatmap.AudioPath, Is.EqualTo("audio.mp3"));
            Assert.That(beatmap.TimingPoints, Has.Count.EqualTo(3));
            Assert.That(beatmap.TimingPoints[0].BeatLengthMilliseconds, Is.EqualTo(500));
            Assert.That(beatmap.TimingPoints[1].Uninherited, Is.False);
            Assert.That(beatmap.TimingPoints[2].BeatLengthMilliseconds, Is.EqualTo(400));
            Assert.That(beatmap.HitObjects, Has.Count.EqualTo(3));
            Assert.That(beatmap.HitObjects[0].Lane, Is.EqualTo(0));
            Assert.That(beatmap.HitObjects[0].Kind, Is.EqualTo(HitObjectKind.Tap));
            Assert.That(beatmap.HitObjects[1].Lane, Is.EqualTo(1));
            Assert.That(beatmap.HitObjects[1].Kind, Is.EqualTo(HitObjectKind.Hold));
            Assert.That(beatmap.HitObjects[1].EndTimeMilliseconds, Is.EqualTo(1750));
            Assert.That(beatmap.HitObjects[2].Lane, Is.EqualTo(3));
        }

        [Test]
        public void ExportsEditableBeatmapAsReadableOsuMania()
        {
            EditableBeatmap editable = EditableBeatmap.FromBeatmap(OsuManiaBeatmapIO.ReadBeatmap(sampleOsu));

            string exported = OsuManiaBeatmapIO.WriteEditableToString(editable);
            var reparsed = OsuManiaBeatmapIO.ReadBeatmap(exported);

            Assert.That(exported, Does.Contain("Mode: 3"));
            Assert.That(exported, Does.Contain("CircleSize:4"));
            Assert.That(reparsed.KeyMode, Is.EqualTo(KeyMode.FourKey));
            Assert.That(reparsed.HitObjects, Has.Count.EqualTo(3));
            Assert.That(reparsed.HitObjects[1].Kind, Is.EqualTo(HitObjectKind.Hold));
            Assert.That(reparsed.HitObjects[1].EndTimeMilliseconds, Is.EqualTo(1750));
            Assert.That(reparsed.TimingPoints, Is.EqualTo(editable.TimingPoints));
        }

        [Test]
        public void ReadEditableFromFileResolvesAdjacentAudio()
        {
            string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "osu-import", TestContext.CurrentContext.Test.ID);
            Directory.CreateDirectory(directory);
            string audioPath = Path.Combine(directory, "audio.mp3");
            string beatmapPath = Path.Combine(directory, "chart.osu");

            File.WriteAllBytes(audioPath, []);
            File.WriteAllText(beatmapPath, sampleOsu);

            EditableBeatmap editable = OsuManiaBeatmapIO.ReadEditableFromFile(beatmapPath);

            Assert.That(editable.AudioPath, Is.EqualTo(audioPath));
        }

        [Test]
        public void ExportWritesAudioFilenameForAbsoluteAudioPath()
        {
            EditableBeatmap editable = EditableBeatmap.FromBeatmap(OsuManiaBeatmapIO.ReadBeatmap(sampleOsu));
            editable.AudioPath = Path.Combine("C:", "songs", "audio.mp3");

            string exported = OsuManiaBeatmapIO.WriteEditableToString(editable);

            Assert.That(exported, Does.Contain("AudioFilename: audio.mp3"));
        }

        private const string sampleOsu = """
osu file format v14

[General]
AudioFilename: audio.mp3
Mode: 3

[Metadata]
Title:Test Song
Artist:Test Artist
Creator:Mapper
Version:4K

[Difficulty]
CircleSize:4
OverallDifficulty:8

[TimingPoints]
0,500,4,2,0,100,1,0
1000,-50,4,2,0,80,0,1
2000,400,3,2,1,70,1,0

[HitObjects]
64,192,1000,1,0,0:0:0:0:
192,192,1500,128,0,1750:0:0:0:0:
448,192,2000,1,0,0:0:0:0:
""";
    }
}
