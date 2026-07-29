using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Core.Timing;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class OsuStandardPatternGeneratorTest
{
    [Test]
    public void FastStreamUsesLazerStairWithoutStacking()
    {
        ManiaConversionSource source = createSource(
            circle(200, 1000),
            circle(260, 1090));

        IReadOnlyList<YokkoHitObject> converted =
            OsuStandardManiaConverter.Convert(
                source,
                6,
                [YokkoTimingPoint.Default]);

        Assert.Multiple(() =>
        {
            Assert.That(converted, Has.Count.EqualTo(2));
            Assert.That(
                converted[1].Lane,
                Is.EqualTo((converted[0].Lane + 1) % 6));
        });
    }

    [Test]
    public void SamePositionStreamCyclesAcrossStage()
    {
        ManiaConversionSource source = createSource(
            circle(200, 1000),
            circle(200, 1130));

        IReadOnlyList<YokkoHitObject> converted =
            OsuStandardManiaConverter.Convert(source, 6);

        Assert.That(
            converted[1].Lane,
            Is.EqualTo(5 - converted[0].Lane));
    }

    [Test]
    public void ClapAndFinishGenerateLegacyChords()
    {
        IReadOnlyList<YokkoHitObject> clap =
            OsuStandardManiaConverter.Convert(
                createSource(circle(200, 1000, hitSound: 8)),
                7);
        IReadOnlyList<YokkoHitObject> finish =
            OsuStandardManiaConverter.Convert(
                createSource(circle(200, 1000, hitSound: 4)),
                7);

        Assert.Multiple(() =>
        {
            Assert.That(clap.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(
                finish.Any(left => finish.Any(right =>
                    right.Lane == 6 - left.Lane
                    && right.Lane != left.Lane)),
                Is.True);
        });
    }

    [Test]
    public void MediumRepeatSliderBecomesLazerStair()
    {
        ManiaConversionSource source = createSource(
            new ManiaConversionHitObject(
                200,
                1000,
                1450,
                ManiaConversionObjectKind.Slider,
                SpanCount: 3));

        IReadOnlyList<YokkoHitObject> converted =
            OsuStandardManiaConverter.Convert(source, 7);

        Assert.Multiple(() =>
        {
            Assert.That(converted, Has.Count.EqualTo(4));
            Assert.That(
                converted.Select(hitObject =>
                    hitObject.StartTimeMilliseconds),
                Is.EqualTo(new[] { 1000, 1150, 1300, 1450 }));
            Assert.That(
                converted.All(hitObject =>
                    hitObject.Kind == HitObjectKind.Tap),
                Is.True);
        });
    }

    [Test]
    public void KiaiSliderHeadSampleForcesLegacyChord()
    {
        ManiaConversionSource source = createSource(
            new ManiaConversionHitObject(
                200,
                1000,
                1500,
                ManiaConversionObjectKind.Slider,
                SpanCount: 1,
                NodeHitSounds: [8, 0]));
        var kiai = new YokkoTimingPoint(
            0,
            500,
            Effects: 1);

        IReadOnlyList<YokkoHitObject> converted =
            OsuStandardManiaConverter.Convert(
                source,
                7,
                [kiai]);

        Assert.That(converted.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(
            converted.All(hitObject =>
                hitObject.Kind == HitObjectKind.Hold),
            Is.True);
    }

    [Test]
    public void ShortFinishSpinnerUsesSpecialColumnInEightKey()
    {
        ManiaConversionSource source = createSource(
            new ManiaConversionHitObject(
                256,
                1000,
                1500,
                ManiaConversionObjectKind.Spinner,
                HitSound: 4));

        IReadOnlyList<YokkoHitObject> converted =
            OsuStandardManiaConverter.Convert(source, 8);

        Assert.That(converted, Has.Count.EqualTo(1));
        Assert.That(converted[0].Lane, Is.Zero);
        Assert.That(converted[0].Kind, Is.EqualTo(HitObjectKind.Hold));
    }

    [Test]
    public void ConversionIsDeterministicForScoreIdentity()
    {
        ManiaConversionSource source = createSource(
            Enumerable.Range(0, 20)
                .Select(index => circle(
                    index % 2 == 0 ? 96 : 416,
                    1000 + index * 137,
                    index % 3 == 0 ? 8 : 0))
                .ToArray());

        IReadOnlyList<YokkoHitObject> first =
            OsuStandardManiaConverter.Convert(source, 7);
        IReadOnlyList<YokkoHitObject> second =
            OsuStandardManiaConverter.Convert(source, 7);

        Assert.That(second, Is.EqualTo(first));
    }

    private static ManiaConversionHitObject circle(
        double x,
        double time,
        int hitSound = 0) =>
        new(
            x,
            time,
            time,
            ManiaConversionObjectKind.Circle,
            hitSound);

    private static ManiaConversionSource createSource(
        params ManiaConversionHitObject[] hitObjects) =>
        new(
            4,
            8,
            9,
            6,
            hitObjects);
}
