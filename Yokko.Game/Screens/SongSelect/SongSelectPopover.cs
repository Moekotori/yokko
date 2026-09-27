using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.SongSelect;

/// <summary>Interruptible reveal with input lifetime independent of the fade.</summary>
internal partial class SongSelectPopover : Container
{
    [Resolved(CanBeNull = true)]
    private YokkoAccessibilitySettings accessibility { get; set; }

    internal bool IsOpen { get; private set; }
    public override bool PropagatePositionalInputSubTree => IsOpen;
    public override bool PropagateNonPositionalInputSubTree => IsOpen;

    internal virtual void Open()
    {
        bool reduced = accessibility?.ReduceMotion.Value == true;
        IsOpen = true;
        ClearTransforms(targetMember: nameof(Alpha));
        ClearTransforms(targetMember: nameof(Scale));
        if (reduced)
            Scale = Vector2.One;
        else if (Alpha == 0)
            Scale = new Vector2(0.975f);
        this.FadeIn(reduced ? 60 : 130, Easing.OutCubic)
            .ScaleTo(1, reduced ? 0 : 180, Easing.OutQuint);
    }

    internal virtual void Close()
    {
        IsOpen = false;
        bool reduced = accessibility?.ReduceMotion.Value == true;
        ClearTransforms(targetMember: nameof(Alpha));
        ClearTransforms(targetMember: nameof(Scale));
        this.FadeOut(reduced ? 60 : 100, Easing.OutCubic)
            .ScaleTo(reduced ? 1 : 0.985f, reduced ? 0 : 100, Easing.OutCubic);
    }
}
