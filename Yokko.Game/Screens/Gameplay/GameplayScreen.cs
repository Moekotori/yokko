using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osuTK;
using osuTK.Input;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Audio;
using Yokko.Game.Audio;
using Yokko.Game.Gameplay;
using Yokko.Game.Input;
using Yokko.Game.Importing;
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

    private readonly YokkoBeatmap originalBeatmap;
    private readonly YokkoBeatmap beatmap;
    private IAudioEngine audioEngine;
    private readonly string skinPath;
    private readonly ManiaModSet mods;
    private readonly GameplayReplay replay;
    private readonly string cinemaArtworkPath;
    private readonly bool quaverHasSignificantScrollVelocities;
    private TextureStore cinemaArtworkTextures;
    private readonly List<GameplayReplayInput> recordedReplayInputs = new();
    private readonly List<JudgementEvent> expiredJudgements = new();
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
    private ManiaHealthState healthState;
    private ManiaAdaptiveSpeedState adaptiveSpeedState;
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
    private bool gameplayFailed;
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
    private GameplayFailOverlay failOverlay;
    private JudgementReadout judgementReadout;
    private Task keysoundPreparationTask = Task.CompletedTask;
    private string[] keysoundPathsByHitObject = [];
    private GameplayKeysoundSelector keysoundSelector;
    private GameplayMutedAudioController mutedAudio;
    private GameplayCinemaIndicator cinemaIndicator;
    private double frameClockGameplayTime;
    private double frameClockLastFrameworkTime;
    private double lastAppliedDynamicRate = double.NaN;

    internal bool GameplayBlocked => gameplayBlocked;
    internal bool GameplayCompleted => gameplayCompleted;
    internal bool GameplayFailed => gameplayFailed;
    internal bool IsPaused => isPaused;
    internal bool PauseTransitionInProgress => pauseTransitionInProgress;
    internal double CurrentGameplayTime => currentGameplayTime;
    internal ManiaScoreResult CompletedResult => completedResult;
    internal bool ReplayMode => replay != null;
    internal bool AutoplayMode => mods.IsAutomation;
    internal ManiaModSet Mods => mods;
    internal ManiaHealthState HealthState => healthState;
    internal GameplayMutedAudioController MutedAudio => mutedAudio;
    internal JudgementWindows ActiveJudgementWindows =>
        judgementState?.Windows;
    internal YokkoBeatmap AppliedBeatmap => beatmap;
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
        string skinPath = null,
        ManiaModSet mods = null,
        string cinemaArtworkPath = null)
        : this(
            beatmap,
            audioEngine,
            skinPath,
            mods,
            null,
            cinemaArtworkPath)
    {
    }

    internal GameplayScreen(
        YokkoBeatmap beatmap,
        IAudioEngine audioEngine,
        string skinPath,
        ManiaModSet mods,
        GameplayReplay replay)
        : this(beatmap, audioEngine, skinPath, mods, replay, null)
    {
    }

    private GameplayScreen(
        YokkoBeatmap beatmap,
        IAudioEngine audioEngine,
        string skinPath,
        ManiaModSet mods,
        GameplayReplay replay,
        string cinemaArtworkPath)
    {
        originalBeatmap = beatmap;
        this.audioEngine = audioEngine;
        this.skinPath = skinPath;
        this.mods = mods ?? replay?.Mods ?? ManiaModSet.Empty;
        this.beatmap = ManiaBeatmapModTransformer.Apply(
            beatmap,
            this.mods);
        quaverHasSignificantScrollVelocities =
            this.beatmap.SourceFormat == ChartSourceFormat.Quaver
            && !this.mods.Contains(ManiaModId.ConstantSpeed)
            && hasSignificantScrollVelocities(this.beatmap);
        this.replay = replay
                      ?? (this.mods.IsAutomation
                          ? GameplayAutoGenerator.Generate(
                              this.beatmap,
                              this.mods)
                          : null);
        this.cinemaArtworkPath = cinemaArtworkPath;
        completionTimeMilliseconds = this.beatmap.HitObjects.Count == 0
            ? 0
            : this.beatmap.HitObjects.Max(hitObject =>
                hitObject.EndTimeMilliseconds
                ?? hitObject.StartTimeMilliseconds);
        firstObjectTimeMilliseconds = this.beatmap.HitObjects.Count == 0
            ? 0
            : this.beatmap.HitObjects.Min(hitObject =>
                hitObject.StartTimeMilliseconds);
    }

    [BackgroundDependencyLoader]
    private void load(IRenderer renderer, GameHost host)
    {
        this.host = host;
        keyBindings = gameplaySettings.SupportedKeyModes.Contains(
            beatmap.KeyMode)
            ? KeyModeBindings.ForMode(
                beatmap.KeyMode,
                gameplaySettings.GetKeys(beatmap.KeyMode))
            : KeyModeBindings.ForMode(
                beatmap.KeyMode,
                beatmap.StageCount);
        pressedLanes = new bool[keyBindings.KeyCount];
        healthState = new ManiaHealthState(beatmap, mods);
        if (mods.HasAdaptiveSpeed)
        {
            adaptiveSpeedState = new ManiaAdaptiveSpeedState(
                beatmap,
                mods.AdaptiveInitialRate);
        }
        judgementState = new BeatmapJudgementState(
            beatmap,
            new JudgementWindows(
                mods.EffectiveOverallDifficulty(
                    beatmap.OverallDifficulty),
                mods.HitWindowSpeedMultiplier,
                mods.HitWindowDifficultyMultiplier,
                mods.Contains(ManiaModId.Classic),
                mods.Contains(ManiaModId.ScoreV2),
                beatmap.ConversionSource is not null),
            mods.Contains(ManiaModId.NoRelease),
            mods.ScoreMultiplier);
        keysoundSelector = new GameplayKeysoundSelector(
            beatmap,
            judgementState);
        loadSkin(renderer);
        bool hasKeysounds = beatmap.HitObjects.Any(
            static hitObject => !string.IsNullOrWhiteSpace(hitObject.SampleKey));
        audioEngine ??= string.IsNullOrWhiteSpace(beatmap.AudioPath)
                         && !hasKeysounds
            ? new NullAudioEngine()
            : AudioEngineFactory.CreateDefault();
        if (audioEngine is IAudioMixControl mixControl)
        {
            if (mods.Contains(ManiaModId.Muted))
            {
                mutedAudio = new GameplayMutedAudioController(
                    beatmap,
                    mods,
                    mixControl,
                    audioSettings.EffectiveMusicVolume,
                    gameplaySettings.KeysoundsEnabled.Value
                        ? audioSettings.EffectiveHitSoundVolume
                        : 0,
                    Math.Clamp(audioSettings.MasterVolume.Value, 0, 1));
            }
            else
            {
                audioSettings.ApplyMixSettings(
                    mixControl,
                    gameplaySettings.KeysoundsEnabled.Value);
            }
        }
        hasAudioClock = !string.IsNullOrWhiteSpace(beatmap.AudioPath)
                        || hasKeysounds && audioEngine is NativeAudioEngine;
        prepareKeysoundPaths();
        keysoundPreparationTask = prepareKeysoundsAsync();

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
                computeApproachTime(
                    gameplaySettings.ScrollSpeed.Value,
                    this.mods.PlaybackRate),
                gameplaySettings.ShowLanePressFeedback.Value,
                mods)
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                // Legacy osu!mania values already use the skin's 480px
                // coordinate space. Scale the complete stage uniformly to the
                // available height so it stays grounded without distorting
                // skin geometry.
                Scale = Vector2.One,
            },
            hud = new GameplayHud(beatmap, mods)
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

        if (mods.IsCinema)
        {
            Texture cinemaTexture = loadCinemaTexture(renderer);
            if (cinemaTexture != null)
            {
                AddInternal(new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Texture = cinemaTexture,
                    FillMode = FillMode.Fill,
                    Colour = new osuTK.Graphics.Color4(
                        0.82f,
                        0.82f,
                        0.86f,
                        1),
                    Depth = -10,
                });
            }
            playfield.Alpha = 0;
            hud.Alpha = 0;
            judgementReadout.Alpha = 0;
            AddInternal(cinemaIndicator = new GameplayCinemaIndicator
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-24, 24),
                Depth = -200,
            });
        }

        if (!hasAudioClock)
            hud.ShowFrameClock();

        gameplaySettings.ScrollSpeed.BindValueChanged(
            onScrollSpeedChanged,
            true);
        host.Deactivated += onHostDeactivated;
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        if (!ReplayMode && !isPaused)
            keyInputTimestamps.BeginCapture();

        startTimeMilliseconds = Time.Current + leadInMilliseconds;
        frameClockGameplayTime =
            -leadInMilliseconds * mods.PlaybackRate;
        frameClockLastFrameworkTime = Time.Current;

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

        if (gameplayBlocked
            || gameplayCompleted
            || gameplayFailed
            || isPaused)
            return;

        adaptiveSpeedState?.Update(Time.Elapsed);
        GameplayClockObservation clockObservation = observeGameplayClock();

        if (hasAudioClock && audioStarted)
        {
            AudioEngineStatus audioStatus = clockObservation.Audio.Status;
            hud.UpdateAudioStatus(audioStatus, activeRequestedBackend);
            if (audioStatus.IsFaulted
                || (!audioStatus.IsRunning && !introSkipInProgress))
            {
                failAudioRuntime(audioStatus);
                return;
            }
        }

        double gameplayTime = clockObservation.GameplayTime;
        updateDynamicRate(gameplayTime);
        updateQuaverScrollRate(gameplayTime);
        if (mutedAudio != null)
        {
            mutedAudio.Update(
                Time.Elapsed,
                judgementState.Combo,
                gameplayTime);
            hud.UpdateMutedMix(mutedAudio.Current);
        }
        double visualGameplayTime = hasAudioClock
            ? GameplayPresentationClock.EstimateVisualTime(
                gameplayTime,
                host.Window?.CurrentDisplayMode.Value.RefreshRate ?? 60)
            : gameplayTime;

        if (ReplayMode)
            drainReplayInput(gameplayTime);
        else
            drainRawInput(clockObservation);

        expiredJudgements.Clear();
        judgementState.CollectExpiredMisses(
            gameplayTime,
            expiredJudgements);
        foreach (JudgementEvent missed in expiredJudgements)
        {
            applyJudgement(missed);
            if (gameplayFailed)
                return;
        }

        playfield.UpdateGameplayTime(visualGameplayTime, judgementState);
        hud.UpdateState(gameplayTime, judgementState, healthState);

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
        if (beatmap.StageCount == 2 && DrawWidth > 0)
        {
            scale = Math.Min(
                scale,
                DrawWidth * 0.94f / playfield.Width);
        }
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

        if (gameplayFailed)
            return failOverlay?.HandleKey(e.Key) ?? true;

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
        if (gameplayCompleted
            || gameplayFailed
            || ReplayMode
            || isPaused)
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

        mutedAudio?.Restore();
        _ = audioEngine.StopAsync();
        return base.OnExiting(e);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            if (!ReplayMode)
                keyInputTimestamps.EndCapture();

            host.Deactivated -= onHostDeactivated;
            gameplaySettings.ScrollSpeed.ValueChanged -=
                onScrollSpeedChanged;
            mutedAudio?.Restore();
            _ = audioEngine.DisposeAsync();
            maniaSkin?.Dispose();
            cinemaArtworkTextures?.Dispose();
        }

        base.Dispose(isDisposing);
    }

    private async Task startAudioAsync()
    {
        try
        {
            await keysoundPreparationTask.ConfigureAwait(true);
            activeUserOffsetMilliseconds =
                audioSettings.UserOffsetMilliseconds.Value;
            AudioEngineStartRequest startRequest =
                audioSettings.CreateStartRequest(
                    beatmap.AudioPath,
                    mods.PlaybackRate,
                    mods.ChangesAudioPitch
                        ? AudioPitchMode.ScaleWithRate
                        : AudioPitchMode.Preserve,
                    mods.FixedAudioFrequencyScale);
            if (mods.HasDynamicRate)
            {
                startRequest = startRequest with
                {
                    DynamicPlaybackRate = true,
                };
            }
            activeRequestedBackend = startRequest.PreferredBackend;

            await audioEngine.StartAsync(startRequest)
                             .ConfigureAwait(true);

            AudioEngineSnapshot audioSnapshot = audioEngine.Snapshot;
            if (!audioSnapshot.Status.IsRunning)
            {
                failAudioStart("The audio engine returned without starting playback.");
                return;
            }

            audioStarted = true;
            lastStableAudioGameplayTime =
                audioSnapshot.PlaybackTimeMilliseconds
                + activeUserOffsetMilliseconds;
            hud.UpdateAudioStatus(
                audioSnapshot.Status,
                activeRequestedBackend);

            if (isPaused)
            {
                pausedAudioPosition = Math.Max(
                    0,
                    audioSnapshot.PlaybackTimeMilliseconds);
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

    private double currentGameplayTime =>
        observeGameplayClock().GameplayTime;

    private GameplayClockObservation observeGameplayClock()
    {
        long observationStart = Stopwatch.GetTimestamp();
        AudioEngineSnapshot audioSnapshot = default;
        double gameplayTime;

        if (isPaused)
        {
            gameplayTime = pausedGameplayTime;
        }
        else if (hasAudioClock)
        {
            audioSnapshot = audioEngine.Snapshot;
            if (audioSnapshot.Status.IsRunning)
            {
                lastStableAudioGameplayTime =
                    audioSnapshot.PlaybackTimeMilliseconds
                    + activeUserOffsetMilliseconds;
                gameplayTime = lastStableAudioGameplayTime;
            }
            else if (audioStarted)
                gameplayTime = lastStableAudioGameplayTime;
            else
            {
                // Hold at zero while the audio device opens so its startup
                // time cannot consume notes at the beginning of a chart.
                gameplayTime = Math.Min(
                    0,
                    (Time.Current - startTimeMilliseconds)
                    * mods.PlaybackRate);
            }
        }
        else
        {
            if (mods.HasDynamicRate)
            {
                double elapsed = Math.Max(
                    0,
                    Time.Current - frameClockLastFrameworkTime);
                double rate = currentDynamicRate(
                    frameClockGameplayTime);
                frameClockGameplayTime += elapsed * rate;
                frameClockLastFrameworkTime = Time.Current;
                gameplayTime = frameClockGameplayTime;
            }
            else
            {
                gameplayTime =
                    (Time.Current - startTimeMilliseconds)
                    * mods.PlaybackRate;
            }
        }

        long observationEnd = Stopwatch.GetTimestamp();
        return new GameplayClockObservation(
            audioSnapshot,
            gameplayTime,
            observationStart
            + (observationEnd - observationStart) / 2);
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

        return gameplayTimeForTimestamp(
            eventTimestamp,
            timestampKind,
            observeGameplayClock());
    }

    private double gameplayTimeForTimestamp(
        long eventTimestamp,
        KeyInputTimestampKind timestampKind,
        GameplayClockObservation observation)
    {
        if (GameplayInputClock.TryGetEventAgeMilliseconds(
                eventTimestamp,
                observation.Timestamp,
                Stopwatch.Frequency,
                out double eventAgeMilliseconds))
        {
            inputAgeTracker.Record(eventAgeMilliseconds, timestampKind);
        }

        return GameplayInputClock.AtEventTimestamp(
            observation.GameplayTime,
            eventTimestamp,
            observation.Timestamp,
            gameplayRate: currentPlaybackRate(
                observation.GameplayTime));
    }

    private void drainRawInput(GameplayClockObservation observation)
    {
        while (keyInputTimestamps.TryDequeueRaw(out TimestampedKeyInput input))
        {
            if (gameplayFailed)
                break;

            int lane = keyBindings.GetLane(input.Key);
            if (lane < 0)
                continue;

            double inputTime = gameplayTimeForTimestamp(
                input.Timestamp,
                KeyInputTimestampKind.RawInput,
                observation);
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
               && !gameplayFailed
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
        triggerKeysoundForLanePress(lane, inputTime);

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

    private void prepareKeysoundPaths()
    {
        keysoundPathsByHitObject = beatmap.HitObjects
            .Select(hitObject => normaliseKeysoundPath(hitObject.SampleKey))
            .ToArray();
    }

    private async Task prepareKeysoundsAsync()
    {
        if (!gameplaySettings.KeysoundsEnabled.Value
            || audioEngine is not IAudioSamplePlayback samplePlayback)
            return;

        string[] paths = keysoundPathsByHitObject
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (paths.Length == 0)
            return;

        try
        {
            await samplePlayback.PrepareSamplesAsync(paths)
                                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Logger.Error(
                ex,
                "Gameplay keysounds could not be prepared; backing audio will continue.",
                LoggingTarget.Runtime);
        }
    }

    private void triggerKeysoundForLanePress(int lane, double inputTime)
    {
        if (!gameplaySettings.KeysoundsEnabled.Value
            || audioEngine is not IAudioSamplePlayback samplePlayback)
            return;

        int selected = keysoundSelector.Select(lane, inputTime);
        if ((uint)selected >= keysoundPathsByHitObject.Length)
            return;

        string path = keysoundPathsByHitObject[selected];
        if (!string.IsNullOrWhiteSpace(path))
            samplePlayback.TriggerSample(path);
    }

    private string normaliseKeysoundPath(string sampleKey)
    {
        if (string.IsNullOrWhiteSpace(sampleKey))
            return null;

        try
        {
            if (Path.IsPathRooted(sampleKey))
                return Path.GetFullPath(sampleKey);

            string audioDirectory = string.IsNullOrWhiteSpace(beatmap.AudioPath)
                ? null
                : Path.GetDirectoryName(Path.GetFullPath(beatmap.AudioPath));
            return Path.GetFullPath(
                audioDirectory == null
                    ? sampleKey
                    : Path.Combine(audioDirectory, sampleKey));
        }
        catch
        {
            return null;
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
        adaptiveSpeedState?.Apply(judgement);
        ManiaHealthUpdate healthUpdate = healthState.Apply(
            judgement,
            judgementState.Accuracy,
            judgementState.MaximumAchievableAccuracy);
        if (judgement.Phase is not JudgementPhase.Hold
            and not JudgementPhase.HoldBody)
        {
            judgementReadout.Show(judgement);
        }

        if (healthUpdate.ExtraLifeConsumed)
            hud.ShowExtraLifeUsed();

        if (healthUpdate.Failed)
            failGameplay();
    }

    private void failGameplay()
    {
        if (gameplayFailed || gameplayCompleted)
            return;

        gameplayFailed = true;
        mutedAudio?.Restore();
        for (int lane = 0; lane < pressedLanes.Length; lane++)
        {
            pressedLanes[lane] = false;
            playfield.SetLanePressed(lane, false);
        }

        _ = audioEngine.StopAsync();
        AddInternal(failOverlay = new GameplayFailOverlay(
            beatmap,
            judgementState,
            healthState,
            mods,
            retryGameplay,
            () => this.Exit()));
    }

    private void completeGameplay()
    {
        if (gameplayCompleted)
            return;

        gameplayCompleted = true;
        mutedAudio?.Restore();
        ManiaScoreResult rawResult =
            judgementState.CreateResult();
        completedResult = rawResult with
        {
            Rank = mods.AdjustRank(rawResult.Rank),
        };
        completedReplay = replay
                          ?? new GameplayReplay(
                              recordedReplayInputs,
                              mods);
        bool isNewBest = !ReplayMode
                         && scoreStore.SaveBest(
                             originalBeatmap,
                             mods,
                             completedResult);
        _ = audioEngine.StopAsync();
        AddInternal(new GameplayResultOverlay(
            beatmap,
            completedResult,
            mods,
            isNewBest,
            retryGameplay,
            watchCompletedReplay,
            () => this.Exit()));
    }

    internal void TogglePause()
    {
        if (pauseTransitionInProgress
            || gameplayBlocked
            || gameplayCompleted
            || gameplayFailed)
        {
            return;
        }

        if (isPaused)
            _ = resumeGameplayAsync();
        else
            _ = pauseGameplayAsync();
    }

    internal void HandleHostDeactivated()
    {
        if (gameplaySettings.PauseWhenUnfocused.Value
            && !isPaused
            && !pauseTransitionInProgress
            && !gameplayBlocked
            && !gameplayCompleted
            && !gameplayFailed)
        {
            TogglePause();
        }
    }

    private void onHostDeactivated() =>
        HandleHostDeactivated();

    private async Task pauseGameplayAsync()
    {
        if (isPaused || pauseTransitionInProgress)
            return;

        pauseTransitionInProgress = true;
        GameplayClockObservation observation = observeGameplayClock();
        pausedGameplayTime = observation.GameplayTime;
        pausedAudioPosition = hasAudioClock
            ? Math.Max(
                0,
                observation.Audio.PlaybackTimeMilliseconds)
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
            {
                await audioEngine.SeekAsync(pausedAudioPosition)
                                 .ConfigureAwait(true);
                lastAppliedDynamicRate = double.NaN;
            }
            else
            {
                startTimeMilliseconds =
                    Time.Current
                    - pausedGameplayTime / mods.PlaybackRate;
                frameClockGameplayTime = pausedGameplayTime;
                frameClockLastFrameworkTime = Time.Current;
            }

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
            originalBeatmap,
            skinPath: skinPath,
            mods: mods,
            cinemaArtworkPath: cinemaArtworkPath));

    private void watchCompletedReplay()
    {
        if (completedReplay == null)
            return;

        this.Push(new GameplayScreen(
            originalBeatmap,
            null,
            skinPath,
            mods,
            completedReplay,
            cinemaArtworkPath));
    }

    private void updateDynamicRate(double gameplayTime)
    {
        if (!mods.HasDynamicRate)
            return;

        double rate = currentDynamicRate(gameplayTime);
        hud.UpdateDynamicRate(rate);
        if (!audioStarted
            || audioEngine is not IAudioRateControl rateControl
            || Math.Abs(rate - lastAppliedDynamicRate) < 0.005)
        {
            return;
        }

        rateControl.SetPlaybackRate(rate);
        lastAppliedDynamicRate = rate;
    }

    private double currentPlaybackRate(double gameplayTime) =>
        mods.HasDynamicRate
            ? currentDynamicRate(gameplayTime)
            : mods.PlaybackRate;

    private double currentDynamicRate(double gameplayTime) =>
        adaptiveSpeedState?.CurrentRate
        ?? mods.PlaybackRateAt(
            gameplayTime,
            firstObjectTimeMilliseconds,
            completionTimeMilliseconds);

    private Texture loadCinemaTexture(IRenderer renderer)
    {
        if (string.IsNullOrWhiteSpace(cinemaArtworkPath))
            return null;

        try
        {
            cinemaArtworkTextures = new TextureStore(
                renderer,
                new TextureLoaderStore(
                    new ConstrainedTextureResourceStore(
                        new ChartArtworkResourceStore(),
                        renderer.MaxTextureSize)),
                scaleAdjust: 1);
            return cinemaArtworkTextures.Get(cinemaArtworkPath);
        }
        catch
        {
            cinemaArtworkTextures?.Dispose();
            cinemaArtworkTextures = null;
            return null;
        }
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
        playfield.SetApproachTime(computeApproachTime(
            change.NewValue,
            currentPlaybackRate(currentGameplayTime)));
    }

    private double computeApproachTime(
        double scrollSpeed,
        double playbackRate = 1)
    {
        double baseApproachTime = maniaSkin == null
            ? OsuManiaScrollSpeed.ComputeScrollTime(scrollSpeed)
            : OsuManiaScrollSpeed.ComputeScrollTime(
                scrollSpeed,
                maniaSkin.Configuration.HitPosition);
        return AdjustApproachTimeForPlaybackRate(
            baseApproachTime,
            beatmap.SourceFormat,
            playbackRate,
            gameplaySettings.QuaverScrollRateNormalization.Value,
            quaverHasSignificantScrollVelocities);
    }

    private void updateQuaverScrollRate(double gameplayTime)
    {
        if (beatmap.SourceFormat != ChartSourceFormat.Quaver)
            return;

        playfield.SetApproachTime(computeApproachTime(
            gameplaySettings.ScrollSpeed.Value,
            currentPlaybackRate(gameplayTime)));
    }

    internal static double AdjustApproachTimeForPlaybackRate(
        double baseApproachTime,
        ChartSourceFormat sourceFormat,
        double playbackRate,
        double quaverNormalizationPercentage = 0,
        bool hasSignificantScrollVelocities = true)
    {
        if (!double.IsFinite(baseApproachTime)
            || baseApproachTime <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseApproachTime));
        }

        if (!double.IsFinite(playbackRate) || playbackRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(playbackRate));

        if (sourceFormat != ChartSourceFormat.Quaver)
            return baseApproachTime;

        if (!double.IsFinite(quaverNormalizationPercentage)
            || quaverNormalizationPercentage < 0
            || quaverNormalizationPercentage > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quaverNormalizationPercentage));
        }

        // Mirrors Quaver's NormaliseScrollVelocityByRatePercentage:
        // 0% divides visual speed by the full audio rate, while 100%
        // cancels that division and lets rate mods scale approach time.
        double normalization = hasSignificantScrollVelocities
            ? quaverNormalizationPercentage
            : 0;
        double rateScaling = 1
                             + (playbackRate - 1)
                             * normalization
                             / 100;
        return baseApproachTime * playbackRate / rateScaling;
    }

    private static bool hasSignificantScrollVelocities(
        YokkoBeatmap beatmap) =>
        beatmap.ScrollVelocities.Any(isSignificantScrollVelocity)
        || beatmap.ScrollProfiles.Values.Any(profile =>
            profile.ScrollVelocities.Any(isSignificantScrollVelocity));

    private static bool isSignificantScrollVelocity(
        Yokko.Core.Timing.YokkoScrollVelocity velocity) =>
        velocity.Multiplier > 1.01
        || velocity.Multiplier < 0.99;

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
            frameClockGameplayTime = target;
            frameClockLastFrameworkTime = Time.Current;
            return true;
        }

        introSkipInProgress = true;

        if (!audioEngine.Snapshot.Status.IsRunning)
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
            lastAppliedDynamicRate = double.NaN;
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

    private readonly record struct GameplayClockObservation(
        AudioEngineSnapshot Audio,
        double GameplayTime,
        long Timestamp);
}
