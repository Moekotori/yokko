using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Gameplay;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Gameplay;

internal readonly record struct GameplayGhostRaceSnapshot(
    string Label,
    GameplayGhostSnapshot Snapshot);

internal partial class GameplayGhostComparisonPanel : CompositeDrawable
{
    private readonly SpriteText statusText;
    private readonly SpriteText[] rows = new SpriteText[3];

    internal string DisplayedScore => rows[0].Text.ToString();
    internal string DisplayedAccuracy => rows[1].Text.ToString();
    internal string DisplayedMiss => rows[2].Text.ToString();

    internal GameplayGhostComparisonPanel()
    {
        Size = new Vector2(354, 142);
        Alpha = 0;
        Masking = true;
        CornerRadius = 6;
        BorderThickness = 1;
        BorderColour = new Color4(
            HomeControlColours.PaleCyan.R,
            HomeControlColours.PaleCyan.G,
            HomeControlColours.PaleCyan.B,
            0.42f);

        InternalChildren =
        [
            new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(0.025f, 0.03f, 0.045f, 0.88f) },
            new Box { Size = new Vector2(4, 142), Colour = YokkoPalette.Cyan },
            new SpriteText { Position = new Vector2(15, 11), Text = "LOCAL RACE", Font = HomeTypography.Display(10), Spacing = new Vector2(0.8f, 0), Colour = YokkoPalette.Cyan },
            statusText = new SpriteText { Anchor = Anchor.TopRight, Origin = Anchor.TopRight, Position = new Vector2(-13, 12), Font = HomeTypography.Display(8), Colour = YokkoPalette.TextMuted },
            rows[0] = createValueText(new Vector2(15, 43)),
            rows[1] = createValueText(new Vector2(15, 73)),
            rows[2] = createValueText(new Vector2(15, 103)),
        ];
    }

    internal void ShowLoading()
    {
        statusText.Text = "LOADING";
        rows[0].Text = "PB        --";
        rows[1].Text = "LAST      --";
        rows[2].Text = "BEST ACC  --";
        this.FadeIn(120, Easing.OutQuint);
    }

    internal void HidePanel() => this.FadeOut(120, Easing.OutQuint);

    internal void UpdateComparisons(
        long liveScore,
        double liveAccuracy,
        int liveMissCount,
        IReadOnlyList<GameplayGhostRaceSnapshot> ghosts)
    {
        statusText.Text = $"LIVE vs {ghosts.Count}";
        for (int i = 0; i < rows.Length; i++)
        {
            if (i >= ghosts.Count)
            {
                rows[i].Text = string.Empty;
                continue;
            }

            GameplayGhostRaceSnapshot item = ghosts[i];
            long scoreDelta = liveScore - item.Snapshot.Score;
            double accuracyDelta = (liveAccuracy - item.Snapshot.Accuracy) * 100;
            rows[i].Text =
                $"{item.Label,-8} {formatSigned(scoreDelta),9}  "
                + $"{accuracyDelta,+6:0.00;-0.00;0.00}pp  "
                + $"M {liveMissCount}:{item.Snapshot.MissCount}";
            rows[i].Colour = deltaColour(scoreDelta);
        }
    }

    internal void UpdateComparison(
        long liveScore,
        double liveAccuracy,
        int liveMissCount,
        GameplayGhostSnapshot ghost) => UpdateComparisons(
        liveScore,
        liveAccuracy,
        liveMissCount,
        [new GameplayGhostRaceSnapshot("PB", ghost)]);

    private static SpriteText createValueText(Vector2 position) => new()
    {
        Position = position,
        Font = new FontUsage("PlusJakartaSans").With(size: 12),
        Colour = HomeControlColours.Ivory,
    };

    private static string formatSigned(long value) =>
        value > 0 ? $"+{value:N0}" : value.ToString("N0");

    private static Color4 deltaColour(double value) => value switch
    {
        > 0 => HomeControlColours.PaleCyan,
        < 0 => HomeControlColours.Pink,
        _ => HomeControlColours.Ivory,
    };
}
