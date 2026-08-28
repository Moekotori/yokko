using System;
using System.Collections.Generic;
using System.Linq;

namespace Yokko.Game.Screens.Settings;

internal readonly record struct SettingsSearchMatch(
    SettingsPageKind Page,
    string ItemId,
    float ScrollY,
    params string[] Terms);

/// <summary>
/// Cross-page settings search index. Terms are matched against user queries
/// to open the correct page and scroll to the relevant row.
/// </summary>
internal static class SettingsSearchCatalog
{
    internal static IReadOnlyList<SettingsSearchMatch> All { get; } =
    [
        new(SettingsPageKind.General, "language", 276, "language", "locale", "语言"),
        new(SettingsPageKind.General, "home-music", 344, "home music", "主页音乐", "startup"),
        new(SettingsPageKind.General, "player-name", 412, "player", "name", "玩家", "display name"),
        new(SettingsPageKind.General, "debug-console", 588, "debug", "console", "f12", "调试"),
        new(SettingsPageKind.General, "startup", 620, "startup", "launch", "启动"),
        new(SettingsPageKind.General, "config", 680, "export", "import", "reset", "backup", "导出", "导入"),
        new(SettingsPageKind.Display, "window-mode", 276, "window", "fullscreen", "borderless", "窗口", "全屏"),
        new(SettingsPageKind.Display, "resolution", 338, "resolution", "ratio", "分辨率"),
        new(SettingsPageKind.Display, "frame-limit", 400, "fps", "frame", "vsync", "帧率"),
        new(SettingsPageKind.Display, "ui-scale", 462, "scale", "interface", "界面", "缩放"),
        new(SettingsPageKind.Audio, "offset", 526, "offset", "timing", "calibration", "偏移", "校准"),
        new(SettingsPageKind.Audio, "volume", 410, "volume", "master", "music", "音量"),
        new(SettingsPageKind.Audio, "backend", 236, "wasapi", "asio", "backend", "audio device"),
        new(SettingsPageKind.Gameplay, "scroll-speed", 20, "scroll", "speed", "滚速", "流速"),
        new(SettingsPageKind.Gameplay, "input-offset", 325, "offset", "input", "timing", "偏移"),
        new(SettingsPageKind.Gameplay, "keys", 20, "key", "bind", "lane", "按键", "键位"),
        new(SettingsPageKind.Gameplay, "judgement", 20, "judgement", "judge", "判定"),
        new(SettingsPageKind.Gameplay, "layout-preset", 20, "layout", "hud", "preset", "布局"),
        new(SettingsPageKind.Mods, "remember-mods", 236, "mod", "mods", "modifier", "模组"),
        new(SettingsPageKind.Shortcuts, "shortcuts", 20, "shortcut", "hotkey", "快捷键"),
        new(SettingsPageKind.Skins, "skin", 310, "skin", "noteskin", "皮肤"),
        new(SettingsPageKind.Import, "import", 380, "import", "folder", "osu", "导入"),
        new(SettingsPageKind.Import, "watch-folder", 520, "watch", "folder", "monitor", "监视"),
        new(SettingsPageKind.Safety, "crash-reports", 318, "crash", "report", "崩溃"),
        new(SettingsPageKind.Safety, "diagnostics", 390, "diagnostic", "export", "诊断"),
        new(SettingsPageKind.Accessibility, "reduce-motion", 236, "motion", "animation", "动画", "减少动画"),
        new(SettingsPageKind.Accessibility, "contrast", 298, "contrast", "high contrast", "对比度"),
        new(SettingsPageKind.Editor, "grid", 236, "grid", "snap", "网格"),
        new(SettingsPageKind.Editor, "autosave", 354, "autosave", "save", "自动保存"),
        new(SettingsPageKind.Desktop, "alt-tab", 274, "alt tab", "fast", "boss", "minimise"),
        new(SettingsPageKind.About, "update", 174, "update", "version", "更新"),
        new(SettingsPageKind.General, "privacy", 740, "privacy", "replay", "username", "隐私"),
    ];

    internal static SettingsSearchMatch? FindBest(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        string normalized = query.Trim();
        SettingsSearchMatch? best = null;
        int bestScore = SettingsSearchMatcher.NoMatch;

        foreach (SettingsSearchMatch match in All)
        {
            int score = SettingsSearchMatcher.Score(
                normalized,
                string.Join(' ', match.Terms),
                string.Join(' ', match.Terms));
            if (score <= bestScore)
                continue;

            bestScore = score;
            best = match;
        }

        return best;
    }
}
