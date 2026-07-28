using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Scoring;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

internal partial class GameplayResultOverlay : CompositeDrawable
{
    public GameplayResultOverlay(ManiaScoreResult result, bool isNewBest)
    {
        RelativeSizeAxes = Axes.Both;
        Depth = -10;

        string rank = result.Rank == ScoreRank.X
            ? "SS"
            : result.Rank.ToString();

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.01f, 0.02f, 0.05f, 0.82f),
            },
            new FillFlowContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 12),
                Children = new Drawable[]
                {
                    line("RESULT", 28, YokkoPalette.TextMuted),
                    line(rank, 72, YokkoPalette.Cyan),
                    line($"{result.Score:0000000}", 42, YokkoPalette.Text),
                    line(
                        $"{result.Accuracy * 100:0.00}%  ·  Max Combo {result.MaxCombo}",
                        22,
                        YokkoPalette.Text),
                    line(
                        $"P {result.Perfect}  G {result.Great}  Good {result.Good}  "
                        + $"Ok {result.Ok}  Meh {result.Meh}  M {result.Miss}",
                        18,
                        YokkoPalette.TextMuted),
                    line(
                        isNewBest ? "NEW BEST · Press Enter or Esc to return" : "Press Enter or Esc to return",
                        18,
                        isNewBest ? YokkoPalette.Cyan : YokkoPalette.TextMuted),
                },
            },
        };
    }

    private static SpriteText line(string text, float size, Color4 colour) => new()
    {
        Anchor = Anchor.TopCentre,
        Origin = Anchor.TopCentre,
        Text = text,
        Font = FontUsage.Default.With(size: size),
        Colour = colour,
    };
}
