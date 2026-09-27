using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Audio;
using Yokko.Core.Timing;
using Yokko.Game.Audio;
using Yokko.Game.Input;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Screens.Settings;

/// <summary>Modal in the settings stage's explicitly retained internal canvas.</summary>
internal partial class AudioCalibrationOverlay : CompositeDrawable
{
    [Resolved] private KeyInputTimestampSource timestamps { get; set; }
    [Resolved] private GameHost host { get; set; }
    private readonly ISongSelectPreviewHost previewHost;
    private readonly YokkoAudioSettings settings;
    private readonly AudioSettingsTestPlayer player;
    private readonly Action close;
    private readonly SpriteText status;
    private readonly SpriteText details;
    private readonly SpriteText progress;
    private readonly SpriteText outputInfo;
    private readonly SpriteText statistics;
    private readonly Container advancedHost, volumeHost, keyHint, resultSummary;
    private readonly SpriteText previousValue, suggestedValue;
    private readonly Box progressFill;
    private readonly GameplayCompactButton listenButton, detailButton;
    private bool listening, hasResult, advancedOpen;
    private readonly Container distribution;
    private readonly GameplayCompactButton startButton, beforeButton, afterButton, applyButton, undoButton, closeButton;
    private CancellationTokenSource run;
    private InputCalibrationSession session;
    private CalibrationResult? recommendation;
    private string measuredRoute;
    private int measuredBuffer;
    private double originalOffset;
    private double? comparisonOffset;
    private double lastAudioTime;
    private double lastProgressAt;
    private bool running, disposed, applied, captured, spaceHeld;
    public override bool AcceptsFocus => true;
    private Task playbackTask = Task.CompletedTask;

    public AudioCalibrationOverlay(YokkoAudioSettings settings, string directory, Action close,
        ISongSelectPreviewHost previewHost = null)
    {
        this.settings = settings;
        this.close = close;
        this.previewHost = previewHost;
        player = new(settings, AudioEngineFactory.CreateDefault, directory);
        originalOffset = settings.UserOffsetMilliseconds.Value;
        measuredRoute = AudioDeviceProfiles.RouteKey(settings);
        measuredBuffer = settings.PreferredBufferSize.Value;
        RelativeSizeAxes = Axes.Both;
        Depth = -1000;
        var panel = new Container
        {
            Anchor = Anchor.Centre, Origin = Anchor.Centre, Size = new Vector2(940, 630),
            Masking = true, CornerRadius = 14,
        };
        InternalChildren = new Drawable[]
        {
            new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(0, 0, 0, 0.65f) }, panel,
        };
        panel.Add(new Box { RelativeSizeAxes = Axes.Both, Colour = HomeControlColours.Ivory });
        panel.Add(label("calibration.title", 32, 28, 28));
        panel.Add(label("calibration.simple.steps", 32, 76, 15));
        panel.Add(closeButton = button("calibration.close", this.close, 775, 24, 130));
        panel.Add(status = label("calibration.simple.ready", 32, 138, 30));
        panel.Add(details = label("calibration.simple.instruction", 32, 196, 17));
        panel.Add(progress = label("calibration.simple.duration", 32, 252, 22));
        panel.Add(volumeHost = new Container
        {
            Position = new Vector2(32, 328), Size = new Vector2(450, 65),
            Children = new Drawable[]
            {
                new SettingsVolumeSlider(YokkoStrings.Get("calibration.simple.master"), settings.MasterVolume, false),
                new SettingsVolumeSlider(YokkoStrings.Get("calibration.simple.click_volume"), settings.HitSoundVolume, false)
                { X = 225 },
            },
        });
        panel.Add(keyHint = new Container
        {
            Position = new Vector2(557, 320), Size = new Vector2(348, 80), Masking = true, CornerRadius = 9,
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = SettingsTheme.PaleCyan },
                new SettingsReadableText { Anchor = Anchor.Centre, Origin = Anchor.Centre,
                    Text = YokkoStrings.Get("calibration.simple.space"), Font = HomeTypography.Display(24), Colour = HomeControlColours.Navy },
            },
        });
        panel.Add(resultSummary = new Container
        {
            Position = new Vector2(32, 320), Size = new Vector2(873, 106), Masking = true, CornerRadius = 9,
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = SettingsTheme.PaleCyan },
                label("calibration.simple.old_value", 24, 14, 15),
                previousValue = new SettingsReadableText { Position = new Vector2(24, 45), Font = HomeTypography.Display(32), Colour = HomeControlColours.Navy },
                new SpriteIcon { Position = new Vector2(426, 43), Size = new Vector2(24), Icon = FontAwesome.Solid.ArrowRight, Colour = HomeControlColours.Navy },
                label("calibration.simple.new_value", 500, 14, 15),
                suggestedValue = new SettingsReadableText { Position = new Vector2(500, 45), Font = HomeTypography.Display(32), Colour = HomeControlColours.Navy },
            },
        });
        panel.Add(advancedHost = new Container { Position = new Vector2(32, 300), Size = new Vector2(878, 176) });
        advancedHost.Add(statistics = label("calibration.simple.no_stats", 0, 0, 13));
        advancedHost.Add(distribution = new Container { Position = new Vector2(8, 25), Size = new Vector2(860, 95) });
        advancedHost.Add(label("calibration.early", 8, 122, 13));
        advancedHost.Add(new SpriteText { Position = new Vector2(438, 122), Origin = Anchor.TopCentre,
            Text = "0", Font = HomeTypography.Body(13), Colour = HomeControlColours.Navy });
        advancedHost.Add(new SpriteText { Position = new Vector2(868, 122), Origin = Anchor.TopRight,
            Text = YokkoStrings.Get("calibration.late"), Font = HomeTypography.Body(13), Colour = HomeControlColours.Navy });
        advancedHost.Add(outputInfo = label("calibration.output_pending", 0, 154, 12));
        outputInfo.Width = 870;
        outputInfo.Truncate = true;
        panel.Add(progressFill = new Box { Position = new Vector2(32, 448), Height = 5, Width = 0, Colour = HomeControlColours.Cyan });
        panel.Add(listenButton = button("calibration.simple.listen", listen, 32, 490, 205));
        panel.Add(startButton = button("calibration.simple.retry", () => start(null), 430, 490, 205));
        panel.Add(applyButton = button("calibration.simple.start", PerformPrimaryAction, 650, 490, 255));
        applyButton.IsSelected = true;
        panel.Add(detailButton = button("calibration.simple.more", toggleDetails, 32, 554, 205));
        panel.Add(beforeButton = button("calibration.before", () => start(originalOffset), 253, 554, 205));
        panel.Add(afterButton = button("calibration.after", () => start(recommendation?.SuggestedOffsetMilliseconds), 474, 554, 205));
        panel.Add(undoButton = button("calibration.undo", UndoAppliedOffset, 695, 554, 210));
        resetDistribution();
        refreshButtons();
    }

    private static SpriteText label(string key, float x, float y, float size) => new()
    {
        Position = new Vector2(x, y), Text = YokkoStrings.Get(key), Font = HomeTypography.Body(size),
        Colour = HomeControlColours.Navy,
    };
    private static GameplayCompactButton button(string key, Action action, float x, float y, float width) =>
        new(YokkoStrings.Get(key), action, width) { Position = new Vector2(x, y) };

    private bool sameSetup => measuredRoute == AudioDeviceProfiles.RouteKey(settings)
                              && measuredBuffer == settings.PreferredBufferSize.Value;

    private void start(double? compare)
    {
        if (running || listening || !playbackTask.IsCompleted) return;
        if (settings.EffectiveHitSoundVolume <= 0)
        {
            status.Text = YokkoStrings.Get("calibration.muted");
            return;
        }
        if (compare.HasValue && (!sameSetup || !recommendation.HasValue)) return;
        if (!compare.HasValue)
        {
            recommendation = null;
            originalOffset = settings.UserOffsetMilliseconds.Value;
            applied = false;
            hasResult = false;
            advancedOpen = false;
        }
        measuredRoute = AudioDeviceProfiles.RouteKey(settings);
        measuredBuffer = settings.PreferredBufferSize.Value;
        GetContainingFocusManager()?.ChangeFocus(this);
        spaceHeld = false;
        comparisonOffset = compare;
        session = new();
        resetDistribution();
        statistics.Text = YokkoStrings.Get("calibration.simple.no_stats");
        run?.Dispose();
        run = new();
        running = true;
        lastAudioTime = 0;
        lastProgressAt = Time.Current;
        status.Text = YokkoStrings.Get("calibration.opening");
        details.Text = YokkoStrings.Get("calibration.simple.instruction");
        refreshButtons();
        playbackTask = play(run.Token);
    }

    private async Task play(CancellationToken token)
    {
        try
        {
            // Release the real home output before opening an exclusive calibration stream.
            if (previewHost != null)
                await previewHost.SuspendOutputAsync();
            token.ThrowIfCancellationRequested();
            await player.PlayCalibrationAsync(() => Scheduler.Add(() =>
            {
                if (disposed || token.IsCancellationRequested) return;
                timestamps.BeginCapture();
                captured = true;
                lastProgressAt = Time.Current;
                status.Text = YokkoStrings.Get("calibration.warmup");
            }), token);
            Scheduler.Add(() =>
            {
                if (disposed || token.IsCancellationRequested) return;
                if (lastAudioTime < InputCalibrationSession.DurationMilliseconds - 750)
                    fail("calibration.clock_failed");
                else finish();
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            osu.Framework.Logging.Logger.Error(ex, "Calibration output failed.");
            Scheduler.Add(() => { if (!disposed && !token.IsCancellationRequested) fail("calibration.audio_failed"); });
        }
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        GetContainingFocusManager()?.ChangeFocus(this);
    }

    protected override void Update()
    {
        base.Update();
        if (listening && (!host.IsActive.Value || !sameSetup)) { cancelRun(); return; }
        if (!running) { refreshButtons(); return; }
        if (!host.IsActive.Value || !sameSetup || settings.EffectiveHitSoundVolume <= 0)
        {
            fail("calibration.interrupted");
            return;
        }
        if (!captured) return;
        if (!player.TryGetCalibrationTime(Stopwatch.GetTimestamp(), out double time, out AudioEngineStatus output))
        {
            if (output.IsFaulted || output.HasUnderrun || Time.Current - lastProgressAt > 1200)
                fail("calibration.clock_failed");
            return;
        }
        if (time + 2 < lastAudioTime) { fail("calibration.clock_failed"); return; }
        if (time > lastAudioTime) lastProgressAt = Time.Current;
        else if (Time.Current - lastProgressAt > 1200) { fail("calibration.clock_failed"); return; }
        lastAudioTime = time;
        progressFill.Width = (float)(870 * Math.Clamp(time / InputCalibrationSession.DurationMilliseconds, 0, 1));
        outputInfo.Text = YokkoStrings.Get("calibration.output", output.DeviceName ?? "Default",
            output.ActiveBackend, output.SampleRate, output.BufferSize);
        while (timestamps.TryDequeueRaw(out TimestampedKeyInput input))
        {
            if (input.Key != Key.Space) continue;
            if (input.IsPressed && !spaceHeld) record(input.Timestamp);
            spaceHeld = input.IsPressed;
        }
        status.Text = YokkoStrings.Get(time < 3000 ? "calibration.warmup" : "calibration.listen");
        progress.Text = YokkoStrings.Get("calibration.simple.progress", session.SampleCount,
            Math.Max(0, (int)Math.Ceiling((InputCalibrationSession.DurationMilliseconds - time) / 1000)));
        if (time >= InputCalibrationSession.DurationMilliseconds - 100) finish();
    }

    private void record(long timestamp)
    {
        if (!running || !captured) return;
        if (player.TryGetCalibrationTime(timestamp, out double time, out _)) session.TryRecordTap(time);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key == Key.Escape) { close(); return true; }
        if (!e.Repeat && e.Key is Key.Enter or Key.KeypadEnter)
        {
            PerformPrimaryAction();
            return true;
        }
        if (e.Key == Key.Tab && !running && !listening)
        {
            var controls = new[] { listenButton, startButton, applyButton, detailButton, beforeButton, afterButton, undoButton, closeButton }
                .Where(control => control.IsEnabled && control.Alpha > 0).ToArray();
            int index = Array.FindIndex(controls, control => control.HasFocus);
            int next = (index + (e.ShiftPressed ? -1 : 1) + controls.Length) % controls.Length;
            GetContainingFocusManager()?.ChangeFocus(controls[next]);
            return true;
        }
        if (e.Key == Key.Space && !e.Repeat && captured && !timestamps.IsRawInputAvailable)
        {
            // No frame-time fallback: if the OS event timestamp is unavailable, require a retry.
            if (timestamps.TryTake(e.Key, true, out long timestamp)) record(timestamp);
        }
        return true;
    }
    protected override void OnKeyUp(KeyUpEvent e)
    {
        if (captured && !timestamps.IsRawInputAvailable) timestamps.TryTake(e.Key, false, out _);
        base.OnKeyUp(e);
    }
    protected override bool OnMouseDown(MouseDownEvent e) => true;
    protected override bool OnScroll(ScrollEvent e) => true;

    private void finish()
    {
        if (!running) return;
        ShowCompletedMeasurement(session);
    }

    internal void ShowCompletedMeasurement(InputCalibrationSession completed)
    {
        session = completed;
        CalibrationResult result = completed.Analyse();
        hasResult = true;
        stopCapture();
        run?.Cancel();
        if (!comparisonOffset.HasValue) recommendation = result.CanApply ? result : null;
        else if (!result.CanApply || (comparisonOffset == recommendation?.SuggestedOffsetMilliseconds
                                     && Math.Abs(result.MedianMilliseconds + comparisonOffset.Value) > 20))
        {
            recommendation = null;
            if (result.CanApply) result = result with { Quality = CalibrationQuality.Unstable };
        }
        status.Text = YokkoStrings.Get(result.Quality switch
        {
            CalibrationQuality.Stable => comparisonOffset.HasValue ? "calibration.compared" : "calibration.stable",
            CalibrationQuality.Unstable => "calibration.unstable",
            CalibrationQuality.OutOfRange => "calibration.range",
            _ => "calibration.few",
        });
        statistics.Text = YokkoStrings.Get("calibration.stats", result.AcceptedSamples, result.RejectedSamples,
            result.SpreadMilliseconds, result.DriftMilliseconds);
        details.Text = YokkoStrings.Get(result.CanApply ? "calibration.simple.good" : "calibration.simple.try_again");
        progress.Text = comparisonOffset.HasValue
            ? YokkoStrings.Get("calibration.residual", comparisonOffset.Value,
                result.MedianMilliseconds + comparisonOffset.Value)
            : result.CanApply ? YokkoStrings.Get("calibration.simple.confirm_hint")
                : YokkoStrings.Get("calibration.retry_hint");
        resetDistribution();
        int index = 0;
        foreach (double error in session.Offsets)
            distribution.Add(new Box
            {
                Position = new Vector2((float)(430 + Math.Clamp(error + (comparisonOffset ?? 0), -220, 220) * 1.9), 8 + index++ % 5 * 15),
                Size = new Vector2(4, 10), Colour = HomeControlColours.Navy,
            });
        refreshButtons();
    }

    private void resetDistribution()
    {
        distribution.Clear();
        distribution.Add(new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(0.94f, 0.96f, 1, 1) });
        distribution.Add(new Box { X = 430, Width = 2, Height = 90, Colour = HomeControlColours.Navy });
        distribution.Add(new Box { Y = 89, Width = 860, Height = 1, Colour = SettingsTheme.Divider });
    }

    private void apply()
    {
        if (running || !sameSetup || recommendation?.CanApply != true) return;
        settings.UserOffsetMilliseconds.Value = recommendation.Value.SuggestedOffsetMilliseconds;
        applied = true;
        status.Text = YokkoStrings.Get("calibration.simple.applied");
        details.Text = YokkoStrings.Get("calibration.simple.applied_note");
        progress.Text = YokkoStrings.Get("calibration.simple.next_playback");
        refreshButtons();
    }
    internal void UndoAppliedOffset()
    {
        if (running || !sameSetup || !applied) return;
        settings.UserOffsetMilliseconds.Value = originalOffset;
        applied = false;
        status.Text = YokkoStrings.Get("calibration.undone");
        details.Text = YokkoStrings.Get("calibration.simple.undo_note");
        progress.Text = YokkoStrings.Get("calibration.simple.confirm_hint");
        refreshButtons();
    }
    private void fail(string key)
    {
        stopCapture();
        run?.Cancel();
        recommendation = null;
        hasResult = true;
        details.Text = YokkoStrings.Get("calibration.simple.try_again");
        status.Text = YokkoStrings.Get(key);
        progress.Text = YokkoStrings.Get("calibration.retry_hint");
        refreshButtons();
    }
    private void stopCapture()
    {
        running = false;
        if (captured) timestamps.EndCapture();
        captured = false;
    }
    internal bool CanApplyRecommendation => recommendation?.CanApply == true && sameSetup;
    internal bool ShowsAdvancedDetails => advancedHost.Alpha > 0;
    internal int VisibleActionCount => new[] { listenButton, startButton, applyButton, detailButton, beforeButton, afterButton, undoButton }
        .Count(control => control.Alpha > 0);
    internal void ToggleDetails() => toggleDetails();

    internal void PerformPrimaryAction()
    {
        if (running || listening) { cancelRun(); return; }
        if (applied) { close(); return; }
        if (recommendation.HasValue) apply();
        else start(null);
    }
    private void toggleDetails() { advancedOpen = !advancedOpen; refreshButtons(); }
    private void cancelRun()
    {
        stopCapture();
        listening = false;
        if (!applied) { recommendation = null; hasResult = false; }
        run?.Cancel();
        status.Text = YokkoStrings.Get("calibration.simple.stopped");
        details.Text = YokkoStrings.Get("calibration.simple.instruction");
        progress.Text = YokkoStrings.Get("calibration.simple.duration");
        refreshButtons();
    }
    private void listen()
    {
        if (running || listening || !playbackTask.IsCompleted) return;
        if (settings.EffectiveHitSoundVolume <= 0)
        {
            status.Text = YokkoStrings.Get("calibration.muted");
            return;
        }
        measuredRoute = AudioDeviceProfiles.RouteKey(settings);
        measuredBuffer = settings.PreferredBufferSize.Value;
        run?.Dispose();
        run = new();
        listening = true;
        GetContainingFocusManager()?.ChangeFocus(this);
        status.Text = YokkoStrings.Get("calibration.simple.listening");
        refreshButtons();
        playbackTask = listenAsync(run.Token);
    }
    private async Task listenAsync(CancellationToken token)
    {
        try
        {
            if (previewHost != null) await previewHost.SuspendOutputAsync();
            token.ThrowIfCancellationRequested();
            await player.PlayCalibrationAsync(() => run.CancelAfter(1800), token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            osu.Framework.Logging.Logger.Error(ex, "Calibration preview failed.");
            Scheduler.Add(() => { if (!disposed && listening) { listening = false; fail("calibration.audio_failed"); } });
            return;
        }
        Scheduler.Add(() =>
        {
            if (disposed || !listening) return;
            listening = false;
            status.Text = YokkoStrings.Get("calibration.simple.ready");
            refreshButtons();
        });
    }
    private void refreshButtons()
    {
        bool busy = running || listening;
        bool available = playbackTask.IsCompleted;
        setEnabled(applyButton, busy || available);
        applyButton.SetText(YokkoStrings.Get(busy ? "calibration.simple.stop"
            : !available ? "calibration.simple.wait"
            : applied ? "calibration.simple.done"
            : recommendation.HasValue ? "calibration.simple.apply" : "calibration.simple.start"));
        startButton.Alpha = !busy && recommendation.HasValue && !applied ? 1 : 0;
        setEnabled(startButton, available);
        listenButton.Alpha = !busy && !recommendation.HasValue && !applied ? 1 : 0;
        setEnabled(listenButton, available);
        detailButton.Alpha = !busy && hasResult ? 1 : 0;
        detailButton.SetText(YokkoStrings.Get(advancedOpen ? "calibration.simple.less" : "calibration.simple.more"));
        bool showDetails = advancedOpen && !busy;
        advancedHost.Alpha = showDetails ? 1 : 0;
        resultSummary.Alpha = !showDetails && !busy && recommendation.HasValue ? 1 : 0;
        previousValue.Text = $"{originalOffset:+0;-0;0} ms";
        suggestedValue.Text = $"{recommendation?.SuggestedOffsetMilliseconds ?? 0:+0;-0;0} ms";
        bool canCompare = !busy && available && sameSetup && recommendation.HasValue;
        beforeButton.Alpha = afterButton.Alpha = showDetails && recommendation.HasValue ? 1 : 0;
        setEnabled(beforeButton, canCompare);
        setEnabled(afterButton, canCompare);
        undoButton.Alpha = !busy && applied ? 1 : 0;
        setEnabled(undoButton, !busy && sameSetup && applied);
        volumeHost.Alpha = !showDetails && !busy && !recommendation.HasValue && !applied ? 1 : 0;
        keyHint.Alpha = !showDetails && !recommendation.HasValue && !applied ? 1 : 0;
        progressFill.Alpha = running ? 1 : 0;
    }
    private static void setEnabled(GameplayCompactButton button, bool enabled)
    {
        if (button.IsEnabled != enabled) button.IsEnabled = enabled;
    }
    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            disposed = true;
            stopCapture();
            run?.Cancel();
            run?.Dispose();
            Task release = Task.WhenAll(playbackTask, player.DisposeAsync().AsTask());
            previewHost?.CompletePreviewHandoff(release);
        }
        base.Dispose(isDisposing);
    }
}
