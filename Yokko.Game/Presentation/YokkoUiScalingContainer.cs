using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace Yokko.Game.Presentation;

/// <summary>
/// Scales Yokko's authored UI from the live client resolution and applies
/// the selected 100%, 90%, or 80% relative size.
/// </summary>
/// <remarks>
/// The dynamic target-size technique follows osu!framework's
/// DrawSizePreservingFillContainer implementation at tag 2026.629.0:
/// osu.Framework/Graphics/Containers/DrawSizePreservingFillContainer.cs.
/// </remarks>
internal partial class YokkoUiScalingContainer : DrawSizePreservingFillContainer
{
    private readonly IBindable<YokkoUiScale> uiScale;
    private readonly IBindable<int> accessibilityTextScale;

    internal float CurrentContentScale { get; private set; } = 1;

    public YokkoUiScalingContainer(
        IBindable<YokkoUiScale> uiScale,
        IBindable<int> accessibilityTextScale = null)
    {
        this.uiScale = uiScale;
        this.accessibilityTextScale = accessibilityTextScale;
        Strategy = DrawSizePreservationStrategy.Minimum;
    }

    protected override void Update()
    {
        Vector2 availableDrawSize = Parent?.ChildSize
                                    ?? YokkoDisplaySettings.ReferenceLayoutSize;
        CurrentContentScale = YokkoDisplaySettings.CalculateContentScale(
            availableDrawSize,
            uiScale.Value)
            * ((accessibilityTextScale?.Value ?? 100) / 100f);

        // Matching the target aspect ratio to the live viewport makes both
        // axes resolve to the same exact scale in the framework container.
        TargetDrawSize = availableDrawSize / CurrentContentScale;
        base.Update();
    }
}
