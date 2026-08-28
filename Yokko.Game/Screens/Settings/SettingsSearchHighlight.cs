using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK.Graphics;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

/// <summary>
/// Visual feedback when settings search lands on a row or card.
/// </summary>
internal static class SettingsSearchHighlight
{
    private const float row_tolerance = 48f;

    internal static void PulseRow(Container content, float y)
    {
        if (content == null)
            return;

        Pulse(findNearY(content, y));
    }

    internal static void Pulse(Drawable target)
    {
        if (target == null)
            return;

        if (target is SettingsStickerCard card)
            card.Background?.FlashColour(SettingsTheme.PaleCyan, 180, Easing.OutQuint);

        if (target is Container bordered && bordered.BorderThickness > 0)
        {
            Color4 originalColour = bordered.BorderColour;
            bordered.TransformTo(
                    nameof(bordered.BorderColour),
                    HomeControlColours.Pink,
                    100,
                    Easing.OutQuint)
                .Then()
                .TransformTo(
                    nameof(bordered.BorderColour),
                    originalColour,
                    350,
                    Easing.OutQuint);
        }

        target.ClearTransforms();
        target.ScaleTo(1.015f, 120, Easing.OutQuint)
              .Then()
              .ScaleTo(1, 220, Easing.OutQuint);
    }

    private static Drawable findNearY(Container content, float y)
    {
        Drawable best = null;
        float bestDistance = row_tolerance;

        foreach (Drawable child in content.Children)
        {
            float distance = Math.Abs(child.Y - y);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = child;
        }

        return best;
    }
}
