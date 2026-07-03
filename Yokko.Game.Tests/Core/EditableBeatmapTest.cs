using NUnit.Framework;
using Yokko.Core.Editing;
using Yokko.Core.Gameplay;

namespace Yokko.Game.Tests.Core
{
    [TestFixture]
    public sealed class EditableBeatmapTest
    {
        [Test]
        public void ToggleNoteAddsAndRemovesNote()
        {
            EditableBeatmap beatmap = EditableBeatmap.Create(KeyMode.FourKey);

            beatmap.ToggleNote(1, 8);
            Assert.That(beatmap.HasNoteAt(1, 8), Is.True);
            Assert.That(beatmap.Notes, Has.Count.EqualTo(1));

            beatmap.ToggleNote(1, 8);
            Assert.That(beatmap.HasNoteAt(1, 8), Is.False);
            Assert.That(beatmap.Notes, Is.Empty);
        }

        [Test]
        public void ConvertsToPlayableBeatmapInTimeOrder()
        {
            EditableBeatmap beatmap = EditableBeatmap.Create(KeyMode.SevenKey);

            beatmap.ToggleNote(3, 8);
            beatmap.ToggleNote(1, 2);

            var playable = beatmap.ToBeatmap();

            Assert.That(playable.KeyMode, Is.EqualTo(KeyMode.SevenKey));
            Assert.That(playable.HitObjects, Has.Count.EqualTo(2));
            Assert.That(playable.HitObjects[0].Lane, Is.EqualTo(1));
            Assert.That(playable.HitObjects[0].StartTimeMilliseconds, Is.EqualTo(250));
            Assert.That(playable.HitObjects[1].Lane, Is.EqualTo(3));
            Assert.That(playable.HitObjects[1].StartTimeMilliseconds, Is.EqualTo(1000));
        }

        [Test]
        public void ToggleNoteExtendsRowsWhenChartingPastEnd()
        {
            EditableBeatmap beatmap = EditableBeatmap.Create(KeyMode.FourKey);

            beatmap.ToggleNote(0, 48);

            Assert.That(beatmap.Rows, Is.EqualTo(49));
            Assert.That(beatmap.HasNoteAt(0, 48), Is.True);
        }

        [Test]
        public void TimelineViewportClampsToAvailableRows()
        {
            var viewport = new TimelineViewport(0, 24);

            viewport.MoveByRows(100, 32);
            Assert.That(viewport.StartRow, Is.EqualTo(8));
            Assert.That(viewport.EndRowExclusive, Is.EqualTo(32));

            viewport.MoveByRows(-100, 32);
            Assert.That(viewport.StartRow, Is.EqualTo(0));
        }
    }
}
