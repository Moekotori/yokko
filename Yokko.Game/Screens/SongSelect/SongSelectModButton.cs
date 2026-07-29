using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectModButton : ClickableContainer
{
    private readonly Color4 accent;
    private readonly Box background;
    private readonly SpriteText label;

    public SongSelectModButton(
        string acronym,
        Color4 accent,
        Action action)
    {
        this.accent = accent;
        Action = action;
        Size = new Vector2(48, 34);
        Masking = true;
        CornerRadius = 4;
        BorderThickness = 2;
        BorderColour = accent;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
            },
            label = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = acronym,
                Font = HomeTypography.Display(18),
            },
        };

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        background.Colour = selected
            ? accent
            : new Color4(
                SongSelectTheme.Navy.R,
                SongSelectTheme.Navy.G,
                SongSelectTheme.Navy.B,
                0.62f);
        label.Colour = selected
            ? SongSelectTheme.DeepNavy
            : accent;
        this.ScaleTo(selected ? 1.06f : 1, 120);
    }
}
