using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Platform;

namespace Yokko.Game.Presentation;

/// <summary>
/// Samples the real host clocks for AUTO without requiring the visual
/// performance readout to be enabled.
/// </summary>
internal partial class YokkoAdaptiveFrameRateMonitor : Drawable
{
    private const double warmup_milliseconds = 4000;
    private const double evaluation_interval_milliseconds = 500;

    private readonly FrameTimingTracker drawTracker = new();
    private readonly FrameTimingTracker updateTracker = new();
    private GameHost host;
    private YokkoFrameRateAdaptation adaptation;
    private IFrameTimingSource timingSource;
    private double targetDrawFramesPerSecond;
    private double targetUpdateFramesPerSecond;
    private double lastDrawMarker;
    private double elapsedSinceTargetChange;
    private double elapsedSinceEvaluation;
    private bool wasMonitoring;
    private int observedAdaptationRevision = -1;

    public override bool IsPresent => true;

    [BackgroundDependencyLoader]
    private void load(
        GameHost host,
        YokkoFrameRateAdaptation adaptation)
    {
        this.host = host;
        this.adaptation = adaptation;
        timingSource = new GameHostFrameTimingSource(host);
    }

    protected override void Update()
    {
        base.Update();

        bool shouldMonitor =
            adaptation?.IsEnabled == true
            && host?.IsActive.Value == true;
        if (!shouldMonitor)
        {
            if (wasMonitoring)
                reset();
            wasMonitoring = false;
            return;
        }

        if (observedAdaptationRevision
            != adaptation.Revision)
        {
            observedAdaptationRevision =
                adaptation.Revision;
            reset();
        }

        wasMonitoring = true;
        if (timingSource?.TryRead(out FrameTimingSample sample)
            != true)
        {
            return;
        }

        if (targetsChanged(sample))
        {
            targetDrawFramesPerSecond =
                sample.TargetDrawFramesPerSecond;
            targetUpdateFramesPerSecond =
                sample.TargetUpdateFramesPerSecond;
            resetTrackers();
        }

        if (sample.DrawMarker != lastDrawMarker)
        {
            lastDrawMarker = sample.DrawMarker;
            drawTracker.Record(sample.DrawFrameTimeMilliseconds);
        }
        updateTracker.Record(sample.UpdateFrameTimeMilliseconds);

        elapsedSinceTargetChange += Time.Elapsed;
        elapsedSinceEvaluation += Time.Elapsed;
        if (elapsedSinceTargetChange < warmup_milliseconds
            || elapsedSinceEvaluation
               < evaluation_interval_milliseconds)
        {
            return;
        }

        elapsedSinceEvaluation %=
            evaluation_interval_milliseconds;
        double drawTargetFrameTime =
            framesPerSecondToFrameTime(
                targetDrawFramesPerSecond);
        double updateTargetFrameTime =
            framesPerSecondToFrameTime(
                targetUpdateFramesPerSecond);
        FramePacingHealth drawHealth =
            drawTracker.EvaluateHealth(drawTargetFrameTime);
        FramePacingHealth updateHealth =
            updateTracker.EvaluateHealth(updateTargetFrameTime);
        adaptation.Observe(
            (FramePacingHealth)Math.Max(
                (int)drawHealth,
                (int)updateHealth));
    }

    private bool targetsChanged(FrameTimingSample sample) =>
        targetDrawFramesPerSecond
        != sample.TargetDrawFramesPerSecond
        || targetUpdateFramesPerSecond
        != sample.TargetUpdateFramesPerSecond;

    private void reset()
    {
        targetDrawFramesPerSecond = 0;
        targetUpdateFramesPerSecond = 0;
        resetTrackers();
    }

    private void resetTrackers()
    {
        drawTracker.Reset();
        updateTracker.Reset();
        lastDrawMarker = 0;
        elapsedSinceTargetChange = 0;
        elapsedSinceEvaluation = 0;
    }

    private static double framesPerSecondToFrameTime(
        double framesPerSecond) =>
        double.IsFinite(framesPerSecond)
        && framesPerSecond > 0
            ? 1000 / framesPerSecond
            : double.PositiveInfinity;
}
