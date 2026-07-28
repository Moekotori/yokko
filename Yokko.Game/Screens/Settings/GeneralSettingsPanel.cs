using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
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
/// Owns application-wide preferences that do not belong to a specialised
/// subsystem. Locale remains backed by osu!framework's persistent config.
/// </summary>
internal partial class GeneralSettingsPanel : CompositeDrawable
{
    private readonly Bindable<string> locale;
    private readonly List<SettingsSegmentedChoiceButton> languageButtons = new();
    private readonly SpriteText currentLanguage;

    internal string CurrentLocale => locale.Value;

    public GeneralSettingsPanel(Bindable<string> locale)
    {
        this.locale = locale;
        RelativeSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            new SpriteText
            {
                Position = new Vector2(378, 42),
                Text = YokkoStrings.Get("settings.general.title"),
                Font = HomeTypography.Display(58),
                Spacing = new Vector2(0.45f, 0),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(378, 105),
                Text = YokkoStrings.Get("settings.general.subtitle"),
                Font = HomeTypography.Body(17),
                Spacing = new Vector2(0.2f, 0),
                Colour = SettingsTheme.MutedNavy,
            },
            createLanguageStatus(out currentLanguage),
            createDivider(292),
            createSettingRow(
                318,
                YokkoStrings.Get("settings.general.language"),
                createLanguageControl()),
            createDivider(406),
            new SpriteText
            {
                Position = new Vector2(378, 433),
                Width = 840,
                Text = YokkoStrings.Get("settings.general.language_note"),
                Font = HomeTypography.Body(15),
                Colour = SettingsTheme.MutedNavy,
            },
            new SettingsPanelFooter(),
            new HomeDotCross
            {
                Position = new Vector2(1088, 594),
                Scale = new Vector2(1.1f),
            },
            createDecorationIcon(FontAwesome.Solid.Plus, 1172, 601, 16, HomeControlColours.Pink),
            createDecorationIcon(FontAwesome.Solid.Plus, 1200, 637, 12, HomeControlColours.Yellow),
        };

        locale.BindValueChanged(onLocaleChanged, true);
    }

    internal void SelectLanguage(string language)
    {
        if (!YokkoLocale.SUPPORTED.Contains(language))
            throw new ArgumentOutOfRangeException(nameof(language), language, "Unsupported locale.");

        locale.Value = language;
    }

    private Drawable createLanguageControl()
    {
        var options = new[]
        {
            (YokkoLocale.English, YokkoStrings.Get("settings.language.english"), FontAwesome.Solid.Font),
            (YokkoLocale.Chinese, YokkoStrings.Get("settings.language.chinese"), FontAwesome.Solid.Language),
            (YokkoLocale.Japanese, YokkoStrings.Get("settings.language.japanese"), FontAwesome.Solid.GlobeAsia),
        };

        foreach ((string value, LocalisableString label, IconUsage icon) in options)
        {
            string capturedValue = value;
            languageButtons.Add(new SettingsSegmentedChoiceButton(
                label,
                icon,
                () => SelectLanguage(capturedValue),
                199)
            {
                Value = value,
            });
        }

        return new Container
        {
            Size = new Vector2(598, 54),
            Masking = true,
            CornerRadius = 7,
            BorderThickness = 1.4f,
            BorderColour = HomeControlColours.Navy,
            Child = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Children = languageButtons.Cast<Drawable>().ToArray(),
            },
        };
    }

    private static Drawable createLanguageStatus(out SpriteText value)
    {
        var result = new Container
        {
            Position = new Vector2(378, 174),
            Size = new Vector2(840, 86),
            Masking = true,
            CornerRadius = 8,
        };

        result.Children = new Drawable[]
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
                X = 48,
                Size = new Vector2(56),
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 48,
                Size = new Vector2(26),
                Icon = FontAwesome.Solid.Globe,
                Colour = HomeControlColours.Navy,
            },
            new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 105,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = YokkoStrings.Get("settings.general.current_language"),
                        Font = HomeTypography.Display(19),
                        Colour = HomeControlColours.Navy,
                    },
                    value = new SpriteText
                    {
                        Font = HomeTypography.Body(15),
                        Colour = HomeControlColours.Navy,
                    },
                },
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -34,
                Size = new Vector2(44),
                Icon = FontAwesome.Solid.Language,
                Colour = Color4.White,
            },
        };

        return result;
    }

    private static Drawable createSettingRow(float y, LocalisableString title, Drawable control) => new Container
    {
        Position = new Vector2(378, y),
        Size = new Vector2(840, 68),
        Children = new Drawable[]
        {
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = title,
                Font = HomeTypography.Display(23),
                Colour = HomeControlColours.Navy,
            },
            new Container
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Size = new Vector2(598, 54),
                Child = control,
            },
        },
    };

    private static Drawable createDivider(float y) => new Box
    {
        Position = new Vector2(378, y),
        Width = 840,
        Height = 1,
        Colour = SettingsTheme.Divider,
    };

    private static Drawable createDecorationIcon(IconUsage icon, float x, float y, float size, Color4 colour) => new SpriteIcon
    {
        Position = new Vector2(x, y),
        Size = new Vector2(size),
        Icon = icon,
        Colour = colour,
    };

    private void onLocaleChanged(ValueChangedEvent<string> _) => refreshSelection();

    private void refreshSelection()
    {
        currentLanguage.Text = locale.Value switch
        {
            YokkoLocale.Chinese => YokkoStrings.Get("settings.language.chinese"),
            YokkoLocale.Japanese => YokkoStrings.Get("settings.language.japanese"),
            _ => YokkoStrings.Get("settings.language.english"),
        };

        foreach (SettingsSegmentedChoiceButton button in languageButtons)
            button.SetSelected(button.Value is string value && value == locale.Value);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            locale.ValueChanged -= onLocaleChanged;

        base.Dispose(isDisposing);
    }
}
