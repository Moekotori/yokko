using System.Reflection;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

internal partial class AboutSettingsPanel : CompositeDrawable
{
    public AboutSettingsPanel()
    {
        RelativeSizeAxes = Axes.Both;

        string version = Assembly.GetEntryAssembly()?
                                 .GetName().Version?.ToString()
                         ?? "development";

        InternalChildren = new Drawable[]
        {
            SettingsChrome.CreateHeader(
                YokkoStrings.Get("settings.about.title"),
                YokkoStrings.Get("settings.about.subtitle"),
                FontAwesome.Solid.InfoCircle,
                (int)SettingsPageKind.About + 1),
            createInformationCard(
                174,
                FontAwesome.Solid.InfoCircle,
                YokkoStrings.Get("settings.about.section_version"),
                version,
                SettingsTheme.StatusCyan),
            createInformationCard(
                278,
                FontAwesome.Solid.Pen,
                YokkoStrings.Get("settings.about.section_credits"),
                YokkoStrings.Get("settings.about.creator"),
                SettingsTheme.PaleCyan),
            createInformationCard(
                382,
                FontAwesome.Solid.Heart,
                YokkoStrings.Get("settings.about.section_acknowledgements"),
                YokkoStrings.Get("settings.about.acknowledgements"),
                Color4.White),
            new SettingsPanelFooter(YokkoStrings.Get("settings.about.description")),
        };
    }

    private static Drawable createInformationCard(
        float y,
        IconUsage icon,
        LocalisableString title,
        LocalisableString value,
        Color4 colour)
    {
        var card = new SettingsStickerCard(
            new Vector2(SettingsChrome.ContentWidth, 86),
            9,
            colour)
        {
            Position = new Vector2(SettingsChrome.ContentX, y),
        };

        card.SetContent(
            new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 48,
                Size = new Vector2(54),
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 48,
                Size = new Vector2(25),
                Icon = icon,
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(92, 16),
                Text = title,
                Font = HomeTypography.Display(21),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(92, 48),
                Text = value,
                Font = HomeTypography.Body(18),
                Colour = SettingsTheme.MutedNavy,
            });

        return card;
    }
}
