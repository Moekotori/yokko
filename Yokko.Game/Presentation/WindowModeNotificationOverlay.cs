using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;
using Yokko.Game.Screens.Settings;

namespace Yokko.Game.Presentation;

internal partial class WindowModeNotificationOverlay : CompositeDrawable
{
    private readonly SpriteText title;
    private readonly SpriteText detail;

    internal WindowModeNotificationOverlay()
    {
        Anchor = Anchor.TopCentre;
        Origin = Anchor.TopCentre;
        Y = 24;
        Size = new Vector2(390, 74);
        Masking = true;
        CornerRadius = 10;
        BorderThickness = 2;
        BorderColour = HomeControlColours.Navy;
        Alpha = 0;
        Depth = float.MinValue;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(1, 1, 1, 0.97f),
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 7,
                Colour = HomeControlColours.Cyan,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 40,
                Size = new Vector2(23),
                Icon = FontAwesome.Solid.Desktop,
                Colour = HomeControlColours.Navy,
            },
            title = new SpriteText
            {
                Position = new Vector2(72, 13),
                Font = HomeTypography.Display(18),
                Colour = HomeControlColours.Navy,
            },
            detail = new SpriteText
            {
                Position = new Vector2(72, 40),
                Font = HomeTypography.Body(14),
                Colour = SettingsTheme.MutedNavy,
            },
        };
    }

    internal void Show(WindowMode mode, DisplayMode displayMode)
    {
        title.Text = GetModeName(mode);
        detail.Text = displayMode.Size.Width > 0
            ? $"{displayMode.Size.Width} \u00d7 {displayMode.Size.Height}  \u00b7  {displayMode.RefreshRate:0} Hz  \u00b7  Alt+Enter"
            : "Alt+Enter";

        this.ClearTransforms();
        Alpha = 0;
        Y = 12;
        this.FadeIn(120, Easing.OutQuint);
        this.MoveToY(24, 160, Easing.OutQuint);
        this.Delay(1800).FadeOut(180, Easing.OutQuint);
    }

    internal static LocalisableString GetModeName(WindowMode mode) =>
        YokkoStrings.Get(mode switch
        {
            WindowMode.Windowed => "settings.display.windowed",
            WindowMode.Borderless => "settings.display.borderless",
            _ => "settings.display.fullscreen",
        });
}
