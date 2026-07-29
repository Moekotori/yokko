using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace Yokko.Game.Presentation;

/// <summary>
/// Keeps Yokko's UI density independent from unbounded desktop resolution
/// growth while retaining DPI scaling and small-window fit.
/// </summary>
/// <remarks>
/// The dynamic target-size technique follows osu!framework's
/// DrawSizePreservingFillContainer implementation at tag 2026.629.0:
/// osu.Framework/Graphics/Containers/DrawSizePreservingFillContainer.cs.
/// </remarks>
internal partial class YokkoUiScalingContainer : DrawSizePreservingFillContainer
{
    private readonly IBindable<YokkoUiScale> uiScale;
    private readonly Func<float> displayScale;

    internal float CurrentContentScale { get; private set; } = 1;

    public YokkoUiScalingContainer(
        IBindable<YokkoUiScale> uiScale,
        Func<float> displayScale)
    {
        this.uiScale = uiScale;
        this.displayScale = displayScale;
        Strategy = DrawSizePreservationStrategy.Minimum;
    }

    protected override void Update()
    {
        Vector2 availableDrawSize = Parent?.ChildSize
                                    ?? YokkoDisplaySettings.DesignedDrawSize;
        CurrentContentScale = YokkoDisplaySettings.CalculateContentScale(
            availableDrawSize,
            displayScale(),
            uiScale.Value);

        // Matching the target aspect ratio to the live viewport makes both
        // axes resolve to the same exact scale in the framework container.
        TargetDrawSize = availableDrawSize / CurrentContentScale;
        base.Update();
    }
}
