using NUnit.Framework;
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

[HitObjects]
64,192,1000,1,0,0:0:0:0:
192,192,1500,128,0,1750:0:0:0:0:
448,192,2000,1,0,0:0:0:0:
""";
    }
}
