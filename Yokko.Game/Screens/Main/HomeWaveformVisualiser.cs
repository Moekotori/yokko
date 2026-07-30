using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Audio;

namespace Yokko.Game.Screens.Main;

/// <summary>
/// 主页底部青色舞台上的滚动波形带：以播放头为中心，展示主页播放器当前歌曲的
/// 波形窗口。波形数据来自 <see cref="AudioWaveformAnalyzer"/> 对真实音频的离线分析，
/// 随播放进度逐帧滚动；左侧为已播放（暗）、右侧为未播放（亮），播放头用粉色标记。
/// </summary>
public partial class HomeWaveformVisualiser : CompositeDrawable
{
    /// <summary>
    /// 离线分析的采样点数。点数决定滚动窗口的时间分辨率。
    /// </summary>
    public const int AnalysisPointCount = 1024;

    /// <summary>
    /// 波形带左缘（设计坐标）。避开左下角玩家卡片（右缘 592）与键位试玩盘。
    /// </summary>
    internal const float LeftEdge = 610;

    internal const float BandHeight = 44;
    internal const float BottomMargin = 8;

    private const int bar_count = 108;
    private const float min_bar_half = 1.5f;
    private const float max_bar_half = 19f;

    private static readonly Color4 futureColour =
        new(HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.5f);
    private static readonly Color4 pastColour =
        new(HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.26f);

    private readonly Box[] bars = new Box[bar_count];
    private readonly Box playhead;
    private readonly SpriteText stateLabel;

    private float[] peaks;
    private double durationMilliseconds;
    private double progressMilliseconds;

    public HomeWaveformVisualiser()
    {
        Anchor = Anchor.BottomLeft;
        Origin = Anchor.BottomLeft;
        Height = BandHeight;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.16f),
            },
            stateLabel = new SpriteText
            {
                Position = new Vector2(2, -15),
                Text = "AUDIO WAVE // IDLE",
                Font = HomeTypography.Display(9),
                Spacing = new Vector2(1.5f, 0),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.55f),
            },
        };

        for (int i = 0; i < bar_count; i++)
        {
            AddInternal(bars[i] = new Box
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                Size = new Vector2(3, min_bar_half * 2),
                Colour = futureColour,
            });
        }

        AddInternal(playhead = new Box
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.Centre,
            RelativeSizeAxes = Axes.Y,
            Width = 2,
            Colour = HomeControlColours.Pink,
        });
    }

    /// <summary>
    /// 切换波形数据；传 null 回到待机的平线状态。
    /// </summary>
    internal void SetWaveform(AudioWaveformAnalysis analysis)
    {
        peaks = analysis?.Peaks;
        durationMilliseconds = analysis?.DurationMilliseconds ?? 0;
        stateLabel.Text = peaks == null
            ? "AUDIO WAVE // IDLE"
            : "AUDIO WAVE // LIVE";
    }

    /// <summary>
    /// 每帧由主页播放器喂入当前播放进度（毫秒）。
    /// </summary>
    internal void UpdatePlayback(double progress) => progressMilliseconds = progress;

    protected override void Update()
    {
        base.Update();

        if (DrawWidth <= 0)
            return;

        float pitch = DrawWidth / bar_count;
        float barWidth = MathF.Max(2, pitch * 0.45f);
        double centrePoint = peaks == null || durationMilliseconds <= 0
            ? 0
            : progressMilliseconds / durationMilliseconds * (peaks.Length - 1);
        float blend = 1f - MathF.Exp((float)(-Clock.ElapsedFrameTime / 70));

        for (int i = 0; i < bar_count; i++)
        {
            Box bar = bars[i];
            bar.X = i * pitch + pitch / 2;
            bar.Width = barWidth;

            float targetHalf = peaks == null
                ? min_bar_half
                : min_bar_half
                  + SamplePeak(peaks, centrePoint + i - bar_count / 2f) * max_bar_half;

            float half = bar.Height / 2 + (targetHalf - bar.Height / 2) * blend;
            bar.Height = half * 2;

            bar.Colour = i < bar_count / 2
                ? pastColour
                : i > bar_count / 2
                    ? futureColour
                    : HomeControlColours.Pink;
        }

        playhead.X = DrawWidth / 2f;
    }

    /// <summary>
    /// 在采样点数组上取带线性插值的峰值；越界（歌曲头尾之外）返回 0，
    /// 让波形窗口在接近头尾时自然收平。
    /// </summary>
    internal static float SamplePeak(float[] peaks, double position)
    {
        if (peaks == null || peaks.Length == 0 || position < 0 || position > peaks.Length - 1)
            return 0;

        int lower = (int)position;
        int upper = Math.Min(lower + 1, peaks.Length - 1);
        float fraction = (float)(position - lower);
        return peaks[lower] + (peaks[upper] - peaks[lower]) * fraction;
    }
}
