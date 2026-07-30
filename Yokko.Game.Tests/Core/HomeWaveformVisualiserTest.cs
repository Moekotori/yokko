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
