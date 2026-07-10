using System.Linq;
using System;
using System.Threading.Tasks;
using osu.Framework.Audio;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Core.Beatmaps;
using Yokko.Core.Scoring;
using Yokko.Audio;
using Yokko.Game.Audio;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

public partial class GameplayScreen : Screen
{
    private const float designedWidth = 1040;
    private const float designedHeight = 760;
    private const double leadInMilliseconds = 900;

    private readonly YokkoBeatmap beatmap;
    private IAudioEngine audioEngine;

    private BeatmapJudgementState judgementState;
    private GameplayHud hud;
    private GameplayPlayfield playfield;
    private JudgementReadout judgementReadout;
    private KeyModeBindings keyBindings;
    private bool[] pressedLanes;
    private double startTimeMilliseconds;
    private bool hasAudioClock;
    private SpriteText clockStatusText;

    [Resolved]
    private AudioManager audioManager { get; set; }

    public GameplayScreen(YokkoBeatmap beatmap, IAudioEngine audioEngine = null)
    {
        this.beatmap = beatmap;
        this.audioEngine = audioEngine;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        keyBindings = KeyModeBindings.ForMode(beatmap.KeyMode);
        pressedLanes = new bool[keyBindings.KeyCount];
        judgementState = new BeatmapJudgementState(beatmap, JudgementWindows.DefaultMania);
        audioEngine ??= string.IsNullOrWhiteSpace(beatmap.AudioPath)
            ? new NullAudioEngine()
            : new OsuFrameworkAudioEngine(audioManager);
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
                    playfield = new GameplayPlayfield(beatmap, keyBindings)
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Y = 32,
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
                        Text = hasAudioClock ? "Audio clock active. Esc returns." : "Press mapped keys. Esc returns.",
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

        int lane = keyBindings.GetLane(e.Key);

        if (lane < 0)
            return base.OnKeyDown(e);

        if (pressedLanes[lane])
            return true;

        pressedLanes[lane] = true;
        playfield.SetLanePressed(lane, true);

        JudgementEvent judgement = judgementState.TryJudgeLanePress(lane, currentGameplayTime);

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

        pressedLanes[lane] = false;
        playfield.SetLanePressed(lane, false);
    }

    public override bool OnExiting(ScreenExitEvent e)
    {
        _ = audioEngine.StopAsync();
        return base.OnExiting(e);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            _ = audioEngine.DisposeAsync();

        base.Dispose(isDisposing);
    }

    private async Task startAudioAsync()
    {
        try
        {
            await audioEngine.StartAsync(new AudioEngineStartRequest(
                beatmap.AudioPath,
                AudioBackendKind.SharedWasapi,
                null,
                0,
                0,
                0)).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            hasAudioClock = false;
            clockStatusText.Text = $"Audio unavailable: {ex.Message}";
        }
    }

    private double currentGameplayTime
    {
        get
        {
            if (hasAudioClock && audioEngine.Status.IsRunning)
                return audioEngine.PlaybackTimeMilliseconds;

            return Time.Current - startTimeMilliseconds;
        }
    }

    private void applyJudgement(JudgementEvent judgement)
    {
        playfield.ApplyJudgement(judgement);
        judgementReadout.Show(judgement);
    }
}
