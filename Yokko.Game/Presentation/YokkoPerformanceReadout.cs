using System;
using System.Diagnostics;
using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Screens.Main;
using Yokko.Game.Diagnostics;

namespace Yokko.Game.Presentation;

internal readonly record struct FrameTimingSample(
    double DrawMarker,
    double DrawFrameTimeMilliseconds,
    double UpdateFrameTimeMilliseconds,
    double InputFramesPerSecond,
    double TargetDrawFramesPerSecond,
    double TargetUpdateFramesPerSecond);

internal interface IFrameTimingSource
{
    bool TryRead(out FrameTimingSample sample);
}

internal sealed class GameHostFrameTimingSource : IFrameTimingSource
{
    private readonly GameHost host;

    public GameHostFrameTimingSource(GameHost host)
    {
        this.host = host;
    }

    public bool TryRead(out FrameTimingSample sample)
    {
        if (host.DrawThread?.Clock == null
            || host.UpdateThread?.Clock == null
            || host.InputThread?.Clock == null)
        {
            sample = default;
            return false;
        }

        YokkoFrameRates targets = new(
            host.DrawThread.Clock.MaximumUpdateHz,
            host.UpdateThread.Clock.MaximumUpdateHz);

        sample = new FrameTimingSample(
            host.DrawThread.Clock.CurrentTime,
            host.DrawThread.Clock.ElapsedFrameTime,
            host.UpdateThread.Clock.ElapsedFrameTime,
            host.InputThread.Clock.FramesPerSecond,
            targets.MaximumDrawHz,
            targets.MaximumUpdateHz);
        return sample.DrawMarker > 0
               && sample.DrawFrameTimeMilliseconds > 0
               && sample.UpdateFrameTimeMilliseconds > 0;
    }
}

internal partial class YokkoPerformanceReadout : CompositeDrawable
{
    internal const float CardWidth = 324;
    internal const float CardHeight = 36;
    internal const float ExpandedCardHeight = 88;
    internal const float EdgeInset = 12;

    private const double display_refresh_milliseconds = 250;

    private readonly FrameTimingTracker drawTracker = new();
    private readonly FrameTimingTracker updateTracker = new();
    private readonly YokkoDiagnostics diagnostics;
    private readonly Process process;
    private IFrameTimingSource timingSource;
    private Box statusAccent;
    private Container details;
    private SpriteText drawFpsText;
    private SpriteText updateFrameTimeText;
    private SpriteText inputRateText;
    private SpriteText drawP95Text;
    private SpriteText drawP99Text;
    private SpriteText drawMissText;
    private SpriteText updateP95Text;
    private SpriteText updateP99Text;
    private SpriteText updateMissText;
    private double lastDrawMarker;
    private double elapsedSinceDisplayRefresh;
    private double targetDrawFramesPerSecond;
    private double targetUpdateFramesPerSecond;
    private FrameTimingSample latestSample;
    private bool hasSample;
    private bool trackingEnabled = true;
    private DateTimeOffset previousProcessSampleTime;
    private TimeSpan previousProcessCpuTime;

    internal YokkoPerformanceReadout(
        IFrameTimingSource timingSource = null,
        YokkoDiagnostics diagnostics = null)
    {
        this.timingSource = timingSource;
        this.diagnostics = diagnostics;
        if (diagnostics != null)
            process = Process.GetCurrentProcess();
        Anchor = Anchor.BottomRight;
        Origin = Anchor.BottomRight;
        Position = GetLayoutPosition(Vector2.Zero, 0, 0);
        Size = new Vector2(CardWidth, CardHeight);
        Masking = true;
        CornerRadius = 5;
        BorderThickness = 1;
        BorderColour = new Color4(
            HomeControlColours.Navy.R,
            HomeControlColours.Navy.G,
            HomeControlColours.Navy.B,
            0.5f);

        InternalChild = createCard();
    }

    internal string DisplayedDrawFramesPerSecond =>
        drawFpsText?.Text.ToString() ?? string.Empty;

    internal string DisplayedUpdateFrameTime =>
        updateFrameTimeText == null
            ? string.Empty
            : $"{updateFrameTimeText.Text} ms";

    internal string DisplayedInputRate =>
        inputRateText?.Text.ToString() ?? string.Empty;

    internal FramePacingHealth DisplayedHealth { get; private set; }
    internal bool TrackingEnabled => trackingEnabled;

    internal static Vector2 GetLayoutPosition(
        Vector2 viewportSize,
        double offsetX,
        double offsetY) =>
        new(
            -EdgeInset + (float)offsetX * viewportSize.X,
            -EdgeInset + (float)offsetY * viewportSize.Y);

    [BackgroundDependencyLoader]
    private void load(GameHost host)
    {
        timingSource ??= new GameHostFrameTimingSource(
            host);
        refreshDisplay();
    }

    protected override void Update()
    {
        base.Update();

        if (!trackingEnabled)
            return;

        if (timingSource?.TryRead(out FrameTimingSample sample) == true)
        {
            if (targetsChanged(sample))
            {
                targetDrawFramesPerSecond =
                    sample.TargetDrawFramesPerSecond;
                targetUpdateFramesPerSecond =
                    sample.TargetUpdateFramesPerSecond;
                drawTracker.Reset();
                updateTracker.Reset();
                lastDrawMarker = 0;
            }

            latestSample = sample;
            hasSample = true;

            if (sample.DrawMarker != lastDrawMarker)
            {
                lastDrawMarker = sample.DrawMarker;
                drawTracker.Record(
                    sample.DrawFrameTimeMilliseconds);
            }

            updateTracker.Record(
                sample.UpdateFrameTimeMilliseconds);
        }

        elapsedSinceDisplayRefresh += Time.Elapsed;

        if (elapsedSinceDisplayRefresh
            >= display_refresh_milliseconds)
        {
            elapsedSinceDisplayRefresh %=
                display_refresh_milliseconds;
            refreshDisplay();
        }
    }

    protected override bool OnHover(HoverEvent e)
    {
        this.ResizeHeightTo(
            ExpandedCardHeight,
            180,
            Easing.OutQuint);
        details.FadeIn(140, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        details.FadeOut(90, Easing.OutQuint);
        this.ResizeHeightTo(
            CardHeight,
            150,
            Easing.OutQuint);
        base.OnHoverLost(e);
    }

    internal void RefreshForTesting() => refreshDisplay();

    internal void SetTrackingEnabled(bool enabled)
    {
        if (trackingEnabled == enabled)
            return;

        trackingEnabled = enabled;
        if (!enabled)
            return;

        drawTracker.Reset();
        updateTracker.Reset();
        targetDrawFramesPerSecond = 0;
        targetUpdateFramesPerSecond = 0;
        lastDrawMarker = 0;
        hasSample = false;
        previousProcessSampleTime = default;
        previousProcessCpuTime = default;
        elapsedSinceDisplayRefresh =
            display_refresh_milliseconds;
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            process?.Dispose();
        base.Dispose(isDisposing);
    }

    private Drawable createCard()
    {
        var card = new Container
        {
            Size = new Vector2(
                CardWidth,
                ExpandedCardHeight),
        };

        card.Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = HomeControlColours.Ivory,
                Alpha = 0.96f,
            },
            details = new Container
            {
                Position = new Vector2(0, CardHeight),
                Size = new Vector2(
                    CardWidth,
                    ExpandedCardHeight - CardHeight),
                Alpha = 0,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = HomeControlColours.PaleCyan,
                        Alpha = 0.62f,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 1,
                        Colour = new Color4(
                            HomeControlColours.Navy.R,
                            HomeControlColours.Navy.G,
                            HomeControlColours.Navy.B,
                            0.18f),
                    },
                    createDetailRow(
                        5,
                        "DRAW",
                        out drawP95Text,
                        out drawP99Text,
                        out drawMissText),
                    createDetailRow(
                        28,
                        "UPDATE",
                        out updateP95Text,
                        out updateP99Text,
                        out updateMissText),
                },
            },
            statusAccent = new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 2,
                Colour = HomeControlColours.Cyan,
            },
            new Circle
            {
                Position = new Vector2(10, 16),
                Size = new Vector2(6),
                Colour = HomeControlColours.Pink,
            },
            createMetric(
                26,
                72,
                "DRAW",
                "fps",
                out drawFpsText),
            createSeparator(105),
            createMetric(
                116,
                95,
                "UPDATE",
                "ms",
                out updateFrameTimeText),
            createSeparator(224),
            createMetric(
                235,
                80,
                "INPUT",
                "Hz",
                out inputRateText),
        };

        return card;
    }

    private static Drawable createMetric(
        float x,
        float width,
        string label,
        string unit,
        out SpriteText value)
    {
        var result = new Container
        {
            Position = new Vector2(x, 2),
            Size = new Vector2(width, CardHeight - 2),
        };

        result.Children = new Drawable[]
        {
            new SpriteText
            {
                Position = new Vector2(0, 2),
                Text = label,
                Font = HomeTypography.Display(6),
                Spacing = new Vector2(0.45f, 0),
                Colour = HomeControlColours.Navy,
                Alpha = 0.68f,
            },
            value = new SpriteText
            {
                Position = new Vector2(0, 12),
                Width = 44,
                Font = HomeTypography.Display(12)
                    .With(fixedWidth: true),
                UseFullGlyphHeight = true,
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(47, 20),
                Text = unit,
                Font = HomeTypography.Display(6),
                Colour = HomeControlColours.Navy,
                Alpha = 0.72f,
            },
        };

        return result;
    }

    private static Drawable createSeparator(float x) => new Box
    {
        Position = new Vector2(x, 8),
        Size = new Vector2(1, 20),
        Colour = new Color4(
            HomeControlColours.Navy.R,
            HomeControlColours.Navy.G,
            HomeControlColours.Navy.B,
            0.2f),
    };

    private static Drawable createDetailRow(
        float y,
        string label,
        out SpriteText p95,
        out SpriteText p99,
        out SpriteText miss)
    {
        var result = new Container
        {
            Position = new Vector2(12, y),
            Size = new Vector2(CardWidth - 24, 20),
        };

        result.Children = new Drawable[]
        {
            new SpriteText
            {
                Position = new Vector2(0, 4),
                Text = label,
                Font = HomeTypography.Display(7),
                Spacing = new Vector2(0.35f, 0),
                Colour = HomeControlColours.Navy,
            },
            p95 = createDetailValue(68),
            p99 = createDetailValue(146),
            miss = createDetailValue(224),
        };

        return result;
    }

    private static SpriteText createDetailValue(float x) =>
        new()
        {
            Position = new Vector2(x, 3),
            Width = 72,
            Font = HomeTypography.Body(8)
                .With(fixedWidth: true),
            Colour = HomeControlColours.Navy,
        };

    private void refreshDisplay()
    {
        if (!hasSample)
        {
            setTextIfChanged(drawFpsText, "—");
            setTextIfChanged(updateFrameTimeText, "—");
            setTextIfChanged(inputRateText, "—");
            setDetailPlaceholders();
            return;
        }

        double drawTargetFrameTime =
            framesPerSecondToFrameTime(
                targetDrawFramesPerSecond);
        double updateTargetFrameTime =
            framesPerSecondToFrameTime(
                targetUpdateFramesPerSecond);
        FrameTimingSnapshot drawSnapshot =
            drawTracker.Snapshot(drawTargetFrameTime);
        FrameTimingSnapshot updateSnapshot =
            updateTracker.Snapshot(updateTargetFrameTime);

        if (drawSnapshot.Count == 0
            || updateSnapshot.Count == 0)
            return;

        setTextIfChanged(
            drawFpsText,
            FrameTimingTracker.QuantizeFramesPerSecond(
                drawSnapshot.FramesPerSecond).ToString(
                CultureInfo.InvariantCulture));
        setTextIfChanged(
            updateFrameTimeText,
            formatFrameTime(
                updateSnapshot.FrameTimeMilliseconds));
        setTextIfChanged(
            inputRateText,
            formatFramesPerSecond(
                latestSample.InputFramesPerSecond));

        setTextIfChanged(
            drawP95Text,
            $"P95 {formatFrameTime(drawSnapshot.P95FrameTimeMilliseconds)}");
        setTextIfChanged(
            drawP99Text,
            $"P99 {formatFrameTime(drawSnapshot.P99FrameTimeMilliseconds)}");
        setTextIfChanged(
            drawMissText,
            $"MISS {drawSnapshot.BudgetMissCount}");
        setTextIfChanged(
            updateP95Text,
            $"P95 {formatFrameTime(updateSnapshot.P95FrameTimeMilliseconds)}");
        setTextIfChanged(
            updateP99Text,
            $"P99 {formatFrameTime(updateSnapshot.P99FrameTimeMilliseconds)}");
        setTextIfChanged(
            updateMissText,
            $"MISS {updateSnapshot.BudgetMissCount}");

        DisplayedHealth = FramePacingHealthEvaluator.Evaluate(
            drawSnapshot,
            drawTargetFrameTime,
            updateSnapshot,
            updateTargetFrameTime);
        statusAccent.FadeColour(
            healthColour(DisplayedHealth),
            120,
            Easing.OutQuint);
        reportPerformance(drawSnapshot, updateSnapshot);
    }

    private void reportPerformance(
        FrameTimingSnapshot drawSnapshot,
        FrameTimingSnapshot updateSnapshot)
    {
        if (diagnostics == null)
            return;

        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        TimeSpan cpuTime;
        long workingSet;

        try
        {
            process.Refresh();
            cpuTime = process.TotalProcessorTime;
            workingSet = process.WorkingSet64;
        }
        catch
        {
            cpuTime = previousProcessCpuTime;
            workingSet = 0;
        }

        double elapsedMilliseconds =
            (timestamp - previousProcessSampleTime).TotalMilliseconds;
        double cpuPercent = previousProcessSampleTime == default
                            || elapsedMilliseconds <= 0
            ? 0
            : (cpuTime - previousProcessCpuTime).TotalMilliseconds
              / elapsedMilliseconds
              / Math.Max(1, Environment.ProcessorCount)
              * 100;
        cpuPercent = Math.Clamp(cpuPercent, 0, 100);
        previousProcessSampleTime = timestamp;
        previousProcessCpuTime = cpuTime;

        diagnostics.ReportPerformance(new YokkoPerformanceSnapshot(
            timestamp,
            drawSnapshot.FramesPerSecond,
            drawSnapshot.FrameTimeMilliseconds,
            drawSnapshot.P95FrameTimeMilliseconds,
            drawSnapshot.P99FrameTimeMilliseconds,
            drawSnapshot.MaximumFrameTimeMilliseconds,
            drawSnapshot.BudgetMissCount,
            drawSnapshot.BudgetMissRatio,
            updateSnapshot.FramesPerSecond,
            updateSnapshot.FrameTimeMilliseconds,
            updateSnapshot.P95FrameTimeMilliseconds,
            updateSnapshot.P99FrameTimeMilliseconds,
            updateSnapshot.MaximumFrameTimeMilliseconds,
            updateSnapshot.BudgetMissCount,
            updateSnapshot.BudgetMissRatio,
            latestSample.InputFramesPerSecond,
            cpuPercent,
            workingSet,
            GC.GetTotalMemory(false),
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
            DisplayedHealth switch
            {
                FramePacingHealth.Critical =>
                    YokkoPerformanceHealth.Critical,
                FramePacingHealth.Warning =>
                    YokkoPerformanceHealth.Warning,
                _ => YokkoPerformanceHealth.Stable,
            }));
    }

    private bool targetsChanged(FrameTimingSample sample) =>
        targetDrawFramesPerSecond
        != sample.TargetDrawFramesPerSecond
        || targetUpdateFramesPerSecond
        != sample.TargetUpdateFramesPerSecond;

    private void setDetailPlaceholders()
    {
        setTextIfChanged(drawP95Text, "P95 —");
        setTextIfChanged(drawP99Text, "P99 —");
        setTextIfChanged(drawMissText, "MISS —");
        setTextIfChanged(updateP95Text, "P95 —");
        setTextIfChanged(updateP99Text, "P99 —");
        setTextIfChanged(updateMissText, "MISS —");
    }

    private static Color4 healthColour(
        FramePacingHealth health) => health switch
        {
            FramePacingHealth.Critical =>
                HomeControlColours.Pink,
            FramePacingHealth.Warning =>
                HomeControlColours.Yellow,
            _ => HomeControlColours.Cyan,
        };

    private static double framesPerSecondToFrameTime(
        double framesPerSecond) =>
        double.IsFinite(framesPerSecond)
        && framesPerSecond > 0
            ? 1000 / framesPerSecond
            : double.PositiveInfinity;

    private static string formatFrameTime(
        double frameTimeMilliseconds) =>
        frameTimeMilliseconds < 10
            ? frameTimeMilliseconds.ToString(
                "0.0",
                CultureInfo.InvariantCulture)
            : frameTimeMilliseconds.ToString(
                "0",
                CultureInfo.InvariantCulture);

    private static string formatFramesPerSecond(
        double framesPerSecond)
    {
        if (!double.IsFinite(framesPerSecond)
            || framesPerSecond <= 0)
            return "—";

        return FrameTimingTracker.QuantizeFramesPerSecond(
                Math.Max(
                    1,
                    (int)Math.Round(framesPerSecond)))
            .ToString(CultureInfo.InvariantCulture);
    }

    private static void setTextIfChanged(
        SpriteText target,
        string value)
    {
        if (target.Text.ToString() != value)
            target.Text = value;
    }
}
