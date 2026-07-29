using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Gameplay;

internal partial class GameplayFailOverlay : CompositeDrawable
{
    private readonly Action retry;
    private readonly Action exit;
    private Container card;

    internal ManiaFailReason Reason { get; }

    public GameplayFailOverlay(
        YokkoBeatmap beatmap,
        BeatmapJudgementState judgementState,
        ManiaHealthState healthState,
        ManiaModSet mods,
        Action retry,
        Action exit)
    {
        this.retry = retry;
        this.exit = exit;
        Reason = healthState.FailureReason;
        RelativeSizeAxes = Axes.Both;
        Depth = -1000;

        string reason = Reason switch
        {
            ManiaFailReason.SuddenDeath =>
                "COMBO BROKEN · SUDDEN DEATH",
            ManiaFailReason.PerfectBroken =>
                "PERFECT RUN BROKEN",
            ManiaFailReason.AccuracyChallenge =>
                "ACCURACY TARGET LOST",
            _ => "HP DEPLETED",
        };
        string guidance = Reason switch
        {
            ManiaFailReason.SuddenDeath =>
                "Sudden Death ends the run on the first combo-breaking miss.",
            ManiaFailReason.PerfectBroken =>
                mods.PerfectRequirePerfectHits
                    ? "Perfect requires the highest judgement on every scoring object."
                    : "Perfect requires Great or better on every scoring object.",
            ManiaFailReason.AccuracyChallenge =>
                $"Accuracy can no longer stay above "
                + $"{mods.AccuracyChallengeMinimum * 100:0.0}%.",
            _ => "Your health reached zero before the chart was cleared.",
        };
        string modSummary = mods.IsEmpty
            ? "NO MOD"
            : string.Join("  ", mods.DisplayLabels);

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.01f, 0.012f, 0.025f, 0.88f),
            },
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(
                    HomeControlColours.Pink.R,
                    HomeControlColours.Pink.G,
                    HomeControlColours.Pink.B,
                    0.08f),
            },
            card = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(650, 340),
                Masking = true,
                CornerRadius = 18,
                BorderThickness = 2,
                BorderColour = HomeControlColours.Pink,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(0.035f, 0.043f, 0.075f, 0.99f),
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 10,
                        Colour = HomeControlColours.Pink,
                    },
                    new SpriteIcon
                    {
                        Position = new Vector2(42, 39),
                        Size = new Vector2(36),
                        Icon = FontAwesome.Solid.HeartBroken,
                        Colour = HomeControlColours.Pink,
                    },
                    new SpriteText
                    {
                        Position = new Vector2(96, 31),
                        Text = "FAILED",
                        Font = HomeTypography.Hero(42),
                        Colour = Color4.White,
                    },
                    new SpriteText
                    {
                        Position = new Vector2(42, 101),
                        Text = reason,
                        Font = HomeTypography.Display(20),
                        Colour = HomeControlColours.Yellow,
                    },
                    new SpriteText
                    {
                        Position = new Vector2(42, 136),
                        Text = guidance,
                        Font = HomeTypography.Body(15),
                        Colour = new Color4(0.79f, 0.84f, 0.93f, 1f),
                    },
                    new Box
                    {
                        Position = new Vector2(42, 176),
                        Size = new Vector2(566, 1),
                        Colour = new Color4(1f, 1f, 1f, 0.18f),
                    },
                    new SpriteText
                    {
                        Position = new Vector2(42, 195),
                        Text =
                            $"{beatmap.Title}  ·  {modSummary}",
                        Font = HomeTypography.Display(15),
                        Colour = HomeControlColours.Cyan,
                    },
                    new SpriteText
                    {
                        Position = new Vector2(42, 225),
                        Text =
                            $"SCORE {judgementState.Score:0000000}"
                            + $"  ·  ACC {judgementState.Accuracy * 100:0.00}%"
                            + $"  ·  MAX {judgementState.MaxCombo}",
                        Font = HomeTypography.Body(14),
                        Colour = Color4.White,
                    },
                    new FailActionButton(
                        "RETRY",
                        "R / ENTER",
                        HomeControlColours.Pink,
                        retry)
                    {
                        Position = new Vector2(42, 272),
                    },
                    new FailActionButton(
                        "BACK",
                        "ESC",
                        HomeControlColours.Cyan,
                        exit)
                    {
                        Position = new Vector2(331, 272),
                    },
                },
            },
        };
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        card.Scale = new Vector2(0.96f);
        card.Alpha = 0;
        card.FadeIn(180, Easing.OutQuint)
            .ScaleTo(1, 260, Easing.OutBack);
    }

    public bool HandleKey(Key key)
    {
        switch (key)
        {
            case Key.R:
            case Key.Enter:
                retry();
                return true;

            case Key.Escape:
                exit();
                return true;

            default:
                return true;
        }
    }

    private partial class FailActionButton : ClickableContainer
    {
        private readonly Box background;

        public FailActionButton(
            string label,
            string hint,
            Color4 accent,
            Action action)
        {
            Action = action;
            Size = new Vector2(277, 48);
            Masking = true;
            CornerRadius = 8;
            BorderThickness = 1.5f;
            BorderColour = accent;
            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        accent.R,
                        accent.G,
                        accent.B,
                        0.15f),
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 18,
                    Text = label,
                    Font = HomeTypography.Display(16),
                    Colour = Color4.White,
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -16,
                    Text = hint,
                    Font = HomeTypography.Body(11),
                    Colour = accent,
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeTo(1.8f, 90, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeTo(1, 90, Easing.OutQuint);
            base.OnHoverLost(e);
        }
    }
}
