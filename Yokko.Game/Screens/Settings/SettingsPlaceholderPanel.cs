using System.Collections.Generic;
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

/// <summary>
/// Honest placeholder for settings categories whose behaviour is not implemented
/// yet. The collapsed section layout is also the structure future controls plug into.
/// </summary>
internal partial class SettingsPlaceholderPanel : CompositeDrawable, ISettingsTransientUi
{
    private readonly List<SettingsPlaceholderSection> sections = new();
    private SettingsPlaceholderSection expandedSection;

    internal int ExpandedSectionIndex => expandedSection == null ? -1 : sections.IndexOf(expandedSection);

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
                Text = YokkoStrings.Get("settings.planned_sections"),
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
                Origin = Anchor.Centre,
                X = 58,
                Size = new Vector2(70),
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 58,
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
                        Text = YokkoStrings.Get("settings.coming_soon"),
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
                        Text = YokkoStrings.Get("settings.planned"),
                        Font = HomeTypography.Display(11),
                        Spacing = new Vector2(1.1f, 0),
                        Colour = HomeControlColours.Navy,
                    },
                },
            },
        },
    };

    public bool DismissTransientUi()
    {
        if (expandedSection == null)
            return false;

        expandedSection.SetExpanded(false);
        expandedSection = null;
        return true;
    }

    private Drawable createSectionList(SettingsPageDefinition page)
    {
        var flow = new FillFlowContainer
        {
            Position = new Vector2(378, 356),
            Width = 840,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 10),
        };

        foreach (LocalisableString section in page.PlannedSections)
        {
            var row = new SettingsPlaceholderSection(section, toggleSection);
            sections.Add(row);
            flow.Add(row);
        }

        return flow;
    }

    internal void ToggleSection(int index) => toggleSection(sections[index]);

    private void toggleSection(SettingsPlaceholderSection section)
    {
        if (expandedSection == section)
        {
            section.SetExpanded(false);
            expandedSection = null;
            return;
        }

        expandedSection?.SetExpanded(false);
        expandedSection = section;
        expandedSection.SetExpanded(true);
    }

    private static Drawable createDecorationIcon(IconUsage icon, float x, float y, float size, Color4 colour) => new SpriteIcon
    {
        Position = new Vector2(x, y),
        Size = new Vector2(size),
        Icon = icon,
        Colour = colour,
    };
}

internal partial class SettingsPlaceholderSection : ClickableContainer
{
    private readonly Box background;
    private readonly Box divider;
    private readonly SpriteText detail;
    private readonly SpriteText stateText;
    private readonly SpriteIcon plus;
    private bool expanded;

    internal bool IsExpanded => expanded;

    public SettingsPlaceholderSection(LocalisableString title, System.Action<SettingsPlaceholderSection> onToggle)
    {
        Action = () => onToggle(this);
        Size = new Vector2(840, 58);
        Masking = true;
        CornerRadius = 7;
        BorderThickness = 1.2f;
        BorderColour = SettingsTheme.Divider;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new SpriteText
            {
                Position = new Vector2(20, 18),
                Text = title,
                Font = HomeTypography.Display(16),
                Colour = HomeControlColours.Navy,
            },
            stateText = new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-48, 20),
                Text = YokkoStrings.Get("settings.not_available"),
                Font = HomeTypography.Body(12),
                Colour = SettingsTheme.MutedNavy,
            },
            plus = new SpriteIcon
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-18, 22),
                Size = new Vector2(12),
                Icon = FontAwesome.Solid.Plus,
                Colour = HomeControlColours.Pink,
            },
            divider = new Box
            {
                Position = new Vector2(20, 57),
                Width = 800,
                Height = 1,
                Colour = SettingsTheme.Divider,
                Alpha = 0,
            },
            detail = new SpriteText
            {
                Position = new Vector2(20, 72),
                Text = YokkoStrings.Get("settings.future_section"),
                Font = HomeTypography.Body(13),
                Colour = SettingsTheme.MutedNavy,
                Alpha = 0,
            },
        };
    }

    public void SetExpanded(bool isExpanded)
    {
        expanded = isExpanded;
        this.ResizeHeightTo(expanded ? 106 : 58, 180, Easing.OutQuint);
        background.FadeColour(expanded ? SettingsTheme.PaleCyan : Color4.White, 150, Easing.OutQuint);
        divider.FadeTo(expanded ? 1 : 0, 120, Easing.OutQuint);
        detail.FadeTo(expanded ? 1 : 0, 140, Easing.OutQuint);
        stateText.FadeTo(expanded ? 0 : 1, 100, Easing.OutQuint);
        plus.RotateTo(expanded ? 45 : 0, 160, Easing.OutQuint);
    }

    protected override bool OnHover(osu.Framework.Input.Events.HoverEvent e)
    {
        if (!expanded)
            background.FadeColour(SettingsTheme.PaleCyan, 120, Easing.OutQuint);

        return true;
    }

    protected override void OnHoverLost(osu.Framework.Input.Events.HoverLostEvent e)
    {
        if (!expanded)
            background.FadeColour(Color4.White, 140, Easing.OutQuint);
    }
}
