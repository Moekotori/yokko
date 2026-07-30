using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Gameplay;

/// <summary>
/// Big centred 3-2-1 shown while a resume from pause is buffered. The
/// gameplay clock stays frozen for the countdown so the player gets a moment
/// to re-orient before notes start moving again.
/// </summary>
internal partial class GameplayResumeCountdown : CompositeDrawable
{
    internal const int CountSteps = 3;

    private const float count_size = 168;

    private readonly SpriteText countText;

    public GameplayResumeCountdown()
    {
        Anchor = Anchor.Centre;
        Origin = Anchor.Centre;
        Size = new Vector2(count_size * 1.6f);
        // In front of gameplay and the HUD but behind the pause overlay.
        Depth = -900;
        Alpha = 0;

        InternalChildren = new Drawable[]
        {
            new SpriteText
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.TopCentre,
                Text = "READY",
                Font = HomeTypography.Body(20),
                Colour = HomeControlColours.Cyan,
            },
            countText = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Font = HomeTypography.Display(count_size),
                Colour = Color4.White,
                Shadow = true,
                ShadowColour = new Color4(0f, 0f, 0f, 0.55f),
            },
        };
    }

    public void ShowCount(int value)
    {
        Alpha = 1;
        countText.Text = value.ToString();
        countText.FinishTransforms();
        countText.Scale = new Vector2(1.4f);
        countText.Alpha = 1;
        countText.ScaleTo(1, 240, Easing.OutQuint);
    }
}
