using System;
using System.Collections.Generic;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using Yokko.Game.Localisation;

namespace Yokko.Game.Screens.Settings;

internal enum SettingsPageKind
{
    General,
    Display,
    Audio,
    Gameplay,
    Shortcuts,
    Skins,
    Editor,
    Import,
    Accessibility,
    About,
}

internal interface ISettingsTransientUi
{
    bool DismissTransientUi();
}

internal sealed record SettingsPageDefinition(
    SettingsPageKind Kind,
    LocalisableString Title,
    LocalisableString Subtitle,
    LocalisableString Description,
    IconUsage Icon,
    IReadOnlyList<LocalisableString> PlannedSections,
    string SearchTerms);

internal static class SettingsPages
{
    public static SettingsPageDefinition Get(SettingsPageKind kind) => kind switch
    {
        SettingsPageKind.General => new(
            kind,
            YokkoStrings.Get("settings.general.title"),
            YokkoStrings.Get("settings.general.subtitle"),
            YokkoStrings.Get("settings.general.description"),
            FontAwesome.Solid.Cog,
            new[]
            {
                YokkoStrings.Get("settings.general.section_language"),
                YokkoStrings.Get("settings.general.section_startup"),
                YokkoStrings.Get("settings.general.section_updates"),
            },
            YokkoStrings.SearchTerms(
                "settings.general.title",
                "settings.general.subtitle",
                "settings.general.section_language",
                "settings.general.section_startup",
                "settings.general.section_updates",
                "settings.general.mania_scroll_speed")),
        SettingsPageKind.Display => new(
            kind,
            YokkoStrings.Get("settings.display.title"),
            YokkoStrings.Get("settings.display.subtitle"),
            YokkoStrings.Get("settings.display.description"),
            FontAwesome.Solid.Desktop,
            Array.Empty<LocalisableString>(),
            YokkoStrings.SearchTerms(
                "settings.display.title",
                "settings.display.subtitle",
                "settings.display.window_mode",
                "settings.display.resolution",
                "settings.display.performance_readout")),
        SettingsPageKind.Audio => new(
            kind,
            YokkoStrings.Get("settings.audio.title"),
            YokkoStrings.Get("settings.audio.subtitle"),
            YokkoStrings.Get("settings.audio.description"),
            FontAwesome.Solid.VolumeUp,
            new[]
            {
                YokkoStrings.Get("settings.audio.backend"),
                YokkoStrings.Get("settings.audio.device"),
                YokkoStrings.Get("settings.audio.buffer"),
                YokkoStrings.Get("settings.audio.offset"),
            },
            YokkoStrings.SearchTerms(
                "settings.audio.title",
                "settings.audio.subtitle",
                "settings.audio.backend",
                "settings.audio.device",
                "settings.audio.buffer",
                "settings.audio.offset",
                "settings.audio.exclusive",
                "settings.audio.shared")),
        SettingsPageKind.Gameplay => new(
            kind,
            YokkoStrings.Get("settings.gameplay.title"),
            YokkoStrings.Get("settings.gameplay.subtitle"),
            YokkoStrings.Get("settings.gameplay.description"),
            FontAwesome.Solid.Gamepad,
            new[]
            {
                YokkoStrings.Get("settings.gameplay.section_input"),
                YokkoStrings.Get("settings.gameplay.section_timing"),
                YokkoStrings.Get("settings.gameplay.section_judgement"),
                YokkoStrings.Get("settings.gameplay.section_feedback"),
            },
            YokkoStrings.SearchTerms(
                "settings.gameplay.title",
                "settings.gameplay.subtitle",
                "settings.gameplay.section_input",
                "settings.gameplay.section_timing",
                "settings.gameplay.section_judgement",
                "settings.gameplay.etterna_justice",
                "settings.gameplay.section_feedback",
                "settings.gameplay.mines")),
        SettingsPageKind.Shortcuts => new(
            kind,
            YokkoStrings.Get("settings.shortcuts.title"),
            YokkoStrings.Get("settings.shortcuts.subtitle"),
            YokkoStrings.Get("settings.shortcuts.description"),
            FontAwesome.Solid.Keyboard,
            new[]
            {
                YokkoStrings.Get("settings.gameplay.shortcuts_gameplay"),
                YokkoStrings.Get("settings.gameplay.shortcuts_menu"),
                YokkoStrings.Get("settings.gameplay.shortcuts_results"),
            },
            YokkoStrings.SearchTerms(
                "settings.shortcuts.title",
                "settings.shortcuts.subtitle",
                "settings.gameplay.shortcuts_gameplay",
                "settings.gameplay.shortcuts_menu",
                "settings.gameplay.shortcuts_results",
                "settings.gameplay.shortcut_pause_back",
                "settings.gameplay.shortcut_quick_retry",
                "settings.gameplay.shortcut_retry",
                "settings.gameplay.shortcut_watch_replay")),
        SettingsPageKind.Skins => new(
            kind,
            YokkoStrings.Get("settings.skins.title"),
            YokkoStrings.Get("settings.skins.subtitle"),
            YokkoStrings.Get("settings.skins.description"),
            FontAwesome.Solid.PaintBrush,
            new[]
            {
                YokkoStrings.Get("settings.skins.section_library"),
                YokkoStrings.Get("settings.skins.section_import"),
            },
            YokkoStrings.SearchTerms(
                "settings.skins.title",
                "settings.skins.subtitle",
                "settings.skins.section_library",
                "settings.skins.section_import")),
        SettingsPageKind.Editor => new(
            kind,
            YokkoStrings.Get("settings.editor.title"),
            YokkoStrings.Get("settings.editor.subtitle"),
            YokkoStrings.Get("settings.editor.description"),
            FontAwesome.Solid.Pen,
            new[]
            {
                YokkoStrings.Get("settings.editor.section_workspace"),
                YokkoStrings.Get("settings.editor.section_grid"),
                YokkoStrings.Get("settings.editor.section_autosave"),
            },
            YokkoStrings.SearchTerms(
                "settings.editor.title",
                "settings.editor.subtitle",
                "settings.editor.section_workspace",
                "settings.editor.section_grid",
                "settings.editor.section_autosave")),
        SettingsPageKind.Import => new(
            kind,
            YokkoStrings.Get("settings.import.title"),
            YokkoStrings.Get("settings.import.subtitle"),
            YokkoStrings.Get("settings.import.description"),
            FontAwesome.Solid.FolderOpen,
            new[]
            {
                YokkoStrings.Get("settings.import.section_formats"),
                YokkoStrings.Get("settings.import.section_behaviour"),
                YokkoStrings.Get("settings.import.section_locations"),
            },
            YokkoStrings.SearchTerms(
                "settings.import.title",
                "settings.import.subtitle",
                "settings.import.section_formats",
                "settings.import.section_behaviour",
                "settings.import.section_locations")),
        SettingsPageKind.Accessibility => new(
            kind,
            YokkoStrings.Get("settings.accessibility.title"),
            YokkoStrings.Get("settings.accessibility.subtitle"),
            YokkoStrings.Get("settings.accessibility.description"),
            FontAwesome.Solid.UniversalAccess,
            new[]
            {
                YokkoStrings.Get("settings.accessibility.section_visual"),
                YokkoStrings.Get("settings.accessibility.section_input"),
                YokkoStrings.Get("settings.accessibility.section_motion"),
            },
            YokkoStrings.SearchTerms(
                "settings.accessibility.title",
                "settings.accessibility.subtitle",
                "settings.accessibility.section_visual",
                "settings.accessibility.section_input",
                "settings.accessibility.section_motion")),
        SettingsPageKind.About => new(
            kind,
            YokkoStrings.Get("settings.about.title"),
            YokkoStrings.Get("settings.about.subtitle"),
            YokkoStrings.Get("settings.about.description"),
            FontAwesome.Solid.InfoCircle,
            new[]
            {
                YokkoStrings.Get("settings.about.section_version"),
                YokkoStrings.Get("settings.about.section_credits"),
                YokkoStrings.Get("settings.about.section_licences"),
            },
            YokkoStrings.SearchTerms(
                "settings.about.title",
                "settings.about.subtitle",
                "settings.about.section_version",
                "settings.about.section_credits",
                "settings.about.section_licences")),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
