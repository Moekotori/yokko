using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Gameplay;

/// <summary>
/// Small ticket shown while the quick-retry key is held. Quick retry only
/// fires once the fill completes, so a single accidental key press can no
/// longer throw away a run. Follows the hold-to-confirm pattern of
/// osu!stable's quick retry.
/// </summary>
internal partial class GameplayQuickRetryIndicator : CompositeDrawable
{
    private const float width = 336;
    private const float height = 46;

    private readonly Box fill;

    public GameplayQuickRetryIndicator(string keyName)
    {
        Anchor = Anchor.BottomCentre;
        Origin = Anchor.BottomCentre;
        // Above the judgement timing bar.
        Y = -92;
        Size = new Vector2(width, height);
        Depth = -112;
        Alpha = 0;
        Masking = true;
        CornerRadius = 8;
        BorderThickness = 1.5f;
        BorderColour = new Color4(
            HomeControlColours.Pink.R,
            HomeControlColours.Pink.G,
            HomeControlColours.Pink.B,
            0.85f);

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.02f, 0.05f, 0.16f, 0.88f),
            },
            fill = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Width = 0,
                Colour = new Color4(
                    HomeControlColours.Pink.R,
                    HomeControlColours.Pink.G,
                    HomeControlColours.Pink.B,
                    0.38f),
            },
            new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = $"HOLD {keyName} TO RETRY",
                Font = HomeTypography.Body(14),
                Colour = Color4.White,
            },
        };
    }

    public void ShowHold()
    {
        UpdateProgress(0);
        this.FadeIn(90, Easing.OutQuint);
    }

    public void UpdateProgress(double progress)
    {
        fill.Width = (float)Math.Clamp(progress, 0, 1);
    }

    public void CancelHold()
    {
        this.FadeOut(140, Easing.OutQuint);
    }
}
