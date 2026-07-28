using System.Linq;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Screens;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Core.Beatmaps;
using Yokko.Core.Scoring;
using Yokko.Audio;
using Yokko.Game.Audio;
using Yokko.Game.Gameplay;
using Yokko.Game.Input;
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Screens.Gameplay;

public partial class GameplayScreen : Screen
{
    private const float designedWidth = 1040;
    private const float designedHeight = 760;
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
    private GameplayHud hud;
    private GameplayPlayfield playfield;
    private JudgementReadout judgementReadout;
    private KeyModeBindings keyBindings;
    private bool[] pressedLanes;
    private double startTimeMilliseconds;
    private bool hasAudioClock;
    private SpriteText clockStatusText;
    private SpriteText scrollSpeedText;
    private OsuManiaSkin maniaSkin;
    private string skinStatusText;
    private double activeUserOffsetMilliseconds;

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
            new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(designedWidth, designedHeight),
                Children = new Drawable[]
                {
                    new Box
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Width = 980,
                        Height = 720,
                        Colour = new Color4(0.045f, 0.058f, 0.078f, 0.86f),
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
                        Y = 32,
                        // Legacy osu!mania values already use the skin's
                        // 480px coordinate space. A second skin-only scale
                        // makes wide and circle skins visibly oversized.
                        Scale = Vector2.One,
                    },
                    hud = new GameplayHud(beatmap)
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Position = new Vector2(-42, 42),
                        Alpha = gameplaySettings.ShowGameplayHud.Value ? 1 : 0,
                    },
                    judgementReadout = new JudgementReadout(
                        gameplaySettings.ShowHitError.Value)
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Y = -62,
                    },
                    clockStatusText = new SpriteText
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Y = -30,
                        Text = statusText(
                            hasAudioClock ? "Audio clock active. Esc returns." : "Press mapped keys. Esc returns."),
                        Font = FontUsage.Default.With(size: 18),
                        Colour = YokkoPalette.TextDim,
                    },
                    scrollSpeedText = new SpriteText
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        Position = new Vector2(42, 42),
                        Font = FontUsage.Default.With(size: 22),
                        Colour = YokkoPalette.Cyan,
                        Alpha = 0,
                    },
                }
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

        double gameplayTime = currentGameplayTime;

        foreach (JudgementEvent missed in judgementState.CollectExpiredMisses(gameplayTime))
            applyJudgement(missed);

        playfield.UpdateGameplayTime(gameplayTime, judgementState);
        hud.UpdateState(gameplayTime, judgementState);
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

        double inputTime = gameplayTimeForInput(e.Key, true);
        pressedLanes[lane] = true;
        playfield.SetLanePressed(lane, true);

        JudgementEvent judgement = judgementState.TryJudgeLanePress(lane, inputTime);

        if (judgement != null)
            applyJudgement(judgement);

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

        double inputTime = gameplayTimeForInput(e.Key, false);
        pressedLanes[lane] = false;
        playfield.SetLanePressed(lane, false);

        JudgementEvent judgement = judgementState.TryJudgeLaneRelease(lane, inputTime);

        if (judgement != null)
            applyJudgement(judgement);
    }

    public override bool OnExiting(ScreenExitEvent e)
    {
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
                clockStatusText.Text = statusText("Native audio unavailable. Renderer clock active.");
            }
            else
            {
                AudioEngineStatus status = audioEngine.Status;
                clockStatusText.Text = statusText(
                    $"{status.ActiveBackend} · {status.BufferSize} frames · "
                    + $"{status.EstimatedOutputLatencyMilliseconds:F2} ms");
            }
        }
        catch (Exception ex)
        {
            hasAudioClock = false;
            clockStatusText.Text = statusText($"Audio unavailable: {ex.Message}");
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
            keyInputTimestamps.TryTake(key, isPressed, out long eventTimestamp);
        long observationStart = Stopwatch.GetTimestamp();
        double gameplayTime = currentGameplayTime;
        long observationEnd = Stopwatch.GetTimestamp();

        if (!hasEventTimestamp)
            return gameplayTime;

        long observationTimestamp =
            observationStart + (observationEnd - observationStart) / 2;
        return GameplayInputClock.AtEventTimestamp(
            gameplayTime,
            eventTimestamp,
            observationTimestamp);
    }

    private void applyJudgement(JudgementEvent judgement)
    {
        playfield.ApplyJudgement(judgement);
        judgementReadout.Show(judgement);
    }

    private void onScrollSpeedChanged(
        osu.Framework.Bindables.ValueChangedEvent<double> change)
    {
        playfield.SetApproachTime(
            OsuManiaScrollSpeed.ComputeScrollTime(change.NewValue));
        scrollSpeedText.Text = YokkoStrings.Get(
            "gameplay.scroll_speed_status",
            change.NewValue);
        scrollSpeedText.FinishTransforms();
        scrollSpeedText.Alpha = 1;
        scrollSpeedText.Delay(900).FadeOut(350, Easing.OutQuint);
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
            string name = maniaSkin.Info.Name == "Unknown"
                ? System.IO.Path.GetFileNameWithoutExtension(resolvedPath)
                : maniaSkin.Info.Name;
            skinStatusText = $"osu!mania skin: {name}";
        }
        catch (Exception ex)
        {
            skinStatusText = $"Skin fallback: {ex.Message}";
        }
    }

    private string statusText(string audioStatus) =>
        string.IsNullOrWhiteSpace(skinStatusText) ? audioStatus : $"{audioStatus}  {skinStatusText}";
}
