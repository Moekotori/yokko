using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Bindings;
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
    private const double replaySeekStepMilliseconds = 5000;
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
    private bool developerAutoplayRun;
    private JudgementConfiguration judgementConfiguration;
    private bool minesEnabled;
    private readonly string artworkPath;
    private readonly Texture preparedArtworkTexture;
    private readonly bool quaverHasSignificantScrollVelocities;
    private readonly BeatTimingMap beatTimingMap;
    private TextureStore artworkTextures;
    private Sprite artworkBackground;
    private readonly List<GameplayReplayInput> recordedReplayInputs = new();
    private readonly List<JudgementEvent> expiredJudgements = new();
    private readonly List<JudgementEvent> inputJudgements = new(8);
    private readonly List<JudgementInputEvent> inputTimingEvents = new(8);
    private readonly List<double> resultHitErrors = new(1024);
    [Resolved]
    private YokkoAudioSettings audioSettings { get; set; }
    [Resolved]
    private OsuManiaSkinLibrary skinLibrary { get; set; }
    [Resolved(canBeNull: true)]
    private YokkoSkinSettings skinSettings { get; set; }
    [Resolved]
    private YokkoGameplaySettings gameplaySettings { get; set; }
    [Resolved]
    private SkinHudLayoutStore skinHudLayoutStore { get; set; }
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
    private LayoutAutoplayRollbackState layoutAutoplayRollback;
    private bool layoutAutoplayDemoActive;
    private bool layoutPreviewReturnInProgress;
    private KeyModeBindings keyBindings;
    private bool[] pressedLanes;
    private double startTimeMilliseconds;
    private bool gameplayStarted;
    private bool inputCaptureActive;
    private bool hasAudioClock;
    private bool audioStarted;
    private OsuManiaSkin maniaSkin;
    private OsuManiaSkinLease maniaSkinLease;
    private string appliedSelectedSkinId = string.Empty;

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
    private double quickRetryHoldStartTime = double.NaN;
    private GameplayHud hud;
    private ManiaScoreResult completedResult;
    private GameplayResultOverlay resultOverlay;
    private GameplayResultPresentation completedResultPresentation;
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
    private GameplayReplayTimeline replayTimeline;
    private GameplayReplay completedReplay;
    private float playfieldWidthScale = 1;
    private GameHost host;
    private IRenderer renderer;
    private bool isPaused;
    private bool pauseTransitionInProgress;
    private int pausesUsed;
    private bool resumeCountdownInProgress;
    private double pausedGameplayTime;
    private double pausedAudioPosition;
    private GameplayPauseOverlay pauseOverlay;
    private GameplayResumeCountdown resumeCountdown;
    private GameplayFailOverlay failOverlay;
    private Box backgroundDim;
    private GameplayComboReadout comboReadout;
    private JudgementReadout judgementReadout;
    private GameplayTimingBar timingBar;
    private GameplayScrollSpeedOverlay scrollSpeedOverlay;
    private GameplayPlaybackRateOverlay playbackRateOverlay;
    private GameplayReplayControlsOverlay replayControls;
    private bool focusModeActive;
    private bool replaySeekInProgress;
    private bool replaySeekPauseRequested;
    private double pendingReplaySeekTarget = double.NaN;
    private double appliedScrollSpeed;
    private readonly CancellationTokenSource gameplayLifetimeCancellation =
        new();
    private CancellationTokenSource keysoundPreparationCancellation;
    private Task keysoundPreparationTask = Task.CompletedTask;
    private int keysoundPreparationGeneration;
    private int disposalStarted;
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
    private double lastReplayAdaptiveSimulationTime = double.NaN;
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
    internal bool PresentationTexturesReady =>
        (artworkBackground?.Texture?.UploadComplete ?? true)
        && (maniaSkin?.AllTexturesUploaded ?? true);
    internal int PendingPresentationTextureUploads =>
        (artworkBackground?.Texture?.UploadComplete == false ? 1 : 0)
        + (maniaSkin?.PendingTextureUploadCount ?? 0);
    internal bool IsPaused => isPaused;
    internal bool ReplaySeekInProgress => replaySeekInProgress;
    internal bool IsLayoutEditing =>
        layoutEditor?.IsSessionActive == true;
    internal bool IsLayoutTestPlaying =>
        layoutEditor?.IsTestingLayout == true;
    internal bool IsLayoutAutoplayPlaying =>
        layoutEditor?.IsAutoplayDemo == true;
    internal bool FocusModeActive => focusModeActive;
    internal float LayoutOverviewAspectRatio =>
        layoutEditor?.OverviewAspectRatio ?? 0;
    internal bool PauseTransitionInProgress => pauseTransitionInProgress;
    internal int PausesUsed => pausesUsed;
    internal int PausesRemaining => mods.Contains(ManiaModId.NoPause)
        ? Math.Max(0, mods.NoPauseAllowedPauses - pausesUsed)
        : int.MaxValue;
    internal bool ResumeCountdownInProgress => resumeCountdownInProgress;
    internal bool QuickRetryHoldActive =>
        !double.IsNaN(quickRetryHoldStartTime);
    internal double? ResumeCountdownMillisecondsOverride;
    internal double QuickRetryHoldMilliseconds = 180;

    private double resumeCountdownDuration =>
        ResumeCountdownMillisecondsOverride
        ?? (gameplaySettings.ResumeCountdownEnabled.Value
            ? gameplaySettings.ResumeCountdownMilliseconds.Value
            : 0);
    internal double CurrentGameplayTime => currentGameplayTime;
    internal ManiaScoreResult CompletedResult => completedResult;
    internal ManiaScoreResult CurrentResultForTest =>
        judgementState.CreateResult();
    internal int ResultHitErrorCountForTest => resultHitErrors.Count;

    internal double PlayfieldApproachTimeForTest =>
        playfield?.ApproachTimeMilliseconds ?? 0;

    internal bool PlayfieldJudgementRegionAlignedForTest =>
        playfield?.JudgementRegionAlignedForTest == true;
    internal string SavedReplayPath { get; private set; }
    internal bool BestScoreSaved { get; private set; }
    internal bool ReplayMode => replay != null;
    internal bool AutoplayMode => mods.IsAutomation
                                  || layoutAutoplayDemoActive;
    internal bool DeveloperAutoplayRun => developerAutoplayRun;
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
    internal bool HasArtworkBackground => artworkBackground != null;
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
        string artworkPath = null,
        Texture preparedArtworkTexture = null)
        : this(
            beatmap,
            audioEngine,
            skinPath,
            mods,
            null,
            artworkPath,
            preparedArtworkTexture)
    {
    }

    internal GameplayScreen(
        YokkoBeatmap beatmap,
        IAudioEngine audioEngine,
        string skinPath,
        ManiaModSet mods,
        GameplayReplay replay)
        : this(
            beatmap,
            audioEngine,
            skinPath,
            mods,
            replay,
            null,
            null)
    {
    }

    private GameplayScreen(
        YokkoBeatmap beatmap,
        IAudioEngine audioEngine,
        string skinPath,
        ManiaModSet mods,
        GameplayReplay replay,
        string artworkPath,
        Texture preparedArtworkTexture)
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
        this.artworkPath = artworkPath;
        this.preparedArtworkTexture = preparedArtworkTexture;
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
        if (replay?.Frames.Count > 0)
        {
            completionTimeMilliseconds = Math.Max(
                completionTimeMilliseconds,
                replay.Frames[^1].TimeMilliseconds);
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
            developerAutoplayRun = mods.IsDeveloperAutoplay;
            replay = GameplayAutoGenerator.Generate(
                beatmap,
                mods,
                judgementConfiguration);
        }
        if (replay != null)
            replayTimeline = new GameplayReplayTimeline(replay.Frames);
        keyBindings = beatmap.ScratchLanes.Count > 0
            ? KeyModeBindings.ForMode(
                beatmap.KeyMode,
                beatmap.ScratchLanes.Count == 2
                    ? gameplaySettings.GetBmsDoublePlayInputKeys(
                        (int)beatmap.KeyMode)
                    : gameplaySettings.GetBmsInputKeys(
                        (int)beatmap.KeyMode,
                        beatmap.ScratchLanes[0]))
            : gameplaySettings.SupportedKeyModes.Contains(beatmap.KeyMode)
                ? KeyModeBindings.ForMode(
                    beatmap.KeyMode,
                    gameplaySettings.GetInputKeys(beatmap.KeyMode))
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
        double judgementOverallDifficulty =
            mods.EffectiveOverallDifficulty(beatmap.OverallDifficulty);
        double judgementDifficultyMultiplier =
            mods.HitWindowDifficultyMultiplier;
        if (judgementConfiguration.Mode == JudgementMode.OsuStable
            && mods.Contains(ManiaModId.Easy))
        {
            // stable Easy halves OD; unlike lazer's Mania implementation it
            // does not scale every window (including MAX) by 1.4x.
            judgementOverallDifficulty *= 0.5;
            judgementDifficultyMultiplier = 1;
        }

        OsuStableScoreV1ModMultipliers stableScoreMods =
            judgementConfiguration.Mode == JudgementMode.OsuStable
                ? OsuStableScoreV1Mods.Calculate(beatmap, mods)
                : new OsuStableScoreV1ModMultipliers(
                    mods.ScoreMultiplier,
                    1);
        judgementState = new BeatmapJudgementState(
            beatmap,
            new JudgementWindows(
                judgementOverallDifficulty,
                mods.HitWindowSpeedMultiplier,
                judgementDifficultyMultiplier,
                mods.Contains(ManiaModId.Classic),
                mods.Contains(ManiaModId.ScoreV2),
                beatmap.ConversionSource is not null,
                judgementConfiguration,
                beatmap.BmsJudgement?.WindowMultiplier
                ?? BmsJudgementMetadata.Default.WindowMultiplier,
                beatmap.BmsJudgement?.RegularKeysPerStage
                ?? (beatmap.RegularLaneCount / beatmap.StageCount == 5
                    ? 5
                    : 7)),
            mods.Contains(ManiaModId.NoRelease),
            stableScoreMods.ScoreMultiplier,
            minesEnabled,
            stableScoreMods.BonusPunishmentDivider);
        keysoundSelector = new GameplayKeysoundSelector(
            beatmap,
            judgementState);
        loadSkin(renderer);
        appliedSelectedSkinId = skinSettings?.SelectedSkinId.Value
                                ?? string.Empty;
        prepareHitSamples();
        bool hasSamplePlayback = headSamplesByHitObject.Any(
                                     static samples => samples.Length > 0)
                                 || tailSamplesByHitObject.Any(
                                     static samples => samples.Length > 0)
                                 || slidingSamplesByHitObject.Any(
                                     static samples => samples.Length > 0)
                                 || scheduledSamples.Length > 0;
        audioEngine ??= string.IsNullOrWhiteSpace(beatmap.AudioPath)
                         && !hasSamplePlayback
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
        audioSettings.MixChanged += onAudioMixChanged;
        hasAudioClock = !string.IsNullOrWhiteSpace(beatmap.AudioPath)
                        || hasSamplePlayback && audioEngine is NativeAudioEngine;
        restartKeysoundPreparation();

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
                gameplaySettings.JudgementOpacity.Value,
                gameplaySettings.JudgementHitErrorScale.Value)
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
                beginLayoutAutoplayDemo,
                () => _ = returnToLayoutEditorFromTestAsync(),
                saveGameplayLayout,
                closeGameplayLayoutEditor),
        };

        if (ReplayMode && !mods.IsCinema)
        {
            AddInternal(replayControls = new GameplayReplayControlsOverlay(
                () => _ = toggleReplayPlaybackAsync(),
                () => requestReplaySeek(-1),
                () => requestReplaySeek(1),
                requestReplaySeekTo,
                () => adjustReplayPlaybackRate(-playbackRateStep),
                () => adjustReplayPlaybackRate(playbackRateStep),
                gameplaySettings.ReplayControlsOffsetX,
                gameplaySettings.ReplayControlsOffsetY,
                saveGameplayLayout));
        }

        playfieldWidthScale = (float)Math.Clamp(
            gameplaySettings.LayoutPlayfieldWidthScale.Value,
            YokkoGameplaySettings.MinimumPlayfieldWidthScale,
            YokkoGameplaySettings.MaximumPlayfieldWidthScale);
        playfield.SetWidthScale(playfieldWidthScale);
        applySavedHudVisibility();

        Texture artworkTexture = loadArtworkTexture(renderer);
        if (artworkTexture != null)
        {
            AddInternal(artworkBackground = new Sprite
            {
                RelativeSizeAxes = Axes.Both,
                Texture = artworkTexture,
                FillMode = FillMode.Fill,
                Colour = mods.IsCinema
                    ? new osuTK.Graphics.Color4(0.82f, 0.82f, 0.86f, 1)
                    : osuTK.Graphics.Color4.White,
                Depth = 950,
            });
        }

        if (mods.IsCinema)
        {
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

        if (skinSettings != null)
            skinSettings.SelectedSkinId.ValueChanged +=
                onSelectedSkinIdChanged;

        playfield.SetApproachTime(computeApproachTime(
            appliedScrollSpeed,
            currentPlaybackRate(currentGameplayTime)));

        diagnostics.Trace(
            "GAMEPLAY",
            "loaded",
            $"first-object={firstObjectTimeMilliseconds:0.###}ms"
            + $" | completion={completionTimeMilliseconds:0.###}ms");
    }

    private void onSelectedSkinIdChanged(ValueChangedEvent<string> _)
    {
        Scheduler.Add(() =>
        {
            if (!isPaused
                || layoutEditor?.IsSessionActive != true
                || layoutEditor.IsAutoplayDemo)
            {
                return;
            }

            applySelectedSkinIfChanged();
        });
    }

    public override void OnEntering(ScreenTransitionEvent e)
    {
        base.OnEntering(e);
        if (gameplayStarted)
            return;

        gameplayStarted = true;
        host.Deactivated += onHostDeactivated;
        if (!ReplayMode && !isPaused)
            beginInputCapture();

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
            "started",
            $"lead-in={leadInMilliseconds:0.###}ms"
            + $" | first-object={firstObjectTimeMilliseconds:0.###}ms"
            + $" | completion={completionTimeMilliseconds:0.###}ms"
            + $" | raw-input={keyInputTimestamps.IsRawInputAvailable}");
    }

    public override void OnResuming(ScreenTransitionEvent e)
    {
        base.OnResuming(e);

        // Settings is pushed above the still-paused gameplay screen. Apply a
        // changed library selection to this existing run before it is shown
        // again, rather than requiring the player to restart the chart.
        if (isPaused)
            applySelectedSkinIfChanged();
    }

    protected override void Update()
    {
        base.Update();
        updatePlayfieldLayout();
        drainAudioSampleTriggerTelemetry();
        updateQuickRetryHold();
        updateDiagnosticSnapshot();
        replayControls?.UpdateState(
            currentGameplayTime,
            completionTimeMilliseconds,
            currentPlaybackRate(currentGameplayTime),
            isPaused);

        if (gameplayCompletionTransitionActive)
        {
            updateGameplayCompletionTransition();
            return;
        }

        if (gameplayBlocked
            || gameplayCompleted
            || gameplayFailed
            || retryTransitionInProgress
            || replaySeekInProgress
            || isPaused)
            return;

        if (!ReplayMode)
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
                                    && layoutAutoplayDemoActive
            ? GameplayPresentationClock.EstimateVisualTime(
                gameplayTime,
                audioEngine as ITimestampedAudioClock,
                clockObservation.Audio,
                Stopwatch.GetTimestamp(),
                Stopwatch.Frequency,
                activeUserOffsetMilliseconds)
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
        {
            comboReadout.UpdateState(judgementState.Combo);
            if (playfield.UsesSkinJudgementOverlay)
                comboReadout.Alpha = 0;
        }

        if (judgementState.IsComplete
            && gameplayTime >= completionTimeMilliseconds)
        {
            if (layoutEditor?.IsTestingLayout == true)
                _ = returnToLayoutEditorFromTestAsync();
            else
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
        playfield.SetSkinFeedbackLayout(
            new Vector2(
                (float)gameplaySettings.LayoutComboOffsetX.Value
                * DrawWidth / Math.Max(0.01f, playfield.Scale.X),
                (float)gameplaySettings.LayoutComboOffsetY.Value
                * DrawHeight / Math.Max(0.01f, playfield.Scale.Y)),
            new Vector2(
                (float)Math.Clamp(
                    gameplaySettings.LayoutComboScaleX.Value,
                    YokkoGameplaySettings.MinimumLayoutScale,
                    YokkoGameplaySettings.MaximumLayoutScale),
                (float)Math.Clamp(
                    gameplaySettings.LayoutComboScaleY.Value,
                    YokkoGameplaySettings.MinimumLayoutScale,
                    YokkoGameplaySettings.MaximumLayoutScale)),
            new Vector2(
                (float)gameplaySettings.LayoutJudgementOffsetX.Value
                * DrawWidth / Math.Max(0.01f, playfield.Scale.X),
                (float)gameplaySettings.LayoutJudgementOffsetY.Value
                * DrawHeight / Math.Max(0.01f, playfield.Scale.Y)),
            new Vector2(
                (float)Math.Clamp(
                    gameplaySettings.LayoutJudgementScaleX.Value,
                    YokkoGameplaySettings.MinimumLayoutScale,
                    YokkoGameplaySettings.MaximumLayoutScale),
                (float)Math.Clamp(
                    gameplaySettings.LayoutJudgementScaleY.Value,
                    YokkoGameplaySettings.MinimumLayoutScale,
                    YokkoGameplaySettings.MaximumLayoutScale)));

        hud.Position = new Vector2(-20, 20);
        hud.Scale = Vector2.One;
        hud.SetLayoutTransforms(
            new Vector2(
                (float)gameplaySettings.LayoutAccuracyOffsetX.Value
                * DrawWidth,
                (float)gameplaySettings.LayoutAccuracyOffsetY.Value
                * DrawHeight),
            new Vector2(
                (float)Math.Clamp(
                    gameplaySettings.LayoutAccuracyScaleX.Value,
                    YokkoGameplaySettings.MinimumLayoutScale,
                    YokkoGameplaySettings.MaximumLayoutScale),
                (float)Math.Clamp(
                    gameplaySettings.LayoutAccuracyScaleY.Value,
                    YokkoGameplaySettings.MinimumLayoutScale,
                    YokkoGameplaySettings.MaximumLayoutScale)),
            new Vector2(
                (float)gameplaySettings.LayoutProgressOffsetX.Value
                * DrawWidth,
                (float)gameplaySettings.LayoutProgressOffsetY.Value
                * DrawHeight),
            new Vector2(
                (float)Math.Clamp(
                    gameplaySettings.LayoutProgressScaleX.Value,
                    YokkoGameplaySettings.MinimumLayoutScale,
                    YokkoGameplaySettings.MaximumLayoutScale),
                (float)Math.Clamp(
                    gameplaySettings.LayoutProgressScaleY.Value,
                    YokkoGameplaySettings.MinimumLayoutScale,
                    YokkoGameplaySettings.MaximumLayoutScale)),
            new Vector2(
                (float)gameplaySettings.LayoutHudOffsetX.Value * DrawWidth,
                (float)gameplaySettings.LayoutHudOffsetY.Value * DrawHeight),
            new Vector2(
                (float)Math.Clamp(
                    gameplaySettings.LayoutHudScaleX.Value,
                    YokkoGameplaySettings.MinimumLayoutScale,
                    YokkoGameplaySettings.MaximumLayoutScale),
                (float)Math.Clamp(
                    gameplaySettings.LayoutHudScaleY.Value,
                    YokkoGameplaySettings.MinimumLayoutScale,
                    YokkoGameplaySettings.MaximumLayoutScale)));

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
        applySavedHudVisibility();
    }

    protected override bool OnScroll(ScrollEvent e)
    {
        if (layoutEditor?.IsSessionActive == true)
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
                e.ControlPressed,
                e.ShiftPressed))
        {
            return true;
        }

        return base.OnKeyDown(e);
    }

    internal bool HandleKeyDownInput(
        Key key,
        bool repeat,
        bool altPressed,
        bool controlPressed,
        bool shiftPressed = false)
    {
        if (retryTransitionInProgress)
            return true;

        if (layoutEditor?.IsTestingLayout == true)
        {
            if (!repeat
                && matchesShortcut(
                    ManiaShortcutAction.PauseOrBack,
                    key))
            {
                _ = returnToLayoutEditorFromTestAsync();
            }

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

        if (ReplayMode && key is Key.Left or Key.Right)
        {
            requestReplaySeek(key == Key.Left ? -1 : 1);
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

        if (ReplayMode && key == Key.Space)
        {
            _ = toggleReplayPlaybackAsync();
            return true;
        }

        if (isPaused)
            return pauseOverlay?.HandleKey(key) ?? true;

        if (key == Key.Tab && shiftPressed)
        {
            setFocusMode(!focusModeActive);
            return true;
        }

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
            if (!repeat)
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

    protected override void UpdateAfterChildren()
    {
        base.UpdateAfterChildren();

        if (focusModeActive)
            hideFocusModePresentation();
    }

    private void setFocusMode(bool active)
    {
        focusModeActive = active;
        playfield.SetFocusMode(active);

        if (active)
        {
            hideFocusModePresentation();
            return;
        }

        if (mods.IsCinema)
            return;

        hud.Alpha = 1;
        comboReadout.UpdateState(judgementState.Combo);
        if (playfield.UsesSkinJudgementOverlay)
            comboReadout.Alpha = 0;
        timingBar.Alpha = gameplaySettings.ShowTimingBar.Value ? 1 : 0;
        replayControls?.Show();
    }

    private void hideFocusModePresentation()
    {
        hud.Alpha = 0;
        comboReadout.Alpha = 0;
        judgementReadout.Alpha = 0;
        timingBar.Alpha = 0;
        scrollSpeedOverlay.Alpha = 0;
        playbackRateOverlay.Alpha = 0;
        if (replayControls != null)
            replayControls.Alpha = 0;
    }

    protected override void OnKeyUp(KeyUpEvent e)
    {
        if (!HandleKeyUpInput(e.Key))
            base.OnKeyUp(e);
    }

    protected override bool OnJoystickPress(JoystickPressEvent e) =>
        handleDevicePress(KeyCombination.FromJoystickButton(e.Button))
        || base.OnJoystickPress(e);

    protected override void OnJoystickRelease(JoystickReleaseEvent e)
    {
        if (!handleDeviceRelease(KeyCombination.FromJoystickButton(e.Button)))
            base.OnJoystickRelease(e);
    }

    protected override bool OnMidiDown(MidiDownEvent e) =>
        handleDevicePress(KeyCombination.FromMidiKey(e.Key))
        || base.OnMidiDown(e);

    protected override void OnMidiUp(MidiUpEvent e)
    {
        if (!handleDeviceRelease(KeyCombination.FromMidiKey(e.Key)))
            base.OnMidiUp(e);
    }

    private bool handleDevicePress(InputKey key)
    {
        int lane = keyBindings.GetLane(key);
        if (lane < 0)
            return false;

        if (retryTransitionInProgress
            || layoutEditor?.IsEditing == true
            || resumeCountdownInProgress
            || gameplayBlocked
            || gameplayFailed
            || isPaused
            || gameplayCompleted
            || ReplayMode)
        {
            return true;
        }

        applyLanePress(lane, currentGameplayTime);
        return true;
    }

    private bool handleDeviceRelease(InputKey key)
    {
        int lane = keyBindings.GetLane(key);
        if (lane < 0)
            return false;

        if (!gameplayCompleted
            && !gameplayFailed
            && !retryTransitionInProgress
            && !ReplayMode
            && !isPaused)
        {
            applyLaneRelease(lane, currentGameplayTime);
        }

        return true;
    }

    internal bool HandleKeyUpInput(Key key)
    {
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
            endInputCapture();
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
        if (isDisposing
            && Interlocked.Exchange(ref disposalStarted, 1) == 0)
        {
            gameplayLifetimeCancellation.Cancel();
            keysoundPreparationCancellation?.Cancel();
            CancellationTokenSource preparationCancellation =
                keysoundPreparationCancellation;
            keysoundPreparationCancellation = null;

            if (!ReplayMode)
            {
                disableRawKeysoundFastPath();
                endInputCapture();
            }

            host.Deactivated -= onHostDeactivated;
            audioSettings.MixChanged -= onAudioMixChanged;
            gameplaySettings.ScrollSpeed.ValueChanged -=
                onScrollSpeedChanged;
            if (skinSettings != null)
            {
                skinSettings.SelectedSkinId.ValueChanged -=
                    onSelectedSkinIdChanged;
            }
            if (!completionAudioStopRequested)
                mutedAudio?.Restore();
            stopAllSlidingSamples();
            _ = disposeAudioResourcesAsync(
                audioEngine,
                keysoundPreparationTask,
                preparationCancellation,
                gameplayLifetimeCancellation);
            maniaSkinLease?.Dispose();
            artworkTextures?.Dispose();
        }

        base.Dispose(isDisposing);
    }

    private void onAudioMixChanged()
    {
        if (gameplayCompletionTransitionActive
            || gameplayCompleted
            || audioEngine is not IAudioMixControl mixControl)
        {
            return;
        }

        if (mutedAudio != null)
        {
            mutedAudio.SetOutputVolumes(
                audioSettings.EffectiveMusicVolume,
                gameplaySettings.KeysoundsEnabled.Value
                    ? audioSettings.EffectiveHitSoundVolume
                    : 0,
                audioSettings.EffectiveMasterVolume);
        }
        else
        {
            audioSettings.ApplyMixSettings(
                mixControl,
                gameplaySettings.KeysoundsEnabled.Value);
        }
    }

    private async Task startAudioAsync()
    {
        CancellationToken lifetimeToken = gameplayLifetimeCancellation.Token;

        try
        {
            await waitForLatestKeysoundPreparationAsync(lifetimeToken)
                .ConfigureAwait(true);
            lifetimeToken.ThrowIfCancellationRequested();
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

            await audioEngine.StartAsync(
                                 startRequest,
                                 lifetimeToken)
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

            if (ReplayMode
                && double.IsFinite(pendingReplaySeekTarget))
            {
                await processReplaySeekRequestsAsync()
                    .ConfigureAwait(true);
                return;
            }

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
        catch (OperationCanceledException)
            when (lifetimeToken.IsCancellationRequested)
        {
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
        while (!gameplayFailed)
        {
            ulong previousLanes = replayTimeline.PressedLanes;
            if (!replayTimeline.MoveNext(
                    gameplayTime,
                    out GameplayReplayFrame frame))
            {
                break;
            }

            advanceReplayAdaptiveSpeedTo(
                Math.BitDecrement(frame.TimeMilliseconds));
            collectAndApplyPassiveJudgements(
                Math.BitDecrement(frame.TimeMilliseconds));
            if (gameplayFailed)
                break;

            advanceReplayAdaptiveSpeedTo(frame.TimeMilliseconds);
            ulong changedLanes = previousLanes ^ frame.PressedLanes;
            for (int lane = 0; lane < pressedLanes.Length; lane++)
            {
                ulong laneMask = 1UL << lane;
                if ((changedLanes & laneMask) == 0)
                    continue;

                if ((frame.PressedLanes & laneMask) != 0)
                    applyLanePress(lane, frame.TimeMilliseconds);
                else
                    applyLaneRelease(lane, frame.TimeMilliseconds);
            }
        }

        advanceReplayAdaptiveSpeedTo(gameplayTime);
    }

    private void advanceReplayAdaptiveSpeedTo(double gameplayTime)
    {
        if (adaptiveSpeedState != null
            && double.IsFinite(lastReplayAdaptiveSimulationTime)
            && gameplayTime > lastReplayAdaptiveSimulationTime)
        {
            adaptiveSpeedState.AdvanceByGameplayTime(
                gameplayTime - lastReplayAdaptiveSimulationTime);
        }

        lastReplayAdaptiveSimulationTime = gameplayTime;
    }

    private void collectAndApplyPassiveJudgements(double gameplayTime)
    {
        expiredJudgements.Clear();
        judgementState.CollectMineJudgements(
            gameplayTime,
            pressedLanes,
            expiredJudgements);
        judgementState.CollectExpiredMisses(
            gameplayTime,
            expiredJudgements);
        foreach (JudgementEvent judgement in expiredJudgements)
        {
            applyJudgement(judgement);
            if (gameplayFailed)
                break;
        }

        if (expiredJudgements.Count > 0 && !gameplayFailed)
            syncAllSlidingSamples();
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
        inputTimingEvents.Clear();
        judgementState.JudgeLanePress(
            lane,
            inputTime,
            inputJudgements,
            inputTimingEvents);
        foreach (JudgementEvent judgement in inputJudgements)
        {
            applyJudgement(judgement);
        }
        showInputTimings();
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
                     false,
                     sample.UseMusicBus
                         ? AudioSampleBus.Music
                         : AudioSampleBus.HitSound))
            .ToArray();
    }

    private void restartKeysoundPreparation()
    {
        CancellationTokenSource previousCancellation =
            keysoundPreparationCancellation;
        Task previousTask = keysoundPreparationTask;
        previousCancellation?.Cancel();
        if (previousCancellation != null)
        {
            _ = disposeCancellationAfterTaskAsync(
                previousCancellation,
                previousTask);
        }

        keysoundPreparationCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                gameplayLifetimeCancellation.Token);
        Interlocked.Increment(ref keysoundPreparationGeneration);
        keysoundPreparationTask = prepareKeysoundsAfterAsync(
            previousTask,
            keysoundPreparationCancellation.Token);
    }

    private async Task prepareKeysoundsAfterAsync(
        Task previousTask,
        CancellationToken cancellationToken)
    {
        try
        {
            await previousTask.ConfigureAwait(true);
        }
        catch
        {
        }

        cancellationToken.ThrowIfCancellationRequested();
        await prepareKeysoundsAsync(cancellationToken)
            .ConfigureAwait(true);
    }

    private async Task waitForLatestKeysoundPreparationAsync(
        CancellationToken cancellationToken)
    {
        while (true)
        {
            int generation = Volatile.Read(
                ref keysoundPreparationGeneration);
            Task preparationTask = keysoundPreparationTask;
            await preparationTask.ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            if (generation == Volatile.Read(
                    ref keysoundPreparationGeneration))
            {
                return;
            }
        }
    }

    private static async Task disposeCancellationAfterTaskAsync(
        CancellationTokenSource cancellation,
        Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private static async Task disposeAudioResourcesAsync(
        IAudioEngine engine,
        Task preparationTask,
        CancellationTokenSource preparationCancellation,
        CancellationTokenSource lifetimeCancellation)
    {
        try
        {
            await preparationTask.ConfigureAwait(false);
        }
        catch
        {
        }
        finally
        {
            try
            {
                await engine.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Logger.Error(
                    exception,
                    "The gameplay audio engine could not be disposed.",
                    LoggingTarget.Runtime);
            }
            finally
            {
                preparationCancellation?.Dispose();
                lifetimeCancellation.Dispose();
            }
        }
    }

    private async Task prepareKeysoundsAsync(
        CancellationToken cancellationToken)
    {
        if (audioEngine is not IAudioSamplePlayback samplePlayback)
            return;

        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<GameplayHitSamplePlaybackBinding> samplesToPrepare =
            scheduledSamples.Where(static sample =>
                sample.Bus == AudioSampleBus.Music);
        if (gameplaySettings.KeysoundsEnabled.Value)
        {
            samplesToPrepare = samplesToPrepare
                               .Concat(headSamplesByHitObject.SelectMany(
                                   static samples => samples))
                               .Concat(tailSamplesByHitObject.SelectMany(
                                   static samples => samples))
                               .Concat(slidingSamplesByHitObject.SelectMany(
                                   static samples => samples))
                               .Concat(scheduledSamples.Where(static sample =>
                                   sample.Bus == AudioSampleBus.HitSound));
        }

        string[] paths = samplesToPrepare
            .Select(static sample => sample.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (paths.Length == 0)
            return;

        try
        {
            await samplePlayback.PrepareSamplesAsync(
                                    paths,
                                    cancellationToken)
                                .ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            if (samplePlayback is IPreparedAudioSamplePlayback preparedPlayback)
                bindPreparedSampleHandles(preparedPlayback, paths);
            enableRawKeysoundFastPath();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
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
                && (scheduled.UseMusicBus
                    || gameplaySettings.KeysoundsEnabled.Value)
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
        inputTimingEvents.Clear();
        judgementState.JudgeLaneRelease(
            lane,
            inputTime,
            inputJudgements,
            inputTimingEvents);
        foreach (JudgementEvent judgement in inputJudgements)
        {
            applyJudgement(judgement);
        }
        showInputTimings();
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
        if (GameplayTimingStatistics.TryGetRealInputError(
                judgement,
                judgementState.Windows.SpeedMultiplier,
                out double realHitErrorMilliseconds))
        {
            resultHitErrors.Add(realHitErrorMilliseconds);
        }
        adaptiveSpeedState?.Apply(judgement);
        ManiaHealthUpdate healthUpdate = healthState.Apply(
            judgement,
            judgementState.Accuracy,
            judgementState.MaximumAchievableAccuracy);
        if (!playfield.UsesSkinJudgementOverlay
            && judgement.Phase != JudgementPhase.HoldBody
            // stable resolves an LN as one scorable Hold result; lazer's
            // non-scorable parent result should remain hidden.
            && (judgement.Phase != JudgementPhase.Hold
                || judgement.Rating.AffectsAccuracy())
            && (!isMine || judgement.IsMiss))
        {
            judgementReadout.Show(judgement);
        }

        if (healthUpdate.ExtraLifeConsumed)
            hud.ShowExtraLifeUsed();

        if (healthUpdate.Failed)
            failGameplay();
    }

    private void showInputTimings()
    {
        if (!gameplaySettings.ShowTimingBar.Value)
            return;

        foreach (JudgementInputEvent input in inputTimingEvents)
            timingBar.Show(input);
    }

    private void failGameplay()
    {
        if (layoutAutoplayDemoActive)
        {
            _ = returnToLayoutEditorFromTestAsync();
            return;
        }

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
        if (layoutAutoplayDemoActive)
        {
            _ = returnToLayoutEditorFromTestAsync();
            return;
        }

        if (gameplayCompleted)
            return;

        gameplayCompleted = true;
        gameplayCompletionTransitionActive = true;
        completionTransitionElapsedMilliseconds = 0;
        disableRawKeysoundFastPath();
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
                          ?? GameplayReplay.FromRecordedInputs(
                              recordedReplayInputs,
                              mods,
                              judgementConfiguration);
        DateTimeOffset completedAt = DateTimeOffset.UtcNow;
        GameplayTimingStatistics completedTiming =
            GameplayTimingStatistics.FromHitErrors(resultHitErrors);
        StoredGameplayScore previousBest = scoreStore.GetBest(
            originalBeatmap,
            mods,
            judgementConfiguration);
        string playerName = yokkoConfig.Get<string>(
            YokkoSetting.PlayerDisplayName);
        string playerId = yokkoConfig.Get<string>(YokkoSetting.PlayerId);
        if (!ReplayMode || developerAutoplayRun)
            saveCompletedReplay(completedAt);
        completedResultIsNewBest = BestScoreSaved =
            (!ReplayMode || developerAutoplayRun)
            && (!mods.IsAutomation || developerAutoplayRun)
            && !manualPlaybackRateUsed
            && scoreStore.SaveBest(
                originalBeatmap,
                mods,
                judgementConfiguration,
                completedResult,
                SavedReplayPath,
                completedAt,
                playerName,
                playerId,
                completedTiming);
        completedResultPresentation = new GameplayResultPresentation(
            playerName,
            playerId,
            completedAt,
            previousBest?.Score,
            !string.IsNullOrWhiteSpace(SavedReplayPath)
            && File.Exists(SavedReplayPath),
            completedTiming);

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
            judgementConfiguration,
            presentation: completedResultPresentation);
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

    private void saveCompletedReplay(DateTimeOffset recordedAt)
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
                imported?.Result.SourceHash,
                recordedAt);
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
        else if (!mods.Contains(ManiaModId.NoPause)
                 || pausesUsed < mods.NoPauseAllowedPauses)
            _ = pauseGameplayAsync();
        else
            diagnostics.Trace(
                "GAMEPLAY",
                "pause-blocked",
                $"No Pause allowance exhausted ({pausesUsed}/{mods.NoPauseAllowedPauses}).",
                LogLevel.Important);
    }

    internal void HandleHostDeactivated()
    {
        replayControls?.CancelSeekPreview();

        if (ReplayMode
            && replaySeekInProgress
            && gameplaySettings.PauseWhenUnfocused.Value)
        {
            replaySeekPauseRequested = true;
            return;
        }

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
            endInputCapture();

        AddInternal(pauseOverlay = createPauseOverlay());

        try
        {
            if (hasAudioClock)
                await audioEngine.PauseAsync().ConfigureAwait(true);
            pausesUsed++;
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
            await restoreLayoutEditorAfterTestAsync().ConfigureAwait(true);
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
                completionTimeMilliseconds,
                pausesUsed + 1),
            TogglePause,
            RetryGameplay,
            () => this.Push(new SettingsScreen()),
            exitPausedGameplay,
            openGameplayLayoutEditorFromPause,
            audioSettings);

    private void beginResumeCountdown()
    {
        if (!isPaused
            || pauseTransitionInProgress
            || resumeCountdownInProgress)
        {
            return;
        }

        pauseTransitionInProgress = true;
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
                beginInputCapture();
        }
        catch (Exception ex)
        {
            Logger.Error(
                ex,
                "The audio engine could not resume paused gameplay.",
                LoggingTarget.Runtime);
        }
    }

    private void cancelFailedPause()
    {
        isPaused = false;
        pauseOverlay?.Expire();
        pauseOverlay = null;

        if (!ReplayMode)
            beginInputCapture();
    }

    private void recoverLiveInputState(
        double gameplayTime,
        string reason)
    {
        endInputCapture();
        releasePressedLanesAt(gameplayTime);
        inputDropTracker.MarkBackendReset();

        if (!gameplayFailed
            && !gameplayCompleted
            && !gameplayBlocked
            && !retryTransitionInProgress
            && !isPaused)
        {
            rawKeysoundDispatcher?.RefreshAllAndEnable();
            beginInputCapture();
        }

        Logger.Log(
            $"Gameplay input state recovered: {reason}.",
            LoggingTarget.Runtime,
            LogLevel.Important);
    }

    private void beginQuickRetryHold()
    {
        if (QuickRetryHoldMilliseconds <= 0)
        {
            RetryGameplay();
            return;
        }

        if (double.IsNaN(quickRetryHoldStartTime))
            quickRetryHoldStartTime = Time.Current;
    }

    private void cancelQuickRetryHold() =>
        quickRetryHoldStartTime = double.NaN;

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

        if (Time.Current - quickRetryHoldStartTime
            < QuickRetryHoldMilliseconds)
        {
            return;
        }

        cancelQuickRetryHold();
        RetryGameplay();
    }

    private void beginInputCapture()
    {
        if (ReplayMode || inputCaptureActive)
            return;

        keyInputTimestamps.BeginCapture();
        inputCaptureActive = true;
    }

    private void endInputCapture()
    {
        if (!inputCaptureActive)
            return;

        keyInputTimestamps.EndCapture();
        inputCaptureActive = false;
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
        retryTransitionInProgress = true;
        findGameplaySessionRoot()?.BeginRetryTransition();
        diagnostics.Trace(
            "GAMEPLAY",
            "retry-requested",
            $"time={currentGameplayTime:0.###}ms | paused={isPaused} | failed={gameplayFailed}",
            LogLevel.Important);
        if (!ReplayMode)
            endInputCapture();

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
        GameplaySessionRootScreen sessionRoot = null;
        try
        {
            var replacement = new GameplayScreen(
                originalBeatmap,
                skinPath: skinPath,
                mods: mods,
                artworkPath: artworkPath,
                preparedArtworkTexture: preparedArtworkTexture);
            replacement.manualPlaybackRateAdjustment =
                manualPlaybackRateAdjustment;
            replacement.manualPlaybackRateUsed =
                Math.Abs(manualPlaybackRateAdjustment) > 0.000001;

            // Loading presentation does not start gameplay audio (that is
            // guarded by OnEntering), so overlap it with release of the old
            // endpoint instead of paying both waits serially.
            sessionRoot = findGameplaySessionRoot();
            sessionRoot?.PrepareGameplayReplacement(replacement);

            // A retry creates a fresh audio engine. Wait until this engine has
            // released its WASAPI endpoint before entering the replacement.
            if (completionAudioStopRequested)
                await completionAudioStopTask.ConfigureAwait(true);
            else
                await audioEngine.StopAsync().ConfigureAwait(true);

            if (!this.IsCurrentScreen())
            {
                sessionRoot?.CancelRetryTransition();
                return;
            }

            if (sessionRoot != null)
            {
                sessionRoot.CommitGameplayReplacement(replacement);
                return;
            }

            IScreen destination = this.GetParentScreen();
            while (destination is GameplayScreen)
                destination = destination.GetParentScreen();

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
            sessionRoot?.CancelRetryTransition();
            if (!ReplayMode && this.IsCurrentScreen())
                beginInputCapture();

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
            artworkPath,
            preparedArtworkTexture)
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

    private Texture loadArtworkTexture(IRenderer renderer)
    {
        if (preparedArtworkTexture != null)
            return preparedArtworkTexture;

        if (string.IsNullOrWhiteSpace(artworkPath))
            return null;

        try
        {
            artworkTextures = new TextureStore(
                renderer,
                new TextureLoaderStore(
                    new ConstrainedTextureResourceStore(
                        new ChartArtworkResourceStore(),
                        renderer.MaxTextureSize,
                        maximumPixelCount: 1920L * 1080)),
                scaleAdjust: 1);
            return artworkTextures.Get(artworkPath);
        }
        catch
        {
            artworkTextures?.Dispose();
            artworkTextures = null;
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
        if (LoadState != LoadState.Loaded
            || scrollSpeedOverlay?.LoadState != LoadState.Loaded)
        {
            appliedScrollSpeed = change.NewValue;
            return;
        }

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

    private double computeBaseApproachTime(
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

    private double computeApproachTime(
        double scrollSpeed,
        double playbackRate = 1,
        GameplayPlayfield targetPlayfield = null)
    {
        GameplayPlayfield target = targetPlayfield ?? playfield;
        return computeBaseApproachTime(scrollSpeed, playbackRate)
               * (target?.JudgementTravelScale ?? 1);
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

    private void adjustReplayPlaybackRate(double amount)
    {
        if (!ReplayMode
            || gameplayBlocked
            || gameplayCompleted
            || gameplayFailed)
        {
            return;
        }

        double gameplayTime = currentGameplayTime;
        double currentRate = currentPlaybackRate(gameplayTime);
        double adjustedRate = AdjustPlaybackRate(currentRate, amount);
        manualPlaybackRateAdjustment += adjustedRate - currentRate;
        if (Math.Abs(adjustedRate - currentRate) > 0.000001)
            manualPlaybackRateUsed = true;
        updatePlaybackRate(gameplayTime, true);
        updatePlaybackRateAdjustedApproachTime(gameplayTime, true);
        replayControls?.UpdateState(
            gameplayTime,
            completionTimeMilliseconds,
            adjustedRate,
            isPaused);
    }

    private void requestReplaySeek(double direction)
    {
        if (!ReplayMode
            || direction == 0
            || pauseTransitionInProgress
            || gameplayBlocked
            || gameplayCompleted
            || gameplayFailed
            || retryTransitionInProgress)
        {
            return;
        }

        double origin = double.IsFinite(pendingReplaySeekTarget)
            ? pendingReplaySeekTarget
            : currentGameplayTime;
        double step = replaySeekStepMilliseconds
                      * currentPlaybackRate(origin);
        requestReplaySeekTo(
            origin + Math.Sign(direction) * step);
    }

    private void requestReplaySeekTo(double targetGameplayTime)
    {
        if (!ReplayMode
            || !double.IsFinite(targetGameplayTime)
            || pauseTransitionInProgress
            || gameplayBlocked
            || gameplayCompleted
            || gameplayFailed
            || retryTransitionInProgress)
        {
            replayControls?.CompleteSeekPreview(
                isPaused ? pausedGameplayTime : currentGameplayTime);
            return;
        }

        pendingReplaySeekTarget = Math.Clamp(
            targetGameplayTime,
            0,
            completionTimeMilliseconds);
        if (hasAudioClock && !audioStarted)
            return;

        if (!replaySeekInProgress)
            _ = processReplaySeekRequestsAsync();
    }

    private async Task processReplaySeekRequestsAsync()
    {
        if (replaySeekInProgress)
            return;

        replaySeekInProgress = true;
        try
        {
            while (double.IsFinite(pendingReplaySeekTarget))
            {
                double target = pendingReplaySeekTarget;
                await seekReplayToAsync(target).ConfigureAwait(true);
                if (pendingReplaySeekTarget == target)
                    pendingReplaySeekTarget = double.NaN;
            }
        }
        catch (Exception exception)
        {
            pendingReplaySeekTarget = double.NaN;
            Logger.Error(
                exception,
                "Replay seek processing failed unexpectedly.",
                LoggingTarget.Runtime);
            replayControls?.CompleteSeekPreview(
                isPaused ? pausedGameplayTime : currentGameplayTime);
        }
        finally
        {
            replaySeekInProgress = false;
            replaySeekPauseRequested = false;
        }
    }

    private async Task seekReplayToAsync(double targetGameplayTime)
    {
        bool remainPaused = isPaused;
        double previousGameplayTime = currentGameplayTime;
        double previousAudioPosition = hasAudioClock
            ? remainPaused
                ? pausedAudioPosition
                : Math.Max(
                    0,
                    previousGameplayTime - activeUserOffsetMilliseconds)
            : 0;
        targetGameplayTime = Math.Clamp(
            targetGameplayTime,
            0,
            completionTimeMilliseconds);
        ReplaySeekRollbackState rollback = captureReplaySeekRollbackState();
        bool audioAvailable = hasAudioClock && audioStarted;
        isPaused = true;
        pausedGameplayTime = previousGameplayTime;
        try
        {
            stopAllSlidingSamples();
            if (audioAvailable)
                await audioEngine.PauseAsync().ConfigureAwait(true);

            JudgementWindows windows = rollback.JudgementState.Windows;
            GameplayReplayRestoredState restored = await Task.Run(() =>
                GameplayReplayStateRebuilder.Rebuild(
                    beatmap,
                    replay,
                    mods,
                    windows,
                    judgementConfiguration,
                    minesEnabled,
                    targetGameplayTime)).ConfigureAwait(true);
            bool shouldRemainPaused =
                remainPaused || replaySeekPauseRequested;
            if (restored.HealthState.HasFailed)
            {
                if (audioAvailable)
                {
                    await audioEngine.SeekAsync(previousAudioPosition)
                                     .ConfigureAwait(true);
                    if (shouldRemainPaused)
                        await audioEngine.PauseAsync().ConfigureAwait(true);
                }

                isPaused = shouldRemainPaused;
                if (!shouldRemainPaused)
                    syncAllSlidingSamples();
                diagnostics.Trace(
                    "REPLAY",
                    "seek-rejected",
                    $"target={targetGameplayTime:0.###}ms | reason=failed-state");
                replayControls?.CompleteSeekPreview(previousGameplayTime);
                return;
            }

            if (audioAvailable)
            {
                double targetAudioPosition = Math.Max(
                    0,
                    targetGameplayTime - activeUserOffsetMilliseconds);
                await audioEngine.SeekAsync(targetAudioPosition)
                                 .ConfigureAwait(true);
                shouldRemainPaused =
                    remainPaused || replaySeekPauseRequested;
                if (shouldRemainPaused)
                    await audioEngine.PauseAsync().ConfigureAwait(true);
                pausedAudioPosition = targetAudioPosition;
            }
            else
            {
                frameClockGameplayTime = targetGameplayTime;
                frameClockLastFrameworkTime = Time.Current;
                startTimeMilliseconds =
                    Time.Current
                    - targetGameplayTime
                      / currentPlaybackRate(targetGameplayTime);
                pausedAudioPosition = Math.Max(
                    0,
                    targetGameplayTime - activeUserOffsetMilliseconds);
            }

            applyReplayRestoredState(restored, targetGameplayTime);
            shouldRemainPaused = remainPaused || replaySeekPauseRequested;
            isPaused = shouldRemainPaused;
            if (!shouldRemainPaused)
                syncAllSlidingSamples();
            diagnostics.Trace(
                "REPLAY",
                "seeked",
                $"from={previousGameplayTime:0.###}ms"
                + $" | target={targetGameplayTime:0.###}ms"
                + $" | paused={shouldRemainPaused}");
        }
        catch (Exception exception)
        {
            pendingReplaySeekTarget = double.NaN;
            bool shouldRemainPaused =
                remainPaused || replaySeekPauseRequested;
            try
            {
                restoreReplaySeekRollbackState(
                    rollback,
                    previousGameplayTime);
                if (audioAvailable)
                {
                    await audioEngine.SeekAsync(previousAudioPosition)
                                     .ConfigureAwait(true);
                    if (shouldRemainPaused)
                        await audioEngine.PauseAsync().ConfigureAwait(true);
                }

                isPaused = shouldRemainPaused;
                if (!shouldRemainPaused)
                    syncAllSlidingSamples();
            }
            catch (Exception restoreException)
            {
                isPaused = true;
                Logger.Error(
                    restoreException,
                    "Replay playback could not restore state after a failed seek.",
                    LoggingTarget.Runtime);
            }

            Logger.Error(
                exception,
                "Replay playback could not seek.",
                LoggingTarget.Runtime);
        }
        finally
        {
            if (!double.IsFinite(pendingReplaySeekTarget)
                || pendingReplaySeekTarget == targetGameplayTime)
            {
                replayControls?.CompleteSeekPreview(
                    isPaused ? pausedGameplayTime : currentGameplayTime);
            }
            replayControls?.UpdateState(
                isPaused ? pausedGameplayTime : currentGameplayTime,
                completionTimeMilliseconds,
                currentPlaybackRate(
                    isPaused ? pausedGameplayTime : currentGameplayTime),
                isPaused);
        }
    }

    private ReplaySeekRollbackState captureReplaySeekRollbackState() => new(
        judgementState,
        healthState,
        adaptiveSpeedState,
        replayTimeline,
        keysoundSelector,
        (bool[])pressedLanes.Clone(),
        nextScheduledSampleIndex,
        previousScheduledSampleTime,
        pausedGameplayTime,
        pausedAudioPosition,
        lastStableAudioGameplayTime,
        lastAppliedPlaybackRate,
        lastApproachPlaybackRate,
        lastReplayAdaptiveSimulationTime,
        frameClockGameplayTime,
        frameClockLastFrameworkTime,
        startTimeMilliseconds);

    private void restoreReplaySeekRollbackState(
        ReplaySeekRollbackState rollback,
        double gameplayTime)
    {
        judgementState = rollback.JudgementState;
        healthState = rollback.HealthState;
        adaptiveSpeedState = rollback.AdaptiveSpeedState;
        replayTimeline = rollback.Timeline;
        keysoundSelector = rollback.KeysoundSelector;
        Array.Copy(
            rollback.PressedLanes,
            pressedLanes,
            pressedLanes.Length);
        nextScheduledSampleIndex = rollback.NextScheduledSampleIndex;
        previousScheduledSampleTime = rollback.PreviousScheduledSampleTime;
        pausedGameplayTime = rollback.PausedGameplayTime;
        pausedAudioPosition = rollback.PausedAudioPosition;
        lastStableAudioGameplayTime = rollback.LastStableAudioGameplayTime;
        lastAppliedPlaybackRate = rollback.LastAppliedPlaybackRate;
        lastApproachPlaybackRate = rollback.LastApproachPlaybackRate;
        lastReplayAdaptiveSimulationTime =
            rollback.LastReplayAdaptiveSimulationTime;
        frameClockGameplayTime = rollback.FrameClockGameplayTime;
        frameClockLastFrameworkTime = rollback.FrameClockLastFrameworkTime;
        startTimeMilliseconds = rollback.StartTimeMilliseconds;
        refreshReplayPresentation(gameplayTime);
    }

    private void applyReplayRestoredState(
        GameplayReplayRestoredState restored,
        double targetGameplayTime)
    {
        judgementState = restored.JudgementState;
        healthState = restored.HealthState;
        adaptiveSpeedState = restored.AdaptiveSpeedState;
        replayTimeline = restored.Timeline;
        keysoundSelector = new GameplayKeysoundSelector(
            beatmap,
            judgementState);
        Array.Copy(
            restored.PressedLanes,
            pressedLanes,
            pressedLanes.Length);
        expiredJudgements.Clear();
        inputJudgements.Clear();
        resetScheduledSampleCursor(targetGameplayTime);
        pausedGameplayTime = targetGameplayTime;
        lastStableAudioGameplayTime = targetGameplayTime;
        lastAppliedPlaybackRate = double.NaN;
        lastApproachPlaybackRate = double.NaN;
        lastReplayAdaptiveSimulationTime = targetGameplayTime;
        refreshReplayPresentation(targetGameplayTime);
    }

    private void refreshReplayPresentation(double gameplayTime)
    {
        timingBar.Clear();
        judgementReadout.Clear();
        comboReadout.UpdateState(judgementState.Combo);
        for (int lane = 0; lane < pressedLanes.Length; lane++)
            playfield.SetLanePressed(lane, pressedLanes[lane]);
        playfield.SetApproachTime(computeApproachTime(
            gameplaySettings.ScrollSpeed.Value,
            currentPlaybackRate(gameplayTime)));
        playfield.ResetForReplaySeek(
            gameplayTime,
            judgementState,
            healthState);
        hud.UpdateState(gameplayTime, judgementState, healthState);
    }

    private sealed record ReplaySeekRollbackState(
        BeatmapJudgementState JudgementState,
        ManiaHealthState HealthState,
        ManiaAdaptiveSpeedState AdaptiveSpeedState,
        GameplayReplayTimeline Timeline,
        GameplayKeysoundSelector KeysoundSelector,
        bool[] PressedLanes,
        int NextScheduledSampleIndex,
        double PreviousScheduledSampleTime,
        double PausedGameplayTime,
        double PausedAudioPosition,
        double LastStableAudioGameplayTime,
        double LastAppliedPlaybackRate,
        double LastApproachPlaybackRate,
        double LastReplayAdaptiveSimulationTime,
        double FrameClockGameplayTime,
        double FrameClockLastFrameworkTime,
        double StartTimeMilliseconds);

    private sealed record LayoutAutoplayRollbackState(
        GameplayReplay Replay,
        GameplayReplayTimeline Timeline,
        GameplayReplay BaselineReplay,
        ReplaySeekRollbackState Timing,
        double[] ResultHitErrors,
        int PausesUsed);

    private void resetScheduledSampleCursor(double gameplayTime)
    {
        nextScheduledSampleIndex = 0;
        while (nextScheduledSampleIndex < beatmap.ScheduledSamples.Count
               && beatmap.ScheduledSamples[nextScheduledSampleIndex]
                         .TimeMilliseconds <= gameplayTime)
        {
            nextScheduledSampleIndex++;
        }

        previousScheduledSampleTime = gameplayTime;
    }

    private async Task toggleReplayPlaybackAsync()
    {
        if (!ReplayMode
            || pauseTransitionInProgress
            || gameplayBlocked
            || gameplayCompleted
            || gameplayFailed
            || retryTransitionInProgress
            || replaySeekInProgress)
        {
            return;
        }

        pauseTransitionInProgress = true;
        bool wasPaused = isPaused;
        try
        {
            if (!isPaused)
            {
                GameplayClockObservation observation =
                    observeGameplayClock();
                pausedGameplayTime = observation.GameplayTime;
                pausedAudioPosition = hasAudioClock
                    ? Math.Max(
                        0,
                        observation.Audio.PlaybackTimeMilliseconds)
                    : 0;
                isPaused = true;
                stopAllSlidingSamples();
                if (hasAudioClock && audioStarted)
                    await audioEngine.PauseAsync().ConfigureAwait(true);
                diagnostics.Trace(
                    "REPLAY",
                    "paused",
                    $"gameplay={pausedGameplayTime:0.###}ms");
            }
            else
            {
                if (hasAudioClock && audioStarted)
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
                syncAllSlidingSamples();
                diagnostics.Trace(
                    "REPLAY",
                    "resumed",
                    $"gameplay={pausedGameplayTime:0.###}ms");
            }
        }
        catch (Exception exception)
        {
            isPaused = wasPaused;
            try
            {
                if (wasPaused)
                    stopAllSlidingSamples();
                else
                    syncAllSlidingSamples();
            }
            catch (Exception sampleException)
            {
                Logger.Error(
                    sampleException,
                    "Replay sliding samples could not restore their pause state.",
                    LoggingTarget.Runtime);
            }
            Logger.Error(
                exception,
                "Replay playback could not change pause state.",
                LoggingTarget.Runtime);
        }
        finally
        {
            pauseTransitionInProgress = false;
            replayControls?.UpdateState(
                currentGameplayTime,
                completionTimeMilliseconds,
                currentPlaybackRate(currentGameplayTime),
                isPaused);
        }
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
            computeBaseApproachTime(
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
            gameplaySettings.ScrollDirection.Value,
            judgementConfiguration)
        {
            Anchor = Anchor.BottomCentre,
            Origin = Anchor.BottomCentre,
            Scale = Vector2.One,
        };

        result.SetJudgementLineOffset(
            gameplaySettings.LayoutJudgementLineOffsetY.Value);

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
            targetPlayfield.HasSkinHealthBar,
            maniaSkin,
            firstObjectTimeMilliseconds,
            completionTimeMilliseconds)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Position = new Vector2(-20, 20),
        };

    private void applySavedHudVisibility()
    {
        if (mods.IsCinema)
            return;

        bool playfieldVisible =
            gameplaySettings.LayoutPlayfieldVisible.Value >= 0.5;
        bool accuracyVisible =
            gameplaySettings.LayoutAccuracyVisible.Value >= 0.5;
        bool progressVisible =
            gameplaySettings.LayoutProgressVisible.Value >= 0.5;
        bool informationVisible =
            gameplaySettings.LayoutInformationVisible.Value >= 0.5;
        bool timingBarVisible =
            gameplaySettings.LayoutTimingBarVisible.Value >= 0.5;
        bool comboVisible =
            gameplaySettings.LayoutComboVisible.Value >= 0.5;
        bool judgementVisible =
            gameplaySettings.LayoutJudgementVisible.Value >= 0.5;
        bool hitEffectsVisible =
            gameplaySettings.LayoutHitEffectsVisible.Value >= 0.5;

        playfield.SetJudgementLineOffset(
            gameplaySettings.LayoutJudgementLineOffsetY.Value);

        playfield.Alpha = playfieldVisible ? 1 : 0;
        hud.AccuracyLayoutDrawable.Alpha = accuracyVisible ? 1 : 0;
        hud.ProgressLayoutDrawable.Alpha = progressVisible ? 1 : 0;
        hud.InformationLayoutDrawable.Alpha = informationVisible ? 1 : 0;
        timingBar.Alpha = timingBarVisible
                          && gameplaySettings.ShowTimingBar.Value
            ? 1
            : 0;
        playfield.SetSkinComboVisible(comboVisible);
        playfield.SetSkinJudgementVisible(judgementVisible);
        playfield.SetHitEffectsVisible(hitEffectsVisible);
        comboReadout.Alpha = comboVisible
                             && !playfield.UsesSkinJudgementOverlay
            ? 1
            : 0;
        judgementReadout.Alpha = judgementVisible
                                 && !playfield.UsesSkinJudgementOverlay
            ? 1
            : 0;
    }

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
            () => skinSettings?.LongNoteCutEnabled.Value
                  ?? YokkoSkinSettings.DefaultLongNoteCutEnabled,
            setLayoutEditorLongNoteCutEnabled,
            () => skinSettings?.LongNoteCutAmount.Value
                  ?? YokkoSkinSettings.DefaultLongNoteCutAmount,
            setLayoutEditorLongNoteCutAmount,
            () => gameplaySettings
                .JudgementDisplayDurationMilliseconds.Value,
            setLayoutEditorJudgementDisplayDuration,
            () => gameplaySettings.JudgementOpacity.Value,
            setLayoutEditorJudgementOpacity,
            () => gameplaySettings.JudgementHitErrorScale.Value,
            setLayoutEditorJudgementHitErrorScale,
            () => gameplaySettings.ShowJudgementHitError.Value,
            setLayoutEditorShowJudgementHitError,
            () => gameplaySettings.ShowTimingBar.Value,
            setLayoutEditorShowTimingBar);

    private void setLayoutEditorLongNoteCutEnabled(bool value)
    {
        if (skinSettings == null)
            return;

        skinSettings.LongNoteCutEnabled.Value = value;
        applyLayoutEditorLongNoteCut();
    }

    private void setLayoutEditorLongNoteCutAmount(double value)
    {
        if (skinSettings == null)
            return;

        double step = YokkoSkinSettings.LongNoteCutAmountStep;
        skinSettings.LongNoteCutAmount.Value = Math.Clamp(
            Math.Round(value / step) * step,
            YokkoSkinSettings.MinimumLongNoteCutAmount,
            YokkoSkinSettings.MaximumLongNoteCutAmount);
        applyLayoutEditorLongNoteCut();
    }

    private void applyLayoutEditorLongNoteCut()
    {
        double amount = skinSettings?.LongNoteCutEnabled.Value == true
            ? Math.Clamp(
                skinSettings.LongNoteCutAmount.Value,
                YokkoSkinSettings.MinimumLongNoteCutAmount,
                YokkoSkinSettings.MaximumLongNoteCutAmount)
            : 0;
        playfield.SetLongNoteCutAmount(amount);
    }

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

    private void setLayoutEditorJudgementHitErrorScale(double value)
    {
        gameplaySettings.SetJudgementHitErrorScale(value);
        judgementReadout.SetHitErrorScale(
            gameplaySettings.JudgementHitErrorScale.Value);
    }

    private void setLayoutEditorShowJudgementHitError(bool value)
    {
        gameplaySettings.ShowJudgementHitError.Value = value;
        judgementReadout.SetShowHitError(value);
    }

    private void setLayoutEditorShowTimingBar(bool value)
    {
        gameplaySettings.ShowTimingBar.Value = value;
        applySavedHudVisibility();
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

        applySelectedSkinIfChanged();
    }

    private void applySelectedSkinIfChanged()
    {
        string selectedSkinId = skinSettings?.SelectedSkinId.Value
                                ?? string.Empty;
        if (string.Equals(
                appliedSelectedSkinId,
                selectedSkinId,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        appliedSelectedSkinId = selectedSkinId;
        rebuildGameplayPresentation(reloadSkin: true);

        // A mania skin can also provide hitsounds. The visual tree and sample
        // bindings must therefore move to the new skin together while input is
        // still suspended by the pause state.
        keyInputTimestamps.SetRawInputFastPathSink(null);
        rawKeysoundDispatcher?.Disable();
        rawKeysoundDispatcher = null;
        prepareHitSamples();
        restartKeysoundPreparation();
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
        nextPlayfield.SetFocusMode(focusModeActive);
        for (int lane = 0; lane < pressedLanes.Length; lane++)
            nextPlayfield.SetLanePressed(lane, pressedLanes[lane]);

        double gameplayTime = isPaused
            ? pausedGameplayTime
            : currentGameplayTime;
        nextPlayfield.SetApproachTime(computeApproachTime(
            gameplaySettings.ScrollSpeed.Value,
            currentPlaybackRate(gameplayTime),
            nextPlayfield));
        if (layoutAutoplayDemoActive)
            nextPlayfield.SetLayoutAutoplayDemo(true, gameplayTime);
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
        applySavedHudVisibility();
        layoutEditor?.ReplaceTargets(nextPlayfield, nextHud, reloadSkin);
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

    internal void BeginLayoutAutoplayDemoForTest() =>
        beginLayoutAutoplayDemo();

    internal void RestartKeysoundPreparationForTest() =>
        restartKeysoundPreparation();

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

    internal bool LayoutEditorLongNoteCutEnabledForTest =>
        skinSettings?.LongNoteCutEnabled.Value == true;

    internal double LayoutEditorLongNoteCutAmountForTest =>
        skinSettings?.LongNoteCutAmount.Value
        ?? YokkoSkinSettings.DefaultLongNoteCutAmount;

    internal double AppliedLongNoteCutAmountForTest =>
        playfield.LongNoteCutAmount;

    internal int LayoutAutoplayDemoLongNoteCountForTest =>
        playfield.LayoutAutoplayDemoLongNoteCount;

    internal int VisibleLayoutAutoplayDemoLongNoteCountForTest =>
        playfield.VisibleLayoutAutoplayDemoLongNoteCount;

    internal float LayoutAutoplayDemoLongNoteCutDistanceForTest =>
        playfield.LayoutAutoplayDemoLongNoteCutDistance;

    internal void SetLayoutEditorLongNoteCutEnabledForTest(bool enabled) =>
        setLayoutEditorLongNoteCutEnabled(enabled);

    internal void SetLayoutEditorLongNoteCutAmountForTest(double amount) =>
        setLayoutEditorLongNoteCutAmount(amount);

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

    internal void SetLayoutEditorHitErrorScaleForTest(double value) =>
        setLayoutEditorJudgementHitErrorScale(value);

    internal void SetLayoutEditorShowHitErrorForTest(bool value) =>
        setLayoutEditorShowJudgementHitError(value);

    internal void SetLayoutEditorShowTimingBarForTest(bool value) =>
        setLayoutEditorShowTimingBar(value);

    internal double LayoutEditorJudgementDurationForTest =>
        judgementReadout.DisplayDurationForTest;

    internal float LayoutEditorJudgementOpacityForTest =>
        judgementReadout.ContentOpacityForTest;

    internal float LayoutEditorHitErrorScaleForTest =>
        judgementReadout.HitErrorScaleForTest;

    internal bool LayoutEditorShowsHitErrorForTest =>
        judgementReadout.ShowsHitErrorForTest;

    private void beginLayoutTestPlay()
    {
        beginLayoutPreview(false);
    }

    private void beginLayoutAutoplayDemo()
    {
        beginLayoutPreview(true);
    }

    private void beginLayoutPreview(bool autoplay)
    {
        if (layoutEditor?.IsEditing != true
            || !isPaused
            || pauseTransitionInProgress
            || resumeCountdownInProgress)
        {
            return;
        }

        if (autoplay)
        {
            beginLayoutAutoplayReplay();
            layoutEditor.BeginAutoplayDemo();
        }
        else
            layoutEditor.BeginTestPlay();

        beginResumeCountdown();
    }

    private void beginLayoutAutoplayReplay()
    {
        GameplayReplay baselineReplay = replay
            ?? GameplayReplay.FromRecordedInputs(
                recordedReplayInputs,
                mods,
                judgementConfiguration);
        ReplaySeekRollbackState timing = captureReplaySeekRollbackState();
        layoutAutoplayRollback = new LayoutAutoplayRollbackState(
            replay,
            replayTimeline,
            baselineReplay,
            timing,
            resultHitErrors.ToArray(),
            pausesUsed);
        replay = GameplayAutoGenerator.Generate(
            beatmap,
            mods,
            judgementConfiguration);
        replayTimeline = new GameplayReplayTimeline(replay.Frames);
        replayTimeline.Seek(Math.BitDecrement(pausedGameplayTime));
        lastReplayAdaptiveSimulationTime = pausedGameplayTime;

        layoutAutoplayDemoActive = true;
        playfield.SetLayoutAutoplayDemo(true, pausedGameplayTime);
    }

    private async Task endLayoutAutoplayReplayAsync()
    {
        if (!layoutAutoplayDemoActive)
            return;

        LayoutAutoplayRollbackState rollback = layoutAutoplayRollback;
        if (rollback == null)
            throw new InvalidOperationException(
                "The layout autoplay rollback state is missing.");

        stopAllSlidingSamples();
        GameplayReplayRestoredState restored = await Task.Run(() =>
            GameplayReplayStateRebuilder.Rebuild(
                beatmap,
                rollback.BaselineReplay,
                mods,
                rollback.Timing.JudgementState.Windows,
                judgementConfiguration,
                minesEnabled,
                rollback.Timing.PausedGameplayTime)).ConfigureAwait(true);

        if (hasAudioClock && audioStarted)
        {
            await audioEngine.SeekAsync(
                rollback.Timing.PausedAudioPosition).ConfigureAwait(true);
            await audioEngine.PauseAsync().ConfigureAwait(true);
        }

        applyReplayRestoredState(
            restored,
            rollback.Timing.PausedGameplayTime);
        replay = rollback.Replay;
        replayTimeline = rollback.Timeline;
        nextScheduledSampleIndex = rollback.Timing.NextScheduledSampleIndex;
        previousScheduledSampleTime =
            rollback.Timing.PreviousScheduledSampleTime;
        pausedGameplayTime = rollback.Timing.PausedGameplayTime;
        pausedAudioPosition = rollback.Timing.PausedAudioPosition;
        lastStableAudioGameplayTime =
            rollback.Timing.LastStableAudioGameplayTime;
        lastAppliedPlaybackRate = rollback.Timing.LastAppliedPlaybackRate;
        lastApproachPlaybackRate = rollback.Timing.LastApproachPlaybackRate;
        lastReplayAdaptiveSimulationTime =
            rollback.Timing.LastReplayAdaptiveSimulationTime;
        frameClockGameplayTime = rollback.Timing.FrameClockGameplayTime;
        frameClockLastFrameworkTime = rollback.Timing.FrameClockLastFrameworkTime;
        startTimeMilliseconds = rollback.Timing.StartTimeMilliseconds;
        resultHitErrors.Clear();
        resultHitErrors.AddRange(rollback.ResultHitErrors);
        pausesUsed = rollback.PausesUsed;
        playfield.SetLayoutAutoplayDemo(
            false,
            rollback.Timing.PausedGameplayTime);
        layoutAutoplayRollback = null;
        layoutAutoplayDemoActive = false;
    }

    private async Task returnToLayoutEditorFromTestAsync()
    {
        if (layoutEditor?.IsTestingLayout != true
            || layoutPreviewReturnInProgress)
        {
            return;
        }

        layoutPreviewReturnInProgress = true;
        try
        {
            if (resumeCountdownInProgress)
            {
                cancelResumeCountdown();
                await restoreLayoutEditorAfterTestAsync().ConfigureAwait(true);
                return;
            }

            if (!isPaused)
                await pauseGameplayAsync().ConfigureAwait(true);
            else
                await restoreLayoutEditorAfterTestAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            isPaused = true;
            Logger.Error(
                exception,
                "The layout preview could not restore its entry state.",
                LoggingTarget.Runtime);
        }
        finally
        {
            layoutPreviewReturnInProgress = false;
        }
    }

    private async Task restoreLayoutEditorAfterTestAsync()
    {
        if (layoutEditor?.IsTestingLayout != true || !isPaused)
            return;

        if (pauseOverlay != null)
            pauseOverlay.Alpha = 0;
        await endLayoutAutoplayReplayAsync().ConfigureAwait(true);
        applySelectedSkinIfChanged();
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

        if (focusModeActive)
            setFocusMode(false);

        skinHudLayoutStore.BeginEditSession();
        layoutEditor.SetEditing(true);
    }

    private void closeGameplayLayoutEditor()
    {
        skinHudLayoutStore.CancelEditSession();
        applySelectedSkinIfChanged();
        layoutEditor?.SetEditing(false);

        if (pauseOverlay != null)
            pauseOverlay.Alpha = 1;
    }

    private void saveGameplayLayout()
    {
        skinHudLayoutStore.CommitEditSession();
        skinHudLayoutStore.Flush();
        yokkoConfig.Save();
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
