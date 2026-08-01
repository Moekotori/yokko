using System.Linq;
using NUnit.Framework;
using Yokko.Core.Beatmaps;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class YokkoBeatmapFingerprintTest
{
    [Test]
    public void MovingAudioDoesNotOrphanReplayIdentity()
    {
        YokkoBeatmap original = DemoBeatmaps.CreateFourKeyDemo();
        YokkoBeatmap moved = original with
        {
            AudioPath = @"D:\another-library\song.ogg",
        };

        Assert.That(
            YokkoBeatmapFingerprint.Compute(moved),
            Is.EqualTo(YokkoBeatmapFingerprint.Compute(original)));
    }

    [Test]
    public void JudgementCriticalObjectChangeProducesNewIdentity()
    {
        YokkoBeatmap original = DemoBeatmaps.CreateFourKeyDemo();
        YokkoBeatmap changed = original with
        {
            HitObjects =
            [
                new YokkoHitObject(
                    original.HitObjects[0].Lane,
                    original.HitObjects[0].StartTimeMilliseconds + 1,
                    original.HitObjects[0].EndTimeMilliseconds,
                    original.HitObjects[0].Kind,
                    original.HitObjects[0].SampleKey,
                    original.HitObjects[0].ScrollProfileId,
                    original.HitObjects[0].SamplePayload),
                .. original.HitObjects.Skip(1),
            ],
        };

        Assert.That(
            YokkoBeatmapFingerprint.Compute(changed),
            Is.Not.EqualTo(YokkoBeatmapFingerprint.Compute(original)));
    }

    [Test]
    public void HitSamplePayloadParticipatesInExactReplayIdentity()
    {
        YokkoBeatmap original = DemoBeatmaps.CreateFourKeyDemo();
        YokkoHitObject first = original.HitObjects[0];
        YokkoBeatmap changed = original with
        {
            HitObjects =
            [
                new YokkoHitObject(
                    first.Lane,
                    first.StartTimeMilliseconds,
                    first.EndTimeMilliseconds,
                    first.Kind,
                    first.SampleKey,
                    first.ScrollProfileId,
                    new YokkoHitSamplePayload(
                        [new YokkoHitSample(
                            YokkoHitSample.HitClap)])),
                .. original.HitObjects.Skip(1),
            ],
        };

        Assert.That(
            YokkoBeatmapFingerprint.Compute(changed),
            Is.Not.EqualTo(YokkoBeatmapFingerprint.Compute(original)));
    }

    [Test]
    public void BmsScratchIdentityParticipatesInReplayFingerprint()
    {
        YokkoBeatmap ordinary = DemoBeatmaps.CreateFourKeyDemo() with
        {
            SourceFormat = ChartSourceFormat.Bms,
        };
        YokkoBeatmap scratch = ordinary with
        {
            ScratchLane = 0,
        };

        Assert.That(
            YokkoBeatmapFingerprint.Compute(scratch),
            Is.Not.EqualTo(YokkoBeatmapFingerprint.Compute(ordinary)));
    }

    [Test]
    public void ScheduledSampleBusParticipatesInReplayFingerprint()
    {
        YokkoBeatmap hitSound = DemoBeatmaps.CreateFourKeyDemo() with
        {
            ScheduledSamples =
            [
                new YokkoScheduledSample(500, "sample.wav"),
            ],
        };
        YokkoBeatmap music = hitSound with
        {
            ScheduledSamples =
            [
                new YokkoScheduledSample(
                    500,
                    "sample.wav",
                    UseMusicBus: true),
            ],
        };

        Assert.That(
            YokkoBeatmapFingerprint.Compute(music),
            Is.Not.EqualTo(YokkoBeatmapFingerprint.Compute(hitSound)));
    }
}
