using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osuTK;
using osuTK.Input;
using Yokko.Core.Beatmaps;
using Yokko.Core.Scoring;
using Yokko.Audio;
using Yokko.Game.Audio;
using Yokko.Game.Gameplay;
using Yokko.Game.Input;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Settings;
using Yokko.Game.Skinning.OsuMania;
using Yokko.Game.Scoring;

namespace Yokko.Game.Screens.Gameplay;

public partial class GameplayScreen : Screen
{
    private const double leadInMilliseconds = 900;
    private const float playfieldWidthStep = 0.1f;
    private const float minimumPlayfieldWidthScale = 0.2f;
    private const float maximumPlayfieldWidthScale = 2.5f;

    private readonly YokkoBeatmap beatmap;
    private IAudioEngine audioEngine;
    private readonly string skinPath;
    private readonly GameplayReplay replay;
    private readonly List<GameplayReplayInput> recordedReplayInputs = new();
    [Resolved]
    private YokkoAudioSettings audioSettings { get; set; }
    [Resolved]
    private OsuManiaSkinLibrary skinLibrary { get; set; }
    [Resolved]
    private YokkoGameplaySettings gameplaySettings { get; set; }
    [Resolved]
    private KeyInputTimestampSource keyInputTimestamps { get; set; }
    [Resolved]
    private GameplayScoreStore scoreStore { get; set; }

    private BeatmapJudgementState judgementState;
    private GameplayPlayfield playfield;
    private KeyModeBindings keyBindings;
    private bool[] pressedLanes;
    private double startTimeMilliseconds;
    private bool hasAudioClock;
    private bool audioStarted;
    private OsuManiaSkin maniaSkin;
    private double activeUserOffsetMilliseconds;
    private AudioBackendKind activeRequestedBackend;
    private double lastStableAudioGameplayTime;
    private readonly InputAgeTracker inputAgeTracker = new();
    private bool gameplayBlocked;
    private bool gameplayCompleted;
    private GameplayHud hud;
    private ManiaScoreResult completedResult;
    private readonly double completionTimeMilliseconds;
    private readonly double firstObjectTimeMilliseconds;
    private bool introSkipInProgress;
    private bool introSkipUsed;
    private double pendingIntroSkipMilliseconds = double.NaN;
    private int replayInputIndex;
    private GameplayReplay completedReplay;
    private float playfieldWidthScale = 1;
    private GameHost host;
    private bool isPaused;
    private bool pauseTransitionInProgress;
    private double pausedGameplayTime;
    private double pausedAudioPosition;
    private GameplayPauseOverlay pauseOverlay;
    private JudgementReadout judgementReadout;

    internal bool GameplayBlocked => gameplayBlocked;
    internal bool GameplayCompleted => gameplayCompleted;
    internal bool IsPaused => isPaused;
    internal bool PauseTransitionInProgress => pauseTransitionInProgress;
    internal double CurrentGameplayTime => currentGameplayTime;
    internal ManiaScoreResult CompletedResult => completedResult;
    internal bool ReplayMode => replay != null;
    internal bool IntroSkipAvailable =>
        !gameplayBlocked
        && !gameplayCompleted
        && !isPaused
        && !introSkipInProgress
        && !introSkipUsed
        && IntroSkipTargetMilliseconds > 0
        && currentGameplayTime < IntroSkipTargetMilliseconds;
    internal double IntroSkipTargetMilliseconds =>
        Math.Max(
            0,
            firstObjectTimeMilliseconds
            - Math.Max(
                leadInMilliseconds,
                playfield?.ApproachTimeMilliseconds ?? leadInMilliseconds));

    public GameplayScreen(
        YokkoBeatmap beatmap,
        IAudioEngine audioEngine = null,
        string skinPath = null)
        : this(beatmap, audioEngine, skinPath, null)
    {
    }

    internal GameplayScreen(
        YokkoBeatmap beatmap,
        IAudioEngine audioEngine,
        string skinPath,
        GameplayReplay replay)
    {
        this.beatmap = beatmap;
        this.audioEngine = audioEngine;
        this.skinPath = skinPath;
        this.replay = replay;
        completionTimeMilliseconds = beatmap.HitObjects.Count == 0
            ? 0
            : beatmap.HitObjects.Max(hitObject =>
                hitObject.EndTimeMilliseconds
                ?? hitObject.StartTimeMilliseconds);
        firstObjectTimeMilliseconds = beatmap.HitObjects.Count == 0
            ? 0
            : beatmap.HitObjects.Min(hitObject =>
                hitObject.StartTimeMilliseconds);
    }

    [BackgroundDependencyLoader]
    private void load(IRenderer renderer, GameHost host)
    {
        this.host = host;
        keyBindings = KeyModeBindings.ForMode(
            beatmap.KeyMode,
            gameplaySettings.GetKeys(beatmap.KeyMode));
        pressedLanes = new bool[keyBindings.KeyCount];
        judgementState = new BeatmapJudgementState(beatmap);
        loadSkin(renderer);
        audioEngine ??= string.IsNullOrWhiteSpace(beatmap.AudioPath)
            ? new NullAudioEngine()
            : AudioEngineFactory.CreateDefault();
        hasAudioClock = !string.IsNullOrWhiteSpace(beatmap.AudioPath);

        InternalChildren = new Drawable[]
        {
            new Box
            {
                Colour = YokkoPalette.Background,
                RelativeSizeAxes = Axes.Both,
            },
            playfield = new GameplayPlayfield(
                beatmap,
                keyBindings,
                maniaSkin,
                OsuManiaScrollSpeed.ComputeScrollTime(
                    gameplaySettings.ScrollSpeed.Value),
                gameplaySettings.ShowLanePressFeedback.Value)
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                // Legacy osu!mania values already use the skin's 480px
                // coordinate space. Scale the complete stage uniformly to the
                // available height so it stays grounded without distorting
                // skin geometry.
                Scale = Vector2.One,
            },
            hud = new GameplayHud(beatmap)
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-20, 20),
            },
            judgementReadout = new JudgementReadout
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(0, 30),
                Depth = -100,
            },
        };

        if (!hasAudioClock)
            hud.ShowFrameClock();

        gameplaySettings.ScrollSpeed.BindValueChanged(
            onScrollSpeedChanged,
            true);
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        if (!ReplayMode)
            keyInputTimestamps.BeginCapture();

        startTimeMilliseconds = Time.Current + leadInMilliseconds;

        if (hasAudioClock)
        {
            Scheduler.AddDelayed(
                () => _ = startAudioAsync(),
                leadInMilliseconds);
        }
    }

    protected override void Update()
    {
        base.Update();
        updatePlayfieldLayout();

        if (gameplayBlocked || gameplayCompleted || isPaused)
            return;

        if (hasAudioClock && audioStarted)
        {
            AudioEngineStatus audioStatus = audioEngine.Status;
            hud.UpdateAudioStatus(audioStatus, activeRequestedBackend);
            if (audioStatus.IsFaulted)
            {
                failAudioRuntime(audioStatus);
                return;
            }
        }

        double gameplayTime = currentGameplayTime;
        double visualGameplayTime = hasAudioClock
            ? GameplayPresentationClock.EstimateVisualTime(
                gameplayTime,
                host.Window?.CurrentDisplayMode.Value.RefreshRate ?? 60)
            : gameplayTime;

        if (ReplayMode)
            drainReplayInput(gameplayTime);
        else
            drainRawInput();

        foreach (JudgementEvent missed in judgementState.CollectExpiredMisses(gameplayTime))
            applyJudgement(missed);

        playfield.UpdateGameplayTime(visualGameplayTime, judgementState);
        hud.UpdateState(gameplayTime, judgementState);

        if (judgementState.IsComplete
            && gameplayTime >= completionTimeMilliseconds)
        {
            completeGameplay();
        }
    }

    private void updatePlayfieldLayout()
    {
        if (DrawHeight <= 0 || playfield.Height <= 0)
            return;

        float scale = DrawHeight / playfield.Height;
        playfield.Scale = new Vector2(scale);
    }

    protected override bool OnScroll(ScrollEvent e)
    {
        if (isPaused)
            return true;

        if (HandlePlayfieldWidthScroll(e.ScrollDelta.Y, e.ControlPressed))
            return true;

        return base.OnScroll(e);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (gameplayBlocked)
        {
            if (e.Key == Key.Escape)
                this.Exit();

            return true;
        }

        if (isPaused)
            return pauseOverlay?.HandleKey(e.Key) ?? true;

        if (gameplayCompleted)
        {
            switch (e.Key)
            {
                case Key.R:
                    retryGameplay();
                    return true;

                case Key.V:
                    watchCompletedReplay();
                    return true;

                case Key.Enter:
                case Key.Escape:
                    this.Exit();
                    return true;
            }
        }

        if (e.Key == Key.Space && HandleIntroSkip())
            return true;

        if (HandleScrollSpeedShortcut(e.Key, e.ControlPressed))
            return true;

        if (e.Key == Key.Escape)
        {
            TogglePause();
            return true;
        }

        if (ReplayMode)
            return true;

        int lane = keyBindings.GetLane(e.Key);

        if (lane < 0)
            return base.OnKeyDown(e);

        if (pressedLanes[lane])
            return true;

        if (!keyInputTimestamps.IsRawInputAvailable)
        {
            applyLanePress(
                lane,
                gameplayTimeForInput(e.Key, true));
        }

        return true;
    }

    protected override void OnKeyUp(KeyUpEvent e)
    {
        if (gameplayCompleted || ReplayMode || isPaused)
            return;

        int lane = keyBindings.GetLane(e.Key);

        if (lane < 0)
        {
            base.OnKeyUp(e);
            return;
        }

        if (!keyInputTimestamps.IsRawInputAvailable)
        {
            applyLaneRelease(
                lane,
                gameplayTimeForInput(e.Key, false));
        }
    }

    public override bool OnExiting(ScreenExitEvent e)
    {
        logInputTimingSummary();
        if (!ReplayMode)
            keyInputTimestamps.EndCapture();

        _ = audioEngine.StopAsync();
        return base.OnExiting(e);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            if (!ReplayMode)
                keyInputTimestamps.EndCapture();

            gameplaySettings.ScrollSpeed.ValueChanged -=
                onScrollSpeedChanged;
            _ = audioEngine.DisposeAsync();
            maniaSkin?.Dispose();
        }

        base.Dispose(isDisposing);
    }

    private async Task startAudioAsync()
    {
        try
        {
            activeUserOffsetMilliseconds =
                audioSettings.UserOffsetMilliseconds.Value;
            AudioEngineStartRequest startRequest =
                audioSettings.CreateStartRequest(beatmap.AudioPath);
            activeRequestedBackend = startRequest.PreferredBackend;

            await audioEngine.StartAsync(startRequest)
                             .ConfigureAwait(true);

            if (!audioEngine.Status.IsRunning)
            {
                failAudioStart("The audio engine returned without starting playback.");
                return;
            }

            audioStarted = true;
            lastStableAudioGameplayTime =
                audioEngine.PlaybackTimeMilliseconds
                + activeUserOffsetMilliseconds;
            hud.UpdateAudioStatus(
                audioEngine.Status,
                activeRequestedBackend);

            if (isPaused)
            {
                pausedAudioPosition = Math.Max(
                    0,
                    audioEngine.PlaybackTimeMilliseconds);
                await audioEngine.PauseAsync().ConfigureAwait(true);
                return;
            }

            if (double.IsFinite(pendingIntroSkipMilliseconds))
            {
                double target = pendingIntroSkipMilliseconds;
                pendingIntroSkipMilliseconds = double.NaN;
                await seekToIntroAsync(target).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            failAudioStart("The audio engine could not start playback.", ex);
        }
    }

    private void failAudioStart(string reason, Exception exception = null)
    {
        if (exception == null)
        {
            Logger.Log(
                reason,
                LoggingTarget.Runtime,
                LogLevel.Error);
        }
        else
        {
            Logger.Error(
                exception,
                reason,
                LoggingTarget.Runtime);
        }

        Scheduler.Add(() =>
        {
            if (gameplayBlocked)
                return;

            hasAudioClock = false;
            gameplayBlocked = true;
            AddInternal(new GameplayFailureOverlay());
        });
    }

    private void failAudioRuntime(AudioEngineStatus status)
    {
        if (gameplayBlocked)
            return;

        string detail =
            $"{status.ActiveBackend} · "
            + $"{status.BufferSize} frames · "
            + $"{status.EstimatedOutputLatencyMilliseconds:0.00} ms · "
            + $"HRESULT 0x{unchecked((uint)status.BackendError):X8}"
            + $"/{status.BackendErrorStage}";
        Logger.Log(
            $"Gameplay audio output faulted: {detail}",
            LoggingTarget.Runtime,
            LogLevel.Error);
        gameplayBlocked = true;
        AddInternal(new GameplayFailureOverlay(detail));
    }

    private double currentGameplayTime
    {
        get
        {
            if (isPaused)
                return pausedGameplayTime;

            if (hasAudioClock)
            {
                if (audioEngine.Status.IsRunning)
                {
                    lastStableAudioGameplayTime =
                        audioEngine.PlaybackTimeMilliseconds
                        + activeUserOffsetMilliseconds;
                    return lastStableAudioGameplayTime;
                }

                if (audioStarted)
                    return lastStableAudioGameplayTime;

                // Hold at zero while the audio device opens so its startup
                // time cannot consume notes at the beginning of a chart.
                return Math.Min(
                    0,
                    Time.Current - startTimeMilliseconds);
            }

            return Time.Current - startTimeMilliseconds;
        }
    }

    private double gameplayTimeForInput(Key key, bool isPressed)
    {
        bool hasEventTimestamp =
            keyInputTimestamps.TryTake(
                key,
                isPressed,
                out long eventTimestamp,
                out KeyInputTimestampKind timestampKind);

        if (!hasEventTimestamp)
            return currentGameplayTime;

        return gameplayTimeForTimestamp(eventTimestamp, timestampKind);
    }

    private double gameplayTimeForTimestamp(
        long eventTimestamp,
        KeyInputTimestampKind timestampKind)
    {
        long observationStart = Stopwatch.GetTimestamp();
        double gameplayTime = currentGameplayTime;
        long observationEnd = Stopwatch.GetTimestamp();

        long observationTimestamp =
            observationStart + (observationEnd - observationStart) / 2;
        if (GameplayInputClock.TryGetEventAgeMilliseconds(
                eventTimestamp,
                observationTimestamp,
                Stopwatch.Frequency,
                out double eventAgeMilliseconds))
        {
            inputAgeTracker.Record(eventAgeMilliseconds, timestampKind);
        }

        return GameplayInputClock.AtEventTimestamp(
            gameplayTime,
            eventTimestamp,
            observationTimestamp);
    }

    private void drainRawInput()
    {
        while (keyInputTimestamps.TryDequeueRaw(out TimestampedKeyInput input))
        {
            int lane = keyBindings.GetLane(input.Key);
            if (lane < 0)
                continue;

            double inputTime = gameplayTimeForTimestamp(
                input.Timestamp,
                KeyInputTimestampKind.RawInput);
            if (input.IsPressed)
                applyLanePress(lane, inputTime);
            else
                applyLaneRelease(lane, inputTime);
        }
    }

    private void drainReplayInput(double gameplayTime)
    {
        IReadOnlyList<GameplayReplayInput> inputs = replay.Inputs;

        while (replayInputIndex < inputs.Count
               && inputs[replayInputIndex].TimeMilliseconds <= gameplayTime)
        {
            GameplayReplayInput input = inputs[replayInputIndex++];

            if (input.IsPressed)
                applyLanePress(input.Lane, input.TimeMilliseconds);
            else
                applyLaneRelease(input.Lane, input.TimeMilliseconds);
        }
    }

    private void applyLanePress(int lane, double inputTime)
    {
        if (pressedLanes[lane])
            return;

        pressedLanes[lane] = true;
        playfield.SetLanePressed(lane, true);

        if (!ReplayMode)
        {
            recordedReplayInputs.Add(new GameplayReplayInput(
                lane,
                true,
                inputTime));
        }

        foreach (JudgementEvent judgement in
                 judgementState.JudgeLanePress(lane, inputTime))
        {
            applyJudgement(judgement);
        }
    }

    private void applyLaneRelease(int lane, double inputTime)
    {
        if (!pressedLanes[lane])
            return;

        pressedLanes[lane] = false;
        playfield.SetLanePressed(lane, false);

        if (!ReplayMode)
        {
            recordedReplayInputs.Add(new GameplayReplayInput(
                lane,
                false,
                inputTime));
        }

        foreach (JudgementEvent judgement in
                 judgementState.JudgeLaneRelease(lane, inputTime))
        {
            applyJudgement(judgement);
        }
    }

    private void applyJudgement(JudgementEvent judgement)
    {
        playfield.ApplyJudgement(judgement);
        if (judgement.Phase is not JudgementPhase.Hold
            and not JudgementPhase.HoldBody)
        {
            judgementReadout.Show(judgement);
        }
    }

    private void completeGameplay()
    {
        if (gameplayCompleted)
            return;

        gameplayCompleted = true;
        completedResult = judgementState.CreateResult();
        completedReplay = replay ?? new GameplayReplay(recordedReplayInputs);
        bool isNewBest = !ReplayMode
                         && scoreStore.SaveBest(beatmap, completedResult);
        _ = audioEngine.StopAsync();
        AddInternal(new GameplayResultOverlay(
            beatmap,
            completedResult,
            isNewBest,
            retryGameplay,
            watchCompletedReplay,
            () => this.Exit()));
    }

    internal void TogglePause()
    {
        if (pauseTransitionInProgress
            || gameplayBlocked
            || gameplayCompleted)
        {
            return;
        }

        if (isPaused)
            _ = resumeGameplayAsync();
        else
            _ = pauseGameplayAsync();
    }

    private async Task pauseGameplayAsync()
    {
        if (isPaused || pauseTransitionInProgress)
            return;

        pauseTransitionInProgress = true;
        pausedGameplayTime = currentGameplayTime;
        pausedAudioPosition = hasAudioClock
            ? Math.Max(0, audioEngine.PlaybackTimeMilliseconds)
            : 0;
        releasePressedLanesForPause();
        isPaused = true;

        if (!ReplayMode)
            keyInputTimestamps.EndCapture();

        pauseOverlay = new GameplayPauseOverlay(
            beatmap,
            TogglePause,
            retryGameplay,
            () => this.Push(new SettingsScreen()),
            exitPausedGameplay);
        AddInternal(pauseOverlay);

        try
        {
            if (hasAudioClock)
                await audioEngine.PauseAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Logger.Error(
                ex,
                "The audio engine could not pause gameplay.",
                LoggingTarget.Runtime);
            cancelFailedPause();
        }
        finally
        {
            pauseTransitionInProgress = false;
        }
    }

    private async Task resumeGameplayAsync()
    {
        if (!isPaused || pauseTransitionInProgress)
            return;

        pauseTransitionInProgress = true;

        try
        {
            if (hasAudioClock)
                await audioEngine.SeekAsync(pausedAudioPosition)
                                 .ConfigureAwait(true);
            else
                startTimeMilliseconds = Time.Current - pausedGameplayTime;

            isPaused = false;
            pauseOverlay?.FadeOut(140, Easing.OutQuint)
                         .Expire();
            pauseOverlay = null;

            if (!ReplayMode)
                keyInputTimestamps.BeginCapture();
        }
        catch (Exception ex)
        {
            Logger.Error(
                ex,
                "The audio engine could not resume paused gameplay.",
                LoggingTarget.Runtime);
        }
        finally
        {
            pauseTransitionInProgress = false;
        }
    }

    private void cancelFailedPause()
    {
        isPaused = false;
        pauseOverlay?.Expire();
        pauseOverlay = null;

        if (!ReplayMode)
            keyInputTimestamps.BeginCapture();
    }

    private void releasePressedLanesForPause()
    {
        if (ReplayMode)
            return;

        for (int lane = 0; lane < pressedLanes.Length; lane++)
        {
            if (pressedLanes[lane])
                applyLaneRelease(lane, pausedGameplayTime);
        }
    }

    private void exitPausedGameplay()
    {
        _ = audioEngine.StopAsync();
        this.Exit();
    }

    private void retryGameplay() =>
        this.Push(new GameplayScreen(
            beatmap,
            skinPath: skinPath));

    private void watchCompletedReplay()
    {
        if (completedReplay == null)
            return;

        this.Push(new GameplayScreen(
            beatmap,
            null,
            skinPath,
            completedReplay));
    }

    private void logInputTimingSummary()
    {
        KeyInputTimestampBackendStatus backend =
            keyInputTimestamps.Status;
        InputAgeStatistics ages = inputAgeTracker.Snapshot();
        string ageSummary = ages.Count == 0
            ? "no scored input samples"
            : $"input age p50={ages.P50Milliseconds:0.00} ms, "
              + $"p95={ages.P95Milliseconds:0.00} ms, "
              + $"p99={ages.P99Milliseconds:0.00} ms";

        Logger.Log(
            $"Gameplay input timing: {backend.Name}; "
            + $"captured={backend.CapturedEdgeCount}, "
            + $"pending={backend.PendingEdgeCount}, "
            + $"dropped={backend.DroppedEdgeCount}; "
            + ageSummary);
    }

    private void onScrollSpeedChanged(
        osu.Framework.Bindables.ValueChangedEvent<double> change)
    {
        playfield.SetApproachTime(
            OsuManiaScrollSpeed.ComputeScrollTime(change.NewValue));
    }

    internal bool HandleScrollSpeedShortcut(
        Key key,
        bool controlPressed)
    {
        double amount = key switch
        {
            Key.F4 => OsuManiaScrollSpeed.ShortcutStep,
            Key.F3 => -OsuManiaScrollSpeed.ShortcutStep,
            Key.Plus or Key.KeypadPlus when controlPressed =>
                OsuManiaScrollSpeed.ShortcutStep,
            Key.Minus or Key.KeypadMinus when controlPressed =>
                -OsuManiaScrollSpeed.ShortcutStep,
            _ => 0,
        };

        if (amount == 0)
            return false;

        gameplaySettings.AdjustScrollSpeed(amount);
        return true;
    }

    internal bool HandlePlayfieldWidthScroll(
        float scrollDelta,
        bool controlPressed)
    {
        if (!controlPressed || scrollDelta == 0)
            return false;

        playfieldWidthScale = Math.Clamp(
            playfieldWidthScale + Math.Sign(scrollDelta) * playfieldWidthStep,
            minimumPlayfieldWidthScale,
            maximumPlayfieldWidthScale);
        playfield.SetWidthScale(playfieldWidthScale);
        return true;
    }

    internal bool HandleIntroSkip()
    {
        if (!IntroSkipAvailable)
            return false;

        double target = IntroSkipTargetMilliseconds;
        introSkipUsed = true;

        if (!hasAudioClock)
        {
            startTimeMilliseconds = Time.Current - target;
            return true;
        }

        introSkipInProgress = true;

        if (!audioEngine.Status.IsRunning)
        {
            pendingIntroSkipMilliseconds = target;
            return true;
        }

        _ = seekToIntroAsync(target);
        return true;
    }

    private async Task seekToIntroAsync(double targetMilliseconds)
    {
        try
        {
            await audioEngine.SeekAsync(targetMilliseconds)
                             .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Logger.Error(
                ex,
                "The audio engine could not skip the gameplay intro.",
                LoggingTarget.Runtime);
        }
        finally
        {
            introSkipInProgress = false;
        }
    }

    private void loadSkin(IRenderer renderer)
    {
        string resolvedPath = !string.IsNullOrWhiteSpace(skinPath)
            ? skinPath
            : skinLibrary.CurrentSkinPath ?? OsuManiaSkinLocator.FindConfiguredPath();

        if (string.IsNullOrWhiteSpace(resolvedPath))
            return;

        try
        {
            maniaSkin = OsuManiaSkin.Load(resolvedPath, keyBindings.KeyCount, renderer);
        }
        catch (Exception ex)
        {
            // Invalid skins fail closed to Yokko's built-in playfield.
            Logger.Error(
                ex,
                $"Failed to load osu!mania skin '{resolvedPath}'. Falling back to the built-in playfield.",
                LoggingTarget.Runtime);
        }
    }
}
