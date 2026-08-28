using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osuTK;
using osuTK.Graphics;
using SixLabors.ImageSharp.PixelFormats;
using Yokko.Audio;

namespace Yokko.Game.Screens.Main;

/// <summary>
/// 主页贴底的实时频谱可视化条：一排藏青墨色的胶囊细柱紧贴舞台底边向上生长，
/// 随主页播放器当前歌曲的低/中/高频能量实时跳动，
/// 像印刷海报上的录音室 VU 表——与背景装饰里的均衡器纹样同一语言。
/// 数据来自 <see cref="AudioWaveformAnalyzer"/> 对真实音频的离线分析，按播放进度逐帧取样。
/// 立柱经过悬浮卡片下方时自动收低，看起来像从卡片背后穿过。
/// </summary>
/// <remarks>
/// 渲染方式移植自 ppy/osu 的 LogoVisualisation
/// （osu.Game/Screens/Menu/LogoVisualisation.cs，master@48c4800e3a，MIT）：
/// 由单个 <see cref="WaveformBarDrawNode"/> 用共享顶点批一次画完全部柱子，
/// 替代原先 128 个 Masking Container 逐柱独立绘制、每柱各自 flush 一次批次的做法。
/// 圆角胶囊不再依赖逐柱 Masking，而是用共享的抗锯齿圆形纹理
/// 拼出「顶帽半圆 + 柱身 + 底帽半圆」三段四边形，整帧只有一次纹理绑定。
/// </remarks>
public partial class HomeWaveformVisualiser : Drawable, ITexturedShaderDrawable
{
    /// <summary>
    /// 离线分析的采样点数。点数决定跳动的时间分辨率（约每 90ms 一个采样）。
    /// </summary>
    public const int AnalysisPointCount = 4096;

    internal const float BandHeight = 120;

    private const int bar_count = 128;
    private const float min_bar = 3;
    private const float max_bar = 110;

    // 藏青墨色：与背景均衡器纹样、条码、框线相同的印刷油墨色。
    private static readonly Color4 inkColour =
        new(HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.85f);
    private static readonly Color4 pinkInk =
        new(HomeControlColours.Pink.R, HomeControlColours.Pink.G, HomeControlColours.Pink.B, 0.9f);
    private static readonly Color4 yellowInk =
        new(HomeControlColours.Yellow.R, HomeControlColours.Yellow.G, HomeControlColours.Yellow.B, 0.95f);
    private static readonly Color4 idleColour =
        new(HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.2f);

    private readonly float[] barX = new float[bar_count];
    private readonly float[] barHeights = new float[bar_count];
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
    private float layoutDrawWidth = -1;
    private float barWidth;
    private bool hasWaveform;

    public IShader TextureShader { get; private set; }

    private Texture barTexture;

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
            barHeights[i] = min_bar;
        }
    }

    [BackgroundDependencyLoader]
    private void load(IRenderer renderer, ShaderManager shaders)
    {
        TextureShader = shaders.Load(
            VertexShaderDescriptor.TEXTURE_2,
            FragmentShaderDescriptor.TEXTURE);
        barTexture = createCircleTexture(renderer);
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

        if (DrawWidth != layoutDrawWidth)
            updateBarLayout();

        hasWaveform = samples != null && durationMilliseconds > 0;

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
            if (!hasWaveform)
            {
                barHeights[i] = min_bar;
                continue;
            }

            float heightMax = HeightLimitAt(barX[i], obstacles);
            float energy = (low * lowWeight[i] + mid * midWeight[i] + high * highWeight[i])
                           * loudness
                           * jitter[i];
            float target = min_bar + MathF.Pow(energy, 1.2f) * (heightMax - min_bar);
            float blend = target > barHeights[i] ? attack : release;
            barHeights[i] += (target - barHeights[i]) * blend;
        }

        Invalidate(Invalidation.DrawNode);
    }

    protected override DrawNode CreateDrawNode() => new WaveformBarDrawNode(this);

    /// <summary>
    /// 柱条的横向排布只随可视化带宽度变化，缓存后仅在 <see cref="Drawable.DrawWidth"/>
    /// 改变时重算，避免每帧对 128 根柱子重复计算 X 与柱宽。
    /// </summary>
    private void updateBarLayout()
    {
        layoutDrawWidth = DrawWidth;
        float pitch = layoutDrawWidth / bar_count;
        barWidth = BarWidthForPitch(pitch);

        for (int i = 0; i < bar_count; i++)
            barX[i] = i * pitch + pitch / 2;
    }

    /// <summary>
    /// 细柱才有印刷纹样的锐度：柱宽取柱距的 0.4，圆头胶囊由半圆柱帽保证。
    /// </summary>
    internal static float BarWidthForPitch(float pitch) => MathF.Max(2.5f, pitch * 0.4f);

    /// <summary>
    /// 该横向位置允许的最大柱高：命中多个收低区间时取最严格的一个，
    /// 不在任何区间内则回到全局上限。
    /// </summary>
    internal static float HeightLimitAt(float barX, WaveformObstacle[] obstacles)
    {
        float limit = max_bar;
        foreach (WaveformObstacle obstacle in obstacles)
        {
            if (barX >= obstacle.StartX && barX <= obstacle.EndX)
                limit = MathF.Min(limit, obstacle.MaxHeight);
        }

        return limit;
    }

    /// <summary>
    /// 把一根胶囊柱拆成「半圆柱帽 + 直筒柱身」：柱帽高为柱宽一半；
    /// 柱高不足一个整圆时柱帽压扁、柱身收为 0，整柱退化成椭圆，
    /// 与原先 Masking 圆角在极矮柱上的收敛效果一致。
    /// </summary>
    internal static (float CapHeight, float BodyHeight) CapsuleSegments(float width, float height)
    {
        float capHeight = MathF.Min(width, height) / 2;
        return (capHeight, MathF.Max(0, height - 2 * capHeight));
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

    /// <summary>
    /// 生成柱帽与柱身共用的抗锯齿白色圆形纹理：内切圆留 1px 平滑过渡，
    /// 上下半各当半圆柱帽用，水平中线一条当柱身用（中线在圆内全宽不透明，
    /// 左右边缘自带与柱帽一致的抗锯齿），颜色由顶点色染出。
    /// </summary>
    private static Texture createCircleTexture(IRenderer renderer)
    {
        const int size = 64;
        const float radius = size / 2f - 1;

        var image = new SixLabors.ImageSharp.Image<Rgba32>(size, size);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - size / 2f;
                float dy = y + 0.5f - size / 2f;
                float distance = MathF.Sqrt(dx * dx + dy * dy);
                float alpha = Math.Clamp(radius - distance + 0.5f, 0f, 1f);
                image[x, y] = new Rgba32(1f, 1f, 1f, alpha);
            }
        }

        Texture texture = renderer.CreateTexture(size, size);
        texture.SetData(new TextureUpload(image));
        return texture;
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);
        barTexture?.Dispose();
    }

    internal readonly struct WaveformObstacle(float startX, float endX, float maxHeight)
    {
        public readonly float StartX = startX;
        public readonly float EndX = endX;
        public readonly float MaxHeight = maxHeight;
    }

    /// <summary>
    /// 把全部 128 根胶囊柱画进同一个顶点批：每柱最多三个四边形
    /// （顶帽、柱身、底帽）共用同一张圆形纹理，整帧一次绑定、一次提交，
    /// 消除逐柱 Masking Container 造成的批次中断。
    /// 结构参考 ppy/osu LogoVisualisation.VisualisationDrawNode（master@48c4800e3a）。
    /// </summary>
    private class WaveformBarDrawNode : TexturedShaderDrawNode
    {
        protected new HomeWaveformVisualiser Source => (HomeWaveformVisualiser)base.Source;

        private readonly float[] positions = new float[bar_count];
        private readonly float[] heights = new float[bar_count];
        private float width;
        private float bandHeight;
        private bool active;
        private Texture texture;

        private IVertexBatch<TexturedVertex2D> vertexBatch;

        public WaveformBarDrawNode(HomeWaveformVisualiser source)
            : base(source)
        {
        }

        public override void ApplyState()
        {
            base.ApplyState();

            texture = Source.barTexture;
            width = Source.barWidth;
            bandHeight = Source.DrawHeight;
            active = Source.hasWaveform;
            Source.barX.AsSpan().CopyTo(positions);
            Source.barHeights.AsSpan().CopyTo(heights);
        }

        protected override void Draw(IRenderer renderer)
        {
            base.Draw(renderer);

            if (texture?.Available != true || width <= 0)
                return;

            vertexBatch ??= renderer.CreateQuadBatch<TexturedVertex2D>(bar_count * 3, 2);

            BindTextureShader(renderer);

            float textureSize = texture.Width;
            // 三段纹理区域（纹素坐标）：上半圆、过圆心的一像素横条、下半圆。
            var topCapRegion = new RectangleF(0, 0, textureSize, textureSize / 2);
            var bodyRegion = new RectangleF(0, textureSize / 2 - 0.5f, textureSize, 1);
            var bottomCapRegion = new RectangleF(0, textureSize / 2, textureSize, textureSize / 2);

            ColourInfo ink = childColour(inkColour);
            ColourInfo pink = childColour(pinkInk);
            ColourInfo yellow = childColour(yellowInk);
            ColourInfo idle = childColour(idleColour);

            for (int i = 0; i < bar_count; i++)
            {
                float height = heights[i];
                (float capHeight, float bodyHeight) = CapsuleSegments(width, height);

                float left = positions[i];
                float top = bandHeight - height;

                ColourInfo colour = !active
                    ? idle
                    : (i % 16) switch
                    {
                        4 => pink,
                        12 => yellow,
                        _ => ink,
                    };

                drawSegment(renderer, left, top, capHeight, topCapRegion, colour);

                if (bodyHeight > 0)
                    drawSegment(renderer, left, top + capHeight, bodyHeight, bodyRegion, colour);

                drawSegment(renderer, left, top + capHeight + bodyHeight, capHeight, bottomCapRegion, colour);
            }

            UnbindTextureShader(renderer);
        }

        private ColourInfo childColour(Color4 barColour)
        {
            ColourInfo colour = DrawColourInfo.Colour;
            colour.ApplyChild(barColour);
            return colour;
        }

        private void drawSegment(
            IRenderer renderer,
            float x,
            float y,
            float segmentHeight,
            RectangleF textureRegion,
            ColourInfo colour)
        {
            var quad = new Quad(
                Vector2Extensions.Transform(new Vector2(x, y), DrawInfo.Matrix),
                Vector2Extensions.Transform(new Vector2(x + width, y), DrawInfo.Matrix),
                Vector2Extensions.Transform(new Vector2(x, y + segmentHeight), DrawInfo.Matrix),
                Vector2Extensions.Transform(new Vector2(x + width, y + segmentHeight), DrawInfo.Matrix));

            renderer.DrawQuad(texture, quad, colour, textureRegion, vertexBatch.AddAction);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            vertexBatch?.Dispose();
        }
    }
}
