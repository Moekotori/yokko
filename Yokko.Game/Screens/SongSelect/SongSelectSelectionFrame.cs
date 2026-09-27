using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;

namespace Yokko.Game.Screens.SongSelect;

/// <summary>A single travelling focus outline, independent of pooled rows.</summary>
internal partial class SongSelectSelectionFrame : Container
{
    private Vector2 destination;
    private Vector2 destinationSize;
    private bool hasDestination;

    internal SongSelectSelectionFrame()
    {
        Alpha = 0;
        Depth = -5;
        Masking = true;
        CornerRadius = 12;
        BorderThickness = 1.8f;
        BorderColour = SongSelectTheme.Cyan;
        EdgeEffect = new EdgeEffectParameters
        {
            Type = EdgeEffectType.Glow,
            Hollow = true,
            Radius = 9,
            Colour = new Color4(0.29f, 0.81f, 0.94f, 0.20f),
        };
        Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0 };
    }

    public override bool PropagatePositionalInputSubTree => false;
    public override bool PropagateNonPositionalInputSubTree => false;

    internal void Follow(Vector2 position, Vector2 size, bool immediate)
    {
        if (hasDestination && destination == position && destinationSize == size)
        {
            if (immediate)
            {
                ClearTransforms(targetMember: nameof(Position));
                ClearTransforms(targetMember: nameof(Size));
                Position = position;
                Size = size;
            }
            return;
        }
        bool first = !hasDestination;
        hasDestination = true;
        destination = position;
        destinationSize = size;
        double duration = first || immediate ? 0 : 230;
        this.MoveTo(position, duration, Easing.OutCubic);
        this.ResizeTo(size, duration, Easing.OutCubic);
        this.FadeTo(0.9f, immediate ? 0 : 120, Easing.OutQuint);
    }

    internal void ClearSelection()
    {
        if (!hasDestination)
            return;
        hasDestination = false;
        this.FadeOut(100, Easing.OutQuint);
    }
}
