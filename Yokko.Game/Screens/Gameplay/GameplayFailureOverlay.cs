using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Gameplay;

internal partial class GameplayFailureOverlay : CompositeDrawable
{
    public GameplayFailureOverlay(string technicalDetail = null)
    {
        RelativeSizeAxes = Axes.Both;
        Depth = -1000;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.01f, 0.012f, 0.018f, 0.78f),
            },
            new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(600, technicalDetail == null ? 166 : 194),
                Masking = true,
                CornerRadius = 12,
                BorderThickness = 1.2f,
                BorderColour = new Color4(
                    HomeControlColours.Pink.R,
                    HomeControlColours.Pink.G,
                    HomeControlColours.Pink.B,
                    0.65f),
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(0.035f, 0.043f, 0.06f, 0.98f),
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 7,
                        Colour = HomeControlColours.Pink,
                    },
                    new SpriteIcon
                    {
                        Position = new Vector2(34, 30),
                        Size = new Vector2(28),
                        Icon = FontAwesome.Solid.ExclamationTriangle,
                        Colour = HomeControlColours.Pink,
                    },
                    new SpriteText
                    {
                        Position = new Vector2(80, 26),
                        Text = YokkoStrings.Get("gameplay.audio_failed_title"),
                        Font = HomeTypography.Display(24),
                        Colour = Color4.White,
                    },
                    new SpriteText
                    {
                        Position = new Vector2(80, 67),
                        Text = YokkoStrings.Get("gameplay.audio_failed_message"),
                        Font = HomeTypography.Body(16),
                        Colour = new Color4(0.76f, 0.8f, 0.88f, 1f),
                    },
                    new SpriteText
                    {
                        Position = new Vector2(80, 111),
                        Text = YokkoStrings.Get("gameplay.audio_failed_return"),
                        Font = HomeTypography.Body(15),
                        Colour = HomeControlColours.Cyan,
                    },
                    new SpriteText
                    {
                        Position = new Vector2(80, 139),
                        Text = technicalDetail ?? string.Empty,
                        Font = HomeTypography.Body(12),
                        Colour = HomeControlColours.Pink,
                        Alpha = technicalDetail == null ? 0 : 1,
                    },
                },
            },
        };
    }
}
