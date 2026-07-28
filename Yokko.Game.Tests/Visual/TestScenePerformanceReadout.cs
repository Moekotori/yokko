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
                  + YokkoPerformanceReadout.AccentOffset
                  && readout.Height
                  == YokkoPerformanceReadout.CardHeight
                  + YokkoPerformanceReadout.AccentOffset);
        AddStep("provide 480 fps frame", () =>
            source.SetFrame(1, 1000.0 / 480));
        AddUntilStep(
            "shows real frame time",
            () => readout.DisplayedFrameTime == "2.1 ms");
        AddAssert(
            "shows matching fps",
            () => readout.DisplayedFramesPerSecond == "480 FPS");
        AddUntilStep(
            "normal frame is not an alert",
            () => readout.StutterBarCount == 0);
    }

    private sealed class FakeFrameTimingSource : IFrameTimingSource
    {
        private double marker;
        private double frameTime;

        public bool TryRead(
            out double frameMarker,
            out double frameTimeMilliseconds)
        {
            frameMarker = marker;
            frameTimeMilliseconds = frameTime;
            return marker > 0;
        }

        public void SetFrame(
            double frameMarker,
            double frameTimeMilliseconds)
        {
            marker = frameMarker;
            frameTime = frameTimeMilliseconds;
        }
    }
}
