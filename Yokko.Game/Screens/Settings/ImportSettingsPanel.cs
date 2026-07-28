using System;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Importing;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;
using Yokko.Import;

namespace Yokko.Game.Screens.Settings;

/// <summary>
/// Presents importer capabilities from the registry and owns the preferences
/// that affect package selection, keysound preservation and warning display.
/// </summary>
internal partial class ImportSettingsPanel : CompositeDrawable
{
    private readonly YokkoImportSettings settings;

    internal int FormatFamilyCount => KnownChartImporters.Capabilities.Count;
    internal int FileTypeCount => KnownChartImporters.FileExtensions.Length;
    internal bool PreferKeysounds => settings.PreferKeysounds.Value;
    internal bool PreferSscSimfiles => settings.PreferSscSimfiles.Value;
    internal bool ShowCompatibilityWarnings => settings.ShowCompatibilityWarnings.Value;

    public ImportSettingsPanel(YokkoImportSettings settings)
    {
        this.settings = settings;
        RelativeSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            new SpriteText
            {
                Position = new Vector2(378, 42),
                Text = YokkoStrings.Get("settings.import.title"),
                Font = HomeTypography.Display(58),
                Spacing = new Vector2(0.45f, 0),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(378, 105),
                Text = YokkoStrings.Get("settings.import.subtitle"),
                Font = HomeTypography.Body(20),
                Spacing = new Vector2(0.2f, 0),
                Colour = SettingsTheme.MutedNavy,
            },
            createCategoryMark(),
            createImporterStatus(),
            new SpriteText
            {
                Position = new Vector2(378, 251),
                Text = YokkoStrings.Get("settings.import.section_formats"),
                Font = HomeTypography.Display(23),
                Colour = HomeControlColours.Navy,
            },
            createFormatCards(),
            new SpriteText
            {
                Position = new Vector2(378, 380),
                Text = YokkoStrings.Get("settings.import.section_behaviour"),
                Font = HomeTypography.Display(23),
                Colour = HomeControlColours.Navy,
            },
            createBehaviourCards(),
            createLocationNote(),
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

    internal void SetPreferKeysounds(bool value) => settings.PreferKeysounds.Value = value;

    internal void SetPreferSscSimfiles(bool value) => settings.PreferSscSimfiles.Value = value;

    internal void SetShowCompatibilityWarnings(bool value) => settings.ShowCompatibilityWarnings.Value = value;

    private static Drawable createCategoryMark() => new Container
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
                Icon = FontAwesome.Solid.FolderOpen,
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

    private Drawable createImporterStatus() => new Container
    {
        Position = new Vector2(378, 157),
        Size = new Vector2(840, 72),
        Masking = true,
        CornerRadius = 8,
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
                X = 45,
                Size = new Vector2(50),
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 45,
                Size = new Vector2(23),
                Icon = FontAwesome.Solid.Check,
                Colour = HomeControlColours.Navy,
            },
            new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 88,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = YokkoStrings.Get("settings.import.status_title"),
                        Font = HomeTypography.Display(21),
                        Colour = HomeControlColours.Navy,
                    },
                    new SpriteText
                    {
                        Text = YokkoStrings.Get(
                            "settings.import.status_metadata",
                            FormatFamilyCount,
                            FileTypeCount),
                        Font = HomeTypography.Body(16),
                        Colour = HomeControlColours.Navy,
                    },
                },
            },
            createStatusBadge(),
        },
    };

    private static Drawable createStatusBadge() => new Container
    {
        Anchor = Anchor.CentreRight,
        Origin = Anchor.CentreRight,
        X = -22,
        Size = new Vector2(116, 30),
        Masking = true,
        CornerRadius = 15,
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
                Text = YokkoStrings.Get("settings.import.ready"),
                Font = HomeTypography.Display(14),
                Spacing = new Vector2(0.8f, 0),
                Colour = HomeControlColours.Navy,
            },
        },
    };

    private static Drawable createFormatCards()
    {
        var flow = new FillFlowContainer
        {
            Position = new Vector2(378, 284),
            Size = new Vector2(840, 70),
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(10, 0),
        };

        float[] widths = [140, 140, 140, 210, 170];
        string[] names = ["osu!mania", "Quaver", "Malody", "Etterna / StepMania", "BMS"];
        ChartImportCapability[] capabilities = KnownChartImporters.Capabilities.ToArray();

        for (int index = 0; index < capabilities.Length; index++)
        {
            ChartImportCapability capability = capabilities[index];
            flow.Add(new ImportFormatCard(
                names[Math.Min(index, names.Length - 1)],
                string.Join("  ", capability.FileExtensions),
                capability.Format == Yokko.Core.Beatmaps.ChartSourceFormat.Bms,
                widths[Math.Min(index, widths.Length - 1)]));
        }

        return flow;
    }

    private Drawable createBehaviourCards() => new FillFlowContainer
    {
        Position = new Vector2(378, 413),
        Size = new Vector2(840, 106),
        Direction = FillDirection.Horizontal,
        Spacing = new Vector2(15, 0),
        Children = new Drawable[]
        {
            new ImportPreferenceCard(
                YokkoStrings.Get("settings.import.prefer_keysounds"),
                YokkoStrings.Get("settings.import.prefer_keysounds_note"),
                FontAwesome.Solid.Keyboard,
                settings.PreferKeysounds),
            new ImportPreferenceCard(
                YokkoStrings.Get("settings.import.prefer_ssc"),
                YokkoStrings.Get("settings.import.prefer_ssc_note"),
                FontAwesome.Solid.Bars,
                settings.PreferSscSimfiles),
            new ImportPreferenceCard(
                YokkoStrings.Get("settings.import.show_warnings"),
                YokkoStrings.Get("settings.import.show_warnings_note"),
                FontAwesome.Solid.InfoCircle,
                settings.ShowCompatibilityWarnings),
        },
    };

    private static Drawable createLocationNote() => new Container
    {
        Position = new Vector2(378, 543),
        Size = new Vector2(840, 62),
        Masking = true,
        CornerRadius = 7,
        BorderThickness = 1,
        BorderColour = SettingsTheme.Divider,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SettingsTheme.PaleCyan,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 28,
                Size = new Vector2(20),
                Icon = FontAwesome.Solid.FolderOpen,
                Colour = HomeControlColours.Pink,
            },
            new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 58,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = YokkoStrings.Get("settings.import.section_locations"),
                        Font = HomeTypography.Display(17),
                        Colour = HomeControlColours.Navy,
                    },
                    new SpriteText
                    {
                        Text = YokkoStrings.Get("settings.import.location_note"),
                        Font = HomeTypography.Body(14),
                        Colour = SettingsTheme.MutedNavy,
                    },
                },
            },
        },
    };

    private static Drawable createDecorationIcon(IconUsage icon, float x, float y, float size, Color4 colour) => new SpriteIcon
    {
        Position = new Vector2(x, y),
        Size = new Vector2(size),
        Icon = icon,
        Colour = colour,
    };
}

internal partial class ImportFormatCard : CompositeDrawable
{
    public ImportFormatCard(string name, string extensions, bool partialSupport, float width)
    {
        Size = new Vector2(width, 70);
        Masking = true;
        CornerRadius = 7;
        BorderThickness = 1;
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
                Position = new Vector2(12, 10),
                Text = name,
                Font = HomeTypography.Display(16),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(12, 37),
                Text = extensions,
                Font = HomeTypography.Body(14),
                Colour = SettingsTheme.MutedNavy,
            },
            new Circle
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-13, 13),
                Size = new Vector2(19),
                Colour = partialSupport ? HomeControlColours.Yellow : SettingsTheme.StatusCyan,
            },
            new SpriteIcon
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-13, 13),
                Size = new Vector2(10),
                Icon = partialSupport ? FontAwesome.Solid.InfoCircle : FontAwesome.Solid.Check,
                Colour = HomeControlColours.Navy,
            },
        };
    }
}

internal partial class ImportPreferenceCard : ClickableContainer
{
    private readonly BindableBool value;
    private readonly Box background;
    private readonly Box switchTrack;
    private readonly Circle switchThumb;
    private readonly SpriteText state;

    public ImportPreferenceCard(
        LocalisableString title,
        LocalisableString note,
        IconUsage icon,
        BindableBool value)
    {
        this.value = value;
        Action = () => value.Value = !value.Value;
        Size = new Vector2(270, 106);
        Masking = true;
        CornerRadius = 8;
        BorderThickness = 1.2f;
        BorderColour = SettingsTheme.Divider;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Position = new Vector2(15, 14),
                Size = new Vector2(18),
                Icon = icon,
                Colour = HomeControlColours.Pink,
            },
            new SpriteText
            {
                Position = new Vector2(43, 12),
                Text = title,
                Font = HomeTypography.Display(17),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(15, 43),
                Text = note,
                Font = HomeTypography.Body(14),
                Colour = SettingsTheme.MutedNavy,
            },
            new Container
            {
                Position = new Vector2(15, 74),
                Size = new Vector2(48, 24),
                Masking = true,
                CornerRadius = 12,
                Children = new Drawable[]
                {
                    switchTrack = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = SettingsTheme.Divider,
                    },
                    switchThumb = new Circle
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.Centre,
                        X = 12,
                        Size = new Vector2(18),
                        Colour = Color4.White,
                    },
                },
            },
            state = new SpriteText
            {
                Position = new Vector2(74, 76),
                Font = HomeTypography.Display(14),
                Colour = HomeControlColours.Navy,
            },
        };

        value.BindValueChanged(onValueChanged, true);
    }

    private void onValueChanged(ValueChangedEvent<bool> change)
    {
        switchTrack.FadeColour(change.NewValue ? HomeControlColours.Navy : SettingsTheme.Divider, 120, Easing.OutQuint);
        switchThumb.MoveToX(change.NewValue ? 36 : 12, 120, Easing.OutQuint);
        state.Text = YokkoStrings.Get(change.NewValue
            ? "settings.import.enabled"
            : "settings.import.disabled");
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(SettingsTheme.PaleCyan, 120, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        background.FadeColour(Color4.White, 140, Easing.OutQuint);

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            value.ValueChanged -= onValueChanged;

        base.Dispose(isDisposing);
    }
}
