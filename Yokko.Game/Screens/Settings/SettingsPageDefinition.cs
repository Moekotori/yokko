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
    string TitleSearchTerms,
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
            YokkoStrings.SearchTerms("settings.general.title"),
            YokkoStrings.SearchTermsForPrefix(
                "settings.general.",
                "ui interface locale startup update scroll speed",
                "界面 语言 启动 更新 滚速")),
        SettingsPageKind.Display => new(
            kind,
            YokkoStrings.Get("settings.display.title"),
            YokkoStrings.Get("settings.display.subtitle"),
            YokkoStrings.Get("settings.display.description"),
            FontAwesome.Solid.Desktop,
            Array.Empty<LocalisableString>(),
            YokkoStrings.SearchTerms("settings.display.title"),
            YokkoStrings.SearchTermsForPrefix(
                "settings.display.",
                "ui scale fps frame rate fullscreen borderless renderer",
                "界面缩放 帧率 帧数 全屏 无边框 渲染")),
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
            YokkoStrings.SearchTerms("settings.audio.title"),
            YokkoStrings.SearchTermsForPrefix(
                "settings.audio.",
                "sound speaker headphones volume latency asio wasapi",
                "声音 扬声器 耳机 音量 延迟")),
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
                YokkoStrings.Get("settings.gameplay.section_playback_rate"),
                YokkoStrings.Get("settings.gameplay.section_judgement"),
                YokkoStrings.Get("settings.gameplay.section_feedback"),
            },
            YokkoStrings.SearchTerms("settings.gameplay.title"),
            YokkoStrings.SearchTermsForPrefix(
                "settings.gameplay.",
                "keybind timing judgement feedback keysound pause unfocused countdown",
                "键位 按键 判定 反馈 按键音 暂停 失焦 倒计时")),
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
            YokkoStrings.SearchTerms("settings.shortcuts.title"),
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
                YokkoStrings.Get("settings.skins.ln_cut_amount"),
                YokkoStrings.Get("settings.skins.combo_bursts"),
            },
            YokkoStrings.SearchTerms("settings.skins.title"),
            YokkoStrings.SearchTermsForPrefix(
                "settings.skins.",
                "theme appearance noteskin long note ln combo burst",
                "主题 外观 长条 长键 爆气")),
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
            YokkoStrings.SearchTerms("settings.editor.title"),
            YokkoStrings.SearchTermsForPrefix(
                "settings.editor.",
                "chart mapping grid autosave",
                "谱面 制谱 网格 自动保存")),
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
            YokkoStrings.SearchTerms("settings.import.title"),
            YokkoStrings.SearchTermsForPrefix(
                "settings.import.",
                "osu quaver etterna stepmania bms file folder migrate",
                "文件 文件夹 迁移 谱面")),
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
            YokkoStrings.SearchTerms("settings.accessibility.title"),
            YokkoStrings.SearchTermsForPrefix(
                "settings.accessibility.",
                "reduce motion accessibility visual assistance",
                "减少动画 无障碍 视觉辅助")),
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
            YokkoStrings.SearchTerms("settings.about.title"),
            YokkoStrings.SearchTermsForPrefix(
                "settings.about.",
                "build copyright open source",
                "构建 版权 开源")),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
