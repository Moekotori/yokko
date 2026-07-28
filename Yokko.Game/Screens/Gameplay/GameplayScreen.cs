using System.Linq;
using System;
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

    private BeatmapJudgementState judgementState;
    private GameplayHud hud;
    private GameplayPlayfield playfield;
    private JudgementReadout judgementReadout;
    private KeyModeBindings keyBindings;
    private bool[] pressedLanes;
    private double startTimeMilliseconds;
    private bool hasAudioClock;
    private SpriteText clockStatusText;
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
        keyBindings = KeyModeBindings.ForMode(beatmap.KeyMode);
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
                    playfield = new GameplayPlayfield(beatmap, keyBindings, maniaSkin)
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Y = 32,
                        Scale = maniaSkin == null ? Vector2.One : new Vector2(1.25f),
                    },
                    hud = new GameplayHud(beatmap)
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Position = new Vector2(-42, 42),
                    },
                    judgementReadout = new JudgementReadout
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
                }
            },
        };
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
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
        if (e.Key == Key.Escape)
        {
            _ = audioEngine.StopAsync();
            this.Exit();
            return true;
        }

        double inputTime = currentGameplayTime;
        int lane = keyBindings.GetLane(e.Key);

        if (lane < 0)
            return base.OnKeyDown(e);

        if (pressedLanes[lane])
            return true;

        pressedLanes[lane] = true;
        playfield.SetLanePressed(lane, true);

        JudgementEvent judgement = judgementState.TryJudgeLanePress(lane, inputTime);

        if (judgement != null)
            applyJudgement(judgement);

        return true;
    }

    protected override void OnKeyUp(KeyUpEvent e)
    {
        double inputTime = currentGameplayTime;
        int lane = keyBindings.GetLane(e.Key);

        if (lane < 0)
        {
            base.OnKeyUp(e);
            return;
        }

        pressedLanes[lane] = false;
        playfield.SetLanePressed(lane, false);

        JudgementEvent judgement = judgementState.TryJudgeLaneRelease(lane, inputTime);

        if (judgement != null)
            applyJudgement(judgement);
    }

    public override bool OnExiting(ScreenExitEvent e)
    {
        _ = audioEngine.StopAsync();
        return base.OnExiting(e);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
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

    private void applyJudgement(JudgementEvent judgement)
    {
        playfield.ApplyJudgement(judgement);
        judgementReadout.Show(judgement);
    }

    private void loadSkin(IRenderer renderer)
    {
        string resolvedPath = string.IsNullOrWhiteSpace(skinPath)
            ? OsuManiaSkinLocator.FindConfiguredPath()
            : skinPath;

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
