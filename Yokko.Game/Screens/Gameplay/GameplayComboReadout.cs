using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK.Graphics;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

/// <summary>
/// A standalone combo presentation so players can position it independently
/// from the diagnostic HUD.
/// </summary>
internal partial class GameplayComboReadout : CompositeDrawable
{
    private readonly SpriteText comboText;
    private bool editorPreview;

    internal int DisplayedCombo { get; private set; }

    public GameplayComboReadout()
    {
        AutoSizeAxes = Axes.Both;
        InternalChild = comboText = new SpriteText
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Text = "128x",
            Font = new FontUsage("NotoSansCJK").With(size: 38, weight: "Bold"),
            Colour = Color4.White,
        };
        Alpha = 0;
    }

    public void UpdateState(int combo)
    {
        DisplayedCombo = combo;
        if (editorPreview)
            return;

        comboText.Text = $"{combo}x";
        Alpha = combo > 0 ? 1 : 0;
    }

    internal void SetEditorPreview(bool preview)
    {
        editorPreview = preview;
        if (preview)
        {
            comboText.Text = "128x";
            comboText.Colour = YokkoPalette.Text;
            Alpha = 1;
            return;
        }

        comboText.Text = $"{DisplayedCombo}x";
        Alpha = DisplayedCombo > 0 ? 1 : 0;
    }
}
