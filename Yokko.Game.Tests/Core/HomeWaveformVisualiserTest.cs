using NUnit.Framework;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class HomeWaveformVisualiserTest
{
    [Test]
    public void SampleChannelInterpolatesBetweenPoints()
    {
        float[] channel = { 0f, 1f, 0.5f, 0.25f };

        Assert.Multiple(() =>
        {
            Assert.That(
                HomeWaveformVisualiser.SampleChannel(channel, 0),
                Is.EqualTo(0f).Within(0.0001f));
            Assert.That(
                HomeWaveformVisualiser.SampleChannel(channel, 1),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                HomeWaveformVisualiser.SampleChannel(channel, 1.5),
                Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(
                HomeWaveformVisualiser.SampleChannel(channel, 3),
                Is.EqualTo(0.25f).Within(0.0001f));
        });
    }

    [Test]
    public void SampleChannelFlattensOutsideTrackBounds()
    {
        float[] channel = { 0.4f, 0.9f };

        Assert.Multiple(() =>
        {
            Assert.That(HomeWaveformVisualiser.SampleChannel(channel, -0.5), Is.EqualTo(0f));
            Assert.That(HomeWaveformVisualiser.SampleChannel(channel, 1.5), Is.EqualTo(0f));
            Assert.That(HomeWaveformVisualiser.SampleChannel(null, 0), Is.EqualTo(0f));
            Assert.That(
                HomeWaveformVisualiser.SampleChannel(System.Array.Empty<float>(), 0),
                Is.EqualTo(0f));
        });
    }

    [Test]
    public void ObstacleMaxHeightIsClamped()
    {
        var visualiser = new HomeWaveformVisualiser();

        visualiser.SetObstacles((0, 100, 5), (200, 300, 999));

        Assert.Multiple(() =>
        {
            Assert.That(visualiser.Obstacles[0].MaxHeight, Is.EqualTo(24));
            Assert.That(visualiser.Obstacles[1].MaxHeight, Is.EqualTo(110));
        });
    }

    [Test]
    public void HeightLimitRespectsObstacleIntervals()
    {
        var visualiser = new HomeWaveformVisualiser();
        visualiser.SetObstacles((100, 200, 40), (150, 400, 60));

        HomeWaveformVisualiser.WaveformObstacle[] obstacles = visualiser.Obstacles;

        Assert.Multiple(() =>
        {
            // 区间之外回到全局上限。
            Assert.That(
                HomeWaveformVisualiser.HeightLimitAt(50, obstacles),
                Is.EqualTo(110));
            // 单一区间内取该区间的限高。
            Assert.That(
                HomeWaveformVisualiser.HeightLimitAt(120, obstacles),
                Is.EqualTo(40));
            Assert.That(
                HomeWaveformVisualiser.HeightLimitAt(300, obstacles),
                Is.EqualTo(60));
            // 区间重叠时取最严格的限高。
            Assert.That(
                HomeWaveformVisualiser.HeightLimitAt(180, obstacles),
                Is.EqualTo(40));
        });
    }

    [Test]
    public void CapsuleSegmentsSplitBarIntoCapsAndBody()
    {
        Assert.Multiple(() =>
        {
            // 常规柱：柱帽高为柱宽一半，柱身补足剩余高度。
            (float capHeight, float bodyHeight) =
                HomeWaveformVisualiser.CapsuleSegments(6, 40);
            Assert.That(capHeight, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(bodyHeight, Is.EqualTo(34f).Within(0.0001f));
            Assert.That(capHeight * 2 + bodyHeight, Is.EqualTo(40f).Within(0.0001f));

            // 柱高恰为一个整圆：柱身收为 0。
            (capHeight, bodyHeight) = HomeWaveformVisualiser.CapsuleSegments(6, 6);
            Assert.That(capHeight, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(bodyHeight, Is.EqualTo(0f));

            // 极矮柱（待机平线）：柱帽压扁成椭圆的一半，柱身不出现负值。
            (capHeight, bodyHeight) = HomeWaveformVisualiser.CapsuleSegments(6, 3);
            Assert.That(capHeight, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(bodyHeight, Is.EqualTo(0f));
        });
    }

    [Test]
    public void BarWidthKeepsPrintSharpness()
    {
        Assert.Multiple(() =>
        {
            // 柱宽取柱距的 0.4（1920 宽舞台：柱距 15 → 柱宽 6）。
            Assert.That(
                HomeWaveformVisualiser.BarWidthForPitch(15),
                Is.EqualTo(6f).Within(0.0001f));
            // 窄舞台下保底 2.5，避免柱子细到消失。
            Assert.That(
                HomeWaveformVisualiser.BarWidthForPitch(4),
                Is.EqualTo(2.5f).Within(0.0001f));
        });
    }

    [Test]
    public void WaveformBandSpansFullStageWidth()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                MainScreen.CalculateWaveformWidth(new osuTK.Vector2(1280, 720)),
                Is.EqualTo(1280));
            Assert.That(
                MainScreen.CalculateWaveformWidth(new osuTK.Vector2(2133, 1303)),
                Is.EqualTo(2133));
        });
    }
}
