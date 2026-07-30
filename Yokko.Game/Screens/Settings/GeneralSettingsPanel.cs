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
using Yokko.Game.Gameplay;
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
    private readonly YokkoGameplaySettings gameplaySettings;
    private readonly List<SettingsSegmentedChoiceButton> languageButtons = new();
    private readonly SpriteText currentLanguage;

    internal string CurrentLocale => locale.Value;

    internal double CurrentScrollSpeed =>
        gameplaySettings.ScrollSpeed.Value;

    public GeneralSettingsPanel(
        Bindable<string> locale,
        YokkoGameplaySettings gameplaySettings)
    {
        this.locale = locale;
        this.gameplaySettings = gameplaySettings;
        RelativeSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            SettingsChrome.CreateHeader(
                YokkoStrings.Get("settings.general.title"),
                YokkoStrings.Get("settings.general.subtitle"),
                FontAwesome.Solid.Cog,
                1),
            SettingsChrome.CreateStatusCard(
                174,
                FontAwesome.Solid.Globe,
                YokkoStrings.Get("settings.general.current_language"),
                FontAwesome.Solid.Language,
                out currentLanguage),
            SettingsChrome.CreateDivider(292),
            SettingsChrome.CreateSettingRow(
                318,
                YokkoStrings.Get("settings.general.language"),
                createLanguageControl()),
            SettingsChrome.CreateDivider(406),
            SettingsChrome.CreateSettingRow(
                424,
                YokkoStrings.Get(
                    "settings.general.mania_scroll_speed"),
                new GameplayValueStepper(
                    gameplaySettings.ScrollSpeed,
                    OsuManiaScrollSpeed.ShortcutStep,
                    OsuManiaScrollSpeed.Minimum,
                    OsuManiaScrollSpeed.Maximum,
                    formatScrollSpeed)
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                }),
            new SpriteText
            {
                Position = new Vector2(378, 496),
                Width = 840,
                Text = YokkoStrings.Get("settings.general.language_note"),
                Font = HomeTypography.Body(15),
                Colour = SettingsTheme.MutedNavy,
            },
            new SpriteText
            {
                Position = new Vector2(378, 521),
                Width = 840,
                Text = YokkoStrings.Get(
                    "settings.general.mania_scroll_speed_note"),
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

    internal void SetScrollSpeed(double speed) =>
        gameplaySettings.SetScrollSpeed(speed);

    private static string formatScrollSpeed(double speed) =>
        $"{(int)OsuManiaScrollSpeed.ComputeScrollTime(speed)} ms  ·  {speed:0.0}";

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

        return SettingsChrome.CreateSegmentedControl(languageButtons.Cast<Drawable>());
    }

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
