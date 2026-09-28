using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Editor;

/// <summary>
/// The navy top bar of the editor. Presentation mirrors the Song Select top
/// navigation: a yellow active-page badge, a cyan-barred context block, and a
/// cyan hairline along the bottom edge.
/// </summary>
public partial class EditorHeader : CompositeDrawable
{
    public EditorHeader()
    {
        Size = new Vector2(EditorScreen.CanvasWidth, EditorScreen.HeaderHeight);

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = EditorTheme.Navy,
            },
            createActiveBadge(),
            createContextBlock(),
            new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -24,
                Text = YokkoStrings.Get("editor.esc_hint"),
                Font = HomeTypography.Display(9),
                Colour = new Color4(1f, 1f, 1f, 0.58f),
            },
            new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Colour = EditorTheme.Cyan,
                Alpha = 0.42f,
            },
        };
    }

    private static Drawable createActiveBadge() => new Container
    {
        Position = new Vector2(24, 0),
        Size = new Vector2(48, EditorScreen.HeaderHeight),
        Children =
        [
            new Circle
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Y = -2,
                Size = new Vector2(42),
                Colour = EditorTheme.Yellow,
            },
            new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Y = -2,
                Size = new Vector2(20),
                Icon = FontAwesome.Solid.PencilAlt,
                Colour = EditorTheme.Navy,
            },
            new Box
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Y = -5,
                Width = 26,
                Height = 3,
                Colour = EditorTheme.Pink,
            },
        ],
    };

    private static Drawable createContextBlock() => new Container
    {
        Position = new Vector2(96, 12),
        Size = new Vector2(420, 48),
        Children =
        [
            new Box
            {
                Position = new Vector2(0, 3),
                Size = new Vector2(3, 39),
                Colour = EditorTheme.Cyan,
            },
            new SpriteText
            {
                Position = new Vector2(16, 1),
                Text = YokkoStrings.Get("editor.title"),
                Font = HomeTypography.Display(17),
                Colour = Color4.White,
            },
            new SpriteText
            {
                Position = new Vector2(16, 27),
                Text = YokkoStrings.Get("editor.subtitle"),
                Font = HomeTypography.Display(9),
                Colour = new Color4(1f, 1f, 1f, 0.58f),
            },
            new Box
            {
                Position = new Vector2(16, 45),
                Size = new Vector2(54, 2),
                Colour = EditorTheme.Pink,
            },
        ],
    };
}
