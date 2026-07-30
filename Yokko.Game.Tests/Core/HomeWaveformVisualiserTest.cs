using NUnit.Framework;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class HomeWaveformVisualiserTest
{
    [Test]
    public void SamplePeakInterpolatesBetweenPoints()
    {
        float[] peaks = { 0f, 1f, 0.5f, 0.25f };

        Assert.Multiple(() =>
        {
            Assert.That(
                HomeWaveformVisualiser.SamplePeak(peaks, 0),
                Is.EqualTo(0f).Within(0.0001f));
            Assert.That(
                HomeWaveformVisualiser.SamplePeak(peaks, 1),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                HomeWaveformVisualiser.SamplePeak(peaks, 1.5),
                Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(
                HomeWaveformVisualiser.SamplePeak(peaks, 3),
                Is.EqualTo(0.25f).Within(0.0001f));
        });
    }

    [Test]
    public void SamplePeakFlattensOutsideTrackBounds()
    {
        float[] peaks = { 0.4f, 0.9f };

        Assert.Multiple(() =>
        {
            Assert.That(HomeWaveformVisualiser.SamplePeak(peaks, -0.5), Is.EqualTo(0f));
            Assert.That(HomeWaveformVisualiser.SamplePeak(peaks, 1.5), Is.EqualTo(0f));
            Assert.That(HomeWaveformVisualiser.SamplePeak(null, 0), Is.EqualTo(0f));
            Assert.That(
                HomeWaveformVisualiser.SamplePeak(System.Array.Empty<float>(), 0),
                Is.EqualTo(0f));
        });
    }

    [Test]
    public void WaveformBandClearsLeftColumnAndPlayer()
    {
        var compactStage = new osuTK.Vector2(1280, 720);

        Assert.Multiple(() =>
        {
            // 波形带左缘避开左下玩家卡片（X 72 + 宽 520 = 592）与键位试玩盘。
            Assert.That(HomeWaveformVisualiser.LeftEdge, Is.GreaterThan(72 + 520));
            Assert.That(
                MainScreen.CalculateWaveformWidth(compactStage),
                Is.EqualTo(1280 - HomeWaveformVisualiser.LeftEdge));
            // 播放器已上移：其底缘与波形带顶缘之间保留缝隙。
            float playerBottom = MainScreen.CalculateMusicPlayerY(compactStage) + 72;
            float bandTop = compactStage.Y
                            - HomeWaveformVisualiser.BottomMargin
                            - HomeWaveformVisualiser.BandHeight;
            Assert.That(playerBottom, Is.LessThan(bandTop));
        });
    }
}
