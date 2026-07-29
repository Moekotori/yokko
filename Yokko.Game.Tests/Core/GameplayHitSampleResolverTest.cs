using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Game.Gameplay;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class GameplayHitSampleResolverTest
{
    private string directory;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "hit-sample-resolver",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "audio.wav"), [0]);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    [Test]
    public void CustomBankSuffixAndVolumeFollowLazerLookupOrder()
    {
        string expected = Path.Combine(directory, "soft-hitclap2.ogg");
        File.WriteAllBytes(expected, [0]);
        YokkoHitObject hitObject = tap(
            new YokkoHitSample(
                YokkoHitSample.HitClap,
                YokkoHitSample.BankSoft,
                Volume: 40,
                CustomSampleBank: 2));
        var resolver = new GameplayHitSampleResolver(
            beatmap(ChartSourceFormat.OsuMania, hitObject));

        ResolvedGameplayHitSample resolved =
            resolver.ResolveHead(hitObject).Single();

        Assert.Multiple(() =>
        {
            Assert.That(resolved.Path, Is.EqualTo(expected));
            Assert.That(resolved.Gain, Is.EqualTo(0.4).Within(0.0001));
        });
    }

    [Test]
    public void NativeManiaSuppressesOnlyLayeredNormalSample()
    {
        File.WriteAllBytes(
            Path.Combine(directory, "normal-hitnormal.wav"),
            [0]);
        File.WriteAllBytes(
            Path.Combine(directory, "normal-hitclap.wav"),
            [0]);
        YokkoHitObject hitObject = tap(
            new YokkoHitSample(
                YokkoHitSample.HitNormal,
                IsLayered: true),
            new YokkoHitSample(YokkoHitSample.HitClap));

        var native = new GameplayHitSampleResolver(
            beatmap(ChartSourceFormat.OsuMania, hitObject));
        var converted = new GameplayHitSampleResolver(
            beatmap(ChartSourceFormat.OsuStandard, hitObject));

        Assert.Multiple(() =>
        {
            Assert.That(native.ResolveHead(hitObject), Has.Count.EqualTo(1));
            Assert.That(converted.ResolveHead(hitObject), Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void HoldHeadAndTailUseTheirOwnNodeSamples()
    {
        string headPath = Path.Combine(directory, "normal-hitnormal.wav");
        string tailPath = Path.Combine(directory, "drum-hitfinish.wav");
        File.WriteAllBytes(headPath, [0]);
        File.WriteAllBytes(tailPath, [0]);
        var head = new YokkoHitSample(YokkoHitSample.HitNormal);
        var tail = new YokkoHitSample(
            YokkoHitSample.HitFinish,
            YokkoHitSample.BankDrum);
        var hitObject = new YokkoHitObject(
            0,
            1000,
            2000,
            HitObjectKind.Hold,
            SamplePayload: new YokkoHitSamplePayload(
                [head],
                [
                    [head],
                    [tail],
                ]));
        var resolver = new GameplayHitSampleResolver(
            beatmap(ChartSourceFormat.OsuMania, hitObject));

        Assert.Multiple(() =>
        {
            Assert.That(
                resolver.ResolveHead(hitObject).Single().Path,
                Is.EqualTo(headPath));
            Assert.That(
                resolver.ResolveTail(hitObject).Single().Path,
                Is.EqualTo(tailPath));
        });
    }

    [Test]
    public void ConvertedSliderHoldUsesLazerSlidingSampleNames()
    {
        string slidePath = Path.Combine(
            directory,
            "soft-sliderslide2.wav");
        string whistlePath = Path.Combine(
            directory,
            "drum-sliderwhistle.wav");
        File.WriteAllBytes(slidePath, [0]);
        File.WriteAllBytes(whistlePath, [0]);
        var hitObject = new YokkoHitObject(
            0,
            1000,
            2000,
            HitObjectKind.Hold,
            SamplePayload: new YokkoHitSamplePayload(
                [
                    new YokkoHitSample(
                        YokkoHitSample.HitNormal,
                        YokkoHitSample.BankSoft,
                        Volume: 35,
                        CustomSampleBank: 2),
                    new YokkoHitSample(
                        YokkoHitSample.HitWhistle,
                        YokkoHitSample.BankDrum),
                    new YokkoHitSample(YokkoHitSample.HitClap),
                ],
                PlaySlidingSamples: true));
        var resolver = new GameplayHitSampleResolver(
            beatmap(ChartSourceFormat.OsuStandard, hitObject));

        ResolvedGameplayHitSample[] sliding =
            resolver.ResolveSliding(hitObject).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(sliding, Has.Length.EqualTo(2));
            Assert.That(sliding[0].Path, Is.EqualTo(slidePath));
            Assert.That(sliding[0].Gain, Is.EqualTo(0.35).Within(0.0001));
            Assert.That(sliding[1].Path, Is.EqualTo(whistlePath));
        });
    }

    private YokkoBeatmap beatmap(
        ChartSourceFormat sourceFormat,
        YokkoHitObject hitObject) =>
        DemoBeatmaps.CreateFourKeyDemo() with
        {
            SourceFormat = sourceFormat,
            AudioPath = Path.Combine(directory, "audio.wav"),
            HitObjects = [hitObject],
        };

    private static YokkoHitObject tap(
        params YokkoHitSample[] samples) =>
        new(
            0,
            1000,
            null,
            HitObjectKind.Tap,
            SamplePayload: new YokkoHitSamplePayload(samples));
}
