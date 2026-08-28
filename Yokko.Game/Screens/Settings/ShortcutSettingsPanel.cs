using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Core.Gameplay;
using Yokko.Game.Gameplay;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

internal enum ManiaShortcutPage
{
    Gameplay,
    Editor,
    Menu,
    Results,
    System,
}

internal partial class ShortcutSettingsPanel : CompositeDrawable, ISettingsTransientUi
{
    private readonly YokkoGameplaySettings settings;
    private readonly SettingsContentScrollContainer contentHost;
    private readonly SpriteText statusTitle;
    private readonly SpriteText statusMetadata;
    private readonly SpriteIcon statusIcon;
    private readonly Circle statusIconBackground;
    private ManiaShortcutAction? capturingShortcut;
    private ManiaShortcutPage shortcutPage;
    private Dictionary<ManiaShortcutAction, Key> resetUndoSnapshot;
    private bool resetAllPending;
    private LocalisableString transientStatusTitle;
    private LocalisableString transientStatusMetadata;
    private bool hasTransientStatus;

    internal bool IsCapturingShortcut => capturingShortcut.HasValue;

    internal ManiaShortcutPage CurrentShortcutPage => shortcutPage;

    internal bool IsResetAllPending => resetAllPending;

    internal bool CanUndoResetAll => resetUndoSnapshot != null;

    internal int ModifiedShortcutCount =>
        settings.ModifiedShortcutBindingCount;

    public ShortcutSettingsPanel(YokkoGameplaySettings settings)
    {
        this.settings = settings;
        RelativeSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            SettingsChrome.CreateHeader(
                YokkoStrings.Get("settings.shortcuts.title"),
                YokkoStrings.Get("settings.shortcuts.subtitle"),
                FontAwesome.Solid.Keyboard,
                5),
            createStatusCard(
                out statusTitle,
                out statusMetadata,
                out statusIcon,
                out statusIconBackground),
            contentHost = new SettingsContentScrollContainer
            {
                Position = new Vector2(378, 264),
                Size = new Vector2(840, 352),
            },
            new HomeDotCross
            {
                Position = new Vector2(1088, 594),
                Scale = new Vector2(1.1f),
            },
            createDecorationIcon(
                FontAwesome.Solid.Plus,
                1172,
                601,
                16,
                HomeControlColours.Pink),
            createDecorationIcon(
                FontAwesome.Solid.Plus,
                1200,
                637,
                12,
                HomeControlColours.Yellow),
        };

        settings.BindingsChanged += onBindingsChanged;
        refreshStatusCard();
        setContent(createShortcutsSection(), false);
    }

    internal void BeginShortcutCapture(ManiaShortcutAction action)
    {
        resetAllPending = false;
        clearTransientStatus();
        capturingShortcut = capturingShortcut == action ? null : action;
        refreshStatusCard();
        setContent(createShortcutsSection(), false);
    }

    internal void SelectShortcutPage(ManiaShortcutPage page)
    {
        capturingShortcut = null;
        resetAllPending = false;
        clearTransientStatus();
        shortcutPage = page;
        refreshStatusCard();
        setContent(createShortcutsSection(), true);
    }

    internal void ResetShortcutBinding(ManiaShortcutAction action)
    {
        if (settings.IsShortcutBindingDefault(action))
            return;

        capturingShortcut = null;
        resetAllPending = false;
        resetUndoSnapshot = null;
        ManiaShortcutBindingChange change =
            settings.ResetShortcutBindingWithResult(action);
        showTransientStatus(
            YokkoStrings.Get(
                "settings.shortcuts.reset_one_done",
                shortcutLabel(action)),
            YokkoStrings.Get(
                "settings.shortcuts.binding_now",
                KeyModeBindings.FormatKey(change.NewKey)
                               .ToUpperInvariant()));
        setContent(createShortcutsSection(), false);
    }

    internal void ResetShortcutBindings()
    {
        capturingShortcut = null;
        resetAllPending = false;
        resetUndoSnapshot = null;
        settings.ResetShortcutBindings();
        showTransientStatus(
            YokkoStrings.Get("settings.shortcuts.reset_all_done"),
            YokkoStrings.Get("settings.shortcuts.defaults_active_note"));
        setContent(createShortcutsSection(), false);
    }

    internal void RequestResetShortcutBindings()
    {
        capturingShortcut = null;

        if (resetUndoSnapshot != null)
        {
            UndoResetShortcutBindings();
            return;
        }

        if (settings.ModifiedShortcutBindingCount == 0)
            return;

        if (!resetAllPending)
        {
            resetAllPending = true;
            showTransientStatus(
                YokkoStrings.Get("settings.shortcuts.reset_all_confirm_title"),
                YokkoStrings.Get("settings.shortcuts.reset_all_confirm_note"));
            setContent(createShortcutsSection(), false);
            return;
        }

        resetUndoSnapshot = settings.SupportedShortcutActions.ToDictionary(
            action => action,
            settings.GetShortcutBinding);
        resetAllPending = false;
        settings.ResetShortcutBindings();
        showTransientStatus(
            YokkoStrings.Get("settings.shortcuts.reset_all_done"),
            YokkoStrings.Get("settings.shortcuts.undo_available"));
        setContent(createShortcutsSection(), false);
    }

    internal void UndoResetShortcutBindings()
    {
        if (resetUndoSnapshot == null)
            return;

        Dictionary<ManiaShortcutAction, Key> snapshot =
            resetUndoSnapshot;
        resetUndoSnapshot = null;
        resetAllPending = false;
        foreach ((ManiaShortcutAction action, Key key) in snapshot)
            settings.SetShortcutBinding(action, key);

        showTransientStatus(
            YokkoStrings.Get("settings.shortcuts.reset_undone"),
            YokkoStrings.Get(
                "settings.shortcuts.modified_count",
                settings.ModifiedShortcutBindingCount));
        setContent(createShortcutsSection(), false);
    }

    internal Key GetShortcutBinding(ManiaShortcutAction action) =>
        settings.GetShortcutBinding(action);

    internal bool HandleKeyDown(Key key)
    {
        if (!capturingShortcut.HasValue)
            return false;

        if (key == Key.BackSpace)
        {
            capturingShortcut = null;
            showTransientStatus(
                YokkoStrings.Get("settings.shortcuts.capture_cancelled"),
                YokkoStrings.Get("settings.shortcuts.capture_cancelled_note"));
            setContent(createShortcutsSection(), false);
            return true;
        }

        ManiaShortcutAction action = capturingShortcut.Value;
        ManiaShortcutBindingChange change =
            settings.SetShortcutBindingWithResult(action, key);
        capturingShortcut = null;
        resetUndoSnapshot = null;
        resetAllPending = false;
        if (change.SwappedAction.HasValue)
        {
            showTransientStatus(
                YokkoStrings.Get(
                    "settings.shortcuts.binding_swapped",
                    shortcutLabel(action),
                    shortcutLabel(change.SwappedAction.Value)),
                YokkoStrings.Get(
                    "settings.shortcuts.binding_swapped_note",
                    shortcutLabel(action),
                    KeyModeBindings.FormatKey(change.NewKey)
                                   .ToUpperInvariant(),
                    shortcutLabel(change.SwappedAction.Value),
                    KeyModeBindings.FormatKey(change.PreviousKey)
                                   .ToUpperInvariant()));
        }
        else
        {
            showTransientStatus(
                YokkoStrings.Get(
                    "settings.shortcuts.binding_saved",
                    shortcutLabel(action)),
                YokkoStrings.Get(
                    "settings.shortcuts.binding_now",
                    KeyModeBindings.FormatKey(change.NewKey)
                                   .ToUpperInvariant()));
        }
        setContent(createShortcutsSection(), false);
        return true;
    }

    public bool DismissTransientUi()
    {
        if (!capturingShortcut.HasValue)
        {
            if (!resetAllPending)
                return false;

            resetAllPending = false;
            clearTransientStatus();
            refreshStatusCard();
            setContent(createShortcutsSection(), false);
            return true;
        }

        capturingShortcut = null;
        showTransientStatus(
            YokkoStrings.Get("settings.shortcuts.capture_cancelled"),
            YokkoStrings.Get("settings.shortcuts.capture_cancelled_note"));
        setContent(createShortcutsSection(), false);
        return true;
    }

    private Drawable createShortcutsSection()
    {
        var panel = createPanel();
        var children = new List<Drawable>
        {
            new GameplayCompactButton(
                YokkoStrings.Get("settings.gameplay.shortcuts_gameplay"),
                () => SelectShortcutPage(ManiaShortcutPage.Gameplay),
                112)
            {
                Position = new Vector2(20, 10),
                IsSelected = shortcutPage == ManiaShortcutPage.Gameplay,
            },
            new GameplayCompactButton(
                YokkoStrings.Get("settings.gameplay.shortcuts_menu"),
                () => SelectShortcutPage(ManiaShortcutPage.Menu),
                112)
            {
                Position = new Vector2(140, 10),
                IsSelected = shortcutPage == ManiaShortcutPage.Menu,
            },
            new GameplayCompactButton(
                YokkoStrings.Get("settings.gameplay.shortcuts_results"),
                () => SelectShortcutPage(ManiaShortcutPage.Results),
                112)
            {
                Position = new Vector2(260, 10),
                IsSelected = shortcutPage == ManiaShortcutPage.Results,
            },
            new GameplayCompactButton(
                YokkoStrings.Get("settings.gameplay.shortcuts_editor"),
                () => SelectShortcutPage(ManiaShortcutPage.Editor),
                112)
            {
                Position = new Vector2(380, 10),
                IsSelected = shortcutPage == ManiaShortcutPage.Editor,
            },
            new GameplayCompactButton(
                YokkoStrings.Get("settings.shortcuts.system"),
                () => SelectShortcutPage(ManiaShortcutPage.System),
                112)
            {
                Position = new Vector2(500, 10),
                IsSelected = shortcutPage == ManiaShortcutPage.System,
            },
            new GameplayCompactButton(
                resetAllButtonLabel(),
                RequestResetShortcutBindings,
                176,
                resetUndoSnapshot != null
                    ? FontAwesome.Solid.Undo
                    : FontAwesome.Solid.ArrowLeft)
            {
                Position = new Vector2(644, 10),
                IsSelected = resetAllPending,
                IsEnabled = shortcutPage != ManiaShortcutPage.System
                            && (settings.ModifiedShortcutBindingCount > 0
                                || resetUndoSnapshot != null),
            },
        };

        if (shortcutPage == ManiaShortcutPage.System)
        {
            children.Add(new SpriteText
            {
                Position = new Vector2(20, 88),
                Text = YokkoStrings.Get("settings.desktop.boss_key"),
                Font = HomeTypography.Display(18),
                Colour = HomeControlColours.Navy,
            });
            children.Add(new SpriteText
            {
                Position = new Vector2(20, 122),
                Text = YokkoStrings.Get("settings.shortcuts.system_fixed_hint"),
                Font = HomeTypography.Body(14),
                Colour = SettingsTheme.MutedNavy,
            });
            children.Add(new DesktopShortcutHint(
                "F10",
                FontAwesome.Solid.WindowMinimize,
                300)
            {
                Position = new Vector2(520, 72),
            });
        }
        else
        {
            ManiaShortcutAction[] actions = shortcutActionsForPage(shortcutPage);
            for (int index = 0; index < actions.Length; index++)
            {
                ManiaShortcutAction action = actions[index];
                float y = 62 + index * 44;
                children.Add(new SpriteText
                {
                    Position = new Vector2(20, y + 11),
                    Text = shortcutLabel(action),
                    Font = HomeTypography.Display(15),
                    Colour = HomeControlColours.Navy,
                });
                bool isDefault = settings.IsShortcutBindingDefault(action);
                if (!isDefault)
                {
                    children.Add(new SpriteText
                    {
                        Position = new Vector2(358, y + 11),
                        Text = YokkoStrings.Get("settings.shortcuts.modified"),
                        Font = HomeTypography.Display(14),
                        Colour = HomeControlColours.Pink,
                    });
                }
                children.Add(createShortcutButton(
                    action,
                    new Vector2(520, y),
                    174));
                children.Add(new GameplayCompactButton(
                    YokkoStrings.Get(isDefault
                        ? "settings.shortcuts.is_default"
                        : "settings.gameplay.shortcut_default"),
                    () => ResetShortcutBinding(action),
                    116)
                {
                    Position = new Vector2(704, y),
                    IsEnabled = !isDefault,
                });
            }

            children.Add(new SpriteText
            {
                Position = new Vector2(20, 312),
                Text = YokkoStrings.Get("settings.gameplay.shortcut_hint"),
                Font = HomeTypography.Body(14),
                Colour = SettingsTheme.MutedNavy,
            });
        }

        setPanelChildren(panel, children);
        return panel;
    }

    private LocalisableString resetAllButtonLabel()
    {
        if (resetUndoSnapshot != null)
            return YokkoStrings.Get("settings.shortcuts.undo_reset");

        return YokkoStrings.Get(resetAllPending
            ? "settings.shortcuts.reset_all_confirm"
            : "settings.gameplay.shortcut_reset_all");
    }

    private Drawable createShortcutButton(
        ManiaShortcutAction action,
        Vector2 position,
        float width)
    {
        string label = capturingShortcut == action
            ? YokkoStrings.Get("settings.gameplay.press_key").ToString()
            : KeyModeBindings.FormatKey(
                settings.GetShortcutBinding(action)).ToUpperInvariant();
        return new GameplayCompactButton(
            label,
            () => BeginShortcutCapture(action),
            width,
            FontAwesome.Solid.Keyboard)
        {
            Position = position,
            IsSelected = capturingShortcut == action,
        };
    }

    private static ManiaShortcutAction[] shortcutActionsForPage(
        ManiaShortcutPage page) =>
        page switch
        {
            ManiaShortcutPage.Gameplay =>
            [
                ManiaShortcutAction.PauseOrBack,
                ManiaShortcutAction.SkipIntro,
                ManiaShortcutAction.QuickRetry,
                ManiaShortcutAction.DecreaseScrollSpeed,
                ManiaShortcutAction.IncreaseScrollSpeed,
            ],
            ManiaShortcutPage.Editor =>
            [
                ManiaShortcutAction.ToggleLayoutEditorUi,
            ],
            ManiaShortcutPage.Menu =>
            [
                ManiaShortcutAction.MenuPrevious,
                ManiaShortcutAction.MenuPreviousAlternate,
                ManiaShortcutAction.MenuNext,
                ManiaShortcutAction.MenuNextAlternate,
                ManiaShortcutAction.Confirm,
            ],
            ManiaShortcutPage.Results =>
            [
                ManiaShortcutAction.ConfirmAlternate,
                ManiaShortcutAction.Retry,
                ManiaShortcutAction.WatchReplay,
            ],
            ManiaShortcutPage.System => [],
            _ => throw new ArgumentOutOfRangeException(nameof(page)),
        };

    private static LocalisableString shortcutLabel(
        ManiaShortcutAction action) =>
        YokkoStrings.Get(action switch
        {
            ManiaShortcutAction.PauseOrBack =>
                "settings.gameplay.shortcut_pause_back",
            ManiaShortcutAction.ToggleLayoutEditorUi =>
                "settings.gameplay.shortcut_toggle_layout_editor_ui",
            ManiaShortcutAction.SkipIntro =>
                "settings.gameplay.shortcut_skip_intro",
            ManiaShortcutAction.QuickRetry =>
                "settings.gameplay.shortcut_quick_retry",
            ManiaShortcutAction.DecreaseScrollSpeed =>
                "settings.gameplay.shortcut_decrease_speed",
            ManiaShortcutAction.IncreaseScrollSpeed =>
                "settings.gameplay.shortcut_increase_speed",
            ManiaShortcutAction.MenuPrevious =>
                "settings.gameplay.shortcut_menu_previous",
            ManiaShortcutAction.MenuPreviousAlternate =>
                "settings.gameplay.shortcut_menu_previous_alt",
            ManiaShortcutAction.MenuNext =>
                "settings.gameplay.shortcut_menu_next",
            ManiaShortcutAction.MenuNextAlternate =>
                "settings.gameplay.shortcut_menu_next_alt",
            ManiaShortcutAction.Confirm =>
                "settings.gameplay.shortcut_confirm",
            ManiaShortcutAction.ConfirmAlternate =>
                "settings.gameplay.shortcut_confirm_alt",
            ManiaShortcutAction.Retry =>
                "settings.gameplay.shortcut_retry",
            ManiaShortcutAction.WatchReplay =>
                "settings.gameplay.shortcut_watch_replay",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        });

    private static Container createPanel() => new()
    {
        RelativeSizeAxes = Axes.Both,
        Masking = true,
        CornerRadius = 8,
        BorderThickness = 1.2f,
        BorderColour = SettingsTheme.Divider,
    };

    private static void setPanelChildren(
        Container panel,
        IReadOnlyList<Drawable> children)
    {
        panel.Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
        }.Concat(children).ToArray();
    }

    private void setContent(Drawable content, bool animate)
    {
        content.Alpha = animate ? 0 : 1;
        content.X = animate ? 8 : 0;
        contentHost.Child = content;

        if (!animate)
            return;

        content.FadeIn(150, Easing.OutQuint);
        content.MoveToX(0, 170, Easing.OutQuint);
    }

    private static Drawable createStatusCard(
        out SpriteText title,
        out SpriteText metadata,
        out SpriteIcon icon,
        out Circle iconBackground) => SettingsChrome.CreateStickerFrame(new Container
    {
        Position = new Vector2(378, 150),
        Size = new Vector2(840, 92),
        Masking = true,
        CornerRadius = 9,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SettingsTheme.StatusCyan,
            },
            iconBackground = new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 52,
                Size = new Vector2(60),
                Colour = Color4.White,
            },
            icon = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 52,
                Size = new Vector2(27),
                Icon = FontAwesome.Solid.Keyboard,
                Colour = HomeControlColours.Navy,
            },
            new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 96,
                Width = 700,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 3),
                Children = new Drawable[]
                {
                    title = new SpriteText
                    {
                        Font = HomeTypography.Display(22),
                        Colour = HomeControlColours.Navy,
                    },
                    metadata = new SpriteText
                    {
                        Font = HomeTypography.Body(15),
                        Colour = HomeControlColours.Navy,
                    },
                },
            },
        },
    });

    private void onBindingsChanged()
    {
        if (!hasTransientStatus && !capturingShortcut.HasValue)
            refreshStatusCard();
    }

    private void refreshStatusCard()
    {
        statusIcon.Icon = capturingShortcut.HasValue
            ? FontAwesome.Solid.Keyboard
            : settings.ModifiedShortcutBindingCount > 0
                ? FontAwesome.Solid.Pen
                : FontAwesome.Solid.Check;
        statusIconBackground.Colour = capturingShortcut.HasValue
            ? SettingsTheme.PaleCyan
            : Color4.White;

        if (capturingShortcut.HasValue)
        {
            statusTitle.Text = YokkoStrings.Get(
                "settings.shortcuts.capture_title",
                shortcutLabel(capturingShortcut.Value));
            statusMetadata.Text = YokkoStrings.Get(
                "settings.shortcuts.capture_note");
            return;
        }

        if (hasTransientStatus)
        {
            statusTitle.Text = transientStatusTitle;
            statusMetadata.Text = transientStatusMetadata;
            return;
        }

        int modified = settings.ModifiedShortcutBindingCount;
        statusTitle.Text = YokkoStrings.Get(modified == 0
            ? "settings.shortcuts.defaults_active"
            : "settings.shortcuts.custom_active");
        statusMetadata.Text = YokkoStrings.Get(modified == 0
            ? "settings.shortcuts.defaults_active_note"
            : "settings.shortcuts.modified_count",
            modified);
    }

    private void showTransientStatus(
        LocalisableString title,
        LocalisableString metadata)
    {
        transientStatusTitle = title;
        transientStatusMetadata = metadata;
        hasTransientStatus = true;
        refreshStatusCard();
    }

    private void clearTransientStatus()
    {
        hasTransientStatus = false;
        transientStatusTitle = default;
        transientStatusMetadata = default;
    }

    private static Drawable createDecorationIcon(
        IconUsage icon,
        float x,
        float y,
        float size,
        Color4 colour) => new SpriteIcon
    {
        Position = new Vector2(x, y),
        Size = new Vector2(size),
        Icon = icon,
        Colour = colour,
    };

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            settings.BindingsChanged -= onBindingsChanged;

        base.Dispose(isDisposing);
    }
}
