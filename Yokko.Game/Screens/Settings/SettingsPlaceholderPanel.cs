using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

/// <summary>
/// Honest placeholder for settings categories whose behaviour is not implemented
/// yet. The collapsed section layout is also the structure future controls plug into.
/// </summary>
internal partial class SettingsPlaceholderPanel : CompositeDrawable
{
    public SettingsPlaceholderPanel(SettingsPageDefinition page)
    {
        RelativeSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            new SpriteText
            {
                Position = new Vector2(378, 42),
                Text = page.Title,
                Font = HomeTypography.Display(58),
                Spacing = new Vector2(0.45f, 0),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(378, 105),
                Text = page.Subtitle,
                Font = HomeTypography.Body(17),
                Spacing = new Vector2(0.2f, 0),
                Colour = SettingsTheme.MutedNavy,
            },
            createCategoryMark(page.Icon),
            createComingSoonCard(page),
            new SpriteText
            {
                Position = new Vector2(378, 323),
                Text = "Planned sections",
                Font = HomeTypography.Display(20),
                Colour = HomeControlColours.Navy,
            },
            createSectionList(page),
            new SettingsPanelFooter(),
            new HomeDotCross
            {
                Position = new Vector2(1088, 594),
                Scale = new Vector2(1.1f),
            },
            createDecorationIcon(FontAwesome.Solid.Plus, 1172, 601, 16, HomeControlColours.Pink),
            createDecorationIcon(FontAwesome.Solid.Plus, 1200, 637, 12, HomeControlColours.Yellow),
        };
    }

    private static Drawable createCategoryMark(IconUsage icon) => new Container
    {
        Position = new Vector2(1094, 40),
        Size = new Vector2(124, 92),
        Children = new Drawable[]
        {
            new Circle
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(78),
                Colour = SettingsTheme.PaleCyan,
            },
            new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(34),
                Icon = icon,
                Colour = HomeControlColours.Navy,
            },
            new SpriteIcon
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-8, 8),
                Size = new Vector2(14),
                Icon = FontAwesome.Solid.Plus,
                Colour = HomeControlColours.Pink,
            },
        },
    };

    private static Drawable createComingSoonCard(SettingsPageDefinition page) => new Container
    {
        Position = new Vector2(378, 174),
        Size = new Vector2(840, 118),
        Masking = true,
        CornerRadius = 9,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SettingsTheme.StatusCyan,
            },
            new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 58,
                Size = new Vector2(70),
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 42,
                Size = new Vector2(32),
                Icon = page.Icon,
                Colour = HomeControlColours.Navy,
            },
            new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 118,
                Width = 570,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 7),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = "Coming soon",
                        Font = HomeTypography.Display(22),
                        Colour = HomeControlColours.Navy,
                    },
                    new SpriteText
                    {
                        Text = page.Description,
                        Font = HomeTypography.Body(14),
                        Colour = HomeControlColours.Navy,
                    },
                },
            },
            new Container
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -28,
                Size = new Vector2(112, 32),
                Masking = true,
                CornerRadius = 16,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                    },
                    new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = "PLANNED",
                        Font = HomeTypography.Display(11),
                        Spacing = new Vector2(1.1f, 0),
                        Colour = HomeControlColours.Navy,
                    },
                },
            },
        },
    };

    private static Drawable createSectionList(SettingsPageDefinition page)
    {
        var flow = new FillFlowContainer
        {
            Position = new Vector2(378, 356),
            Width = 840,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 10),
        };

        foreach (string section in page.PlannedSections)
            flow.Add(new SettingsPlaceholderSection(section));

        return flow;
    }

    private static Drawable createDecorationIcon(IconUsage icon, float x, float y, float size, Color4 colour) => new SpriteIcon
    {
        Position = new Vector2(x, y),
        Size = new Vector2(size),
        Icon = icon,
        Colour = colour,
    };
}

internal partial class SettingsPlaceholderSection : CompositeDrawable
{
    public SettingsPlaceholderSection(string title)
    {
        Size = new Vector2(840, 58);
        Masking = true;
        CornerRadius = 7;
        BorderThickness = 1.2f;
        BorderColour = SettingsTheme.Divider;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 20,
                Text = title,
                Font = HomeTypography.Display(16),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -48,
                Text = "Not available yet",
                Font = HomeTypography.Body(12),
                Colour = SettingsTheme.MutedNavy,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -18,
                Size = new Vector2(12),
                Icon = FontAwesome.Solid.Plus,
                Colour = HomeControlColours.Pink,
            },
        };
    }
}
