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
    // System-provided text is not present in the localisation table, but still
    // needs glyphs in Yokko's deliberately subsetted bitmap font.
    internal const string ExternalTextGlyphs = "扬声器耳机头戴式数字音频线路输出蓝牙【粉投手】";

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
            ["main.music_no_songs"] = new("No imported songs", "暂无导入歌曲", "インポート曲なし"),
            ["main.music_import_hint"] = new("Drop a chart file to listen", "拖入谱面即可播放", "譜面をドロップして再生"),
            ["main.hold_esc_exit"] = new("Hold Esc 3s to exit", "长按 Esc 3 秒退出", "Esc を3秒長押しで終了"),
            ["main.bubble_again"] = new("Again! Again!", "再来一次！", "もう一回！"),
            ["main.bubble_pick_song"] = new("Pick a song~", "选首歌吧~", "曲を選んで〜"),
            ["main.bubble_keys"] = new("D F J K, go!", "D F J K，出发！", "D F J K で行こう！"),
            ["main.utility_exit"] = new("Exit game", "退出游戏", "ゲームを終了"),
            ["main.utility_folder"] = new("Chart folder", "谱面文件夹", "譜面フォルダー"),
            ["main.exit_hold"] = new("Exiting… release to cancel", "正在退出… 松开取消", "終了中… 離すとキャンセル"),

            ["import.chart.importing"] = new("Importing chart", "正在导入谱面", "譜面をインポート中"),
            ["import.chart.success"] = new("Chart ready", "谱面已就绪", "譜面の準備完了"),
            ["import.chart.success_count"] = new(
                "Imported {0} charts",
                "已导入 {0} 张谱面",
                "{0} 個の譜面をインポートしました"),
            ["import.chart.failed"] = new("Chart import failed", "谱面导入失败", "譜面のインポートに失敗"),
            ["import.replay.importing"] = new(
                "Importing osu! replay",
                "正在导入 osu! 回放",
                "osu! リプレイをインポート中"),
            ["import.replay.success"] = new(
                "Replay ready",
                "回放已就绪",
                "リプレイの準備完了"),
            ["import.replay.failed"] = new(
                "Replay import failed",
                "回放导入失败",
                "リプレイのインポートに失敗"),

            ["song_select.search"] = new("Search songs", "搜索歌曲", "楽曲を検索"),
            ["song_select.all_songs"] = new("ALL SONGS", "全部歌曲", "ALL"),
            ["song_select.mods"] = new("MODS", "模组", "MOD"),
            ["song_select.mods_unavailable"] = new("COMING SOON", "即将推出", "近日対応"),
            ["song_select.play"] = new("PLAY", "开始", "プレイ"),
            ["song_select.back"] = new("ESC  BACK", "ESC  返回", "ESC  戻る"),
            ["song_select.no_results"] = new("NO SONGS FOUND", "没有匹配的歌曲", "楽曲が見つかりません"),
            ["song_select.local_best"] = new("LOCAL BEST", "本地分数", "HIGH SCORE"),
            ["song_select.mapped_by"] = new("mapped by {0}", "谱面制作 {0}", "譜面 {0}"),
            ["song_select.global_ranking"] = new("GLOBAL RANKING", "全部排行", "GLOBAL RANKING"),
            ["song_select.my_record"] = new("MY RECORD", "个人分数", "MY RECORD"),
            ["song_select.you"] = new("YOU", "你", "自分"),
            ["song_select.length"] = new("LENGTH", "时长", "長さ"),

            ["gameplay.pause.title"] = new("Paused", "暂停中", "一時停止"),
            ["gameplay.pause.resume"] = new("Resume", "继续游戏", "ゲームに戻る"),
            ["gameplay.pause.resume_hint"] = new("ESC  RESUME", "ESC  继续", "ESC  再開"),
            ["gameplay.pause.retry"] = new("Restart", "重新开始", "リスタート"),
            ["gameplay.pause.settings"] = new("Settings", "设置", "設定"),
            ["gameplay.pause.exit"] = new("Exit play", "退出游玩", "選曲へ戻る"),

            ["gameplay.result.title"] = new("Result", "结算", "リザルト"),
            ["gameplay.result.max_combo"] = new("MAX COMBO", "最大连击", "MAX COMBO"),
            ["gameplay.result.new_best"] = new("NEW BEST", "新纪录", "NEW BEST"),
            ["gameplay.result.retry"] = new("Retry", "再来一次", "リトライ"),
            ["gameplay.result.watch_replay"] = new("Watch replay", "观看回放", "リプレイを見る"),
            ["gameplay.result.return"] = new("Song select", "返回选曲", "曲選択へ"),

            ["gameplay.audio_failed_title"] = new(
                "Audio could not start",
                "音频启动失败",
                "オーディオを開始できません"),
            ["gameplay.audio_failed_message"] = new(
                "Gameplay has been stopped to prevent a silent, unsynchronised run.",
                "已停止游玩，避免在无声且不同步的状态下继续。",
                "無音で同期しないプレイを防ぐため、ゲームを停止しました。"),
            ["gameplay.audio_failed_return"] = new(
                "Press Esc to return",
                "按 Esc 返回",
                "Esc で戻る"),

            ["settings.title"] = new("Settings", "设置", "設定"),
            ["settings.back"] = new("Back", "返回", "戻る"),
            ["settings.search"] = new("Search settings", "搜索设置", "設定を検索"),
            ["settings.no_matches"] = new("No matching settings", "没有匹配的设置", "一致する設定がありません"),
            ["settings.group_core"] = new("CORE", "基础", "基本"),
            ["settings.group_creation"] = new("CREATION", "创作", "制作"),
            ["settings.group_system"] = new("SYSTEM", "系统", "システム"),
            ["settings.changes_apply_instantly"] = new("Changes apply instantly", "更改会立即生效", "変更はすぐに反映されます"),
            ["settings.esc_to_return"] = new("Back", "返回", "戻る"),
            ["settings.planned_sections"] = new("Planned sections", "规划功能", "予定セクション"),
            ["settings.coming_soon"] = new("Coming soon", "即将推出", "近日対応"),
            ["settings.planned"] = new("PLANNED", "规划中", "予定"),
            ["settings.not_available"] = new("Not available yet", "暂不可用", "まだ利用できません"),
            ["settings.future_section"] = new(
                "This section is reserved for a future build. No setting is applied yet.",
                "此区域为后续版本预留，目前不会应用任何设置。",
                "この項目は今後のバージョン向けです。現在は設定を適用しません。"),

            ["settings.general.title"] = new("General", "通用", "一般"),
            ["settings.general.subtitle"] = new(
                "Language, scroll speed and application behaviour",
                "语言、流速与应用行为",
                "言語、スクロール速度、アプリの動作"),
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
            ["settings.general.mania_scroll_speed"] = new(
                "osu!mania scroll speed",
                "osu!mania 流速",
                "osu!mania スクロール速度"),
            ["settings.general.mania_scroll_speed_note"] = new(
                "Uses osu!mania's 1–40 scale · Ctrl + / Ctrl − or F3 / F4 during gameplay.",
                "使用 osu!mania 的 1–40 档位 · 游玩时按 Ctrl + / Ctrl − 或 F3 / F4 调节。",
                "osu!mania と同じ 1–40 段階 · プレイ中は Ctrl + / Ctrl − または F3 / F4。"),
            ["settings.language.english"] = new("English", "English", "English"),
            ["settings.language.chinese"] = new("简体中文", "简体中文", "简体中文"),
            ["settings.language.japanese"] = new("日本語", "日本語", "日本語"),

            ["settings.display.title"] = new("Display", "显示", "表示"),
            ["settings.display.subtitle"] = new(
                "Window, resolution and refresh rate",
                "窗口、分辨率与刷新率",
                "ウィンドウ、解像度、リフレッシュレート"),
            ["settings.display.description"] = new(
                "Display and interface presentation.",
                "调整显示与界面呈现方式。",
                "画面とインターフェースの表示を調整します。"),
            ["settings.display.current_display"] = new("Current display", "当前显示", "現在のディスプレイ"),
            ["settings.display.metadata"] = new(
                "Display {0}  ·  {1} × {2}  ·  {3} Hz",
                "显示器 {0}  ·  {1} × {2}  ·  {3} Hz",
                "ディスプレイ {0}  ·  {1} × {2}  ·  {3} Hz"),
            ["settings.display.window_mode"] = new("Window mode", "窗口模式", "ウィンドウモード"),
            ["settings.display.resolution"] = new("Resolution", "分辨率", "解像度"),
            ["settings.display.frame_limit"] = new("Frame limit", "帧率上限", "フレーム上限"),
            ["settings.display.interface_scale"] = new("Interface scale", "界面缩放", "UI スケール"),
            ["settings.display.performance_readout"] = new(
                "Performance readout",
                "性能读数",
                "パフォーマンス表示"),
            ["settings.display.enabled"] = new("Enabled", "已开启", "オン"),
            ["settings.display.disabled"] = new("Disabled", "已关闭", "オフ"),
            ["settings.display.windowed"] = new("Windowed", "窗口化", "ウィンドウ"),
            ["settings.display.borderless"] = new("Borderless", "无边框", "ボーダーレス"),
            ["settings.display.fullscreen"] = new("Fullscreen", "全屏", "フルスクリーン"),
            ["settings.display.compact"] = new("Compact", "紧凑", "コンパクト"),
            ["settings.display.comfortable"] = new("Comfortable", "舒适", "標準"),
            ["settings.display.spacious"] = new("Large", "大", "大"),

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

            ["settings.skins.title"] = new("Skins", "皮肤", "スキン"),
            ["settings.skins.subtitle"] = new(
                "Your osu!mania skin library",
                "管理你的 osu!mania 皮肤库",
                "osu!mania スキンライブラリ"),
            ["settings.skins.description"] = new(
                "Import, select and manage osu!mania skins.",
                "导入、启用和管理 osu!mania 皮肤。",
                "osu!mania スキンを導入・選択・管理します。"),
            ["settings.skins.section_library"] = new("Installed skins", "已安装皮肤", "インストール済み"),
            ["settings.skins.section_import"] = new("Drag & drop import", "拖拽导入", "ドラッグ＆ドロップ"),
            ["settings.skins.drop_hint"] = new(
                "Drop an .osk package or a skin folder anywhere in Yokko",
                "把 .osk 文件或皮肤文件夹拖到 Yokko 的任意位置",
                ".osk またはスキンフォルダーを Yokko の任意の場所へドロップ"),
            ["settings.skins.empty"] = new(
                "No skins installed yet",
                "还没有安装皮肤",
                "スキンはまだありません"),
            ["settings.skins.empty_note"] = new(
                "Your first imported skin will be enabled automatically.",
                "导入第一款皮肤后会自动启用。",
                "最初に導入したスキンは自動的に有効になります。"),
            ["settings.skins.active"] = new("ACTIVE", "使用中", "使用中"),
            ["settings.skins.use"] = new("USE", "启用", "使用"),
            ["settings.skins.delete"] = new("DELETE", "删除", "削除"),
            ["settings.skins.confirm_delete"] = new("CONFIRM", "确认", "確認"),
            ["settings.skins.keys"] = new("{0}K", "{0}K", "{0}K"),
            ["settings.skins.importing"] = new("Importing osu!mania skin", "正在导入 osu!mania 皮肤", "osu!mania スキンを導入中"),
            ["settings.skins.import_success"] = new("Skin ready", "皮肤已就绪", "スキンの準備完了"),
            ["settings.skins.import_failed"] = new("Skin import failed", "皮肤导入失败", "スキンの導入に失敗"),
            ["settings.gameplay.ready"] = new(
                "Gameplay controls are live",
                "游玩控制已实装",
                "ゲームプレイ設定が有効です"),
            ["settings.gameplay.ready_metadata"] = new(
                "4K  {0}   ·   7K  {1}",
                "4K  {0}   ·   7K  {1}",
                "4K  {0}   ·   7K  {1}"),
            ["settings.gameplay.live"] = new("LIVE", "已启用", "有効"),
            ["settings.gameplay.key_profile"] = new("Key profile", "键位配置", "キープロファイル"),
            ["settings.gameplay.edit_all"] = new("Edit all keys", "修改键位", "キーを一括変更"),
            ["settings.gameplay.reset"] = new("Reset keys", "重置键位", "キーをリセット"),
            ["settings.gameplay.key_capture_hint"] = new(
                "Choose a lane, then press the key you want to use.",
                "选择一个轨道，然后按下你想使用的按键。",
                "レーンを選び、割り当てたいキーを押してください。"),
            ["settings.gameplay.key_swap_hint"] = new(
                "Duplicate keys swap lanes automatically · Esc cancels capture",
                "重复按键会自动交换轨道 · Esc 取消录入",
                "重複したキーは自動で入れ替わります · Esc でキャンセル"),
            ["settings.gameplay.lane"] = new("LANE {0}", "轨道 {0}", "レーン {0}"),
            ["settings.gameplay.click_to_change"] = new("Change key", "修改按键", "キーを変更"),
            ["settings.gameplay.press_key"] = new("PRESS KEY", "请按键", "キーを入力"),
            ["settings.gameplay.esc_cancel"] = new("Esc to cancel", "Esc 取消", "Esc でキャンセル"),
            ["settings.gameplay.sequence_hint"] = new(
                "Press key {0} of {1} · Esc cancels the whole set",
                "请输入第 {0}/{1} 个按键 · Esc 取消整组修改",
                "{0}/{1} 個目のキーを入力 · Esc で全体をキャンセル"),
            ["settings.gameplay.sequence_duplicate"] = new(
                "That key is already used · press a different key",
                "这个按键已经使用 · 请按其他按键",
                "そのキーは使用済みです · 別のキーを入力してください"),
            ["settings.gameplay.sequence_captured"] = new(
                "Captured",
                "已录入",
                "入力済み"),
            ["settings.gameplay.sequence_saved"] = new(
                "{0}K profile saved · {1}",
                "{0}K 键位已保存 · {1}",
                "{0}K プロファイルを保存しました · {1}"),
            ["settings.gameplay.scroll_speed"] = new("Note speed", "音符速度", "ノーツ速度"),
            ["settings.gameplay.scroll_speed_note"] = new(
                "osu!mania 1–40 scale · Ctrl + / Ctrl − or F3 / F4.",
                "osu!mania 1–40 档位 · Ctrl + / Ctrl − 或 F3 / F4。",
                "osu!mania の 1–40 段階 · Ctrl + / Ctrl − または F3 / F4。"),
            ["gameplay.scroll_speed_status"] = new(
                "Scroll speed  {0:0.0}",
                "流速  {0:0.0}",
                "スクロール速度  {0:0.0}"),
            ["gameplay.input_timing_waiting"] = new(
                "{0} timestamped input · waiting for samples",
                "{0} 时间戳输入 · 等待采样",
                "{0} タイムスタンプ入力 · サンプル待機中"),
            ["gameplay.input_timing_status"] = new(
                "{0} input age · p50 {1:0.00} · p95 {2:0.00} · p99 {3:0.00} ms",
                "{0} 输入年龄 · p50 {1:0.00} · p95 {2:0.00} · p99 {3:0.00} ms",
                "{0} 入力エイジ · p50 {1:0.00} · p95 {2:0.00} · p99 {3:0.00} ms"),
            ["settings.gameplay.speed_presets"] = new("Quick presets", "快捷预设", "クイック設定"),
            ["settings.gameplay.input_offset"] = new("Input offset", "输入偏移", "入力オフセット"),
            ["settings.gameplay.input_offset_note"] = new(
                "Shared with Audio so timing has one source of truth.",
                "与音频设置共享，确保时序只有一个真源。",
                "オーディオ設定と共有し、タイミングを一元管理します。"),
            ["settings.gameplay.feedback_heading"] = new(
                "Playfield feedback",
                "游玩界面反馈",
                "プレイ画面のフィードバック"),
            ["settings.gameplay.feedback_note"] = new(
                "Control whether key presses light the lanes.",
                "控制按键时是否点亮对应轨道。",
                "キー入力時にレーンを点灯するか設定します。"),
            ["settings.gameplay.show_lane_feedback"] = new("Lane lighting", "轨道亮灯", "レーン点灯"),
            ["settings.gameplay.show_lane_feedback_note"] = new(
                "Light lanes on key press",
                "按住按键时提供反馈",
                "キー入力時に反応"),
            ["settings.gameplay.enabled"] = new("Enabled", "已启用", "オン"),
            ["settings.gameplay.disabled"] = new("Disabled", "已关闭", "オフ"),

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
            ["settings.import.resource_selector_title"] = new(
                "Choose resource directory",
                "选择资源文件夹",
                "リソースフォルダーを選択"),
            ["settings.import.resource_change"] = new("Change", "更改", "変更"),
            ["settings.import.resource_select"] = new("Select", "选择", "選択"),
            ["settings.import.resource_cancel"] = new("Cancel", "取消", "キャンセル"),
            ["settings.import.resource_default"] = new(
                "Use default",
                "使用默认位置",
                "既定の場所を使用"),
            ["settings.import.resource_custom"] = new(
                "Custom folder",
                "自定义文件夹",
                "カスタムフォルダー"),
            ["settings.import.resource_default_active"] = new(
                "Default folder",
                "默认文件夹",
                "既定のフォルダー"),
            ["settings.import.resource_migrating"] = new(
                "Migrating...",
                "正在迁移…",
                "移行中…"),
            ["settings.import.resource_migrated"] = new(
                "Migrated",
                "迁移完成",
                "移行完了"),
            ["settings.import.resource_migrated_retained"] = new(
                "Migrated · old files retained",
                "已迁移 · 旧文件已保留",
                "移行完了 · 旧ファイルを保持"),
            ["settings.import.resource_failed"] = new(
                "Migration failed",
                "迁移失败",
                "移行失敗"),

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
