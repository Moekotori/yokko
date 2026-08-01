using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;
using Yokko.Game.Screens.Settings;

namespace Yokko.Game.Presentation;

internal partial class VolumeNotificationOverlay : CompositeDrawable
{
    private readonly SpriteIcon icon;
    private readonly SpriteText detail;

    internal VolumeNotificationOverlay()
    {
        Anchor = Anchor.TopCentre;
        Origin = Anchor.TopCentre;
        Y = 24;
        Size = new Vector2(300, 74);
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
                Colour = HomeControlColours.Pink,
            },
            icon = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 40,
                Size = new Vector2(23),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(72, 13),
                Text = YokkoStrings.Get("settings.audio.master_volume"),
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

    internal void Show(double volume, bool increasing)
    {
        icon.Icon = volume <= 0
            ? FontAwesome.Solid.VolumeMute
            : increasing
                ? FontAwesome.Solid.VolumeUp
                : FontAwesome.Solid.VolumeDown;
        detail.Text = $"{Math.Round(volume * 100):0}%";

        this.ClearTransforms();
        Alpha = 0;
        Y = 12;
        this.FadeIn(90, Easing.OutQuint);
        this.MoveToY(24, 130, Easing.OutQuint);
        this.Delay(1200).FadeOut(150, Easing.OutQuint);
    }
}
