using System;
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

internal partial class GameplayGhostComparisonPanel : CompositeDrawable
{
    private readonly SpriteText statusText;
    private readonly SpriteText scoreText;
    private readonly SpriteText accuracyText;
    private readonly SpriteText missText;

    internal string DisplayedScore => scoreText.Text.ToString();
    internal string DisplayedAccuracy => accuracyText.Text.ToString();
    internal string DisplayedMiss => missText.Text.ToString();

    internal GameplayGhostComparisonPanel()
    {
        Size = new Vector2(294, 104);
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
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.025f, 0.03f, 0.045f, 0.88f),
            },
            new Box
            {
                Size = new Vector2(4, 104),
                Colour = YokkoPalette.Cyan,
            },
            new SpriteText
            {
                Position = new Vector2(15, 11),
                Text = "PB GHOST",
                Font = HomeTypography.Display(10),
                Spacing = new Vector2(0.8f, 0),
                Colour = YokkoPalette.Cyan,
            },
            statusText = new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-13, 12),
                Font = HomeTypography.Display(8),
                Colour = YokkoPalette.TextMuted,
            },
            scoreText = createValueText(new Vector2(15, 40)),
            accuracyText = createValueText(new Vector2(15, 62)),
            missText = createValueText(new Vector2(15, 84)),
        ];
    }

    internal void ShowLoading()
    {
        statusText.Text = "LOADING";
        scoreText.Text = "SCORE  --";
        accuracyText.Text = "ACC    --";
        missText.Text = "MISS   --";
        this.FadeIn(120, Easing.OutQuint);
    }

    internal void HidePanel() => this.FadeOut(120, Easing.OutQuint);

    internal void UpdateComparison(
        long liveScore,
        double liveAccuracy,
        int liveMissCount,
        GameplayGhostSnapshot ghost)
    {
        statusText.Text = "LIVE vs PB";
        long scoreDelta = liveScore - ghost.Score;
        double accuracyDelta = (liveAccuracy - ghost.Accuracy) * 100;
        scoreText.Text = $"SCORE  {formatSigned(scoreDelta)}";
        accuracyText.Text = $"ACC    {accuracyDelta:+0.00;-0.00;0.00}pp";
        missText.Text = $"MISS   {liveMissCount} / {ghost.MissCount}";
        scoreText.Colour = deltaColour(scoreDelta);
        accuracyText.Colour = deltaColour(accuracyDelta);
        missText.Colour = liveMissCount <= ghost.MissCount
            ? HomeControlColours.PaleCyan
            : HomeControlColours.Pink;
    }

    private static SpriteText createValueText(Vector2 position) => new()
    {
        Position = position,
        Font = new FontUsage("PlusJakartaSans").With(size: 13),
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
