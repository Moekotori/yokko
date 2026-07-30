using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Game.Gameplay;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class GameplaySlidingSampleIndexTest
{
    [Test]
    public void IndexContainsOnlySlidingObjectsForRequestedLane()
    {
        YokkoHitSamplePayload sliding = new(PlaySlidingSamples: true);
        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
        {
            HitObjects =
            [
                new YokkoHitObject(
                    0,
                    100,
                    null,
                    HitObjectKind.Tap),
                new YokkoHitObject(
                    0,
                    200,
                    400,
                    HitObjectKind.Hold,
                    SamplePayload: sliding),
                new YokkoHitObject(
                    2,
                    300,
                    500,
                    HitObjectKind.Hold,
                    SamplePayload: sliding),
                new YokkoHitObject(
                    0,
                    600,
                    800,
                    HitObjectKind.Hold,
                    SamplePayload: sliding),
            ],
        };

        var index = new GameplaySlidingSampleIndex(beatmap, 4);

        Assert.Multiple(() =>
        {
            Assert.That(index.GetObjectIndices(0), Is.EqualTo(new[] { 1, 3 }));
            Assert.That(index.GetObjectIndices(1), Is.Empty);
            Assert.That(index.GetObjectIndices(2), Is.EqualTo(new[] { 2 }));
            Assert.That(index.GetObjectIndices(4), Is.Empty);
        });
    }

    [Test]
    public void TapOnlyBeatmapHasNoPerInputCandidates()
    {
        YokkoHitObject[] hitObjects = new YokkoHitObject[10_000];
        for (int objectIndex = 0; objectIndex < hitObjects.Length; objectIndex++)
        {
            hitObjects[objectIndex] = new YokkoHitObject(
                objectIndex % 4,
                objectIndex * 10,
                null,
                HitObjectKind.Tap);
        }

        YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
        {
            HitObjects = hitObjects,
        };
        var index = new GameplaySlidingSampleIndex(beatmap, 4);

        for (int lane = 0; lane < 4; lane++)
            Assert.That(index.GetObjectIndices(lane), Is.Empty);
    }
}
