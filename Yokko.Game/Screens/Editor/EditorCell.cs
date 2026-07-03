using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK.Graphics;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Editor;

public partial class EditorCell : ClickableContainer
{
    private readonly Box fill;
    private readonly Box noteFill;

    public EditorCell(int lane, int row, Action<int, int> toggleNote)
    {
        Action = () => toggleNote(lane, row);
        Masking = true;

        InternalChildren = new Drawable[]
        {
            fill = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = row % 4 == 0
                    ? new Color4(0.07f, 0.086f, 0.112f, 1f)
                    : new Color4(0.045f, 0.056f, 0.074f, 1f),
            },
            new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Colour = row % 4 == 0
                    ? new Color4(1f, 1f, 1f, 0.14f)
                    : new Color4(1f, 1f, 1f, 0.05f),
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 1,
                Colour = new Color4(1f, 1f, 1f, 0.06f),
            },
            noteFill = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Margin = new MarginPadding { Horizontal = 5, Vertical = 3 },
                Colour = YokkoPalette.Cyan,
                Alpha = 0,
            },
        };
    }

    public void SetSelected(bool selected)
    {
        noteFill.Alpha = selected ? 0.95f : 0;
        fill.Alpha = selected ? 0.32f : 1;
    }
}
