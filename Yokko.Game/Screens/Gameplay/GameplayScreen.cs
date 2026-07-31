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
using Yokko.Core.Difficulty;
using Yokko.Core.Gameplay;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Core.Timing;
using Yokko.Audio;
using Yokko.Game.Audio;
using Yokko.Game.Diagnostics;
using Yokko.Game.Configuration;
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

    // Matches ppy/osu DrawableRuleset.GameplayStartTime and
    // SubmittingPlayer.AllowCriticalSettingsAdjustment at commit 5da7100
    // (MIT): the adjustment window starts two seconds before the first object.
    private const double gameplayStartLeadInMilliseconds = 2000;
    private const double scrollSpeedAdjustmentGraceMilliseconds = 10000;
    private const float playfieldWidthStep = 0.1f;
    private const float minimumPlayfieldWidthScale = 0.2f;
    private const float maximumPlayfieldWidthScale = 2.5f;
    private const double playbackRateStep = 0.05;
    private const double minimumPlaybackRate = 0.25;
    private const double maximumPlaybackRate = 4;
    private const double completionSettleMilliseconds = 180;
    private const double completionResultRevealMilliseconds = 260;
    private const double completionSkipDelayMilliseconds = 320;
    private const double completionTailFadeStartMilliseconds = 520;
    private const double completionTransitionMilliseconds = 740;

    private readonly YokkoBeatmap originalBeatmap;
    private readonly YokkoBeatmap beatmap;
    private IAudioEngine audioEngine;
    private readonly string skinPath;
    private readonly ManiaModSet mods;
    private GameplayReplay replay;
    private JudgementConfiguration judgementConfiguration;
    private bool minesEnabled;
    private readonly string cinemaArtworkPath;
    private readonly bool quaverHasSignificantScrollVelocities;
    private readonly BeatTimingMap beatTimingMap;
    private TextureStore cinemaArtworkTextures;
    private readonly List<GameplayReplayInput> recordedReplayInputs = new();
    private readonly List<JudgementEvent> expiredJudgements = new();
    private readonly List<JudgementEvent> inputJudgements = new(8);
    [Resolved]
    private YokkoAudioSettings audioSettings { get; set; }
    [Resolved]
    private OsuManiaSkinLibrary skinLibrary { get; set; }
    [Resolved(canBeNull: true)]
    private YokkoSkinSettings skinSettings { get; set; }
    [Resolved]
    private YokkoGameplaySettings gameplaySettings { get; set; }
    [Resolved]
    private YokkoDisplaySettings displaySettings { get; set; }
    [Resolved]
    private KeyInputTimestampSource keyInputTimestamps { get; set; }
    [Resolved]
    private GameplayScoreStore scoreStore { get; set; }
    [Resolved]
    private GameplayReplayStore replayStore { get; set; }
    [Resolved]
    private ImportedChartLibrary importedChartLibrary { get; set; }
    [Resolved]
    private YokkoConfigManager yokkoConfig { get; set; }
    [Resolved]
    private YokkoDiagnostics diagnostics { get; set; }

    private BeatmapJudgementState judgementState;
    private ManiaHealthState healthState;
    private ManiaAdaptiveSpeedState adaptiveSpeedState;
    private GameplayPlayfield playfield;
    private GameplayLaneCovers laneCovers;
    private GameplayLayoutEditorOverlay layoutEditor;
    private KeyModeBindings keyBindings;
    private bool[] pressedLanes;
    private double startTimeMilliseconds;
    private bool hasAudioClock;
    private bool audioStarted;
    private OsuManiaSkin maniaSkin;
    private OsuManiaSkinLease maniaSkinLease;

    [Resolved]
    private OsuManiaSkinCache gameplaySkinCache { get; set; }
    private double activeUserOffsetMilliseconds;
    private AudioBackendKind activeRequestedBackend;
    private double lastStableAudioGameplayTime;
    private readonly InputAgeTracker inputAgeTracker = new();
    private readonly InputPipelineLatencyTracker inputPipelineLatencyTracker =
        new();
    private readonly InputDropTracker inputDropTracker = new();
    private readonly AudioSampleTriggerLatencyTracker
        audioSampleTriggerLatencyTracker = new();
    private ulong previousAudioSampleTelemetryDropped;
    private ulong accumulatedAudioSampleTelemetryDropped;
    private bool gameplayBlocked;
    private bool gameplayCompleted;
    private bool gameplayCompletionTransitionActive;
    private bool gameplayFailed;
    private double diagnosticSnapshotElapsed;
    private bool retryTransitionInProgress;
    private GameplayHud hud;
    private ManiaScoreResult completedResult;
    private GameplayResultOverlay resultOverlay;
    private bool completedResultIsNewBest;
    private double completionTransitionElapsedMilliseconds;
    private IAudioMixControl completionMixControl;
    private double completionMusicVolume;
    private double completionHitSoundVolume;
    private double completionMetronomeVolume;
    private bool completionAudioStopRequested;
    private Task completionAudioStopTask = Task.CompletedTask;
    private double completionTimeMilliseconds;
    private double firstObjectTimeMilliseconds;
    private double gameplayStartTimeMilliseconds;
    private bool introSkipInProgress;
    private bool introSkipUsed;
    private double pendingIntroSkipMilliseconds = double.NaN;
    private int replayInputIndex;
    private GameplayReplay completedReplay;
    private float playfieldWidthScale = 1;
    private GameHost host;
    private IRenderer renderer;
    private bool isPaused;
    private bool pauseTransitionInProgress;
    private bool resumeCountdownInProgress;
    private double pausedGameplayTime;
    private double pausedAudioPosition;
    private GameplayPauseOverlay pauseOverlay;
    private GameplayResumeCountdown resumeCountdown;
    private double quickRetryHoldStartTime = double.NaN;
    private GameplayQuickRetryIndicator quickRetryIndicator;
    private GameplayFailOverlay failOverlay;
    private Box backgroundDim;
    private GameplayComboReadout comboReadout;
    private JudgementReadout judgementReadout;
    private GameplayTimingBar timingBar;
    private GameplayScrollSpeedOverlay scrollSpeedOverlay;
    private GameplayPlaybackRateOverlay playbackRateOverlay;
    private double appliedScrollSpeed;
    private Task keysoundPreparationTask = Task.CompletedTask;
    private GameplayHitSamplePlaybackBinding[][] headSamplesByHitObject = [];
    private GameplayHitSamplePlaybackBinding[][] tailSamplesByHitObject = [];
    private GameplayHitSamplePlaybackBinding[][] slidingSamplesByHitObject = [];
    private GameplayHitSamplePlaybackBinding[] scheduledSamples = [];
    private int nextScheduledSampleIndex;
    private double previousScheduledSampleTime = double.NegativeInfinity;
    private readonly Dictionary<int, List<uint>> activeSlidingSampleLoops = new();
    private GameplayKeysoundSelector keysoundSelector;
    private RawInputKeysoundDispatcher rawKeysoundDispatcher;
    private bool rawKeysoundFastPathAllowed = true;
    private GameplaySlidingSampleIndex slidingSampleIndex;
    private GameplayHitSampleResolver hitSampleResolver;
    private GameplayMutedAudioController mutedAudio;
    private GameplayCinemaIndicator cinemaIndicator;
    private double frameClockGameplayTime;
    private double frameClockLastFrameworkTime;
    private double lastAppliedPlaybackRate = double.NaN;
    private double lastApproachPlaybackRate = double.NaN;
    private double manualPlaybackRateAdjustment;
    private bool manualPlaybackRateUsed;
    private readonly Dictionary<double, ManiaDifficultyRatings>
        difficultyByRate = new();
    private Task<ManiaDifficultyRatings> difficultyCalculationTask;
    private double difficultyCalculationRate = double.NaN;

    internal bool GameplayBlocked => gameplayBlocked;
    internal bool GameplayCompleted => gameplayCompleted;
    internal bool CompletionTransitionActive =>
        gameplayCompletionTransitionActive;
    internal double CompletionTransitionElapsedMilliseconds =>
        completionTransitionElapsedMilliseconds;
    internal bool GameplayFailed => gameplayFailed;
    internal bool IsPaused => isPaused;
    internal bool IsLayoutEditing =>
        layoutEditor?.IsSessionActive == true;
    internal bool IsLayoutTestPlaying =>
        layoutEditor?.IsTestingLayout == true;
    internal float LayoutOverviewAspectRatio =>
        layoutEditor?.OverviewAspectRatio ?? 0;
    internal bool PauseTransitionInProgress => pauseTransitionInProgress;
    internal bool ResumeCountdownInProgress => resumeCountdownInProgress;
    internal bool QuickRetryHoldActive =>
        !double.IsNaN(quickRetryHoldStartTime);
    internal double? ResumeCountdownMillisecondsOverride;
    internal double QuickRetryHoldMilliseconds = 400;

    private double resumeCountdownDuration =>
        ResumeCountdownMillisecondsOverride
        ?? (gameplaySettings.ResumeCountdownEnabled.Value
            ? gameplaySettings.ResumeCountdownMilliseconds.Value
            : 0);
    internal double CurrentGameplayTime => currentGameplayTime;
    internal ManiaScoreResult CompletedResult => completedResult;
    internal string SavedReplayPath { get; private set; }
    internal bool BestScoreSaved { get; private set; }
    internal bool ReplayMode => replay != null;
    internal bool AutoplayMode => mods.IsAutomation;
    internal ManiaModSet Mods => mods;
    internal double CurrentPlaybackRate =>
        currentPlaybackRate(currentGameplayTime);
    internal bool ManualPlaybackRateUsed =>
        manualPlaybackRateUsed;
    internal bool IsLanePressed(int lane) =>
        pressedLanes != null
        && (uint)lane < pressedLanes.Length
        && pressedLanes[lane];
    internal ManiaHealthState HealthState => healthState;
    internal GameplayMutedAudioController MutedAudio => mutedAudio;
    internal JudgementWindows ActiveJudgementWindows =>
        judgementState?.Windows;
    internal JudgementConfiguration ActiveJudgementConfiguration =>
        judgementConfiguration;
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
        beatTimingMap = new BeatTimingMap(this.beatmap.TimingPoints);
        this.replay = replay;
        this.cinemaArtworkPath = cinemaArtworkPath;
        updateGameplayBounds(includeMines: true);
    }

    private void updateGameplayBounds(bool includeMines)
    {
        YokkoHitObject[] gameplayObjects = beatmap.HitObjects
            .Where(hitObject =>
                includeMines
                || hitObject.Kind != HitObjectKind.Mine)
            .ToArray();
        completionTimeMilliseconds = gameplayObjects.Length == 0
            ? 0
            : gameplayObjects.Max(hitObject =>
                hitObject.EndTimeMilliseconds
                ?? hitObject.StartTimeMilliseconds);
        if (beatmap.ScheduledSamples.Count > 0)
        {
            completionTimeMilliseconds = Math.Max(
                completionTimeMilliseconds,
                beatmap.ScheduledSamples.Max(
                    static sample => sample.TimeMilliseconds));
        }
        firstObjectTimeMilliseconds = gameplayObjects.Length == 0
            ? 0
            : gameplayObjects.Min(hitObject =>
                hitObject.StartTimeMilliseconds);
        gameplayStartTimeMilliseconds = gameplayObjects.Length == 0
            ? 0
            : firstObjectTimeMilliseconds
              - gameplayStartLeadInMilliseconds;
    }

    [BackgroundDependencyLoader]
    private void load(IRenderer renderer, GameHost host)
    {
        this.renderer = renderer;
        this.host = host;
        minesEnabled = gameplaySettings.MinesEnabled.Value;
        updateGameplayBounds(minesEnabled);
        judgementConfiguration =
            replay?.JudgementConfiguration
            ?? (beatmap.SourceFormat == ChartSourceFormat.Quaver
                ? JudgementConfiguration.QuaverDefault
                : gameplaySettings.GetJudgementConfiguration());
        if (replay == null && mods.IsAutomation)
        {
            replay = GameplayAutoGenerator.Generate(
                beatmap,
                mods,
                judgementConfiguration);
        }
        keyBindings = gameplaySettings.SupportedKeyModes.Contains(
            beatmap.KeyMode)
            ? KeyModeBindings.ForMode(
                beatmap.KeyMode,
                gameplaySettings.GetKeys(beatmap.KeyMode))
            : KeyModeBindings.ForMode(
                beatmap.KeyMode,
                beatmap.StageCount);
        pressedLanes = new bool[keyBindings.KeyCount];
        slidingSampleIndex = new GameplaySlidingSampleIndex(
            beatmap,
            pressedLanes.Length);
        healthState = new ManiaHealthState(
            beatmap,
            mods,
            judgementConfiguration);
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
                beatmap.ConversionSource is not null,
                judgementConfiguration),
            mods.Contains(ManiaModId.NoRelease),
            mods.ScoreMultiplier,
            minesEnabled);
        keysoundSelector = new GameplayKeysoundSelector(
            beatmap,
            judgementState);
        loadSkin(renderer);
        prepareHitSamples();
        bool hasKeysounds = headSamplesByHitObject.Any(
                                static samples => samples.Length > 0)
                            || tailSamplesByHitObject.Any(
                                static samples => samples.Length > 0)
                            || slidingSamplesByHitObject.Any(
                                static samples => samples.Length > 0);
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
        keysoundPreparationTask = prepareKeysoundsAsync();

        InternalChildren = new Drawable[]
        {
            new Box
            {
                Colour = YokkoPalette.Background,
                RelativeSizeAxes = Axes.Both,
                Depth = 1000,
            },
            backgroundDim = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = osuTK.Graphics.Color4.Black,
                Alpha = (float)gameplaySettings.BackgroundDim.Value,
                Depth = 900,
            },
            playfield = createGameplayPlayfield(),
            laneCovers = new GameplayLaneCovers(
                playfield,
                gameplaySettings),
            hud = createGameplayHud(playfield),
            comboReadout = new GameplayComboReadout
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(0, 120),
                Depth = -99,
            },
            judgementReadout = new JudgementReadout(
                gameplaySettings.ShowJudgementHitError.Value,
                judgementConfiguration,
                gameplaySettings
                    .JudgementDisplayDurationMilliseconds.Value,
                gameplaySettings.JudgementOpacity.Value)
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(0, 30),
                Depth = -100,
            },
            timingBar = new GameplayTimingBar(judgementState.Windows)
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Y = 8,
                Depth = -101,
                Alpha = gameplaySettings.ShowTimingBar.Value ? 1 : 0,
            },
            scrollSpeedOverlay = new GameplayScrollSpeedOverlay
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Position = new Vector2(
                    GameplayScrollSpeedOverlay.PreferredLeft,
                    GameplayScrollSpeedOverlay.TopOffset),
                Depth = -110,
            },
            playbackRateOverlay = new GameplayPlaybackRateOverlay
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Position = new Vector2(
                    GameplayPlaybackRateOverlay.PreferredLeft,
                    GameplayPlaybackRateOverlay.TopOffset),
                Depth = -111,
            },
            layoutEditor = new GameplayLayoutEditorOverlay(
                playfield,
                hud,
                timingBar,
                comboReadout,
                judgementReadout,
                gameplaySettings,
                createLayoutEditorLiveSettings(),
                beginLayoutTestPlay,
                saveGameplayLayout,
                closeGameplayLayoutEditor),
        };

        playfieldWidthScale = (float)Math.Clamp(
            gameplaySettings.LayoutPlayfieldWidthScale.Value,
            YokkoGameplaySettings.MinimumPlayfieldWidthScale,
            YokkoGameplaySettings.MaximumPlayfieldWidthScale);
        playfield.SetWidthScale(playfieldWidthScale);

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
                    Depth = 950,
                });
            }
            playfield.Alpha = 0;
            hud.Alpha = 0;
            comboReadout.Alpha = 0;
            judgementReadout.Alpha = 0;
            timingBar.Alpha = 0;
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

        appliedScrollSpeed = gameplaySettings.ScrollSpeed.Value;
        gameplaySettings.ScrollSpeed.BindValueChanged(
            onScrollSpeedChanged);
        host.Deactivated += onHostDeactivated;
        diagnostics.Trace(
            "GAMEPLAY",
            "constructed",
            $"title={beatmap.Title} | format={beatmap.SourceFormat}"
            + $" | keys={(int)beatmap.KeyMode} | objects={beatmap.HitObjects.Count}"
            + $" | scheduled-samples={beatmap.ScheduledSamples.Count}"
            + $" | replay={ReplayMode} | mods={string.Join(',', mods.DisplayLabels)}"
            + $" | audio-clock={hasAudioClock} | engine={audioEngine.GetType().Name}");
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        if (!ReplayMode && !isPaused)
            keyInputTimestamps.BeginCapture();

        startTimeMilliseconds = Time.Current + leadInMilliseconds;
        frameClockGameplayTime =
            -leadInMilliseconds
            * currentPlaybackRate(-leadInMilliseconds);
        frameClockLastFrameworkTime = Time.Current;
        previousScheduledSampleTime = frameClockGameplayTime;

        if (hasAudioClock)
        {
            Scheduler.AddDelayed(
                () => _ = startAudioAsync(),
                leadInMilliseconds);
        }

        diagnostics.Trace(
            "GAMEPLAY",
            "loaded",
            $"lead-in={leadInMilliseconds:0.###}ms"
            + $" | first-object={firstObjectTimeMilliseconds:0.###}ms"
            + $" | completion={completionTimeMilliseconds:0.###}ms"
            + $" | raw-input={keyInputTimestamps.IsRawInputAvailable}");
    }

    protected override void Update()
    {
        base.Update();
        updatePlayfieldLayout();
        drainAudioSampleTriggerTelemetry();
        updateQuickRetryHold();
        updateDiagnosticSnapshot();

        if (gameplayCompletionTransitionActive)
        {
            updateGameplayCompletionTransition();
            return;
        }

        if (gameplayBlocked
            || gameplayCompleted
            || gameplayFailed
            || retryTransitionInProgress
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
        double playbackRate = currentPlaybackRate(gameplayTime);
        applyAudioPlaybackRate(playbackRate);
        triggerScheduledSamples(gameplayTime);
        if (!ReplayMode)
        {
            drainRawInput(clockObservation);
            if (gameplayFailed)
                return;
        }

        updatePlaybackRateReadout(
            gameplayTime,
            playbackRate);
        updatePlaybackRateAdjustedApproachTime(gameplayTime);
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

        expiredJudgements.Clear();
        judgementState.CollectMineJudgements(
            gameplayTime,
            pressedLanes,
            expiredJudgements);
        judgementState.CollectExpiredMisses(
            gameplayTime,
            expiredJudgements);
        foreach (JudgementEvent missed in expiredJudgements)
        {
            applyJudgement(missed);
            if (gameplayFailed)
                return;
        }
        if (expiredJudgements.Count > 0)
            syncAllSlidingSamples();

        playfield.UpdateGameplayTime(
            visualGameplayTime,
            judgementState,
            healthState);
        hud.UpdateState(gameplayTime, judgementState, healthState);
        if (!mods.IsCinema)
            comboReadout.UpdateState(judgementState.Combo);

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

        float requestedWidthScale = (float)Math.Clamp(
            gameplaySettings.LayoutPlayfieldWidthScale.Value,
            YokkoGameplaySettings.MinimumPlayfieldWidthScale,
            YokkoGameplaySettings.MaximumPlayfieldWidthScale);
        if (Math.Abs(playfieldWidthScale - requestedWidthScale) > 0.0001f)
        {
            playfieldWidthScale = requestedWidthScale;
            playfield.SetWidthScale(playfieldWidthScale);
        }

        float verticalScale = DrawHeight / playfield.Height;
        float horizontalScale = verticalScale;
        float playfieldLeft;
        if (playfield.SkinColumnStart is float columnStart
            && playfield.SkinColumnRight is float columnRight
            && DrawWidth > 0)
        {
            // osu!stable lays legacy mania skins out in a 480px-high logical
            // screen. ColumnRight is the reserved right margin; only the
            // stage contents are squeezed when the requested geometry would
            // extend past the screen.
            float logicalScreenWidth = DrawWidth / verticalScale;
            float logicalLeft = Math.Min(
                columnStart,
                logicalScreenWidth - columnRight);
            float rightMargin = Math.Min(
                columnRight,
                logicalScreenWidth - logicalLeft);
            float overflow = Math.Max(
                0,
                logicalLeft + playfield.Width + rightMargin
                - logicalScreenWidth);
            float horizontalFit = Math.Max(
                0.01f,
                (playfield.Width - overflow) / playfield.Width);

            horizontalScale *= horizontalFit;
            playfield.Anchor = Anchor.BottomLeft;
            playfield.Origin = Anchor.BottomLeft;
            playfield.X = logicalLeft * verticalScale;
            playfieldLeft = playfield.X;
        }
        else
        {
            if (beatmap.StageCount == 2 && DrawWidth > 0)
            {
                verticalScale = Math.Min(
                    verticalScale,
                    DrawWidth * 0.94f / playfield.Width);
                horizontalScale = verticalScale;
            }
            playfield.Anchor = Anchor.BottomCentre;
            playfield.Origin = Anchor.BottomCentre;
            playfield.X = 0;
            playfieldLeft =
                DrawWidth / 2 - playfield.Width * horizontalScale / 2;
        }
        playfield.Scale = new Vector2(
            horizontalScale,
            verticalScale * (float)Math.Clamp(
                gameplaySettings.LayoutPlayfieldHeightScale.Value,
                YokkoGameplaySettings.MinimumLayoutScale,
                YokkoGameplaySettings.MaximumLayoutScale));
        playfield.X += (float)gameplaySettings.LayoutPlayfieldOffsetX.Value
                       * DrawWidth;
        playfield.Y = (float)gameplaySettings.LayoutPlayfieldOffsetY.Value
                      * DrawHeight;
        playfieldLeft +=
            (float)gameplaySettings.LayoutPlayfieldOffsetX.Value
            * DrawWidth;

        hud.Position = new Vector2(
            -20
            + (float)gameplaySettings.LayoutHudOffsetX.Value * DrawWidth,
            20
            + (float)gameplaySettings.LayoutHudOffsetY.Value * DrawHeight);
        hud.Scale = new Vector2(
            (float)Math.Clamp(
                gameplaySettings.LayoutHudScaleX.Value,
                YokkoGameplaySettings.MinimumLayoutScale,
                YokkoGameplaySettings.MaximumLayoutScale),
            (float)Math.Clamp(
                gameplaySettings.LayoutHudScaleY.Value,
                YokkoGameplaySettings.MinimumLayoutScale,
                YokkoGameplaySettings.MaximumLayoutScale));

        timingBar.Position = new Vector2(
            (float)gameplaySettings.LayoutTimingBarOffsetX.Value * DrawWidth,
            8
            + (float)gameplaySettings.LayoutTimingBarOffsetY.Value
            * DrawHeight);
        timingBar.Scale = new Vector2(
            (float)Math.Clamp(
                gameplaySettings.LayoutTimingBarScaleX.Value,
                YokkoGameplaySettings.MinimumLayoutScale,
                YokkoGameplaySettings.MaximumLayoutScale),
            (float)Math.Clamp(
                gameplaySettings.LayoutTimingBarScaleY.Value,
                YokkoGameplaySettings.MinimumLayoutScale,
                YokkoGameplaySettings.MaximumLayoutScale));

        comboReadout.Position = new Vector2(
            (float)gameplaySettings.LayoutComboOffsetX.Value * DrawWidth,
            120
            + (float)gameplaySettings.LayoutComboOffsetY.Value * DrawHeight);
        comboReadout.Scale = new Vector2(
            (float)Math.Clamp(
                gameplaySettings.LayoutComboScaleX.Value,
                YokkoGameplaySettings.MinimumLayoutScale,
                YokkoGameplaySettings.MaximumLayoutScale),
            (float)Math.Clamp(
                gameplaySettings.LayoutComboScaleY.Value,
                YokkoGameplaySettings.MinimumLayoutScale,
                YokkoGameplaySettings.MaximumLayoutScale));

        judgementReadout.Position = new Vector2(
            (float)gameplaySettings.LayoutJudgementOffsetX.Value * DrawWidth,
            30
            + (float)gameplaySettings.LayoutJudgementOffsetY.Value
            * DrawHeight);
        judgementReadout.Scale = new Vector2(
            (float)Math.Clamp(
                gameplaySettings.LayoutJudgementScaleX.Value,
                YokkoGameplaySettings.MinimumLayoutScale,
                YokkoGameplaySettings.MaximumLayoutScale),
            (float)Math.Clamp(
                gameplaySettings.LayoutJudgementScaleY.Value,
                YokkoGameplaySettings.MinimumLayoutScale,
                YokkoGameplaySettings.MaximumLayoutScale));

        backgroundDim.Alpha = (float)Math.Clamp(
            gameplaySettings.BackgroundDim.Value,
            YokkoGameplaySettings.MinimumBackgroundDim,
            YokkoGameplaySettings.MaximumBackgroundDim);

        scrollSpeedOverlay.X = Math.Clamp(
            playfieldLeft
            - GameplayScrollSpeedOverlay.PlayfieldGap
            - scrollSpeedOverlay.Width,
            20,
            GameplayScrollSpeedOverlay.PreferredLeft);
        playbackRateOverlay.X = Math.Clamp(
            playfieldLeft
            - GameplayPlaybackRateOverlay.PlayfieldGap
            - playbackRateOverlay.Width,
            20,
            GameplayPlaybackRateOverlay.PreferredLeft);
    }

    protected override bool OnScroll(ScrollEvent e)
    {
        if (layoutEditor?.IsEditing == true)
            return true;

        if (isPaused)
            return true;

        if (HandlePlayfieldWidthScroll(e.ScrollDelta.Y, e.ControlPressed))
            return true;

        return base.OnScroll(e);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (HandleKeyDownInput(
                e.Key,
                e.Repeat,
                e.AltPressed,
                e.ControlPressed))
        {
            return true;
        }

        return base.OnKeyDown(e);
    }

    internal bool HandleKeyDownInput(
        Key key,
        bool repeat,
        bool altPressed,
        bool controlPressed)
    {
        if (retryTransitionInProgress)
            return true;

        if (layoutEditor?.IsTestingLayout == true
            && !repeat
            && matchesShortcut(
                ManiaShortcutAction.PauseOrBack,
                key))
        {
            _ = returnToLayoutEditorFromTestAsync();
            return true;
        }

        if (layoutEditor?.IsEditing == true)
        {
            if (!repeat
                && matchesShortcut(
                    ManiaShortcutAction.ToggleLayoutEditorUi,
                    key))
            {
                layoutEditor.ToggleChrome();
            }
            else if (!repeat && key == Key.Enter)
                layoutEditor.SaveAndClose();
            else if (!repeat && key == Key.Escape)
                layoutEditor.CancelAndClose();
            else if (!repeat && key == Key.R)
                layoutEditor.ResetAll();

            return true;
        }

        if (resumeCountdownInProgress)
        {
            if (!repeat
                && matchesShortcut(ManiaShortcutAction.PauseOrBack, key))
            {
                cancelResumeCountdown();
            }

            return true;
        }

        // OS key auto-repeat must not re-fire one-shot interactions such as
        // the pause toggle, overlay confirms or quick retry (holding Escape
        // otherwise bounces between pause and resume). Only continuous
        // adjustments are allowed to repeat while held, matching ppy/osu
        // DrawableRuleset shortcut behaviour.
        if (repeat)
        {
            if (!gameplayBlocked
                && !gameplayFailed
                && !isPaused
                && !gameplayCompleted)
            {
                if (HandlePlaybackRateShortcut(key, altPressed))
                    return true;

                if (HandleScrollSpeedShortcut(key, controlPressed))
                    return true;
            }

            return true;
        }

        if (gameplayCompleted && gameplayCompletionTransitionActive)
        {
            if (completionTransitionElapsedMilliseconds
                    >= completionSkipDelayMilliseconds
                && (matchesShortcut(
                        ManiaShortcutAction.Confirm,
                        key)
                    || matchesShortcut(
                        ManiaShortcutAction.ConfirmAlternate,
                        key)
                    || matchesShortcut(
                        ManiaShortcutAction.PauseOrBack,
                        key)))
            {
                finishGameplayCompletionTransition(skipAnimations: true);
            }

            return true;
        }

        if (gameplayBlocked)
        {
            if (matchesShortcut(
                    ManiaShortcutAction.PauseOrBack,
                    key))
            {
                this.Exit();
            }

            return true;
        }

        if (gameplayFailed)
            return failOverlay?.HandleKey(key) ?? true;

        if (isPaused)
            return pauseOverlay?.HandleKey(key) ?? true;

        if (gameplayCompleted)
        {
            if (matchesShortcut(ManiaShortcutAction.Retry, key))
            {
                RetryGameplay();
                return true;
            }

            if (matchesShortcut(
                    ManiaShortcutAction.WatchReplay,
                    key))
            {
                watchCompletedReplay();
                return true;
            }

            if (matchesShortcut(ManiaShortcutAction.Confirm, key)
                || matchesShortcut(
                    ManiaShortcutAction.ConfirmAlternate,
                    key)
                || matchesShortcut(
                    ManiaShortcutAction.PauseOrBack,
                    key))
            {
                this.Exit();
                return true;
            }

            return true;
        }

        if (matchesShortcut(ManiaShortcutAction.SkipIntro, key)
            && HandleIntroSkip())
        {
            return true;
        }

        if (HandlePlaybackRateShortcut(key, altPressed))
            return true;

        if (HandleScrollSpeedShortcut(key, controlPressed))
            return true;

        if (matchesShortcut(
                ManiaShortcutAction.PauseOrBack,
                key))
        {
            TogglePause();
            return true;
        }

        if (ReplayMode)
            return true;

        if (matchesShortcut(ManiaShortcutAction.QuickRetry, key))
        {
            beginQuickRetryHold();
            return true;
        }

        int lane = keyBindings.GetLane(key);

        if (lane < 0)
            return false;

        if (keyInputTimestamps.IsRawInputAvailable)
        {
            // Close the ordering gap when framework dispatch happens after
            // this screen's regular raw-input drain in the same update cycle.
            drainRawInput(observeGameplayClock());
            return true;
        }

        if (pressedLanes[lane])
            return true;

        applyLanePress(
            lane,
            gameplayTimeForInput(key, true));

        return true;
    }

    protected override void OnKeyUp(KeyUpEvent e)
    {
        if (!HandleKeyUpInput(e.Key))
            base.OnKeyUp(e);
    }

    internal bool HandleKeyUpInput(Key key)
    {
        // Releasing the quick-retry key before the hold completes cancels
        // the retry, even when the release lands inside a pause.
        if (matchesShortcut(ManiaShortcutAction.QuickRetry, key))
            cancelQuickRetryHold();

        if (gameplayCompleted
            || gameplayFailed
            || retryTransitionInProgress
            || ReplayMode
            || isPaused)
            return true;

        int lane = keyBindings.GetLane(key);

        if (lane < 0)
            return false;

        if (keyInputTimestamps.IsRawInputAvailable)
        {
            drainRawInput(observeGameplayClock());
            return true;
        }

        applyLaneRelease(
            lane,
            gameplayTimeForInput(key, false));
        return true;
    }

    public override bool OnExiting(ScreenExitEvent e)
    {
        if (!ReplayMode)
        {
            disableRawKeysoundFastPath();
            keyInputTimestamps.EndCapture();
        }
        logInputTimingSummary();

        stopAllSlidingSamples();
        if (!completionAudioStopRequested)
        {
            mutedAudio?.Restore();
            _ = audioEngine.StopAsync();
        }
        return base.OnExiting(e);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            if (!ReplayMode)
            {
                disableRawKeysoundFastPath();
                keyInputTimestamps.EndCapture();
            }

            host.Deactivated -= onHostDeactivated;
            gameplaySettings.ScrollSpeed.ValueChanged -=
                onScrollSpeedChanged;
            if (!completionAudioStopRequested)
                mutedAudio?.Restore();
            stopAllSlidingSamples();
            _ = audioEngine.DisposeAsync();
            maniaSkinLease?.Dispose();
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
            rawKeysoundDispatcher?.SetUserOffset(
                activeUserOffsetMilliseconds);
            double initialPlaybackRate =
                currentPlaybackRate(currentGameplayTime);
            AudioEngineStartRequest startRequest =
                audioSettings.CreateStartRequest(
                    beatmap.AudioPath,
                    initialPlaybackRate,
                    ResolvePlaybackRatePitchMode(
                        mods,
                        audioSettings.ManualPlaybackRatePitchMode.Value),
                    mods.FixedAudioFrequencyScale) with
            {
                DynamicPlaybackRate = true,
            };
            activeRequestedBackend = startRequest.PreferredBackend;
            diagnostics.Trace(
                "AUDIO",
                "start-requested",
                $"backend={startRequest.PreferredBackend}"
                + $" | device={startRequest.DeviceId}"
                + $" | sample-rate={startRequest.PreferredSampleRate}"
                + $" | buffer={startRequest.PreferredBufferSize}"
                + $" | rate={startRequest.PlaybackRate:0.###}"
                + $" | pitch={startRequest.PitchMode}"
                + $" | offset={activeUserOffsetMilliseconds:0.###}ms"
                + $" | path={startRequest.AudioPath}");

            await audioEngine.StartAsync(startRequest)
                             .ConfigureAwait(true);

            AudioEngineSnapshot audioSnapshot = audioEngine.Snapshot;
            if (!audioSnapshot.Status.IsRunning)
            {
                failAudioStart("The audio engine returned without starting playback.");
                return;
            }

            audioStarted = true;
            lastAppliedPlaybackRate = initialPlaybackRate;
            lastStableAudioGameplayTime =
                audioSnapshot.PlaybackTimeMilliseconds
                + activeUserOffsetMilliseconds;
            hud.UpdateAudioStatus(
                audioSnapshot.Status,
                activeRequestedBackend);
            diagnostics.Trace(
                "AUDIO",
                "started",
                formatAudioStatus(audioSnapshot.Status)
                + $" | playback={audioSnapshot.PlaybackTimeMilliseconds:0.###}ms",
                LogLevel.Important);

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
                    * currentPlaybackRate(0));
            }
        }
        else
        {
            double elapsed = Math.Max(
                0,
                Time.Current - frameClockLastFrameworkTime);
            double rate = currentPlaybackRate(
                frameClockGameplayTime);
            frameClockGameplayTime += elapsed * rate;
            frameClockLastFrameworkTime = Time.Current;
            gameplayTime = frameClockGameplayTime;
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

        if (GameplayInputClock.TryAtAudioTimestamp(
                hasAudioClock
                    ? audioEngine as ITimestampedAudioClock
                    : null,
                observation.Audio,
                eventTimestamp,
                Stopwatch.Frequency,
                activeUserOffsetMilliseconds,
                out double timestampedGameplayTime))
        {
            return timestampedGameplayTime;
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
        InputDropObservation drops = inputDropTracker.Observe(
            keyInputTimestamps.Status.DroppedEdgeCount);
        if (drops.RequiresRecovery)
        {
            recoverLiveInputState(
                observation.GameplayTime,
                $"raw input queue dropped {drops.NewlyDropped} edge(s)");
            return;
        }

        while (keyInputTimestamps.TryDequeueRaw(out TimestampedKeyInput input))
        {
            long dequeueTimestamp = Stopwatch.GetTimestamp();
            if (gameplayFailed)
                break;

            int lane = keyBindings.GetLane(input.Key);
            if (lane < 0)
                continue;

            double inputTime = gameplayTimeForTimestamp(
                input.Timestamp,
                KeyInputTimestampKind.RawInput,
                observation);
            long audioEnqueueTimestamp;
            if (input.IsPressed)
            {
                audioEnqueueTimestamp = applyLanePress(
                    lane,
                    inputTime,
                    input.FastPathHitObjectIndex,
                    input.FastPathTriggeredSampleMask,
                    input.FastPathAudioEnqueueTimestamp,
                    input.Timestamp);
                rawKeysoundDispatcher?.RefreshLane(lane);
            }
            else
            {
                applyLaneRelease(lane, inputTime);
                audioEnqueueTimestamp = 0;
            }

            inputPipelineLatencyTracker.Record(
                input.Timestamp,
                dequeueTimestamp,
                audioEnqueueTimestamp,
                Stopwatch.GetTimestamp(),
                Stopwatch.Frequency);
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

    private long applyLanePress(
        int lane,
        double inputTime,
        int fastPathHitObjectIndex = -1,
        ulong fastPathTriggeredSampleMask = 0,
        long fastPathAudioEnqueueTimestamp = 0,
        long captureTimestamp = 0)
    {
        if (pressedLanes[lane])
            return 0;

        pressedLanes[lane] = true;
        // Enter the native audio queue before touching drawable state.
        long audioEnqueueTimestamp =
            triggerKeysoundForLanePress(
                lane,
                inputTime,
                fastPathHitObjectIndex,
                fastPathTriggeredSampleMask,
                fastPathAudioEnqueueTimestamp,
                captureTimestamp);
        playfield.SetLanePressed(lane, true);

        if (!ReplayMode)
        {
            recordedReplayInputs.Add(new GameplayReplayInput(
                lane,
                true,
                inputTime));
        }

        inputJudgements.Clear();
        judgementState.JudgeLanePress(lane, inputTime, inputJudgements);
        foreach (JudgementEvent judgement in inputJudgements)
        {
            applyJudgement(judgement);
        }
        if (!gameplayFailed)
            syncSlidingSamplesForLane(lane);
        if (diagnostics.IsEnabled)
        {
            diagnostics.Trace(
                "INPUT",
                "lane-pressed",
                $"lane={lane} | time={inputTime:0.###}ms"
                + $" | source={(captureTimestamp != 0 ? "raw" : ReplayMode ? "replay" : "framework")}"
                + $" | judgements={inputJudgements.Count}"
                + $" | fast-hit={fastPathHitObjectIndex}"
                + $" | audio-enqueue-ticks={audioEnqueueTimestamp}");
        }
        return audioEnqueueTimestamp;
    }

    private void prepareHitSamples()
    {
        hitSampleResolver = new GameplayHitSampleResolver(
            beatmap,
            maniaSkin == null
                ? null
                : maniaSkin.GetHitSamplePath,
            maniaSkin?.Info.LayeredHitSounds ?? true);
        headSamplesByHitObject = beatmap.HitObjects
            .Select(hitObject =>
                hitSampleResolver.ResolveHead(hitObject)
                                 .Select(static sample =>
                                     new GameplayHitSamplePlaybackBinding(sample))
                                 .ToArray())
            .ToArray();
        tailSamplesByHitObject = beatmap.HitObjects
            .Select(hitObject =>
                hitSampleResolver.ResolveTail(hitObject)
                                 .Select(static sample =>
                                     new GameplayHitSamplePlaybackBinding(sample))
                                 .ToArray())
            .ToArray();
        slidingSamplesByHitObject = beatmap.HitObjects
            .Select(hitObject =>
                hitSampleResolver.ResolveSliding(hitObject)
                                 .Select(static sample =>
                                     new GameplayHitSamplePlaybackBinding(sample))
                                 .ToArray())
            .ToArray();
        scheduledSamples = beatmap.ScheduledSamples
            .Select(static sample =>
                new GameplayHitSamplePlaybackBinding(
                    sample.Path,
                    sample.Volume / 100d,
                    default,
                    false))
            .ToArray();
    }

    private async Task prepareKeysoundsAsync()
    {
        if (!gameplaySettings.KeysoundsEnabled.Value
            || audioEngine is not IAudioSamplePlayback samplePlayback)
            return;

        string[] paths = headSamplesByHitObject
            .SelectMany(static samples => samples)
            .Concat(tailSamplesByHitObject.SelectMany(
                static samples => samples))
            .Concat(slidingSamplesByHitObject.SelectMany(
                static samples => samples))
            .Concat(scheduledSamples)
            .Select(static sample => sample.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (paths.Length == 0)
            return;

        try
        {
            await samplePlayback.PrepareSamplesAsync(paths)
                                .ConfigureAwait(true);
            if (samplePlayback is IPreparedAudioSamplePlayback preparedPlayback)
                bindPreparedSampleHandles(preparedPlayback, paths);
            enableRawKeysoundFastPath();
        }
        catch (Exception ex)
        {
            Logger.Error(
                ex,
                "Gameplay keysounds could not be prepared; backing audio will continue.",
                LoggingTarget.Runtime);
        }
    }

    private void bindPreparedSampleHandles(
        IPreparedAudioSamplePlayback samplePlayback,
        IReadOnlyList<string> paths)
    {
        var handlesByPath = new Dictionary<string, PreparedAudioSampleHandle>(
            StringComparer.OrdinalIgnoreCase);
        foreach (string path in paths)
        {
            if (samplePlayback.TryGetPreparedSampleHandle(path, out var handle))
                handlesByPath[path] = handle;
        }

        bindPreparedSampleHandles(headSamplesByHitObject, handlesByPath);
        bindPreparedSampleHandles(tailSamplesByHitObject, handlesByPath);
        bindPreparedSampleHandles(slidingSamplesByHitObject, handlesByPath);
        for (int index = 0; index < scheduledSamples.Length; index++)
        {
            if (handlesByPath.TryGetValue(
                    scheduledSamples[index].Path,
                    out PreparedAudioSampleHandle handle))
            {
                scheduledSamples[index] =
                    scheduledSamples[index].WithPreparedHandle(handle);
            }
        }
    }

    private void triggerScheduledSamples(double gameplayTime)
    {
        if (beatmap.ScheduledSamples.Count == 0)
            return;

        bool movedBackwards = gameplayTime < previousScheduledSampleTime;
        bool discontinuity =
            gameplayTime - previousScheduledSampleTime > 1000;
        if (movedBackwards || discontinuity)
        {
            nextScheduledSampleIndex = 0;
            while (nextScheduledSampleIndex
                       < beatmap.ScheduledSamples.Count
                   && beatmap.ScheduledSamples[nextScheduledSampleIndex]
                             .TimeMilliseconds <= gameplayTime)
            {
                nextScheduledSampleIndex++;
            }

            previousScheduledSampleTime = gameplayTime;
            return;
        }

        while (nextScheduledSampleIndex < beatmap.ScheduledSamples.Count
               && beatmap.ScheduledSamples[nextScheduledSampleIndex]
                         .TimeMilliseconds <= gameplayTime)
        {
            YokkoScheduledSample scheduled =
                beatmap.ScheduledSamples[nextScheduledSampleIndex];
            if (scheduled.TimeMilliseconds > previousScheduledSampleTime
                && gameplaySettings.KeysoundsEnabled.Value
                && audioEngine is IAudioSamplePlayback samplePlayback)
            {
                GameplayHitSamplePlayer.TriggerSamples(
                    samplePlayback,
                    [scheduledSamples[nextScheduledSampleIndex]]);
            }

            nextScheduledSampleIndex++;
        }

        previousScheduledSampleTime = gameplayTime;
    }

    private static void bindPreparedSampleHandles(
        GameplayHitSamplePlaybackBinding[][] samplesByHitObject,
        IReadOnlyDictionary<string, PreparedAudioSampleHandle> handlesByPath)
    {
        foreach (GameplayHitSamplePlaybackBinding[] samples in samplesByHitObject)
        {
            for (int index = 0; index < samples.Length; index++)
            {
                if (handlesByPath.TryGetValue(
                        samples[index].Path,
                        out PreparedAudioSampleHandle handle))
                {
                    samples[index] = samples[index].WithPreparedHandle(handle);
                }
            }
        }
    }

    private void enableRawKeysoundFastPath()
    {
        if (!rawKeysoundFastPathAllowed
            || ReplayMode
            || audioEngine is not ITimestampedAudioClock audioClock
            || audioEngine is not
                ITimestampedPreparedAudioSamplePlayback samplePlayback
            || !samplePlayback.SupportsSampleTriggerTelemetry)
        {
            return;
        }

        rawKeysoundDispatcher = new RawInputKeysoundDispatcher(
            keyBindings,
            audioEngine,
            audioClock,
            samplePlayback,
            keysoundSelector,
            headSamplesByHitObject);
        rawKeysoundDispatcher.SetUserOffset(activeUserOffsetMilliseconds);
        rawKeysoundDispatcher.RefreshAllAndEnable();
        keyInputTimestamps.SetRawInputFastPathSink(
            rawKeysoundDispatcher);
    }

    private void disableRawKeysoundFastPath()
    {
        rawKeysoundFastPathAllowed = false;
        keyInputTimestamps.SetRawInputFastPathSink(null);
        rawKeysoundDispatcher?.Disable();
    }

    private long triggerKeysoundForLanePress(
        int lane,
        double inputTime,
        int fastPathHitObjectIndex = -1,
        ulong fastPathTriggeredSampleMask = 0,
        long fastPathAudioEnqueueTimestamp = 0,
        long captureTimestamp = 0)
    {
        if (!gameplaySettings.KeysoundsEnabled.Value
            || audioEngine is not IAudioSamplePlayback samplePlayback)
            return 0;

        int selected = keysoundSelector.Select(lane, inputTime);
        if ((uint)selected >= headSamplesByHitObject.Length)
            return 0;

        GameplayHitSamplePlayer.TriggerResult result =
            GameplayHitSamplePlayer.TriggerSamples(
            samplePlayback,
            headSamplesByHitObject[selected],
            selected == fastPathHitObjectIndex
                ? fastPathTriggeredSampleMask
                : 0,
            captureTimestamp,
            Stopwatch.Frequency);
        return result.LastAudioEnqueueTimestamp != 0
            ? result.LastAudioEnqueueTimestamp
            : selected == fastPathHitObjectIndex
              ? fastPathAudioEnqueueTimestamp
              : 0;
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

        inputJudgements.Clear();
        judgementState.JudgeLaneRelease(lane, inputTime, inputJudgements);
        foreach (JudgementEvent judgement in inputJudgements)
        {
            applyJudgement(judgement);
        }
        if (!gameplayFailed)
            syncSlidingSamplesForLane(lane);
        if (diagnostics.IsEnabled)
        {
            diagnostics.Trace(
                "INPUT",
                "lane-released",
                $"lane={lane} | time={inputTime:0.###}ms"
                + $" | source={(ReplayMode ? "replay" : keyInputTimestamps.IsRawInputAvailable ? "raw" : "framework")}"
                + $" | judgements={inputJudgements.Count}");
        }
    }

    private void syncSlidingSamplesForLane(int lane)
    {
        foreach (int index in slidingSampleIndex.GetObjectIndices(lane))
        {
            if ((uint)index >= slidingSamplesByHitObject.Length)
                continue;

            if (judgementState.IsHoldActive(index))
                startSlidingSamples(index);
            else
                stopSlidingSamples(index);
        }
    }

    private void syncAllSlidingSamples()
    {
        for (int lane = 0; lane < pressedLanes.Length; lane++)
            syncSlidingSamplesForLane(lane);
    }

    private void startSlidingSamples(int hitObjectIndex)
    {
        if (activeSlidingSampleLoops.ContainsKey(hitObjectIndex)
            || !gameplaySettings.KeysoundsEnabled.Value
            || (uint)hitObjectIndex >= slidingSamplesByHitObject.Length
            || slidingSamplesByHitObject[hitObjectIndex].Length == 0)
        {
            return;
        }

        IReadOnlyList<GameplayHitSamplePlaybackBinding> samples =
            slidingSamplesByHitObject[hitObjectIndex];
        if (audioEngine is not IAudioLoopingSamplePlayback looping)
        {
            if (audioEngine is IAudioSamplePlayback oneShot)
                GameplayHitSamplePlayer.TriggerSamples(oneShot, samples);
            return;
        }

        var loopIds = new List<uint>(samples.Count);
        foreach (GameplayHitSamplePlaybackBinding sample in samples)
        {
            uint loopId = GameplayHitSamplePlayer.StartLoopingSample(
                looping,
                sample);
            if (loopId != 0)
                loopIds.Add(loopId);
        }

        if (loopIds.Count > 0)
            activeSlidingSampleLoops[hitObjectIndex] = loopIds;
    }

    private void stopSlidingSamples(int hitObjectIndex)
    {
        if (!activeSlidingSampleLoops.Remove(
                hitObjectIndex,
                out List<uint> loopIds)
            || audioEngine is not IAudioLoopingSamplePlayback looping)
        {
            return;
        }

        foreach (uint loopId in loopIds)
            looping.StopLoopingSample(loopId);
    }

    private void stopAllSlidingSamples()
    {
        if (audioEngine is IAudioLoopingSamplePlayback looping)
        {
            foreach (uint loopId in activeSlidingSampleLoops.Values
                         .SelectMany(static loopIds => loopIds))
            {
                looping.StopLoopingSample(loopId);
            }
        }

        activeSlidingSampleLoops.Clear();
    }

    private void applyJudgement(JudgementEvent judgement)
    {
        if (diagnostics.IsEnabled)
        {
            diagnostics.Trace(
                "JUDGEMENT",
                "applied",
                $"object={judgement.HitObjectIndex} | lane={judgement.Lane}"
                + $" | phase={judgement.Phase} | rating={judgement.Rating}"
                + $" | object-time={judgement.ObjectTimeMilliseconds:0.###}ms"
                + $" | hit-time={judgement.HitTimeMilliseconds:0.###}ms"
                + $" | error={judgement.HitErrorMilliseconds:+0.###;-0.###;0}ms");
        }
        if (judgement.Phase == JudgementPhase.HoldTail
            && !judgement.IsMiss
            && gameplaySettings.KeysoundsEnabled.Value
            && audioEngine is IAudioSamplePlayback samplePlayback
            && (uint)judgement.HitObjectIndex
            < tailSamplesByHitObject.Length)
        {
            GameplayHitSamplePlayer.TriggerSamples(
                samplePlayback,
                tailSamplesByHitObject[judgement.HitObjectIndex]);
        }

        playfield.ApplyJudgement(judgement);
        bool isMine = judgement.Phase == JudgementPhase.Mine;
        if (gameplaySettings.ShowTimingBar.Value && !isMine)
            timingBar.Show(judgement);
        adaptiveSpeedState?.Apply(judgement);
        ManiaHealthUpdate healthUpdate = healthState.Apply(
            judgement,
            judgementState.Accuracy,
            judgementState.MaximumAchievableAccuracy);
        if (!playfield.UsesSkinJudgementOverlay
            && judgement.Phase is not JudgementPhase.Hold
            and not JudgementPhase.HoldBody
            && (!isMine || judgement.IsMiss))
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
        diagnostics.Trace(
            "GAMEPLAY",
            "failed",
            $"time={currentGameplayTime:0.###}ms"
            + $" | health={healthState.Health:0.###}",
            LogLevel.Important);
        disableRawKeysoundFastPath();
        mutedAudio?.Restore();
        stopAllSlidingSamples();
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
            gameplaySettings,
            RetryGameplay,
            () => this.Exit()));
    }

    private void completeGameplay()
    {
        if (gameplayCompleted)
            return;

        gameplayCompleted = true;
        gameplayCompletionTransitionActive = true;
        completionTransitionElapsedMilliseconds = 0;
        disableRawKeysoundFastPath();
        cancelQuickRetryHold();
        stopAllSlidingSamples();
        for (int lane = 0; lane < pressedLanes.Length; lane++)
        {
            pressedLanes[lane] = false;
            playfield.SetLanePressed(lane, false);
        }

        ManiaScoreResult rawResult =
            judgementState.CreateResult();
        completedResult = rawResult with
        {
            Rank = mods.AdjustRank(rawResult.Rank),
        };
        diagnostics.Trace(
            "GAMEPLAY",
            "completed",
            $"score={completedResult.Score}"
            + $" | accuracy={completedResult.Accuracy:P4}"
            + $" | combo={completedResult.MaxCombo}"
            + $" | miss={completedResult.Miss}"
            + $" | rank={completedResult.Rank}",
            LogLevel.Important);
        completedReplay = replay
                          ?? new GameplayReplay(
                              recordedReplayInputs,
                              mods,
                              judgementConfiguration);
        completedResultIsNewBest = BestScoreSaved =
            !ReplayMode
            && !manualPlaybackRateUsed
            && scoreStore.SaveBest(
                originalBeatmap,
                mods,
                judgementConfiguration,
                completedResult);
        if (!ReplayMode)
            saveCompletedReplay();

        if (audioEngine is IAudioMixControl mixControl)
        {
            completionMixControl = mixControl;
            completionMusicVolume = mixControl.MusicVolume;
            completionHitSoundVolume = mixControl.HitSoundVolume;
            completionMetronomeVolume = mixControl.MetronomeVolume;
        }

        // Hold the final judgement briefly, then let the playfield recede
        // under the result screen while the song tail fades independently.
        playfield.Delay(completionSettleMilliseconds)
                 .FadeOut(
                     completionTransitionMilliseconds
                     - completionSettleMilliseconds,
                     Easing.OutQuint);
        hud.Delay(completionSettleMilliseconds + 60)
           .FadeOut(420, Easing.OutQuint);
        judgementReadout.Delay(completionSettleMilliseconds)
                        .FadeOut(420, Easing.OutQuint);
        timingBar.Delay(completionSettleMilliseconds + 40)
                 .FadeOut(360, Easing.OutQuint);
        scrollSpeedOverlay.Delay(completionSettleMilliseconds)
                          .FadeOut(340, Easing.OutQuint);
        playbackRateOverlay.Delay(completionSettleMilliseconds)
                           .FadeOut(340, Easing.OutQuint);
        cinemaIndicator?.Delay(completionSettleMilliseconds)
                        .FadeOut(420, Easing.OutQuint);
    }

    private void updateGameplayCompletionTransition()
    {
        completionTransitionElapsedMilliseconds = Math.Min(
            completionTransitionMilliseconds,
            completionTransitionElapsedMilliseconds
            + Math.Max(0, Time.Elapsed));

        updateCompletionAudioFade();
        if (completionTransitionElapsedMilliseconds
                >= completionResultRevealMilliseconds)
        {
            ensureGameplayResultOverlay();
        }

        if (completionTransitionElapsedMilliseconds
                >= completionTransitionMilliseconds)
        {
            finishGameplayCompletionTransition(skipAnimations: false);
        }
    }

    private void updateCompletionAudioFade()
    {
        if (completionMixControl == null)
            return;

        double remainingMusic = calculateCompletionFadeRemaining(
            completionTransitionElapsedMilliseconds,
            completionSettleMilliseconds);
        double remainingTail = CalculateCompletionTailFadeRemaining(
            completionTransitionElapsedMilliseconds);
        completionMixControl.SetMixVolumes(
            completionMusicVolume * remainingMusic,
            completionHitSoundVolume * remainingTail,
            completionMetronomeVolume * remainingTail);
    }

    internal static double CalculateCompletionTailFadeRemaining(
        double elapsedMilliseconds) =>
        calculateCompletionFadeRemaining(
            elapsedMilliseconds,
            completionTailFadeStartMilliseconds);

    private static double calculateCompletionFadeRemaining(
        double elapsedMilliseconds,
        double fadeStartMilliseconds)
    {
        double progress = Math.Clamp(
            (elapsedMilliseconds - fadeStartMilliseconds)
            / (completionTransitionMilliseconds
               - fadeStartMilliseconds),
            0,
            1);
        double smoothProgress = progress * progress * (3 - 2 * progress);
        return 1 - smoothProgress;
    }

    private void ensureGameplayResultOverlay()
    {
        if (resultOverlay != null)
            return;

        resultOverlay = new GameplayResultOverlay(
            beatmap,
            completedResult,
            mods,
            completedResultIsNewBest,
            () => runAfterGameplayCompletionTransition(RetryGameplay),
            () => runAfterGameplayCompletionTransition(
                watchCompletedReplay),
            () => runAfterGameplayCompletionTransition(
                () => this.Exit()),
            manualPlaybackRateUsed,
            judgementConfiguration);
        AddInternal(resultOverlay);
    }

    private void runAfterGameplayCompletionTransition(Action action)
    {
        if (!gameplayCompletionTransitionActive)
            action();
    }

    private void finishGameplayCompletionTransition(bool skipAnimations)
    {
        if (!gameplayCompletionTransitionActive)
            return;

        completionTransitionElapsedMilliseconds =
            completionTransitionMilliseconds;
        updateCompletionAudioFade();
        ensureGameplayResultOverlay();
        if (skipAnimations)
        {
            finishGameplayExitVisuals();
            resultOverlay.CompleteEntrance();
        }

        gameplayCompletionTransitionActive = false;
        requestCompletionAudioStop();
    }

    private void finishGameplayExitVisuals()
    {
        Drawable[] exitDrawables =
        [
            playfield,
            hud,
            judgementReadout,
            timingBar,
            scrollSpeedOverlay,
            playbackRateOverlay,
        ];
        foreach (Drawable drawable in exitDrawables)
        {
            drawable.ClearTransforms();
            drawable.Alpha = 0;
        }

        if (cinemaIndicator != null)
        {
            cinemaIndicator.ClearTransforms();
            cinemaIndicator.Alpha = 0;
        }
    }

    private void requestCompletionAudioStop()
    {
        if (completionAudioStopRequested)
            return;

        completionAudioStopRequested = true;
        completionAudioStopTask = stopCompletionAudioAsync();
    }

    private async Task stopCompletionAudioAsync()
    {
        try
        {
            await audioEngine.StopAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                "Gameplay completion audio could not stop cleanly.",
                LoggingTarget.Runtime);
        }
        finally
        {
            // Muted owns a temporary mix. Restore it only after playback has
            // stopped so completion cannot briefly unmute the song.
            mutedAudio?.Restore();
        }
    }

    private void saveCompletedReplay()
    {
        try
        {
            string fingerprint =
                YokkoBeatmapFingerprint.Compute(originalBeatmap);
            ImportedChart imported = importedChartLibrary
                .GetCharts()
                .FirstOrDefault(chart =>
                    ReferenceEquals(
                        chart.Result.Beatmap,
                        originalBeatmap))
                ?? importedChartLibrary.FindByBeatmapFingerprint(
                    fingerprint);
            SavedReplayPath = replayStore.Save(
                originalBeatmap,
                beatmap,
                completedReplay,
                imported?.Result.SourceHash);
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                "Completed gameplay replay could not be saved.",
                LoggingTarget.Runtime);
        }
    }

    internal void TogglePause()
    {
        if (retryTransitionInProgress
            || gameplayBlocked
            || gameplayCompleted
            || gameplayFailed)
        {
            return;
        }

        // A second pause shortcut during the resume countdown cancels the
        // resume and returns to the pause menu.
        if (resumeCountdownInProgress)
        {
            cancelResumeCountdown();
            return;
        }

        if (pauseTransitionInProgress)
            return;

        if (isPaused)
            beginResumeCountdown();
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
            return;
        }

        if (!ReplayMode
            && !isPaused
            && !pauseTransitionInProgress
            && !gameplayBlocked
            && !gameplayCompleted
            && !gameplayFailed)
        {
            recoverLiveInputState(
                currentGameplayTime,
                "host focus was lost while automatic pause was disabled");
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
        diagnostics.Trace(
            "GAMEPLAY",
            "pause-requested",
            $"gameplay={pausedGameplayTime:0.###}ms"
            + $" | playback={pausedAudioPosition:0.###}ms"
            + $" | audio-clock={hasAudioClock}",
            LogLevel.Important);
        releasePressedLanesAt(pausedGameplayTime);
        isPaused = true;

        if (!ReplayMode)
            keyInputTimestamps.EndCapture();

        cancelQuickRetryHold();
        AddInternal(pauseOverlay = createPauseOverlay());

        try
        {
            if (hasAudioClock)
                await audioEngine.PauseAsync().ConfigureAwait(true);
            diagnostics.Trace("GAMEPLAY", "paused", $"time={pausedGameplayTime:0.###}ms");
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
            restoreLayoutEditorAfterTest();
        }
    }

    private GameplayPauseOverlay createPauseOverlay() =>
        new(
            beatmap,
            gameplaySettings,
            GameplayPauseSnapshot.Capture(
                judgementState,
                mods,
                pausedGameplayTime,
                completionTimeMilliseconds),
            TogglePause,
            RetryGameplay,
            () => this.Push(new SettingsScreen()),
            exitPausedGameplay,
            openGameplayLayoutEditorFromPause);

    private void beginResumeCountdown()
    {
        if (!isPaused
            || pauseTransitionInProgress
            || resumeCountdownInProgress)
        {
            return;
        }

        pauseTransitionInProgress = true;
        cancelQuickRetryHold();
        pauseOverlay?.FadeOut(140, Easing.OutQuint)
                     .Expire();
        pauseOverlay = null;
        resumeCountdownInProgress = true;

        double duration = resumeCountdownDuration;
        diagnostics.Trace(
            "GAMEPLAY",
            "resume-countdown-started",
            $"duration={duration:0.###}ms | position={pausedGameplayTime:0.###}ms");
        if (duration <= 0)
        {
            // Countdown disabled in settings: resume immediately at the
            // paused position.
            _ = completeResumeAsync();
            pauseTransitionInProgress = false;
            return;
        }

        AddInternal(resumeCountdown = new GameplayResumeCountdown());

        // The gameplay clock stays frozen while the countdown runs, then
        // the audio seek below restarts play exactly at the paused position.
        double step = duration / GameplayResumeCountdown.CountSteps;
        for (int index = 0;
             index < GameplayResumeCountdown.CountSteps;
             index++)
        {
            int count = GameplayResumeCountdown.CountSteps - index;
            Scheduler.AddDelayed(
                () =>
                {
                    if (resumeCountdownInProgress)
                        resumeCountdown?.ShowCount(count);
                },
                step * index);
        }

        Scheduler.AddDelayed(
            () => _ = completeResumeAsync(),
            duration);
        pauseTransitionInProgress = false;
    }

    private void cancelResumeCountdown()
    {
        if (!resumeCountdownInProgress)
            return;

        resumeCountdownInProgress = false;
        diagnostics.Trace("GAMEPLAY", "resume-countdown-cancelled");
        resumeCountdown?.FadeOut(120, Easing.OutQuint)
                        .Expire();
        resumeCountdown = null;
        AddInternal(pauseOverlay = createPauseOverlay());
    }

    private async Task completeResumeAsync()
    {
        if (!resumeCountdownInProgress || retryTransitionInProgress)
            return;

        resumeCountdownInProgress = false;
        resumeCountdown?.FadeOut(120, Easing.OutQuint)
                        .Expire();
        resumeCountdown = null;

        try
        {
            if (hasAudioClock)
            {
                await audioEngine.SeekAsync(pausedAudioPosition)
                                 .ConfigureAwait(true);
                lastAppliedPlaybackRate = double.NaN;
            }
            else
            {
                startTimeMilliseconds =
                    Time.Current
                    - pausedGameplayTime
                      / currentPlaybackRate(pausedGameplayTime);
                frameClockGameplayTime = pausedGameplayTime;
                frameClockLastFrameworkTime = Time.Current;
            }

            isPaused = false;
            diagnostics.Trace(
                "GAMEPLAY",
                "resumed",
                $"gameplay={pausedGameplayTime:0.###}ms"
                + $" | playback={pausedAudioPosition:0.###}ms",
                LogLevel.Important);

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
    }

    private void beginQuickRetryHold()
    {
        if (!double.IsNaN(quickRetryHoldStartTime))
            return;

        quickRetryHoldStartTime = Time.Current;
        if (quickRetryIndicator == null)
        {
            AddInternal(quickRetryIndicator =
                new GameplayQuickRetryIndicator(
                    KeyModeBindings.FormatKey(
                            gameplaySettings.GetShortcutBinding(
                                ManiaShortcutAction.QuickRetry))
                        .ToUpperInvariant()));
        }

        quickRetryIndicator.ShowHold();
    }

    private void cancelQuickRetryHold()
    {
        quickRetryHoldStartTime = double.NaN;
        quickRetryIndicator?.CancelHold();
    }

    private void updateQuickRetryHold()
    {
        if (double.IsNaN(quickRetryHoldStartTime))
            return;

        if (gameplayBlocked
            || gameplayCompleted
            || gameplayFailed
            || retryTransitionInProgress
            || isPaused
            || resumeCountdownInProgress)
        {
            cancelQuickRetryHold();
            return;
        }

        double progress =
            (Time.Current - quickRetryHoldStartTime)
            / QuickRetryHoldMilliseconds;
        quickRetryIndicator?.UpdateProgress(progress);

        if (progress >= 1)
        {
            cancelQuickRetryHold();
            RetryGameplay();
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

    private void recoverLiveInputState(
        double gameplayTime,
        string reason)
    {
        keyInputTimestamps.EndCapture();
        releasePressedLanesAt(gameplayTime);
        inputDropTracker.MarkBackendReset();

        if (!gameplayFailed
            && !gameplayCompleted
            && !gameplayBlocked
            && !retryTransitionInProgress
            && !isPaused)
        {
            rawKeysoundDispatcher?.RefreshAllAndEnable();
            keyInputTimestamps.BeginCapture();
        }

        Logger.Log(
            $"Gameplay input state recovered: {reason}.",
            LoggingTarget.Runtime,
            LogLevel.Important);
    }

    private void releasePressedLanesAt(double gameplayTime)
    {
        if (ReplayMode)
            return;

        for (int lane = 0; lane < pressedLanes.Length; lane++)
        {
            if (pressedLanes[lane])
                applyLaneRelease(lane, gameplayTime);
        }
    }

    private void exitPausedGameplay()
    {
        _ = audioEngine.StopAsync();
        this.Exit();
    }

    internal void RetryGameplay()
    {
        if (retryTransitionInProgress
            || findGameplaySessionRoot()?.RetryTransitionActive == true)
            return;

        resumeCountdownInProgress = false;
        resumeCountdown?.Expire();
        resumeCountdown = null;
        cancelQuickRetryHold();
        retryTransitionInProgress = true;
        diagnostics.Trace(
            "GAMEPLAY",
            "retry-requested",
            $"time={currentGameplayTime:0.###}ms | paused={isPaused} | failed={gameplayFailed}",
            LogLevel.Important);
        if (!ReplayMode)
            keyInputTimestamps.EndCapture();

        _ = retryGameplayAsync();
    }

    private GameplaySessionRootScreen findGameplaySessionRoot()
    {
        IScreen destination = this.GetParentScreen();
        while (destination is GameplayScreen)
            destination = destination.GetParentScreen();

        return destination as GameplaySessionRootScreen;
    }

    private async Task retryGameplayAsync()
    {
        try
        {
            // A retry creates a fresh audio engine. Wait until this engine has
            // released its WASAPI endpoint before loading the replacement.
            if (completionAudioStopRequested)
                await completionAudioStopTask.ConfigureAwait(true);
            else
                await audioEngine.StopAsync().ConfigureAwait(true);

            if (!this.IsCurrentScreen())
                return;

            var replacement = new GameplayScreen(
                originalBeatmap,
                skinPath: skinPath,
                mods: mods,
                cinemaArtworkPath: cinemaArtworkPath);
            replacement.manualPlaybackRateAdjustment =
                manualPlaybackRateAdjustment;
            replacement.manualPlaybackRateUsed =
                Math.Abs(manualPlaybackRateAdjustment) > 0.000001;
            IScreen destination = this.GetParentScreen();
            while (destination is GameplayScreen)
                destination = destination.GetParentScreen();

            if (destination is GameplaySessionRootScreen sessionRoot)
            {
                sessionRoot.ReplaceGameplay(replacement);
                return;
            }

            if (destination == null)
            {
                // Only test harnesses normally use gameplay as a stack root.
                // Prevent the stopped run from becoming active again.
                ValidForResume = false;
                this.Push(replacement);
                return;
            }

            destination.MakeCurrent();
            destination.Push(replacement);
        }
        catch (Exception exception)
        {
            retryTransitionInProgress = false;
            if (!ReplayMode && this.IsCurrentScreen())
                keyInputTimestamps.BeginCapture();

            Logger.Error(
                exception,
                "Gameplay retry could not release the current audio session.",
                LoggingTarget.Runtime);
        }
    }

    private void watchCompletedReplay()
    {
        if (completedReplay == null)
            return;

        var replayScreen = new GameplayScreen(
            originalBeatmap,
            null,
            skinPath,
            mods,
            completedReplay,
            cinemaArtworkPath)
        {
            manualPlaybackRateAdjustment =
                this.manualPlaybackRateAdjustment,
            manualPlaybackRateUsed =
                this.manualPlaybackRateUsed,
        };
        this.Push(replayScreen);
    }

    private void updatePlaybackRate(
        double gameplayTime,
        bool showOverlay = false)
    {
        double rate = currentPlaybackRate(gameplayTime);
        applyAudioPlaybackRate(rate);
        updatePlaybackRateReadout(
            gameplayTime,
            rate,
            showOverlay);
    }

    private void updatePlaybackRateReadout(
        double gameplayTime,
        double rate,
        bool showOverlay = false)
    {
        double bpm =
            beatTimingMap.TimingPointAt(gameplayTime).BeatsPerMinute
            * rate;
        bool showReadout =
            mods.HasDynamicRate
            || manualPlaybackRateUsed;
        bool overlayVisible =
            showOverlay || playbackRateOverlay.IsVisible;
        ManiaDifficultyRatings difficulty =
            showReadout || overlayVisible
                ? difficultyAt(rate)
                : null;
        ManiaDifficultyRatingMode difficultyMode =
            displaySettings.DifficultyRatingMode.Value;
        hud.UpdatePlaybackRate(
            rate,
            bpm,
            difficulty,
            difficultyMode,
            showReadout,
            manualPlaybackRateUsed);
        if (showOverlay)
        {
            playbackRateOverlay.Show(
                rate,
                bpm,
                difficulty,
                difficultyMode);
        }
        else if (playbackRateOverlay.IsVisible)
        {
            playbackRateOverlay.UpdateValues(
                rate,
                bpm,
                difficulty,
                difficultyMode);
        }
    }

    private void applyAudioPlaybackRate(double rate)
    {
        if (!audioStarted
            || audioEngine is not IAudioRateControl rateControl
            || Math.Abs(rate - lastAppliedPlaybackRate) < 0.005)
        {
            return;
        }

        rateControl.SetPlaybackRate(rate);
        lastAppliedPlaybackRate = rate;
    }

    private double currentPlaybackRate(double gameplayTime)
    {
        double baseRate = mods.HasDynamicRate
            ? currentDynamicRate(gameplayTime)
            : mods.PlaybackRate;
        return Math.Clamp(
            baseRate + manualPlaybackRateAdjustment,
            minimumPlaybackRate,
            maximumPlaybackRate);
    }

    private double currentDynamicRate(double gameplayTime) =>
        adaptiveSpeedState?.CurrentRate
        ?? mods.PlaybackRateAt(
            gameplayTime,
            firstObjectTimeMilliseconds,
            completionTimeMilliseconds);

    private ManiaDifficultyRatings difficultyAt(double rate)
    {
        collectCompletedDifficultyCalculation();

        double roundedRate = Math.Round(
            Math.Round(
                rate / playbackRateStep,
                MidpointRounding.AwayFromZero)
            * playbackRateStep,
            2,
            MidpointRounding.AwayFromZero);
        if (difficultyByRate.TryGetValue(
                roundedRate,
                out ManiaDifficultyRatings cached))
        {
            return cached;
        }

        if (difficultyCalculationTask == null)
        {
            difficultyCalculationRate = roundedRate;
            ManiaStarRatingContext starRatingContext =
                ManiaStarRatingContext.ForGameplay(
                    beatmap,
                    mods,
                    judgementConfiguration,
                    minesEnabled,
                    roundedRate);
            difficultyCalculationTask = Task.Run(
                () => ManiaDifficultyCalculator.CalculateResult(
                    beatmap,
                    starRatingContext,
                    roundedRate));
        }

        return null;
    }

    private void collectCompletedDifficultyCalculation()
    {
        if (difficultyCalculationTask?.IsCompleted != true)
            return;

        if (difficultyCalculationTask.IsCompletedSuccessfully)
        {
            difficultyByRate[difficultyCalculationRate] =
                difficultyCalculationTask.Result;
        }

        difficultyCalculationTask = null;
        difficultyCalculationRate = double.NaN;
    }

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

    private void drainAudioSampleTriggerTelemetry()
    {
        if (audioEngine is not ITimestampedPreparedAudioSamplePlayback telemetry)
            return;

        while (telemetry.TryDequeueSampleTriggerTelemetry(
                   out AudioSampleTriggerTelemetry sample))
        {
            audioSampleTriggerLatencyTracker.Record(sample);
        }

        ulong dropped = telemetry.SampleTriggerTelemetryStatus.DroppedCount;
        if (dropped < previousAudioSampleTelemetryDropped)
        {
            accumulatedAudioSampleTelemetryDropped +=
                previousAudioSampleTelemetryDropped;
        }
        previousAudioSampleTelemetryDropped = dropped;
    }

    private void logInputTimingSummary()
    {
        drainAudioSampleTriggerTelemetry();
        KeyInputTimestampBackendStatus backend =
            keyInputTimestamps.Status;
        InputDropObservation drops = inputDropTracker.Observe(
            backend.DroppedEdgeCount);
        InputAgeStatistics ages = inputAgeTracker.Snapshot();
        InputPipelineLatencyStatistics pipeline =
            inputPipelineLatencyTracker.Snapshot();
        AudioSampleTriggerLatencyStatistics nativeSamples =
            audioSampleTriggerLatencyTracker.Snapshot();
        ulong nativeTelemetryDropped =
            accumulatedAudioSampleTelemetryDropped
            + previousAudioSampleTelemetryDropped;
        string ageSummary = ages.Count == 0
            ? "no scored input samples"
            : $"input age p50={ages.P50Milliseconds:0.00} ms, "
              + $"p95={ages.P95Milliseconds:0.00} ms, "
              + $"p99={ages.P99Milliseconds:0.00} ms";
        string pipelineSummary = pipeline.CaptureToCompletion.Count == 0
            ? "no raw pipeline samples"
            : $"raw dequeue p50={pipeline.CaptureToDequeue.P50Milliseconds:0.00} ms, "
              + $"p99={pipeline.CaptureToDequeue.P99Milliseconds:0.00} ms; "
              + $"processing p50={pipeline.Processing.P50Milliseconds:0.00} ms, "
              + $"p99={pipeline.Processing.P99Milliseconds:0.00} ms; "
              + $"complete p50={pipeline.CaptureToCompletion.P50Milliseconds:0.00} ms, "
              + $"p99={pipeline.CaptureToCompletion.P99Milliseconds:0.00} ms, "
              + $"max={pipeline.CaptureToCompletion.MaximumMilliseconds:0.00} ms; "
              + formatAudioEnqueue(pipeline.CaptureToAudioEnqueue);
        string nativeSampleSummary =
            nativeSamples.CaptureToCallback.Count == 0
                ? "no native sample telemetry"
                : $"native sample capture->enqueue p50={nativeSamples.CaptureToEnqueue.P50Milliseconds:0.00} ms, "
                  + $"p99={nativeSamples.CaptureToEnqueue.P99Milliseconds:0.00} ms; "
                  + $"enqueue->callback p50={nativeSamples.EnqueueToCallback.P50Milliseconds:0.00} ms, "
                  + $"p99={nativeSamples.EnqueueToCallback.P99Milliseconds:0.00} ms; "
                  + $"capture->callback p99={nativeSamples.CaptureToCallback.P99Milliseconds:0.00} ms; "
                  + $"capture->estimated-presentation p99={nativeSamples.EstimatedCaptureToPresentation.P99Milliseconds:0.00} ms; "
                  + $"telemetry-dropped={nativeTelemetryDropped}";

        Logger.Log(
            $"Gameplay input timing: {backend.Name}; "
            + $"captured={backend.CapturedEdgeCount}, "
            + $"pending={backend.PendingEdgeCount}, "
            + $"dropped={drops.TotalDropped}; "
            + ageSummary + "; "
            + pipelineSummary + "; "
            + nativeSampleSummary);
    }

    private void updateDiagnosticSnapshot()
    {
        if (!diagnostics.IsEnabled)
        {
            diagnosticSnapshotElapsed = 0;
            return;
        }

        diagnosticSnapshotElapsed += Math.Max(0, Time.Elapsed);
        if (diagnosticSnapshotElapsed < 1000)
            return;

        diagnosticSnapshotElapsed %= 1000;
        AudioEngineSnapshot audio = audioEngine.Snapshot;
        KeyInputTimestampBackendStatus input = keyInputTimestamps.Status;
        double gameplayTime = currentGameplayTime;
        diagnostics.Trace(
            "RUNTIME",
            "gameplay-snapshot",
            $"gameplay={gameplayTime:0.###}ms"
            + $" | playback={audio.PlaybackTimeMilliseconds:0.###}ms"
            + $" | rate={currentPlaybackRate(gameplayTime):0.###}"
            + $" | paused={isPaused} | blocked={gameplayBlocked}"
            + $" | completed={gameplayCompleted} | failed={gameplayFailed}"
            + $" | health={healthState.Health:0.###}"
            + $" | accuracy={judgementState.Accuracy:P4}"
            + $" | input={input.Name}/{input.CapturedEdgeCount}/{input.PendingEdgeCount}/{input.DroppedEdgeCount}"
            + $" | {formatAudioStatus(audio.Status)}");
    }

    private static string formatAudioStatus(AudioEngineStatus status) =>
        $"audio={status.ActiveBackend}/{status.DeviceName}"
        + $"/{status.SampleRate}Hz/{status.BufferSize}f"
        + $"/{status.EstimatedOutputLatencyMilliseconds:0.###}ms"
        + $" | running={status.IsRunning} | faulted={status.IsFaulted}"
        + $" | callbacks={status.CallbackCount}"
        + $" | deadline-miss={status.CallbackDeadlineMissCount}"
        + $" | cadence-miss={status.CallbackCadenceMissCount}"
        + $" | overload={status.BackendOverloadCount}"
        + $" | underrun={status.HasUnderrun}"
        + $" | max-callback={status.MaxCallbackDurationMilliseconds:0.###}ms"
        + $" | max-interval={status.MaxCallbackIntervalMilliseconds:0.###}ms"
        + $" | error=0x{unchecked((uint)status.BackendError):X8}/{status.BackendErrorStage}";

    private static string formatAudioEnqueue(
        PipelineStageLatencyStatistics audioEnqueue) =>
        audioEnqueue.Count == 0
            ? "no successful keysound enqueue samples"
            : $"keysound enqueue n={audioEnqueue.Count}, "
              + $"p50={audioEnqueue.P50Milliseconds:0.00} ms, "
              + $"p99={audioEnqueue.P99Milliseconds:0.00} ms, "
              + $"max={audioEnqueue.MaximumMilliseconds:0.00} ms";

    private void onScrollSpeedChanged(
        osu.Framework.Bindables.ValueChangedEvent<double> change)
    {
        double gameplayTime = currentGameplayTime;
        if (layoutEditor?.IsSessionActive != true
            && !IsScrollSpeedAdjustmentAllowed(
                gameplayTime,
                gameplayStartTimeMilliseconds,
                isPaused,
                beatmap.BreakPeriods))
        {
            showScrollSpeedOverlay(appliedScrollSpeed, true);
            return;
        }

        appliedScrollSpeed = change.NewValue;
        playfield.SetApproachTime(computeApproachTime(
            change.NewValue,
            currentPlaybackRate(gameplayTime)));
        showScrollSpeedOverlay(appliedScrollSpeed, false);
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

    private void updatePlaybackRateAdjustedApproachTime(
        double gameplayTime,
        bool force = false)
    {
        if ((!force
             && !mods.HasDynamicRate
             && Math.Abs(manualPlaybackRateAdjustment) <= 0.000001)
            || beatmap.SourceFormat is not (
                ChartSourceFormat.Quaver
                or ChartSourceFormat.OsuMania
                or ChartSourceFormat.OsuStandard))
        {
            return;
        }

        double playbackRate = currentPlaybackRate(gameplayTime);
        if (!force
            && Math.Abs(playbackRate - lastApproachPlaybackRate)
               < 0.0005)
        {
            return;
        }

        playfield.SetApproachTime(computeApproachTime(
            appliedScrollSpeed,
            playbackRate));
        lastApproachPlaybackRate = playbackRate;
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

        if (sourceFormat is ChartSourceFormat.OsuMania
            or ChartSourceFormat.OsuStandard)
        {
            // osu!lazer multiplies Mania's target time range by the active
            // track tempo/frequency adjustment. Gameplay time advances by
            // the same rate, keeping physical scroll velocity stable.
            return baseApproachTime * playbackRate;
        }

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

    internal bool HandlePlaybackRateShortcut(
        Key key,
        bool altPressed,
        double? gameplayTimeOverride = null)
    {
        if (!altPressed)
            return false;

        double amount = key switch
        {
            Key.Plus or Key.KeypadPlus => playbackRateStep,
            Key.Minus or Key.KeypadMinus => -playbackRateStep,
            _ => 0,
        };
        if (amount == 0)
            return false;

        double gameplayTime =
            gameplayTimeOverride ?? currentGameplayTime;
        double currentRate = currentPlaybackRate(gameplayTime);
        double adjustedRate = AdjustPlaybackRate(currentRate, amount);
        manualPlaybackRateAdjustment += adjustedRate - currentRate;
        if (Math.Abs(adjustedRate - currentRate) > 0.000001)
            manualPlaybackRateUsed = true;

        updatePlaybackRate(gameplayTime, true);
        updatePlaybackRateAdjustedApproachTime(
            gameplayTime,
            true);
        return true;
    }

    internal static double AdjustPlaybackRate(
        double playbackRate,
        double amount)
    {
        if (!double.IsFinite(playbackRate)
            || !double.IsFinite(amount))
        {
            throw new ArgumentOutOfRangeException(nameof(playbackRate));
        }

        return Math.Clamp(
            Math.Round(
                playbackRate + amount,
                2,
                MidpointRounding.AwayFromZero),
            minimumPlaybackRate,
            maximumPlaybackRate);
    }

    internal static AudioPitchMode ResolvePlaybackRatePitchMode(
        ManiaModSet mods,
        AudioPitchMode manualPlaybackRatePitchMode)
    {
        ArgumentNullException.ThrowIfNull(mods);

        if (mods.ChangesAudioPitch)
            return AudioPitchMode.ScaleWithRate;

        if (mods.FixedRateMod is not null || mods.HasDynamicRate)
            return AudioPitchMode.Preserve;

        return manualPlaybackRatePitchMode;
    }

    internal bool HandleScrollSpeedShortcut(
        Key key,
        bool controlPressed,
        double? gameplayTimeOverride = null)
    {
        double direction = key switch
        {
            _ when key == gameplaySettings.IncreaseScrollSpeedKey.Value =>
                1,
            _ when key == gameplaySettings.DecreaseScrollSpeedKey.Value =>
                -1,
            Key.Plus or Key.KeypadPlus when controlPressed =>
                1,
            Key.Minus or Key.KeypadMinus when controlPressed =>
                -1,
            _ => 0,
        };

        if (direction == 0)
            return false;

        double gameplayTime =
            gameplayTimeOverride ?? currentGameplayTime;
        if (!IsScrollSpeedAdjustmentAllowed(
                gameplayTime,
                gameplayStartTimeMilliseconds,
                isPaused,
                beatmap.BreakPeriods))
        {
            showScrollSpeedOverlay(appliedScrollSpeed, true);
            return true;
        }

        double previousSpeed = gameplaySettings.ScrollSpeed.Value;
        if (gameplaySettings.ScrollSpeedAdjustmentMode.Value
            == ScrollSpeedAdjustmentMode.Milliseconds)
        {
            gameplaySettings.AdjustScrollTimeMilliseconds(
                -direction
                * OsuManiaScrollSpeed.ScrollTimeStepMilliseconds);
        }
        else
        {
            gameplaySettings.AdjustScrollSpeed(
                direction * OsuManiaScrollSpeed.ShortcutStep);
        }
        if (gameplaySettings.ScrollSpeed.Value == previousSpeed)
            showScrollSpeedOverlay(appliedScrollSpeed, false);

        return true;
    }

    internal static bool IsScrollSpeedAdjustmentAllowed(
        double gameplayTimeMilliseconds,
        double gameplayStartTimeMilliseconds,
        bool paused,
        IReadOnlyList<YokkoBreakPeriod> breakPeriods)
    {
        if (!double.IsFinite(gameplayTimeMilliseconds)
            || !double.IsFinite(gameplayStartTimeMilliseconds)
            || paused)
        {
            return false;
        }

        if (gameplayTimeMilliseconds - gameplayStartTimeMilliseconds
            <= scrollSpeedAdjustmentGraceMilliseconds)
        {
            return true;
        }

        return breakPeriods?.Any(period =>
            gameplayTimeMilliseconds
                >= period.StartTimeMilliseconds
            && gameplayTimeMilliseconds
                <= period.EndTimeMilliseconds) == true;
    }

    private void showScrollSpeedOverlay(
        double speed,
        bool locked)
    {
        scrollSpeedOverlay.Show(
            speed,
            (int)Math.Round(OsuManiaScrollSpeed.ComputeScrollTime(speed)),
            gameplaySettings.ScrollSpeedAdjustmentMode.Value
                == ScrollSpeedAdjustmentMode.Milliseconds,
            locked);
    }

    internal bool MatchesShortcut(
        ManiaShortcutAction action,
        Key key) => matchesShortcut(action, key);

    private bool matchesShortcut(
        ManiaShortcutAction action,
        Key key) => gameplaySettings.GetShortcutBinding(action) == key;

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
        gameplaySettings.LayoutPlayfieldWidthScale.Value =
            playfieldWidthScale;
        playfield.SetWidthScale(playfieldWidthScale);
        return true;
    }

    private GameplayPlayfield createGameplayPlayfield()
    {
        var result = new GameplayPlayfield(
            beatmap,
            keyBindings,
            maniaSkin,
            computeApproachTime(
                gameplaySettings.ScrollSpeed.Value,
                currentPlaybackRate(0)),
            gameplaySettings.ShowLanePressFeedback.Value,
            mods,
            minesEnabled,
            skinSettings?.ShowComboBursts.Value != false,
            skinSettings?.LongNoteCutEnabled.Value == true
                ? Math.Clamp(
                    skinSettings.LongNoteCutAmount.Value,
                    YokkoSkinSettings.MinimumLongNoteCutAmount,
                    YokkoSkinSettings.MaximumLongNoteCutAmount)
                : 0,
            gameplaySettings.ScrollDirection.Value)
        {
            Anchor = Anchor.BottomCentre,
            Origin = Anchor.BottomCentre,
            Scale = Vector2.One,
        };

        result.ConfigureSkinJudgementFeedback(
            gameplaySettings.JudgementDisplayDurationMilliseconds.Value,
            gameplaySettings.JudgementOpacity.Value);
        return result;
    }

    private GameplayHud createGameplayHud(
        GameplayPlayfield targetPlayfield) =>
        new(
            beatmap,
            mods,
            judgementConfiguration,
            targetPlayfield.HasSkinHealthBar)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Position = new Vector2(-20, 20),
        };

    private GameplayLayoutEditorLiveSettings
        createLayoutEditorLiveSettings() =>
        new(
            layoutEditorSkinOptions,
            () => skinSettings?.SelectedSkinId.Value
                  ?? string.Empty,
            selectLayoutEditorSkin,
            () => gameplaySettings.ScrollSpeed.Value,
            gameplaySettings.SetScrollSpeed,
            () => gameplaySettings.ScrollDirection.Value,
            setLayoutEditorScrollDirection,
            () => gameplaySettings.BackgroundDim.Value,
            value => gameplaySettings.BackgroundDim.Value = Math.Clamp(
                value,
                YokkoGameplaySettings.MinimumBackgroundDim,
                YokkoGameplaySettings.MaximumBackgroundDim),
            () => gameplaySettings
                .JudgementDisplayDurationMilliseconds.Value,
            setLayoutEditorJudgementDisplayDuration,
            () => gameplaySettings.JudgementOpacity.Value,
            setLayoutEditorJudgementOpacity,
            () => gameplaySettings.ShowJudgementHitError.Value,
            setLayoutEditorShowJudgementHitError,
            () => gameplaySettings.ShowTimingBar.Value,
            setLayoutEditorShowTimingBar);

    private void setLayoutEditorJudgementDisplayDuration(double value)
    {
        gameplaySettings.SetJudgementDisplayDuration(value);
        judgementReadout.SetDisplayDuration(
            gameplaySettings.JudgementDisplayDurationMilliseconds.Value);
        playfield.ConfigureSkinJudgementFeedback(
            gameplaySettings.JudgementDisplayDurationMilliseconds.Value,
            gameplaySettings.JudgementOpacity.Value);
    }

    private void setLayoutEditorJudgementOpacity(double value)
    {
        gameplaySettings.SetJudgementOpacity(value);
        judgementReadout.SetOpacity(
            gameplaySettings.JudgementOpacity.Value);
        playfield.ConfigureSkinJudgementFeedback(
            gameplaySettings.JudgementDisplayDurationMilliseconds.Value,
            gameplaySettings.JudgementOpacity.Value);
    }

    private void setLayoutEditorShowJudgementHitError(bool value)
    {
        gameplaySettings.ShowJudgementHitError.Value = value;
        judgementReadout.SetShowHitError(value);
    }

    private void setLayoutEditorShowTimingBar(bool value)
    {
        gameplaySettings.ShowTimingBar.Value = value;
        timingBar.Alpha = value ? 1 : 0;
    }

    private IReadOnlyList<GameplayLayoutEditorSkinOption>
        layoutEditorSkinOptions()
    {
        List<GameplayLayoutEditorSkinOption> options =
        [
            new(string.Empty, string.Empty),
        ];
        options.AddRange(
            skinLibrary.GetInstalledSkins()
                       .Select(entry =>
                           new GameplayLayoutEditorSkinOption(
                               entry.Id,
                               entry.Name)));
        return options;
    }

    private void selectLayoutEditorSkin(string id)
    {
        id ??= string.Empty;
        string current = skinSettings?.SelectedSkinId.Value
                         ?? string.Empty;
        if (string.Equals(
                current,
                id,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (skinSettings == null)
            return;

        if (string.IsNullOrWhiteSpace(id))
            skinSettings.SelectedSkinId.Value = string.Empty;
        else if (!skinLibrary.Select(id))
            return;

        rebuildGameplayPresentation(reloadSkin: true);
    }

    private void setLayoutEditorScrollDirection(
        ManiaScrollDirection direction)
    {
        if (gameplaySettings.ScrollDirection.Value == direction)
            return;

        gameplaySettings.ScrollDirection.Value = direction;
        rebuildGameplayPresentation(reloadSkin: false);
    }

    private void rebuildGameplayPresentation(bool reloadSkin)
    {
        GameplayPlayfield previousPlayfield = playfield;
        GameplayLaneCovers previousCovers = laneCovers;
        GameplayHud previousHud = hud;
        OsuManiaSkinLease previousSkinLease = null;

        if (reloadSkin)
        {
            previousSkinLease = maniaSkinLease;
            maniaSkin = null;
            maniaSkinLease = null;
            loadSkin(renderer, includeConfiguredFallback: false);
        }

        GameplayPlayfield nextPlayfield = createGameplayPlayfield();
        nextPlayfield.SetWidthScale(playfieldWidthScale);
        for (int lane = 0; lane < pressedLanes.Length; lane++)
            nextPlayfield.SetLanePressed(lane, pressedLanes[lane]);

        double gameplayTime = isPaused
            ? pausedGameplayTime
            : currentGameplayTime;
        nextPlayfield.SetApproachTime(computeApproachTime(
            gameplaySettings.ScrollSpeed.Value,
            currentPlaybackRate(gameplayTime)));
        nextPlayfield.UpdateGameplayTime(
            gameplayTime,
            judgementState,
            healthState);

        GameplayLaneCovers nextCovers = new(
            nextPlayfield,
            gameplaySettings);
        GameplayHud nextHud = createGameplayHud(nextPlayfield);
        nextHud.UpdateState(gameplayTime, judgementState, healthState);
        if (!hasAudioClock)
            nextHud.ShowFrameClock();
        if (audioEngine != null)
        {
            nextHud.UpdateAudioStatus(
                audioEngine.Snapshot.Status,
                activeRequestedBackend);
        }
        if (mutedAudio != null)
            nextHud.UpdateMutedMix(mutedAudio.Current);

        nextPlayfield.Alpha = previousPlayfield?.Alpha ?? 1;
        nextHud.Alpha = previousHud?.Alpha ?? 1;

        playfield = nextPlayfield;
        laneCovers = nextCovers;
        hud = nextHud;

        AddInternal(nextPlayfield);
        AddInternal(nextCovers);
        AddInternal(nextHud);
        layoutEditor?.ReplaceTargets(nextPlayfield, nextHud);
        updatePlayfieldLayout();

        previousPlayfield?.Expire();
        previousCovers?.Expire();
        previousHud?.Expire();
        if (previousSkinLease != null)
        {
            Scheduler.AddDelayed(
                previousSkinLease.Dispose,
                200);
        }
    }

    internal void BeginLayoutTestPlayForTest() =>
        beginLayoutTestPlay();

    internal void SetLayoutEditorScrollSpeedForTest(double speed) =>
        gameplaySettings.SetScrollSpeed(speed);

    internal void SetLayoutEditorScrollDirectionForTest(
        ManiaScrollDirection direction) =>
        setLayoutEditorScrollDirection(direction);

    internal ManiaScrollDirection LayoutEditorScrollDirectionForTest =>
        gameplaySettings.ScrollDirection.Value;

    internal double AppliedScrollSpeedForTest => appliedScrollSpeed;

    internal double LayoutEditorBackgroundDimForTest =>
        gameplaySettings.BackgroundDim.Value;

    internal float DisplayedBackgroundDimForTest =>
        backgroundDim?.Alpha ?? 0;

    internal void SetLayoutEditorBackgroundDimForTest(double dim) =>
        gameplaySettings.BackgroundDim.Value = Math.Clamp(
            dim,
            YokkoGameplaySettings.MinimumBackgroundDim,
            YokkoGameplaySettings.MaximumBackgroundDim);

    internal void SetLayoutEditorJudgementDurationForTest(double value) =>
        setLayoutEditorJudgementDisplayDuration(value);

    internal void SetLayoutEditorJudgementOpacityForTest(double value) =>
        setLayoutEditorJudgementOpacity(value);

    internal void SetLayoutEditorShowHitErrorForTest(bool value) =>
        setLayoutEditorShowJudgementHitError(value);

    internal void SetLayoutEditorShowTimingBarForTest(bool value) =>
        setLayoutEditorShowTimingBar(value);

    internal double LayoutEditorJudgementDurationForTest =>
        judgementReadout.DisplayDurationForTest;

    internal float LayoutEditorJudgementOpacityForTest =>
        judgementReadout.ContentOpacityForTest;

    internal bool LayoutEditorShowsHitErrorForTest =>
        judgementReadout.ShowsHitErrorForTest;

    private void beginLayoutTestPlay()
    {
        if (layoutEditor?.IsEditing != true
            || !isPaused
            || pauseTransitionInProgress
            || resumeCountdownInProgress)
        {
            return;
        }

        layoutEditor.BeginTestPlay();
        beginResumeCountdown();
    }

    private async Task returnToLayoutEditorFromTestAsync()
    {
        if (layoutEditor?.IsTestingLayout != true)
            return;

        if (resumeCountdownInProgress)
        {
            cancelResumeCountdown();
            restoreLayoutEditorAfterTest();
            return;
        }

        if (!isPaused)
            await pauseGameplayAsync().ConfigureAwait(true);
        else
            restoreLayoutEditorAfterTest();
    }

    private void restoreLayoutEditorAfterTest()
    {
        if (layoutEditor?.IsTestingLayout != true || !isPaused)
            return;

        if (pauseOverlay != null)
            pauseOverlay.Alpha = 0;
        layoutEditor.EndTestPlay();
    }

    private void openGameplayLayoutEditorFromPause()
    {
        if (layoutEditor == null
            || layoutEditor.IsEditing
            || !isPaused
            || gameplayBlocked
            || gameplayCompleted
            || gameplayFailed
            || retryTransitionInProgress)
        {
            return;
        }

        if (pauseOverlay != null)
            pauseOverlay.Alpha = 0;

        layoutEditor.SetEditing(true);
    }

    private void closeGameplayLayoutEditor()
    {
        layoutEditor?.SetEditing(false);

        if (pauseOverlay != null)
            pauseOverlay.Alpha = 1;
    }

    private void saveGameplayLayout() => yokkoConfig.Save();

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
            lastAppliedPlaybackRate = double.NaN;
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

    private void loadSkin(
        IRenderer renderer,
        bool includeConfiguredFallback = true)
    {
        string resolvedPath =
            includeConfiguredFallback
            && !string.IsNullOrWhiteSpace(skinPath)
            ? skinPath
            : skinLibrary.CurrentSkinPath
              ?? (includeConfiguredFallback
                  ? OsuManiaSkinLocator.FindConfiguredPath()
                  : null);

        if (string.IsNullOrWhiteSpace(resolvedPath))
            return;

        try
        {
            maniaSkinLease = gameplaySkinCache.Acquire(
                resolvedPath,
                keyBindings.KeyCount,
                renderer,
                beatmap.StageCount);
            maniaSkin = maniaSkinLease.Skin;
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
