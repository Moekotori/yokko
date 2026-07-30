using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;
using Yokko.Audio;

namespace Yokko.Game.Screens.Main;

/// <summary>
/// 主页贴底的实时频谱可视化条：一排藏青墨色的胶囊细柱紧贴舞台底边向上生长，
/// 随主页播放器当前歌曲的低/中/高频能量实时跳动，柱顶带白色峰值挂留块，
/// 像印刷海报上的录音室 VU 表——与背景装饰里的均衡器纹样同一语言。
/// 数据来自 <see cref="AudioWaveformAnalyzer"/> 对真实音频的离线分析，按播放进度逐帧取样。
/// 立柱经过悬浮卡片下方时自动收低，看起来像从卡片背后穿过。
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

    private const float cap_height = 4;
    private const float cap_gap = 3;
    private const float cap_fall_speed = 160;

    // 藏青墨色：与背景均衡器纹样、条码、框线相同的印刷油墨色。
    private static readonly Color4 inkColour =
        new(HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.85f);
    private static readonly Color4 pinkInk =
        new(HomeControlColours.Pink.R, HomeControlColours.Pink.G, HomeControlColours.Pink.B, 0.9f);
    private static readonly Color4 yellowInk =
        new(HomeControlColours.Yellow.R, HomeControlColours.Yellow.G, HomeControlColours.Yellow.B, 0.95f);
    private static readonly Color4 capColour = new(1f, 1f, 1f, 0.95f);
    private static readonly Color4 idleColour =
        new(HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.2f);

    private readonly Container[] bars = new Container[bar_count];
    private readonly Container[] caps = new Container[bar_count];
    private readonly float[] peaks = new float[bar_count];
    private readonly float[] lowWeight = new float[bar_count];
    private readonly float[] midWeight = new float[bar_count];
    private readonly float[] highWeight = new float[bar_count];
    private readonly float[] jitter = new float[bar_count];

    private float[] samples;
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

            Color4 ink = accentInk(i);
            peaks[i] = min_bar;

            // 柱身与峰值块都是胶囊形（圆角取柱宽一半），纯色平涂保持印刷感。
            AddInternal(bars[i] = new Container
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Masking = true,
                Size = new Vector2(3, min_bar),
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = idleColour,
                },
            });
            AddInternal(caps[i] = new Container
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Masking = true,
                Alpha = 0,
                Size = new Vector2(3, cap_height),
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ink == inkColour ? capColour : ink,
                },
            });
        }
    }

    /// <summary>
    /// 每 16 柱点缀一根粉/黄柱，与页面纸屑装饰同节奏。
    /// </summary>
    private static Color4 accentInk(int index) =>
        (index % 16) switch
        {
            4 => pinkInk,
            12 => yellowInk,
            _ => inkColour,
        };

    /// <summary>
    /// 切换波形数据；传 null 回到待机的平线状态。
    /// </summary>
    internal void SetWaveform(AudioWaveformAnalysis analysis)
    {
        samples = analysis?.Peaks;
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
        // 细柱才有印刷纹样的锐度：柱宽取柱距的 0.4，圆头胶囊由柱宽一半圆角保证。
        float barWidth = MathF.Max(2.5f, pitch * 0.4f);
        bool hasWaveform = samples != null && durationMilliseconds > 0;

        double centrePoint = hasWaveform
            ? Math.Clamp(progressMilliseconds / durationMilliseconds, 0, 1) * (samples.Length - 1)
            : 0;

        float low = SampleChannel(lows, centrePoint);
        float mid = SampleChannel(mids, centrePoint);
        float high = SampleChannel(highs, centrePoint);
        float loudness = 0.35f + 0.65f * SampleChannel(samples, centrePoint);

        float elapsed = (float)Clock.ElapsedFrameTime;
        float attack = 1f - MathF.Exp(-elapsed / 50);
        float release = 1f - MathF.Exp(-elapsed / 240);

        for (int i = 0; i < bar_count; i++)
        {
            Container bar = bars[i];
            Container cap = caps[i];
            bar.X = cap.X = i * pitch + pitch / 2;
            bar.Width = cap.Width = barWidth;
            bar.CornerRadius = cap.CornerRadius = barWidth / 2;

            Box fill = (Box)bar.Child;

            if (!hasWaveform)
            {
                bar.Height = min_bar;
                fill.Colour = idleColour;
                cap.Alpha = 0;
                peaks[i] = min_bar;
                continue;
            }

            fill.Colour = accentInk(i);

            float capMax = max_bar;
            foreach (WaveformObstacle obstacle in obstacles)
            {
                if (bar.X >= obstacle.StartX && bar.X <= obstacle.EndX)
                    capMax = MathF.Min(capMax, obstacle.MaxHeight);
            }

            float energy = (low * lowWeight[i] + mid * midWeight[i] + high * highWeight[i])
                           * loudness
                           * jitter[i];
            float target = min_bar + MathF.Pow(energy, 1.2f) * (capMax - min_bar);
            float blend = target > bar.Height ? attack : release;
            bar.Height += (target - bar.Height) * blend;

            // 峰值挂留：柱高冲过峰值时立即跟上，随后匀速下落，且不超过障碍物限高。
            peaks[i] = MathF.Max(bar.Height, peaks[i] - cap_fall_speed * (elapsed / 1000f));
            peaks[i] = MathF.Min(peaks[i], capMax);

            cap.Y = -(peaks[i] + cap_gap);
            cap.Alpha = peaks[i] > min_bar + 2 ? 1 : 0;
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
