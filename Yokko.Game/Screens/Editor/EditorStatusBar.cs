using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Editor;

/// <summary>
/// The deep navy status bar anchored to the bottom of the editor. It shows
/// the latest editor status message and the escape hint.
/// </summary>
public partial class EditorStatusBar : CompositeDrawable
{
    private readonly SpriteText statusText;

    public EditorStatusBar()
    {
        Size = new Vector2(EditorScreen.CanvasWidth, EditorScreen.StatusBarHeight);

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = EditorTheme.DeepNavy,
            },
            new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Colour = EditorTheme.Cyan,
                Alpha = 0.42f,
            },
            new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 32,
                Size = new Vector2(6),
                Colour = EditorTheme.Cyan,
            },
            statusText = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 48,
                Text = YokkoStrings.Get("editor.ready"),
                Font = HomeTypography.Body(13),
                Colour = EditorTheme.PaleCyan,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -24,
                Text = YokkoStrings.Get("editor.esc_hint"),
                Font = HomeTypography.Display(9),
                Colour = new Color4(1f, 1f, 1f, 0.46f),
            },
        };
    }

    public void SetStatus(LocalisableString message) => statusText.Text = message;

    internal string StatusText => statusText.Text.ToString();
}
