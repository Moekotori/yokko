using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Skinning.OsuMania;

internal partial class SkinImportNotificationOverlay : CompositeDrawable
{
    private readonly Box accent;
    private readonly SpriteIcon icon;
    private readonly SpriteText title;
    private readonly SpriteText detail;

    public SkinImportNotificationOverlay()
    {
        Anchor = Anchor.BottomRight;
        Origin = Anchor.BottomRight;
        Position = new Vector2(-24);
        Size = new Vector2(420, 82);
        Masking = true;
        CornerRadius = 10;
        BorderThickness = 1.2f;
        BorderColour = new Color4(0.08f, 0.15f, 0.22f, 0.35f);
        Alpha = 0;
        Depth = -1000;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            accent = new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 7,
                Colour = HomeControlColours.Cyan,
            },
            icon = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 43,
                Size = new Vector2(24),
                Icon = FontAwesome.Solid.Download,
                Colour = HomeControlColours.Navy,
            },
            title = new SpriteText
            {
                Position = new Vector2(78, 17),
                Text = "Importing skin",
                Font = HomeTypography.Display(19),
                Colour = HomeControlColours.Navy,
            },
            detail = new SpriteText
            {
                Position = new Vector2(78, 45),
                Width = 318,
                Truncate = true,
                Font = HomeTypography.Body(15),
                Colour = HomeControlColours.Navy,
            },
        };
    }

    public void ShowImporting(string path)
    {
        accent.Colour = HomeControlColours.Cyan;
        icon.Icon = FontAwesome.Solid.Download;
        title.Text = YokkoStrings.Get("settings.skins.importing");
        detail.Text = System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
        show();
    }

    public void ShowResult(SkinImportResult result)
    {
        accent.Colour = result.Success ? HomeControlColours.Cyan : HomeControlColours.Pink;
        icon.Icon = result.Success ? FontAwesome.Solid.Check : FontAwesome.Solid.ExclamationTriangle;
        title.Text = result.Success
            ? YokkoStrings.Get("settings.skins.import_success")
            : YokkoStrings.Get("settings.skins.import_failed");
        detail.Text = result.Success ? result.Skin?.Name ?? result.Message : result.Message;
        show();
    }

    private void show()
    {
        this.ClearTransforms();
        Alpha = 0;
        Y = 12;
        this.FadeIn(160, Easing.OutQuint);
        this.MoveToY(0, 180, Easing.OutQuint);
        this.Delay(3600).FadeOut(220, Easing.OutQuint);
    }
}
