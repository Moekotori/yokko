using System;
using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Screens.Main;

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
    private readonly IBindable<FrameSync> frameSync;

    public GameHostFrameTimingSource(
        GameHost host,
        FrameworkConfigManager frameworkConfig)
    {
        this.host = host;
        frameSync = frameworkConfig.GetBindable<FrameSync>(
            FrameworkSetting.FrameSync);
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

        float refreshRate =
            host.Window?.CurrentDisplayMode.Value.RefreshRate ?? 60;
        if (refreshRate <= 0)
            refreshRate = 60;

        YokkoFrameLimit limit =
            YokkoFrameRateLimits.FromFrameworkFrameSync(
                frameSync.Value);
        YokkoFrameRates targets =
            YokkoFrameRateLimits.Calculate(limit, refreshRate);

        if (!host.IsActive.Value)
        {
            targets = new YokkoFrameRates(
                host.MaximumInactiveHz,
                host.MaximumInactiveHz);
        }

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

internal enum FramePacingHealth
{
    Stable,
    Warning,
    Critical,
}

internal partial class YokkoPerformanceReadout : CompositeDrawable
{
    internal const float CardWidth = 324;
    internal const float CardHeight = 36;
    internal const float ExpandedCardHeight = 88;

    private const double display_refresh_milliseconds = 250;

    private readonly FrameTimingTracker drawTracker = new();
    private readonly FrameTimingTracker updateTracker = new();
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

    internal YokkoPerformanceReadout(
        IFrameTimingSource timingSource = null)
    {
        this.timingSource = timingSource;
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

    [BackgroundDependencyLoader]
    private void load(
        GameHost host,
        FrameworkConfigManager frameworkConfig)
    {
        timingSource ??= new GameHostFrameTimingSource(
            host,
            frameworkConfig);
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
        elapsedSinceDisplayRefresh =
            display_refresh_milliseconds;
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

        DisplayedHealth = calculateHealth(
            drawSnapshot,
            drawTargetFrameTime,
            updateSnapshot,
            updateTargetFrameTime);
        statusAccent.FadeColour(
            healthColour(DisplayedHealth),
            120,
            Easing.OutQuint);
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

    private static FramePacingHealth calculateHealth(
        FrameTimingSnapshot draw,
        double drawTargetFrameTime,
        FrameTimingSnapshot update,
        double updateTargetFrameTime)
    {
        double worstP99Ratio = Math.Max(
            safeRatio(
                draw.P99FrameTimeMilliseconds,
                drawTargetFrameTime),
            safeRatio(
                update.P99FrameTimeMilliseconds,
                updateTargetFrameTime));
        double worstMissRatio = Math.Max(
            draw.BudgetMissRatio,
            update.BudgetMissRatio);

        if (worstP99Ratio >= 2
            || worstMissRatio >= 0.02)
            return FramePacingHealth.Critical;

        if (worstP99Ratio >= 1.25
            || draw.BudgetMissCount > 0
            || update.BudgetMissCount > 0)
            return FramePacingHealth.Warning;

        return FramePacingHealth.Stable;
    }

    private static double safeRatio(
        double value,
        double target) =>
        double.IsFinite(target) && target > 0
            ? value / target
            : 0;

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
