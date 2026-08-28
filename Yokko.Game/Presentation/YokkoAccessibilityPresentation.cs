using osu.Framework.Graphics;
using osuTK.Graphics;
using Yokko.Game.Configuration;

namespace Yokko.Game.Presentation;

/// <summary>
/// Applies accessibility preferences to the shared UI theme store.
/// </summary>
internal static class YokkoAccessibilityPresentation
{
    internal static void Apply(
        YokkoUiThemeStore themeStore,
        YokkoAccessibilitySettings settings)
    {
        if (!settings.ReduceMotion.Value && !settings.HighContrast.Value)
        {
            themeStore.Reset();
            return;
        }

        YokkoUiTheme theme = YokkoUiTheme.Default;

        if (settings.ReduceMotion.Value)
        {
            theme = theme with
            {
                Motion = new YokkoUiMotionTokens(
                    0,
                    0,
                    0,
                    0,
                    Easing.None),
            };
        }

        if (settings.HighContrast.Value)
        {
            YokkoDarkColourTokens dark = theme.Colours.Dark;
            theme = theme with
            {
                Colours = theme.Colours with
                {
                    Dark = dark with
                    {
                        Text = Color4.White,
                        TextMuted = new Color4(0.92f, 0.94f, 0.98f, 1f),
                        TextDim = new Color4(0.78f, 0.84f, 0.92f, 1f),
                        Border = new Color4(0.55f, 0.62f, 0.72f, 0.72f),
                    },
                },
            };
        }

        themeStore.Apply(theme, "Accessibility");
    }
}
