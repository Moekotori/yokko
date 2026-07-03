using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Core.Beatmaps;
using Yokko.Core.Scoring;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

public partial class GameplayScreen : Screen
{
    private const double leadInMilliseconds = 900;

    private readonly YokkoBeatmap beatmap;

    private BeatmapJudgementState judgementState;
    private GameplayHud hud;
    private GameplayPlayfield playfield;
    private JudgementReadout judgementReadout;
    private KeyModeBindings keyBindings;
    private bool[] pressedLanes;
    private double startTimeMilliseconds;

    public GameplayScreen(YokkoBeatmap beatmap)
    {
        this.beatmap = beatmap;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        keyBindings = KeyModeBindings.ForMode(beatmap.KeyMode);
        pressedLanes = new bool[keyBindings.KeyCount];
        judgementState = new BeatmapJudgementState(beatmap, JudgementWindows.DefaultMania);

        InternalChildren = new Drawable[]
        {
            new Box
            {
                Colour = YokkoPalette.Background,
                RelativeSizeAxes = Axes.Both,
            },
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
                Position = new osuTK.Vector2(-42, 42),
            },
            judgementReadout = new JudgementReadout
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Y = -62,
            },
            new SpriteText
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Y = -30,
                Text = "Press mapped keys. Esc returns.",
                Font = FontUsage.Default.With(size: 18),
                Colour = YokkoPalette.TextDim,
            },
        };
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        startTimeMilliseconds = Time.Current + leadInMilliseconds;
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

    private double currentGameplayTime => Time.Current - startTimeMilliseconds;

    private void applyJudgement(JudgementEvent judgement)
    {
        playfield.ApplyJudgement(judgement);
        judgementReadout.Show(judgement);
    }
}
