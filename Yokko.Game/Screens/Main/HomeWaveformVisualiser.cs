using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;
using Yokko.Audio;

namespace Yokko.Game.Screens.Main;

/// <summary>
/// 主页贴底的实时频谱可视化条：一排胶囊圆头立柱紧贴舞台底边向上生长，
/// 随主页播放器当前歌曲的低/中/高频能量实时跳动。数据来自
/// <see cref="AudioWaveformAnalyzer"/> 对真实音频的离线分析，按播放进度逐帧取样。
/// 立柱经过悬浮卡片下方时自动收低，看起来像从卡片背后穿过。
/// 外观与主页面可爱贴纸风一致：奶油白的淡青细柱，间以 pastel 粉/黄点缀柱。
/// </summary>
public partial class HomeWaveformVisualiser : CompositeDrawable
{
    /// <summary>
    /// 离线分析的采样点数。点数决定跳动的时间分辨率（约每 90ms 一个采样）。
    /// </summary>
    public const int AnalysisPointCount = 4096;

    internal const float BandHeight = 120;

    private const int bar_count = 128;
    private const float min_bar = 3;
    private const float max_bar = 110;

    // 主色柱：淡青柱身 → 近白柱顶，像奶油条一样轻盈，与青底融为一体。
    private static readonly ColourInfo primaryGradient = ColourInfo.GradientVertical(
        new Color4(1f, 1f, 1f, 0.9f),
        new Color4(HomeControlColours.PaleCyan.R, HomeControlColours.PaleCyan.G, HomeControlColours.PaleCyan.B, 0.6f));

    // 点缀柱：pastel 粉/黄，与页面纸屑、星星装饰同色系但更淡。
    private static readonly ColourInfo pinkGradient = ColourInfo.GradientVertical(
        new Color4(1f, 0.88f, 0.95f, 0.92f),
        new Color4(1f, 0.6f, 0.8f, 0.68f));
    private static readonly ColourInfo yellowGradient = ColourInfo.GradientVertical(
        new Color4(1f, 0.99f, 0.9f, 0.92f),
        new Color4(1f, 0.94f, 0.62f, 0.7f));

    private static readonly Color4 idleColour =
        new(1f, 1f, 1f, 0.4f);

    private readonly Container[] bars = new Container[bar_count];
    private readonly ColourInfo[] gradients = new ColourInfo[bar_count];
    private readonly float[] lowWeight = new float[bar_count];
    private readonly float[] midWeight = new float[bar_count];
    private readonly float[] highWeight = new float[bar_count];
    private readonly float[] jitter = new float[bar_count];

    private float[] peaks;
    private float[] lows;
    private float[] mids;
    private float[] highs;
    private double durationMilliseconds;
    private double progressMilliseconds;
    private WaveformObstacle[] obstacles = [];

    public HomeWaveformVisualiser()
    {
        Anchor = Anchor.BottomLeft;
        Origin = Anchor.BottomLeft;
        Height = BandHeight;

        for (int i = 0; i < bar_count; i++)
        {
            float f = i / (float)(bar_count - 1);
            lowWeight[i] = gaussian(f, 0.16f, 0.19f);
            midWeight[i] = gaussian(f, 0.5f, 0.17f);
            highWeight[i] = gaussian(f, 0.86f, 0.2f);
            jitter[i] = 0.8f + 0.45f * frac(MathF.Sin(i * 12.9898f) * 43758.5453f);
            gradients[i] = accentGradient(i);

            // 胶囊圆头柱：遮罩容器按柱宽的一半取圆角，颜色由内部填充盒承载渐变。
            AddInternal(bars[i] = new Container
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Masking = true,
                Size = new Vector2(4, min_bar),
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = idleColour,
                },
            });
        }
    }

    /// <summary>
    /// 每 16 柱点缀一根粉/黄柱，与页面纸屑装饰同节奏。
    /// </summary>
    private static ColourInfo accentGradient(int index) =>
        (index % 16) switch
        {
            4 => pinkGradient,
            12 => yellowGradient,
            _ => primaryGradient,
        };

    /// <summary>
    /// 切换波形数据；传 null 回到待机的平线状态。
    /// </summary>
    internal void SetWaveform(AudioWaveformAnalysis analysis)
    {
        peaks = analysis?.Peaks;
        lows = analysis?.LowIntensity;
        mids = analysis?.MidIntensity;
        highs = analysis?.HighIntensity;
        durationMilliseconds = analysis?.DurationMilliseconds ?? 0;
    }

    /// <summary>
    /// 每帧由主页播放器喂入当前播放进度（毫秒）。
    /// </summary>
    internal void UpdatePlayback(double progress) => progressMilliseconds = progress;

    /// <summary>
    /// 登记悬浮在波形带上的卡片（舞台绝对坐标）：立柱在这些区间内收低，
    /// 避免盖到卡片上。maxHeight 已换算为该区间内允许的最大柱高。
    /// </summary>
    internal void SetObstacles(params (float StartX, float EndX, float MaxHeight)[] obstacles)
    {
        var converted = new WaveformObstacle[obstacles.Length];
        for (int i = 0; i < obstacles.Length; i++)
            converted[i] = new WaveformObstacle(
                obstacles[i].StartX,
                obstacles[i].EndX,
                Math.Clamp(obstacles[i].MaxHeight, 24, max_bar));

        this.obstacles = converted;
    }

    /// <summary>
    /// 当前生效的收低区间（测试可读）。
    /// </summary>
    internal WaveformObstacle[] Obstacles => obstacles;

    protected override void Update()
    {
        base.Update();

        if (DrawWidth <= 0)
            return;

        float pitch = DrawWidth / bar_count;
        // 细柱更显轻盈：柱宽取柱距的 0.42，圆头胶囊效果由柱宽一半圆角保证。
        float barWidth = MathF.Max(2.5f, pitch * 0.42f);
        bool hasWaveform = peaks != null && durationMilliseconds > 0;

        double centrePoint = hasWaveform
            ? Math.Clamp(progressMilliseconds / durationMilliseconds, 0, 1) * (peaks.Length - 1)
            : 0;

        float low = SampleChannel(lows, centrePoint);
        float mid = SampleChannel(mids, centrePoint);
        float high = SampleChannel(highs, centrePoint);
        float loudness = 0.35f + 0.65f * SampleChannel(peaks, centrePoint);

        float elapsed = (float)Clock.ElapsedFrameTime;
        float attack = 1f - MathF.Exp(-elapsed / 50);
        float release = 1f - MathF.Exp(-elapsed / 240);

        for (int i = 0; i < bar_count; i++)
        {
            Container bar = bars[i];
            bar.X = i * pitch + pitch / 2;
            bar.Width = barWidth;
            bar.CornerRadius = barWidth / 2;

            Box fill = (Box)bar.Child;

            if (!hasWaveform)
            {
                bar.Height = min_bar;
                fill.Colour = idleColour;
                continue;
            }

            fill.Colour = gradients[i];

            float cap = max_bar;
            foreach (WaveformObstacle obstacle in obstacles)
            {
                if (bar.X >= obstacle.StartX && bar.X <= obstacle.EndX)
                    cap = MathF.Min(cap, obstacle.MaxHeight);
            }

            float energy = (low * lowWeight[i] + mid * midWeight[i] + high * highWeight[i])
                           * loudness
                           * jitter[i];
            float target = min_bar + MathF.Pow(energy, 1.2f) * (cap - min_bar);
            float blend = target > bar.Height ? attack : release;
            bar.Height += (target - bar.Height) * blend;
        }
    }

    /// <summary>
    /// 在采样点数组上取带线性插值的瞬时值；越界（歌曲头尾之外）返回 0。
    /// </summary>
    internal static float SampleChannel(float[] channel, double position)
    {
        if (channel == null || channel.Length == 0 || position < 0 || position > channel.Length - 1)
            return 0;

        int lower = (int)position;
        int upper = Math.Min(lower + 1, channel.Length - 1);
        float fraction = (float)(position - lower);
        return channel[lower] + (channel[upper] - channel[lower]) * fraction;
    }

    private static float gaussian(float x, float centre, float width)
    {
        float d = (x - centre) / width;
        return MathF.Exp(-0.5f * d * d);
    }

    private static float frac(float value) => value - MathF.Floor(value);

    internal readonly struct WaveformObstacle(float startX, float endX, float maxHeight)
    {
        public readonly float StartX = startX;
        public readonly float EndX = endX;
        public readonly float MaxHeight = maxHeight;
    }
}
