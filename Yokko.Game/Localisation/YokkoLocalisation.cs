using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Configuration;
using osu.Framework.IO.Stores;
using osu.Framework.Localisation;

namespace Yokko.Game.Localisation;

internal static class YokkoLocale
{
    public const string English = "en";
    public const string Chinese = "zh";
    public const string Japanese = "ja";

    public static readonly IReadOnlyList<string> SUPPORTED = new[]
    {
        English,
        Chinese,
        Japanese,
    };

    public static string Normalize(string locale) =>
        SUPPORTED.Contains(locale) ? locale : English;
}

internal static class YokkoLocalisation
{
    public static LocalisationManager Create(FrameworkConfigManager frameworkConfig)
    {
        var manager = new LocalisationManager(frameworkConfig);
        manager.AddLocaleMappings(new[]
        {
            createMapping(YokkoLocale.English),
            createMapping(YokkoLocale.Chinese),
            createMapping(YokkoLocale.Japanese),
        });
        return manager;
    }

    private static LocaleMapping createMapping(string locale) =>
        new(locale, new DictionaryLocalisationStore(locale, YokkoStrings.ForLocale(locale)));
}

internal static class YokkoStrings
{
    private sealed record Translation(string English, string Chinese, string Japanese);

    private static readonly IReadOnlyDictionary<string, Translation> translations =
        new Dictionary<string, Translation>
        {
            ["common.esc_back"] = new("Esc back", "Esc 返回", "Esc で戻る"),

            ["main.hero_line_1"] = new("Ready for", "准备好", "準備は"),
            ["main.hero_line_2"] = new("a check-up?", "来场检查？", "できた？"),
            ["main.play"] = new("Play", "开始", "プレイ"),
            ["main.song_select"] = new("SONG SELECT", "选择歌曲", "楽曲選択"),
            ["main.editor"] = new("Editor", "编辑器", "エディター"),
            ["main.settings"] = new("Settings", "设置", "設定"),
            ["main.lets_play"] = new("Let's play!", "开始吧！", "遊ぼう！"),
            ["main.audio_unavailable"] = new("Audio unavailable", "音频不可用", "オーディオ利用不可"),

            ["settings.title"] = new("Settings", "设置", "設定"),
            ["settings.back"] = new("Back", "返回", "戻る"),
            ["settings.search"] = new("Search settings", "搜索设置", "設定を検索"),
            ["settings.no_matches"] = new("No matching settings", "没有匹配的设置", "一致する設定がありません"),
            ["settings.group_core"] = new("CORE", "基础", "基本"),
            ["settings.group_creation"] = new("CREATION", "创作", "制作"),
            ["settings.group_system"] = new("SYSTEM", "系统", "システム"),
            ["settings.changes_apply_instantly"] = new("Changes apply instantly", "更改会立即生效", "変更はすぐに反映されます"),
            ["settings.esc_to_return"] = new("Esc to return", "按 Esc 返回", "Esc で戻る"),
            ["settings.planned_sections"] = new("Planned sections", "规划功能", "予定セクション"),
            ["settings.coming_soon"] = new("Coming soon", "即将推出", "近日対応"),
            ["settings.planned"] = new("PLANNED", "规划中", "予定"),
            ["settings.not_available"] = new("Not available yet", "暂不可用", "まだ利用できません"),
            ["settings.future_section"] = new(
                "This section is reserved for a future build. No setting is applied yet.",
                "此区域为后续版本预留，目前不会应用任何设置。",
                "この項目は今後のバージョン向けです。現在は設定を適用しません。"),

            ["settings.general.title"] = new("General", "常规", "一般"),
            ["settings.general.subtitle"] = new(
                "Language, startup and application behaviour",
                "语言、启动与应用行为",
                "言語、起動、アプリの動作"),
            ["settings.general.description"] = new(
                "The essentials that shape how Yokko starts and behaves.",
                "控制 Yokko 启动方式与基础行为。",
                "Yokko の起動方法と基本動作を設定します。"),
            ["settings.general.section_language"] = new("Language & region", "语言与地区", "言語と地域"),
            ["settings.general.section_startup"] = new("Startup behaviour", "启动行为", "起動時の動作"),
            ["settings.general.section_updates"] = new("Updates", "更新", "アップデート"),
            ["settings.general.current_language"] = new("Current language", "当前语言", "現在の言語"),
            ["settings.general.language"] = new("Language", "语言", "言語"),
            ["settings.general.language_note"] = new(
                "Language changes apply immediately and are saved automatically.",
                "语言更改会立即生效并自动保存。",
                "言語の変更はすぐに反映され、自動的に保存されます。"),
            ["settings.language.english"] = new("English", "English", "English"),
            ["settings.language.chinese"] = new("简体中文", "简体中文", "简体中文"),
            ["settings.language.japanese"] = new("日本語", "日本語", "日本語"),

            ["settings.display.title"] = new("Display", "显示", "表示"),
            ["settings.display.subtitle"] = new(
                "Window, resolution and interface scale",
                "窗口、分辨率与界面缩放",
                "ウィンドウ、解像度、UI スケール"),
            ["settings.display.description"] = new(
                "Display and interface presentation.",
                "调整显示与界面呈现方式。",
                "画面とインターフェースの表示を調整します。"),
            ["settings.display.current_display"] = new("Current display", "当前显示", "現在のディスプレイ"),
            ["settings.display.metadata"] = new(
                "Display 1  ·  {0} × {1}  ·  60 Hz",
                "显示器 1  ·  {0} × {1}  ·  60 Hz",
                "ディスプレイ 1  ·  {0} × {1}  ·  60 Hz"),
            ["settings.display.window_mode"] = new("Window mode", "窗口模式", "ウィンドウモード"),
            ["settings.display.resolution"] = new("Resolution", "分辨率", "解像度"),
            ["settings.display.interface_scale"] = new("Interface scale", "界面缩放", "UI スケール"),
            ["settings.display.windowed"] = new("Windowed", "窗口化", "ウィンドウ"),
            ["settings.display.borderless"] = new("Borderless", "无边框", "ボーダーレス"),
            ["settings.display.fullscreen"] = new("Fullscreen", "全屏", "フルスクリーン"),
            ["settings.display.compact"] = new("Compact", "紧凑", "コンパクト"),
            ["settings.display.comfortable"] = new("Comfortable", "舒适", "標準"),
            ["settings.display.spacious"] = new("Spacious", "宽大", "広め"),

            ["settings.audio.title"] = new("Audio", "音频", "オーディオ"),
            ["settings.audio.subtitle"] = new(
                "Output, latency and synchronisation",
                "输出、延迟与同步",
                "出力、レイテンシー、同期"),
            ["settings.audio.description"] = new(
                "Choose the native output path and latency profile used by gameplay.",
                "选择游玩时使用的原生输出链路与延迟配置。",
                "ゲームプレイで使用するネイティブ出力とレイテンシー設定を選択します。"),
            ["settings.audio.section_output"] = new("Output device", "输出设备", "出力デバイス"),
            ["settings.audio.section_latency"] = new("Latency & sync", "延迟与同步", "レイテンシーと同期"),
            ["settings.audio.section_volume"] = new("Volume", "音量", "音量"),
            ["settings.audio.backend"] = new("Output mode", "输出模式", "出力モード"),
            ["settings.audio.device"] = new("Output device", "输出设备", "出力デバイス"),
            ["settings.audio.buffer"] = new("Buffer profile", "缓冲配置", "バッファ設定"),
            ["settings.audio.offset"] = new("Timing offset", "时序偏移", "タイミングオフセット"),
            ["settings.audio.exclusive"] = new("WASAPI Exclusive", "WASAPI 独占", "WASAPI 排他"),
            ["settings.audio.shared"] = new("WASAPI Shared", "WASAPI 共享", "WASAPI 共有"),
            ["settings.audio.frames"] = new("{0} frames", "{0} 帧", "{0} フレーム"),
            ["settings.audio.default_device"] = new(
                "Windows default device",
                "Windows 默认设备",
                "Windows 既定デバイス"),
            ["settings.audio.loading_devices"] = new(
                "Loading devices…",
                "正在读取设备…",
                "デバイスを読み込み中…"),
            ["settings.audio.native_ready"] = new(
                "Yokko native audio ready",
                "Yokko 原生音频已就绪",
                "Yokko ネイティブオーディオ準備完了"),
            ["settings.audio.native_unavailable"] = new(
                "Native audio unavailable",
                "原生音频不可用",
                "ネイティブオーディオを利用できません"),
            ["settings.audio.status_metadata"] = new(
                "{0}  ·  {1} frames requested  ·  {2}",
                "{0}  ·  请求 {1} 帧  ·  {2}",
                "{0}  ·  {1} フレーム要求  ·  {2}"),
            ["settings.audio.apply_next_playback"] = new(
                "Saved instantly · applies when the next playback starts",
                "立即保存 · 下次开始播放时生效",
                "すぐに保存 · 次回の再生開始時に適用"),

            ["settings.gameplay.title"] = new("Gameplay", "游玩", "ゲームプレイ"),
            ["settings.gameplay.subtitle"] = new(
                "Input, timing and playfield feedback",
                "输入、时序与游玩反馈",
                "入力、タイミング、プレイ画面のフィードバック"),
            ["settings.gameplay.description"] = new(
                "Gameplay preferences will stay separate from chart rules and scoring logic.",
                "游玩偏好将与谱面规则和计分逻辑保持分离。",
                "プレイ設定は譜面ルールやスコア処理から分離します。"),
            ["settings.gameplay.section_input"] = new("Input & key bindings", "输入与按键", "入力とキー設定"),
            ["settings.gameplay.section_timing"] = new("Timing & judgement", "时序与判定", "タイミングと判定"),
            ["settings.gameplay.section_feedback"] = new("Visual feedback", "视觉反馈", "視覚フィードバック"),

            ["settings.editor.title"] = new("Editor", "编辑器", "エディター"),
            ["settings.editor.subtitle"] = new(
                "Workspace, snapping and autosave",
                "工作区、吸附与自动保存",
                "ワークスペース、スナップ、自動保存"),
            ["settings.editor.description"] = new(
                "Editor preferences will be grouped into small, collapsible sections.",
                "编辑器偏好将整理为小型的可折叠区域。",
                "エディター設定は小さな折りたたみ項目に整理します。"),
            ["settings.editor.section_workspace"] = new("Workspace", "工作区", "ワークスペース"),
            ["settings.editor.section_grid"] = new("Grid & snapping", "网格与吸附", "グリッドとスナップ"),
            ["settings.editor.section_autosave"] = new("Autosave", "自动保存", "自動保存"),

            ["settings.import.title"] = new("Import", "导入", "インポート"),
            ["settings.import.subtitle"] = new(
                "Formats, file handling and conversion",
                "格式、文件处理与转换",
                "形式、ファイル処理、変換"),
            ["settings.import.description"] = new(
                "Import preferences will remain independent from the importer implementations.",
                "导入偏好将与具体导入器实现保持独立。",
                "インポート設定は各インポーターの実装から分離します。"),
            ["settings.import.section_formats"] = new("Supported formats", "支持的格式", "対応形式"),
            ["settings.import.section_behaviour"] = new("Import behaviour", "导入行为", "インポート動作"),
            ["settings.import.section_locations"] = new("File locations", "文件位置", "ファイルの場所"),
            ["settings.import.status_title"] = new(
                "Chart importers are ready",
                "谱面导入器已就绪",
                "譜面インポーターは準備完了です"),
            ["settings.import.status_metadata"] = new(
                "{0} format families · {1} file types",
                "{0} 类格式 · {1} 种文件类型",
                "{0} 形式 · {1} ファイルタイプ"),
            ["settings.import.ready"] = new("Ready", "可用", "対応"),
            ["settings.import.partial"] = new("Partial", "部分", "一部"),
            ["settings.import.prefer_keysounds"] = new(
                "Preserve keysounds",
                "保留按键音",
                "キー音を保持"),
            ["settings.import.prefer_keysounds_note"] = new(
                "Keep BMS sample paths",
                "保留 BMS 采样路径",
                "BMS サンプルを保持"),
            ["settings.import.prefer_ssc"] = new(
                "Prefer SSC",
                "优先 SSC",
                "SSC を優先"),
            ["settings.import.prefer_ssc_note"] = new(
                "Use richer pack simfiles",
                "优先更完整的包内谱面",
                "より詳細な譜面を使用"),
            ["settings.import.show_warnings"] = new(
                "Show warnings",
                "显示兼容提示",
                "警告を表示"),
            ["settings.import.show_warnings_note"] = new(
                "Report downgraded effects",
                "报告降级处理的效果",
                "未対応効果を通知"),
            ["settings.import.enabled"] = new("Enabled", "已启用", "オン"),
            ["settings.import.disabled"] = new("Disabled", "已关闭", "オフ"),
            ["settings.import.location_note"] = new(
                "Packages are safely extracted to Yokko's temporary cache; bundled audio keeps its relative path.",
                "谱包会安全解压到 Yokko 临时缓存，包内音频保持相对路径。",
                "パックは一時キャッシュへ安全に展開され、音声の相対パスも維持されます。"),

            ["settings.accessibility.title"] = new("Accessibility", "辅助功能", "アクセシビリティ"),
            ["settings.accessibility.subtitle"] = new(
                "Visual, motion and input assistance",
                "视觉、动态与输入辅助",
                "視覚、モーション、入力支援"),
            ["settings.accessibility.description"] = new(
                "Accessibility options will be easy to find and safe to change.",
                "辅助功能选项将易于查找并可安全调整。",
                "アクセシビリティ設定を見つけやすく、安全に変更できるようにします。"),
            ["settings.accessibility.section_visual"] = new("Visual assistance", "视觉辅助", "視覚支援"),
            ["settings.accessibility.section_input"] = new("Input accessibility", "输入辅助", "入力支援"),
            ["settings.accessibility.section_motion"] = new("Reduced motion", "减少动态效果", "動きを減らす"),

            ["settings.about.title"] = new("About", "关于", "情報"),
            ["settings.about.subtitle"] = new(
                "Version, credits and licences",
                "版本、制作人员与许可证",
                "バージョン、クレジット、ライセンス"),
            ["settings.about.description"] = new(
                "Project information and acknowledgements will be collected here.",
                "项目资料与致谢信息将集中在此。",
                "プロジェクト情報と謝辞をここにまとめます。"),
            ["settings.about.section_version"] = new("Version & updates", "版本与更新", "バージョンと更新"),
            ["settings.about.section_credits"] = new("Credits", "制作人员", "クレジット"),
            ["settings.about.section_licences"] = new("Licences", "许可证", "ライセンス"),
        };

    public static LocalisableString Get(string key, params object[] args)
    {
        if (!translations.TryGetValue(key, out Translation translation))
            throw new ArgumentException($"Unknown localisation key: {key}", nameof(key));

        return new TranslatableString(key, translation.English, args);
    }

    public static string SearchTerms(params string[] keys) =>
        string.Join(" ", keys.SelectMany(key =>
        {
            Translation translation = translations[key];
            return new[] { translation.English, translation.Chinese, translation.Japanese };
        }));

    internal static IEnumerable<string> Keys => translations.Keys;

    internal static IReadOnlyDictionary<string, string> ForLocale(string locale) =>
        translations.ToDictionary(
            pair => pair.Key,
            pair => locale switch
            {
                YokkoLocale.Chinese => pair.Value.Chinese,
                YokkoLocale.Japanese => pair.Value.Japanese,
                _ => pair.Value.English,
            });
}

internal sealed class DictionaryLocalisationStore : ILocalisationStore
{
    private readonly IReadOnlyDictionary<string, string> strings;

    public CultureInfo EffectiveCulture { get; }

    public DictionaryLocalisationStore(string locale, IReadOnlyDictionary<string, string> strings)
    {
        EffectiveCulture = CultureInfo.GetCultureInfo(locale);
        this.strings = strings;
    }

    public string Get(string name) => strings.GetValueOrDefault(name);

    public Task<string> GetAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(Get(name));

    public Stream GetStream(string name) => null;

    public IEnumerable<string> GetAvailableResources() => strings.Keys;

    public void Dispose()
    {
    }
}
