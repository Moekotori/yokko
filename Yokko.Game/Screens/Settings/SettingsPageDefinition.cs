using System;
using System.Collections.Generic;
using osu.Framework.Graphics.Sprites;

namespace Yokko.Game.Screens.Settings;

internal enum SettingsPageKind
{
    General,
    Display,
    Audio,
    Gameplay,
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
    string Title,
    string Subtitle,
    string Description,
    IconUsage Icon,
    IReadOnlyList<string> PlannedSections);

internal static class SettingsPages
{
    public static SettingsPageDefinition Get(SettingsPageKind kind) => kind switch
    {
        SettingsPageKind.General => new(
            kind,
            "General",
            "Language, startup and application behaviour",
            "The essentials that shape how Yokko starts and behaves.",
            FontAwesome.Solid.Cog,
            new[] { "Language & region", "Startup behaviour", "Updates" }),
        SettingsPageKind.Display => new(
            kind,
            "Display",
            "Window, resolution and interface scale",
            "Display and interface presentation.",
            FontAwesome.Solid.Desktop,
            Array.Empty<string>()),
        SettingsPageKind.Audio => new(
            kind,
            "Audio",
            "Output, latency and synchronisation",
            "Audio controls will live here without mixing playback state into the settings shell.",
            FontAwesome.Solid.VolumeUp,
            new[] { "Output device", "Latency & sync", "Volume" }),
        SettingsPageKind.Gameplay => new(
            kind,
            "Gameplay",
            "Input, timing and playfield feedback",
            "Gameplay preferences will stay separate from chart rules and scoring logic.",
            FontAwesome.Solid.Gamepad,
            new[] { "Input & key bindings", "Timing & judgement", "Visual feedback" }),
        SettingsPageKind.Editor => new(
            kind,
            "Editor",
            "Workspace, snapping and autosave",
            "Editor preferences will be grouped into small, collapsible sections.",
            FontAwesome.Solid.Pen,
            new[] { "Workspace", "Grid & snapping", "Autosave" }),
        SettingsPageKind.Import => new(
            kind,
            "Import",
            "Formats, file handling and conversion",
            "Import preferences will remain independent from the importer implementations.",
            FontAwesome.Solid.FolderOpen,
            new[] { "Supported formats", "Import behaviour", "File locations" }),
        SettingsPageKind.Accessibility => new(
            kind,
            "Accessibility",
            "Visual, motion and input assistance",
            "Accessibility options will be easy to find and safe to change.",
            FontAwesome.Solid.UniversalAccess,
            new[] { "Visual assistance", "Input accessibility", "Reduced motion" }),
        SettingsPageKind.About => new(
            kind,
            "About",
            "Version, credits and licences",
            "Project information and acknowledgements will be collected here.",
            FontAwesome.Solid.InfoCircle,
            new[] { "Version & updates", "Credits", "Licences" }),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
