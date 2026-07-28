using System;
using System.Diagnostics;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Logging;
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
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Screens.Gameplay;

public partial class GameplayScreen : Screen
{
    private const double leadInMilliseconds = 900;

    private readonly YokkoBeatmap beatmap;
    private IAudioEngine audioEngine;
    private readonly string skinPath;
    [Resolved]
    private YokkoAudioSettings audioSettings { get; set; }
    [Resolved]
    private OsuManiaSkinLibrary skinLibrary { get; set; }
    [Resolved]
    private YokkoGameplaySettings gameplaySettings { get; set; }
    [Resolved]
    private KeyInputTimestampSource keyInputTimestamps { get; set; }

    private BeatmapJudgementState judgementState;
    private GameplayPlayfield playfield;
    private KeyModeBindings keyBindings;
    private bool[] pressedLanes;
    private double startTimeMilliseconds;
    private bool hasAudioClock;
    private OsuManiaSkin maniaSkin;
    private double activeUserOffsetMilliseconds;
    private readonly InputAgeTracker inputAgeTracker = new();

    public GameplayScreen(
        YokkoBeatmap beatmap,
        IAudioEngine audioEngine = null,
        string skinPath = null)
    {
        this.beatmap = beatmap;
        this.audioEngine = audioEngine;
        this.skinPath = skinPath;
    }

    [BackgroundDependencyLoader]
    private void load(IRenderer renderer)
    {
        keyBindings = KeyModeBindings.ForMode(
            beatmap.KeyMode,
            gameplaySettings.GetKeys(beatmap.KeyMode));
        pressedLanes = new bool[keyBindings.KeyCount];
        judgementState = new BeatmapJudgementState(beatmap, JudgementWindows.DefaultMania);
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
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                // Legacy osu!mania values already use the skin's 480px
                // coordinate space. Keep the skin at native scale and let the
                // shared display container handle window/DPI scaling.
                Scale = Vector2.One,
            },
        };

        gameplaySettings.ScrollSpeed.BindValueChanged(
            onScrollSpeedChanged,
            true);
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        keyInputTimestamps.BeginCapture();
        startTimeMilliseconds = Time.Current + leadInMilliseconds;

        if (hasAudioClock)
            _ = startAudioAsync();
    }

    protected override void Update()
    {
        base.Update();
        drainRawInput();

        double gameplayTime = currentGameplayTime;

        foreach (JudgementEvent missed in judgementState.CollectExpiredMisses(gameplayTime))
            applyJudgement(missed);

        playfield.UpdateGameplayTime(gameplayTime, judgementState);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (HandleScrollSpeedShortcut(e.Key, e.ControlPressed))
            return true;

        if (e.Key == Key.Escape)
        {
            _ = audioEngine.StopAsync();
            this.Exit();
            return true;
        }

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
        keyInputTimestamps.EndCapture();
        _ = audioEngine.StopAsync();
        return base.OnExiting(e);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
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

            await audioEngine.StartAsync(
                audioSettings.CreateStartRequest(beatmap.AudioPath))
                             .ConfigureAwait(true);

            if (!audioEngine.Status.IsRunning)
            {
                hasAudioClock = false;
            }
        }
        catch (Exception ex)
        {
            hasAudioClock = false;
            Debug.WriteLine($"Audio unavailable: {ex.Message}");
        }
    }

    private double currentGameplayTime
    {
        get
        {
            if (hasAudioClock && audioEngine.Status.IsRunning)
                return audioEngine.PlaybackTimeMilliseconds
                       + activeUserOffsetMilliseconds;

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

    private void applyLanePress(int lane, double inputTime)
    {
        if (pressedLanes[lane])
            return;

        pressedLanes[lane] = true;
        playfield.SetLanePressed(lane, true);

        JudgementEvent judgement =
            judgementState.TryJudgeLanePress(lane, inputTime);
        if (judgement != null)
            applyJudgement(judgement);
    }

    private void applyLaneRelease(int lane, double inputTime)
    {
        if (!pressedLanes[lane])
            return;

        pressedLanes[lane] = false;
        playfield.SetLanePressed(lane, false);

        JudgementEvent judgement =
            judgementState.TryJudgeLaneRelease(lane, inputTime);
        if (judgement != null)
            applyJudgement(judgement);
    }

    private void applyJudgement(JudgementEvent judgement)
    {
        playfield.ApplyJudgement(judgement);
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
        catch
        {
            // Invalid skins fail closed to Yokko's built-in playfield.
        }
    }
}
