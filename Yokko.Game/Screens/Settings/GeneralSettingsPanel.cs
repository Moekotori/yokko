using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Game.Audio;
using Yokko.Game.Configuration;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

/// <summary>
/// Owns application-wide preferences that do not belong to a specialised
/// subsystem. Locale remains backed by osu!framework's persistent config.
/// </summary>
internal partial class GeneralSettingsPanel
    : CompositeDrawable, ISettingsSearchTarget, ISettingsTransientUi
{
    private readonly Bindable<string> locale;
    private readonly Bindable<string> playerDisplayName;
    private readonly Bindable<string> playerId;
    private readonly List<SettingsSegmentedChoiceButton> languageButtons = new();
    private readonly SettingsContentScrollContainer contentScroll;
    private readonly Container contentRoot;
    private readonly SettingsPlayerNameField playerNameField;
    private readonly SpriteText currentLanguage;
    private readonly SpriteText playerIdText;
    private readonly SpriteText versionText;

    internal string CurrentLocale => locale.Value;

    public GeneralSettingsPanel(
        Bindable<string> locale,
        YokkoAudioSettings audioSettings,
        YokkoStartupSettings startupSettings,
        YokkoPrivacySettings privacySettings,
        YokkoConfigManager yokkoConfig,
        Clipboard clipboard,
        BindableBool showDebugConsole,
        Action onOpenCalibration)
    {
        this.locale = locale;
        playerDisplayName = yokkoConfig.GetBindable<string>(
            YokkoSetting.PlayerDisplayName);
        playerId = yokkoConfig.GetBindable<string>(YokkoSetting.PlayerId);
        RelativeSizeAxes = Axes.Both;

        string version = Assembly.GetEntryAssembly()?
                                 .GetName().Version?.ToString()
                         ?? "development";

        var content = contentRoot = new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Children = new Drawable[]
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
                SettingsChrome.CreateDivider(270),
                SettingsChrome.CreateSettingRow(
                    276,
                    YokkoStrings.Get("settings.general.language"),
                    createLanguageControl()),
                SettingsChrome.CreateDivider(338),
                SettingsChrome.CreateSettingRow(
                    344,
                    YokkoStrings.Get("settings.general.home_music"),
                    new SettingsBooleanToggle(
                        audioSettings.HomeMusicEnabled,
                        "settings.general.enabled",
                        "settings.general.disabled")),
                SettingsChrome.CreateDivider(406),
                SettingsChrome.CreateSettingRow(
                    412,
                    YokkoStrings.Get("settings.general.player_name"),
                    playerNameField = new SettingsPlayerNameField(playerDisplayName)),
                playerIdText = new SpriteText
                {
                    Position = new Vector2(378, 468),
                    Width = 840,
                    Font = HomeTypography.Body(15),
                    Colour = SettingsTheme.MutedNavy,
                },
                SettingsChrome.CreateDivider(494),
                SettingsChrome.CreateSettingRow(
                    500,
                    YokkoStrings.Get("settings.general.version"),
                    versionText = new SpriteText
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Text = version,
                        Font = HomeTypography.Display(19),
                        Colour = HomeControlColours.Navy,
                    }),
                new SpriteText
                {
                    Position = new Vector2(378, 556),
                    Width = 840,
                    Text = YokkoStrings.Get("settings.general.updates_note"),
                    Font = HomeTypography.Body(15),
                    Colour = SettingsTheme.MutedNavy,
                },
                SettingsChrome.CreateDivider(582),
                SettingsChrome.CreateSettingRow(
                    588,
                    YokkoStrings.Get("settings.general.debug_console"),
                    new SettingsBooleanToggle(
                        showDebugConsole,
                        "settings.general.debug_console_visible",
                        "settings.general.debug_console_hidden")),
                SettingsChrome.CreateDivider(612),
                SettingsChrome.CreateSettingRow(
                    620,
                    YokkoStrings.Get("settings.general.open_last_screen"),
                    new SettingsBooleanToggle(
                        startupSettings.OpenLastScreen,
                        "settings.general.enabled",
                        "settings.general.disabled")),
                SettingsChrome.CreateDivider(682),
                new SpriteText
                {
                    Position = new Vector2(SettingsChrome.ContentX, 680),
                    Text = YokkoStrings.Get("settings.general.configuration_backup"),
                    Font = HomeTypography.Display(25),
                    Colour = HomeControlColours.Navy,
                },
                new SettingsConfigActionsControl(yokkoConfig, clipboard)
                {
                    Position = new Vector2(SettingsChrome.ContentX, 728),
                },
                SettingsChrome.CreateDivider(830),
                new SpriteText
                {
                    Position = new Vector2(SettingsChrome.ContentX, 800),
                    Text = YokkoStrings.Get("settings.general.section_privacy"),
                    Font = HomeTypography.Display(25),
                    Colour = HomeControlColours.Navy,
                },
                SettingsChrome.CreateSettingRow(
                    836,
                    YokkoStrings.Get("settings.general.save_local_replays"),
                    new SettingsBooleanToggle(
                        privacySettings.SaveLocalReplays,
                        "settings.general.enabled",
                        "settings.general.disabled")),
                SettingsChrome.CreateDivider(898),
                SettingsChrome.CreateSettingRow(
                    904,
                    YokkoStrings.Get("settings.general.include_username_in_exports"),
                    new SettingsBooleanToggle(
                        privacySettings.IncludeUsernameInExports,
                        "settings.general.enabled",
                        "settings.general.disabled")),
                SettingsChrome.CreateDivider(966),
                SettingsChrome.CreateSettingRow(
                    972,
                    YokkoStrings.Get("settings.gameplay.calibration_start"),
                    new SettingsOutlineButton(
                        YokkoStrings.Get("settings.gameplay.calibration_start"),
                        FontAwesome.Solid.Microphone,
                        onOpenCalibration)),
            },
        };

        InternalChild = contentScroll = new SettingsContentScrollContainer
        {
            RelativeSizeAxes = Axes.Both,
            Child = content,
        };

        locale.BindValueChanged(onLocaleChanged, true);
        playerId.BindValueChanged(onPlayerIdChanged, true);
    }

    public bool TryFocusSearchItem(string itemId) =>
        SettingsSearchScroll.TryFocus(
            SettingsPageKind.General,
            itemId,
            contentScroll,
            contentRoot);

    public bool DismissTransientUi()
    {
        if (!playerNameField.HasFocus)
            return false;

        GetContainingFocusManager()?.ChangeFocus(this);
        return true;
    }

    internal string CurrentPlayerId => playerId.Value;

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

        return SettingsChrome.CreateSegmentedControl(languageButtons.Cast<Drawable>());
    }

    private void onLocaleChanged(ValueChangedEvent<string> _) => refreshSelection();

    private void onPlayerIdChanged(ValueChangedEvent<string> change) =>
        playerIdText.Text = YokkoStrings.Get(
            "settings.general.player_id_value",
            change.NewValue);

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
        {
            locale.ValueChanged -= onLocaleChanged;
            playerId.ValueChanged -= onPlayerIdChanged;
        }

        base.Dispose(isDisposing);
    }
}

internal partial class SettingsPlayerNameField : BasicTextBox
{
    private readonly Bindable<string> playerDisplayName;
    private bool synchronizing;

    protected override float LeftRightPadding => 16;

    internal SettingsPlayerNameField(Bindable<string> playerDisplayName)
    {
        this.playerDisplayName = playerDisplayName;
        Size = new Vector2(SettingsChrome.ControlWidth, SettingsChrome.ControlHeight);
        Masking = true;
        CornerRadius = 8;
        BorderThickness = 1.5f;
        BorderColour = HomeControlColours.Navy;
        BackgroundUnfocused = Color4.White;
        BackgroundFocused = SettingsTheme.PaleCyan;
        FontSize = 18;

        playerDisplayName.BindValueChanged(onPlayerNameChanged, true);
    }

    protected override Drawable GetDrawableCharacter(char c) => new SpriteText
    {
        Text = c.ToString(),
        Font = HomeTypography.Control(18),
        Colour = HomeControlColours.Navy,
    };

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        BorderColour = HomeControlColours.Cyan;
        BorderThickness = 2;
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        BorderColour = HomeControlColours.Navy;
        BorderThickness = 1.5f;
        commit();
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key is Key.Enter or Key.KeypadEnter)
        {
            commit();
            return true;
        }

        return base.OnKeyDown(e);
    }

    private void onPlayerNameChanged(ValueChangedEvent<string> change)
    {
        if (synchronizing || HasFocus)
            return;

        synchronizing = true;
        Current.Value = change.NewValue ?? string.Empty;
        synchronizing = false;
    }

    private void commit() => commit(Current.Value);

    private void commit(string value)
    {
        string normalized = normalize(value);
        synchronizing = true;
        Current.Value = normalized;
        playerDisplayName.Value = normalized;
        synchronizing = false;
    }

    private static string normalize(string value)
    {
        string trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length > 32)
            trimmed = trimmed[..32];

        return trimmed.Length == 0
            ? YokkoStrings.Get("settings.general.default_player_name").ToString()
            : trimmed.ToUpperInvariant();
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            playerDisplayName.ValueChanged -= onPlayerNameChanged;

        base.Dispose(isDisposing);
    }
}
