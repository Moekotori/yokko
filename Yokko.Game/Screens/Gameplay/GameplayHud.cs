using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Beatmaps;
using Yokko.Core.Scoring;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

public partial class GameplayHud : CompositeDrawable
{
    private readonly SpriteText timeText;
    private readonly SpriteText comboText;
    private readonly SpriteText accuracyText;
    private readonly SpriteText countsText;

    public GameplayHud(YokkoBeatmap beatmap)
    {
        Width = 340;
        Height = 176;
        Masking = true;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.035f, 0.045f, 0.065f, 0.92f),
            },
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 10),
                Padding = new MarginPadding(18),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = $"{beatmap.Title} [{beatmap.DifficultyName}]",
                        Font = FontUsage.Default.With(size: 20),
                        Colour = YokkoPalette.Text,
                    },
                    timeText = createLine(),
                    comboText = createLine(),
                    accuracyText = createLine(),
                    countsText = createLine(16),
                },
            },
        };
    }

    public void UpdateState(double gameplayTimeMilliseconds, BeatmapJudgementState state)
    {
        timeText.Text = $"Time {Math.Max(0, gameplayTimeMilliseconds / 1000):0.00}s";
        comboText.Text = $"Combo {state.Combo} / Max {state.MaxCombo}";
        accuracyText.Text = $"Accuracy {state.Accuracy * 100:0.00}%";
        countsText.Text = $"P {state.Counts.Perfect}  G {state.Counts.Great}  Good {state.Counts.Good}  Bad {state.Counts.Bad}  M {state.Counts.Miss}";
    }

    private static SpriteText createLine(float size = 18) => new()
    {
        Font = FontUsage.Default.With(size: size),
        Colour = YokkoPalette.TextMuted,
    };
}
