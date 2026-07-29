using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using Yokko.Game.Presentation;

namespace Yokko.Game.Tests.Visual;

[TestFixture]
public partial class TestScenePerformanceReadout : YokkoTestScene
{
    private readonly FakeFrameTimingSource source = new();
    private readonly YokkoPerformanceReadout readout;

    public TestScenePerformanceReadout()
    {
        Add(readout = new YokkoPerformanceReadout(source)
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
        });
    }

    [Test]
    public void TestCompactLayoutAndLiveValues()
    {
        AddAssert(
            "compact target size",
            () => readout.Width
                  == YokkoPerformanceReadout.CardWidth
                  && readout.Height
                  == YokkoPerformanceReadout.CardHeight);
        AddStep("provide healthy frame data", () =>
            source.SetFrame(
                1000.0 / 480,
                1,
                1000,
                480,
                1000));
        AddUntilStep(
            "shows update frame time",
            () => readout.DisplayedUpdateFrameTime == "1.0 ms");
        AddAssert(
            "shows draw fps",
            () => readout.DisplayedDrawFramesPerSecond == "480");
        AddAssert(
            "shows input rate",
            () => readout.DisplayedInputRate == "1000");
        AddAssert(
            "healthy pacing",
            () => readout.DisplayedHealth
                  == FramePacingHealth.Stable);

        AddStep("provide repeated frame spikes", () =>
            source.SetFrame(
                20,
                20,
                1000,
                480,
                1000));
        AddUntilStep(
            "spikes become critical",
            () => readout.DisplayedHealth
                  == FramePacingHealth.Critical);
    }

    private sealed class FakeFrameTimingSource : IFrameTimingSource
    {
        private double marker;
        private FrameTimingSample sample;
        private bool enabled;

        public bool TryRead(out FrameTimingSample frameTimingSample)
        {
            if (!enabled)
            {
                frameTimingSample = default;
                return false;
            }

            marker++;
            frameTimingSample = sample with
            {
                DrawMarker = marker,
            };
            return true;
        }

        public void SetFrame(
            double drawFrameTime,
            double updateFrameTime,
            double inputFramesPerSecond,
            double targetDrawFramesPerSecond,
            double targetUpdateFramesPerSecond)
        {
            sample = new FrameTimingSample(
                marker,
                drawFrameTime,
                updateFrameTime,
                inputFramesPerSecond,
                targetDrawFramesPerSecond,
                targetUpdateFramesPerSecond);
            enabled = true;
        }
    }
}
