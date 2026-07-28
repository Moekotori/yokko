using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Yokko.Core.Beatmaps;
using Yokko.Core.Editing;
using Yokko.Core.Gameplay;
using Yokko.Core.Timing;

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
        public void AddedNoteUsesTimingPointActiveAtGridRow()
        {
            var source = new YokkoBeatmap(
                "Timing test",
                "Yokko",
                "Yokko",
                "4K",
                KeyMode.FourKey,
                ChartSourceFormat.Yokko,
                [
                    new YokkoTimingPoint(0, 500),
                    new YokkoTimingPoint(2000, 400),
                ],
                null,
                []);
            EditableBeatmap beatmap = EditableBeatmap.FromBeatmap(source);

            beatmap.ToggleNote(0, 17);

            Assert.That(beatmap.Notes.Single().StartTimeMilliseconds, Is.EqualTo(2100).Within(0.001));
        }

        [Test]
        public void PreservesScrollVelocityThroughEditingConversion()
        {
            var source = new YokkoBeatmap(
                "SV test",
                "Yokko",
                "Yokko",
                "4K",
                KeyMode.FourKey,
                ChartSourceFormat.Quaver,
                [YokkoTimingPoint.Default],
                null,
                [new YokkoHitObject(0, 1000, null, HitObjectKind.Tap)],
                ScrollVelocities:
                [
                    new YokkoScrollVelocity(500, 0),
                    new YokkoScrollVelocity(750, -1.5),
                ],
                InitialScrollVelocity: 1.25,
                ScrollSpeedFactors:
                [
                    new YokkoScrollSpeedFactor(250, 0.75),
                    new YokkoScrollSpeedFactor(1000, 1.5),
                ],
                ScrollProfiles: new Dictionary<string, YokkoScrollProfile>
                {
                    ["Reverse"] = new YokkoScrollProfile(
                        -1,
                        [new YokkoScrollVelocity(500, -2)],
                        [new YokkoScrollSpeedFactor(250, 1.25)]),
                })
            {
                HitObjects =
                [
                    new YokkoHitObject(
                        0,
                        1000,
                        null,
                        HitObjectKind.Tap,
                        ScrollProfileId: "Reverse"),
                ],
            };

            YokkoBeatmap playable =
                EditableBeatmap.FromBeatmap(source).ToBeatmap();

            Assert.That(playable.InitialScrollVelocity, Is.EqualTo(1.25));
            Assert.That(playable.ScrollVelocities, Is.EqualTo(source.ScrollVelocities));
            Assert.That(playable.ScrollSpeedFactors, Is.EqualTo(
                source.ScrollSpeedFactors));
            Assert.That(playable.ScrollProfiles, Is.EqualTo(
                source.ScrollProfiles));
            Assert.That(playable.HitObjects.Single().ScrollProfileId,
                Is.EqualTo("Reverse"));
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

        [Test]
        public void TimelineViewportZoomKeepsWindowInRange()
        {
            var viewport = new TimelineViewport(32, 24);

            viewport.SetVisibleRows(12, 64);

            Assert.That(viewport.VisibleRows, Is.EqualTo(12));
            Assert.That(viewport.StartRow, Is.EqualTo(38));

            viewport.SetVisibleRows(80, 64);

            Assert.That(viewport.VisibleRows, Is.EqualTo(80));
            Assert.That(viewport.StartRow, Is.EqualTo(0));
        }
    }
}
