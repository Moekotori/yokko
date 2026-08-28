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
using Yokko.Core.Mods;

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

    public static string Normalize(string locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            return English;

        string language = locale.Trim()
                                .Replace('_', '-')
                                .Split('-', 2)[0]
                                .ToLowerInvariant();
        return SUPPORTED.Contains(language) ? language : English;
    }

    public static string FromSystemCulture(CultureInfo culture) =>
        Normalize(culture?.Name);
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
    // Kept as a regression sample for system-provided text even though the
    // complete Noto Sans CJK UI atlases no longer depend on a hand-curated set.
    internal const string ExternalTextGlyphs = "扬声器耳机头戴式数字音频线路输出蓝牙【粉投手】";

    private sealed record Translation(string English, string Chinese, string Japanese);

    private static readonly IReadOnlyDictionary<string, Translation> translations =
        new Dictionary<string, Translation>
        {
            ["common.esc_back"] = new("Esc back", "Esc 返回", "Esc で戻る"),

            ["main.hero_line_1"] = new("YOKKO", "YOKKO", "YOKKO"),
            ["main.hero_line_2"] = new("DEMO", "DEMO", "DEMO"),
            ["main.play"] = new("Play", "开始", "プレイ"),
            ["main.song_select"] = new("SONG SELECT", "选择歌曲", "楽曲選択"),
            ["main.editor"] = new("Editor", "编辑器", "エディター"),
            ["main.settings"] = new("Settings", "设置", "設定"),
            ["main.multiplayer"] = new("Multiplayer", "在线游戏", "オンライン"),
            ["main.lets_play"] = new("Let's play!", "开始吧！", "遊ぼう！"),
            ["main.audio_unavailable"] = new("Audio unavailable", "音频不可用", "オーディオ利用不可"),
            ["main.music_no_songs"] = new("No imported songs", "暂无导入歌曲", "インポート曲なし"),
            ["main.music_import_hint"] = new("Drop a chart file to listen", "拖入谱面即可播放", "譜面をドロップして再生"),
            ["main.hold_esc_exit"] = new("Hold Esc 2s to exit", "长按 Esc 2 秒退出", "Esc を2秒長押しで終了"),
            ["main.bubble_again"] = new("Again! Again!", "再来一次！", "もう一回！"),
            ["main.bubble_pick_song"] = new("Pick a song~", "选首歌吧~", "曲を選んで〜"),
            ["main.bubble_keys"] = new("D F J K, go!", "D F J K，出发！", "D F J K で行こう！"),
            ["main.utility_exit"] = new("Exit game", "退出游戏", "ゲームを終了"),
            ["main.utility_folder"] = new("Chart management", "谱面管理中心", "譜面管理センター"),
            ["main.exit_hold"] = new("Exiting… release to cancel", "正在退出… 松开取消", "終了中… 離すとキャンセル"),
            ["main.player.rank"] = new("RANK {0}", "RANK {0}", "RANK {0}"),
            ["main.player.next_level"] = new(
                "NEXT LEVEL {0}%",
                "下一等级 {0}%",
                "次のレベル {0}%"),
            ["main.player.highest_combo"] = new(
                "BEST COMBO",
                "最高连击",
                "最大コンボ"),
            ["main.player.played_songs"] = new(
                "SONGS PLAYED",
                "游玩曲目",
                "プレイ楽曲"),

            ["chart_library.title"] = new("Chart management", "谱面管理中心", "譜面管理センター"),
            ["chart_library.subtitle"] = new(
                "Import, organise and connect your osu! Songs library.",
                "导入、整理谱面，并连接你的 osu! Songs 曲库。",
                "譜面を取り込み、整理し、osu! Songs ライブラリに接続します。"),
            ["chart_library.overview"] = new("Library overview", "曲库概览", "ライブラリ概要"),
            ["chart_library.total"] = new("TOTAL CHARTS", "谱面总数", "譜面数"),
            ["chart_library.managed"] = new("YOKKO", "Yokko 托管", "Yokko 管理"),
            ["chart_library.external"] = new("OSU!", "外部 osu!", "外部 osu!"),
            ["chart_library.managed_path"] = new("MANAGED LIBRARY", "Yokko 谱面目录", "Yokko 譜面フォルダー"),
            ["chart_library.osu_path"] = new("OSU! SONGS", "osu! Songs 文件夹", "osu! Songs フォルダー"),
            ["chart_library.auto_find"] = new("Auto find", "自动查找", "自動検出"),
            ["chart_library.disable_osu"] = new("Disconnect", "停用连接", "接続解除"),
            ["chart_library.read_only_hint"] = new(
                "External osu! charts stay read-only; Yokko never deletes the source library.",
                "外部 osu! 谱面始终只读，Yokko 不会删除原曲库文件。",
                "外部 osu! 譜面は読み取り専用で、元ファイルは削除しません。"),
            ["chart_library.search"] = new("Search charts, artists or mappers", "搜索谱面、歌曲或谱师", "譜面・曲・作者を検索"),
            ["chart_library.filter_all"] = new("All", "全部", "すべて"),
            ["chart_library.filter_managed"] = new("Yokko managed", "Yokko 托管", "Yokko 管理"),
            ["chart_library.filter_external"] = new("External osu!", "外部 osu!", "外部 osu!"),
            ["chart_library.result_count"] = new("{0} CHARTS", "{0} 张谱面", "{0} 譜面"),
            ["chart_library.empty"] = new("No charts found", "没有找到谱面", "譜面が見つかりません"),
            ["chart_library.empty_hint"] = new(
                "Import a file, connect osu! Songs, or adjust the filters.",
                "可以导入文件、连接 osu! Songs，或调整筛选条件。",
                "ファイルの取り込み、osu! Songs の接続、または絞り込みを変更してください。"),
            ["chart_library.untitled"] = new("Untitled chart", "未命名谱面", "無題の譜面"),
            ["chart_library.source_external"] = new("READ-ONLY // OSU!", "只读 // OSU!", "読み取り専用 // OSU!"),
            ["chart_library.source_package"] = new("YOKKO // PACKAGE", "YOKKO // 图包", "YOKKO // パッケージ"),
            ["chart_library.source_managed"] = new("YOKKO // MANAGED", "YOKKO // 托管", "YOKKO // 管理"),
            ["chart_library.read_only"] = new("Read-only", "只读", "読み取り専用"),
            ["chart_library.remove"] = new("Remove", "移除", "削除"),
            ["chart_library.remove_confirm"] = new("Confirm", "确认移除", "削除確認"),
            ["chart_library.load_more"] = new("Load {0} more charts", "再显示 {0} 张谱面", "さらに {0} 譜面を表示"),
            ["chart_library.refresh"] = new("Refresh", "刷新", "更新"),
            ["chart_library.select_osu"] = new("Select osu! folder", "选择 osu! 文件夹", "osu! フォルダーを選択"),
            ["chart_library.import"] = new("Import charts", "导入谱面", "譜面を取り込む"),
            ["chart_library.import_folder"] = new("Import folder", "导入文件夹", "フォルダーを取り込む"),
            ["chart_library.ready"] = new("Library ready. Drop chart files anywhere to import them too.", "曲库已就绪，也可以把谱面文件直接拖入 Yokko。", "ライブラリの準備ができました。譜面ファイルのドロップにも対応しています。"),
            ["chart_library.importing"] = new("Importing {0}…", "正在导入 {0}…", "{0} を取り込み中…"),
            ["chart_library.imported"] = new("Imported {0} charts.", "已导入 {0} 张谱面。", "{0} 譜面を取り込みました。"),
            ["chart_library.importing_folder"] = new("Importing folder {0}…", "正在导入文件夹 {0}…", "フォルダー {0} を取り込み中…"),
            ["chart_library.imported_folder"] = new("Imported {0} charts from {1} files.", "已从 {1} 个文件导入 {0} 张谱面。", "{1} ファイルから {0} 譜面を取り込みました。"),
            ["chart_library.imported_folder_with_failures"] = new("Imported {0} charts from {1} files; {2} files failed.", "已从 {1} 个文件导入 {0} 张谱面，{2} 个文件失败。", "{1} ファイルから {0} 譜面を取り込み、{2} ファイルが失敗しました。"),
            ["chart_library.no_importable_files"] = new("No supported chart files were found in this folder.", "这个文件夹中没有找到支持的谱面文件。", "このフォルダーには対応する譜面ファイルがありません。"),
            ["chart_library.folder_import_failed"] = new("Folder import failed for all {0} files.", "文件夹中的 {0} 个谱面文件全部导入失败。", "フォルダー内の {0} ファイルを取り込めませんでした。"),
            ["chart_library.folder_picker_unavailable"] = new("Folder selection is unavailable on this platform.", "当前平台不支持文件夹选择。", "この環境ではフォルダーを選択できません。"),
            ["chart_library.file_picker_unavailable"] = new("File selection is unavailable on this platform.", "当前平台不支持文件选择。", "この環境ではファイルを選択できません。"),
            ["chart_library.osu_not_found"] = new("No osu! Songs folder was found automatically.", "没有自动找到 osu! Songs 文件夹。", "osu! Songs フォルダーを自動検出できませんでした。"),
            ["chart_library.scanning_osu"] = new("Indexing osu! Songs…", "正在同步 osu! Songs…", "osu! Songs を同期中…"),
            ["chart_library.osu_ready"] = new("osu! Songs connected: {0} charts.", "osu! Songs 已连接，共 {0} 张谱面。", "osu! Songs に接続しました: {0} 譜面。"),
            ["chart_library.refreshing"] = new("Refreshing the library…", "正在刷新曲库…", "ライブラリを更新中…"),
            ["chart_library.refreshed"] = new("Library refreshed: {0} charts.", "曲库刷新完成，共 {0} 张谱面。", "ライブラリを更新しました: {0} 譜面。"),
            ["chart_library.osu_disabled"] = new("External osu! library disconnected; source files were left untouched.", "已停用外部 osu! 曲库，原文件未作任何改动。", "外部 osu! ライブラリの接続を解除しました。元ファイルは変更していません。"),
            ["chart_library.removing"] = new("Removing the managed chart…", "正在移除托管谱面…", "管理譜面を削除中…"),
            ["chart_library.removed"] = new("Removed {0} managed charts.", "已移除 {0} 张托管谱面。", "管理譜面を {0} 件削除しました。"),
            ["chart_library.cancelled"] = new("Operation cancelled.", "操作已取消。", "操作をキャンセルしました。"),
            ["chart_library.failed"] = new("The operation failed.", "操作失败。", "操作に失敗しました。"),
            ["chart_library.not_configured"] = new("Not configured", "尚未设置", "未設定"),

            ["import.chart.importing"] = new("Importing chart", "正在导入谱面", "譜面をインポート中"),
            ["import.chart.success"] = new("Chart ready", "谱面已就绪", "譜面の準備完了"),
            ["import.chart.success_count"] = new(
                "Imported {0} charts",
                "已导入 {0} 张谱面",
                "{0} 個の譜面をインポートしました"),
            ["import.chart.failed"] = new("Chart import failed", "谱面导入失败", "譜面のインポートに失敗"),
            ["import.replay.importing"] = new(
                "Importing replay",
                "正在导入回放",
                "リプレイをインポート中"),
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
            ["song_select.all"] = new("ALL", "全部", "すべて"),
            ["song_select.key_mode"] = new("KEY MODE", "键位", "キーモード"),
            ["song_select.difficulty_range"] = new("{0} RANGE", "{0} 范围", "{0} 範囲"),
            ["song_select.filters"] = new("FILTER", "筛选", "絞り込み"),
            ["song_select.filters.title"] = new(
                "FILTER SONGS",
                "筛选歌曲",
                "楽曲を絞り込み"),
            ["song_select.filters.subtitle"] = new(
                "Choose only what you want to see",
                "只保留你想看到的歌曲",
                "表示したい楽曲だけを選べます"),
            ["song_select.filters.current"] = new(
                "CURRENT  ·  {0}",
                "当前  ·  {0}",
                "現在  ·  {0}"),
            ["song_select.filters.search_active"] = new("SEARCH", "搜索", "検索"),
            ["song_select.filters.no_converts"] = new(
                "NO CONVERTS",
                "排除转换谱面",
                "変換譜面を除外"),
            ["song_select.filters.summary_two"] = new("{0}  ·  {1}", "{0}  ·  {1}", "{0}  ·  {1}"),
            ["song_select.filters.summary_more"] = new(
                "{0}  ·  {1}  +{2}",
                "{0}  ·  {1}  +{2}",
                "{0}  ·  {1}  +{2}"),
            ["song_select.filters.keys"] = new("KEYS", "键位", "キー数"),
            ["song_select.filters.converts"] = new(
                "CONVERTED MAPS",
                "转换谱面",
                "変換譜面"),
            ["song_select.filters.include"] = new("INCLUDE", "包含", "含める"),
            ["song_select.filters.exclude"] = new("EXCLUDE", "排除", "除外"),
            ["song_select.filters.reset"] = new("RESET", "重置", "リセット"),
            ["song_select.filters.hint"] = new(
                "ARROWS MOVE  ·  ENTER APPLY  ·  F6 / ESC CLOSE",
                "方向键移动  ·  ENTER 应用  ·  F6 / ESC 关闭",
                "矢印キーで移動  ·  ENTER で適用  ·  F6 / ESC で閉じる"),
            ["song_select.sort"] = new("SORT", "排序", "並び替え"),
            ["song_select.sort.title"] = new("SORT LIBRARY", "曲库排序", "ライブラリを並び替え"),
            ["song_select.sort.subtitle"] = new(
                "Selection and preview stay in place",
                "保持当前选中与试听位置",
                "選択位置とプレビューを維持します"),
            ["song_select.sort.direction"] = new("DIRECTION", "方向", "方向"),
            ["song_select.sort.ascending"] = new("ASCENDING", "升序", "昇順"),
            ["song_select.sort.descending"] = new("DESCENDING", "降序", "降順"),
            ["song_select.sort.current_ascending"] = new("{0}  ↑", "{0}  ↑", "{0}  ↑"),
            ["song_select.sort.current_descending"] = new("{0}  ↓", "{0}  ↓", "{0}  ↓"),
            ["song_select.sort.mode.title"] = new("TITLE", "标题", "タイトル"),
            ["song_select.sort.mode.artist"] = new("ARTIST", "艺术家", "アーティスト"),
            ["song_select.sort.mode.mapper"] = new("MAPPER", "谱师", "譜面作者"),
            ["song_select.sort.mode.difficulty"] = new("DIFFICULTY", "难度", "難易度"),
            ["song_select.sort.mode.bpm"] = new("BPM", "BPM", "BPM"),
            ["song_select.sort.mode.length"] = new("LENGTH", "时长", "長さ"),
            ["song_select.sort.mode.last_played"] = new(
                "LAST PLAYED",
                "最近游玩",
                "最終プレイ"),
            ["song_select.sort.mode.best_score"] = new(
                "BEST SCORE",
                "最佳成绩",
                "ベストスコア"),
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
            ["song_select.reload_working"] = new(
                "Scanning beatmap folders…",
                "正在扫描谱面文件夹…",
                "譜面フォルダーを走査中…"),
            ["song_select.reload_complete"] = new(
                "Library refreshed · {0} beatmaps",
                "曲库已刷新 · 当前 {0} 张谱面",
                "ライブラリ更新完了 · {0} 譜面"),
            ["song_select.reload_failed"] = new(
                "Beatmap reload failed",
                "谱面重载失败",
                "譜面の再読み込みに失敗しました"),
            ["song_select.reload_summary"] = new(
                "+{0} added · −{1} removed · disk rescan complete",
                "新增 {0} · 移除 {1} · 磁盘扫描完成",
                "追加 {0} · 削除 {1} · ディスク再走査完了"),
            ["mods.title"] = new("GAMEPLAY MODS", "游玩 MOD", "ゲームプレイ MOD"),
            ["mods.subtitle"] = new(
                "Customize your play experience.",
                "自定义你的游玩体验。",
                "プレイ体験をカスタマイズ。"),
            ["mods.categories"] = new("CATEGORIES", "分类", "カテゴリー"),
            ["mods.catalogue"] = new("MOD CATALOGUE", "MOD 目录", "MOD カタログ"),
            ["mods.focus_summary"] = new(
                "FOCUS {0}  //  ACTIVE {1:00}",
                "焦点 {0}  //  已启用 {1:00}",
                "フォーカス {0}  //  有効 {1:00}"),
            ["mods.bus_summary"] = new(
                "MOD BUS // {0:00} ACTIVE",
                "MOD 总览 // 已启用 {0:00}",
                "MOD 一覧 // {0:00} 有効"),
            ["mods.choose"] = new("CHOOSE A MOD", "选择 MOD", "MOD を選択"),
            ["mods.choose_hint"] = new(
                "Browse this category · click a card to enable or cycle",
                "点击卡片即可启用或切换",
                "カテゴリーを確認 · カードを押して有効化または切替"),
            ["mods.details"] = new("MOD DETAILS", "当前选择", "MOD 詳細"),
            ["mods.add_focused"] = new(
                "ADD FOCUSED MOD",
                "添加当前 MOD",
                "選択中の MOD を追加"),
            ["mods.category.difficulty_down"] = new("DIFFICULTY DOWN", "难度降低", "難易度を下げる"),
            ["mods.category.difficulty_up"] = new("DIFFICULTY UP", "难度提升", "難易度を上げる"),
            ["mods.category.conversion"] = new("CONVERSION", "谱面转换", "変換"),
            ["mods.category.automation"] = new("AUTOMATION", "自动游玩", "自動プレイ"),
            ["mods.category.fun"] = new("FUN", "娱乐", "お楽しみ"),
            ["mods.speed_multiplier"] = new("SPEED MULTIPLIER", "速度倍率", "スピード倍率"),
            ["mods.active_mods"] = new("ACTIVE MODS", "已启用的 MOD", "有効な MOD"),
            ["mods.active_count"] = new("({0} ACTIVE)", "（已启用 {0}）", "（{0} 有効）"),
            ["mods.active"] = new("ACTIVE", "已启用", "有効"),
            ["mods.settings.active_hint"] = new(
                "Enabled · adjust below",
                "已启用 · 下面可以直接调整",
                "有効 · 下で直接調整できます"),
            ["mods.settings.active_short"] = new(
                "Enabled",
                "已启用",
                "有効"),
            ["mods.settings.title"] = new(
                "Settings",
                "参数设置",
                "設定"),
            ["mods.settings.for_mod"] = new(
                "{0} settings",
                "{0}参数",
                "{0}の設定"),
            ["mods.settings.preview_hint"] = new(
                "Turn it on, then adjust",
                "先启用，再调整参数",
                "有効にしてから調整してください"),
            ["mods.settings.preview_short"] = new(
                "Preview",
                "预览",
                "プレビュー"),
            ["mods.settings.sections"] = new("Pages", "分类", "分類"),
            ["mods.settings.tab_challenge"] = new("CHL", "挑战", "挑戦"),
            ["mods.settings.tab_difficulty"] = new("DA", "数值", "数値"),
            ["mods.settings.tab_visibility"] = new("VIS", "视野", "視野"),
            ["mods.settings.tab_muted"] = new("MU", "静音", "消音"),
            ["mods.settings.tab_rate"] = new("RATE", "速度", "速度"),
            ["mods.settings.tab_conversion"] = new("CVT", "转换", "変換"),
            ["mods.settings.on"] = new("ON", "开", "オン"),
            ["mods.settings.off"] = new("OFF", "关", "オフ"),
            ["mods.settings.select_first"] = new(
                "Enable {0} to adjust these settings",
                "先启用 {0}，再调整这些参数",
                "先に {0} を有効にしてください"),
            ["mods.settings.minimum_accuracy"] = new(
                "Minimum accuracy",
                "最低准确率",
                "最低精度"),
            ["mods.settings.fail_rule"] = new(
                "Fail condition",
                "失败标准",
                "失敗条件"),
            ["mods.settings.maximum_possible"] = new(
                "Maximum possible",
                "理论最高",
                "到達可能な最高値"),
            ["mods.settings.current_accuracy"] = new(
                "Current accuracy",
                "当前准确率",
                "現在の精度"),
            ["mods.settings.fail_below_reachable"] = new(
                "Fail when reachable accuracy drops below {0}",
                "可达到准确率低于 {0} 时失败",
                "到達可能精度が {0} 未満で失敗"),
            ["mods.settings.fail_below_current"] = new(
                "Fail when current accuracy drops below {0}",
                "当前准确率低于 {0} 时失败",
                "現在精度が {0} 未満で失敗"),
            ["mods.settings.hp_drain"] = new(
                "HP drain",
                "掉血速度",
                "HP 減少"),
            ["mods.settings.judgement_difficulty"] = new(
                "Judgement difficulty · OD",
                "判定难度 · OD",
                "判定難易度 · OD"),
            ["mods.settings.map_values"] = new(
                "Use map values",
                "使用谱面数值",
                "譜面の値を使用"),
            ["mods.settings.extended_range"] = new(
                "Extended range",
                "扩展范围",
                "拡張範囲"),
            ["mods.settings.map_value"] = new(
                "Map {0}",
                "谱面 {0}",
                "譜面 {0}"),
            ["mods.settings.coverage"] = new(
                "Coverage",
                "遮挡比例",
                "遮蔽率"),
            ["mods.settings.window_size"] = new(
                "Viewing area",
                "视野大小",
                "視野サイズ"),
            ["mods.settings.with_scroll"] = new(
                "With scroll",
                "顺着下落方向",
                "スクロール方向"),
            ["mods.settings.against_scroll"] = new(
                "Against scroll",
                "反向遮挡",
                "逆方向"),
            ["mods.settings.fixed_size"] = new(
                "Fixed size",
                "固定大小",
                "固定サイズ"),
            ["mods.settings.combo_size"] = new(
                "Shrink with combo",
                "随连击缩小",
                "コンボで縮小"),
            ["mods.settings.cover_with_scroll"] = new(
                "The cover expands with the scroll direction",
                "遮挡区域会顺着下落方向扩大",
                "カバーはスクロール方向に広がります"),
            ["mods.settings.cover_against_scroll"] = new(
                "The cover expands against the scroll direction",
                "遮挡区域会向下落反方向扩大",
                "カバーはスクロールと逆方向に広がります"),
            ["mods.settings.flashlight_combo"] = new(
                "The viewing area shrinks as combo increases",
                "连击越高，可见范围越小",
                "コンボが増えるほど視野が狭くなります"),
            ["mods.settings.flashlight_fixed"] = new(
                "The viewing area stays the same size",
                "可见范围始终保持相同大小",
                "視野サイズは固定です"),
            ["mods.settings.speed_change"] = new(
                "Speed multiplier",
                "速度倍率",
                "速度倍率"),
            ["mods.settings.adjust_pitch"] = new(
                "Change pitch with speed",
                "音高随速度变化",
                "速度に合わせて音程変更"),
            ["mods.settings.pitch_behaviour"] = new(
                "OFF keeps the original pitch · ON follows the rate",
                "关闭时保持原音高 · 开启后音高随速度变化",
                "オフは元の音程を維持 · オンは速度に追従"),
            ["mods.settings.frequency_locked"] = new(
                "Pitch multiplier is fixed at {0}",
                "该模式的音高倍率固定为 {0}",
                "音程倍率は {0} に固定"),
            ["mods.settings.rate_precision"] = new(
                "0.50x–2.00x range · 0.01x per step",
                "范围 0.50x–2.00x · 每次调整 0.01x",
                "範囲 0.50x–2.00x · 1 回 0.01x"),
            ["mods.settings.initial_rate"] = new(
                "Starting speed",
                "初始速度",
                "開始速度"),
            ["mods.settings.final_rate"] = new(
                "Final speed",
                "最终速度",
                "最終速度"),
            ["mods.settings.adaptive_recent"] = new(
                "Responds to your last 8 results",
                "根据最近 8 次判定自动调速",
                "直近 8 回の判定に応じて変化"),
            ["mods.settings.adaptive_rule"] = new(
                "Accurate hits speed up · misses slow down",
                "打得准就加速 · 出现失误就减速",
                "正確な判定で加速 · ミスで減速"),
            ["mods.settings.fade_length"] = new(
                "Fade after",
                "淡出所需连击",
                "フェード開始コンボ"),
            ["mods.settings.combo_count"] = new(
                "{0} combo",
                "{0} 连击",
                "{0} コンボ"),
            ["mods.settings.start_muted"] = new(
                "Start muted",
                "开局静音",
                "開始時からミュート"),
            ["mods.settings.keep_metronome"] = new(
                "Keep metronome",
                "保留节拍器",
                "メトロノームを残す"),
            ["mods.settings.mute_keysounds"] = new(
                "Mute keysounds too",
                "同时静音打击音",
                "キー音もミュート"),
            ["mods.settings.smooth_fade"] = new(
                "Music fades smoothly over 500 ms",
                "音乐会在 500 ms 内平滑淡出",
                "音楽は 500 ms かけて滑らかに消えます"),
            ["mods.settings.default_perfect_rule"] = new(
                "Default lazer mania rule",
                "默认 lazer mania 规则",
                "lazer mania の標準ルール"),
            ["mods.settings.great_keeps_run"] = new(
                "Great or better keeps the run alive",
                "Great 或更好的判定不会失败",
                "Great 以上なら継続"),
            ["mods.settings.extra_rule"] = new(
                "Extra restriction",
                "额外限制",
                "追加制限"),
            ["mods.settings.require_perfect"] = new(
                "Require Perfect hits",
                "只允许 Perfect 判定",
                "Perfect 判定のみ許可"),
            ["mods.settings.allowed_pauses"] = new(
                "Allowed pauses",
                "可暂停次数",
                "一時停止できる回数"),
            ["mods.settings.zero_pauses"] = new(
                "0 means pausing is completely disabled",
                "设为 0 时完全禁止暂停",
                "0 で一時停止を完全に禁止"),
            ["mods.settings.final_rate_at_75"] = new(
                "Reaches the final speed at 75% of the map",
                "打到谱面 75% 时达到最终速度",
                "譜面の 75% で最終速度に到達"),
            ["mods.settings.only_perfect"] = new(
                "Only Perfect judgements are allowed",
                "只有 Perfect 判定才不会失败",
                "Perfect 判定のみ成功"),
            ["mods.settings.great_or_better"] = new(
                "Great or better is allowed",
                "Great 或更好的判定不会失败",
                "Great 以上なら成功"),
            ["mods.settings.pause_disabled"] = new(
                "Pausing is disabled",
                "本局完全禁止暂停",
                "一時停止は禁止"),
            ["mods.settings.pause_count_allowed"] = new(
                "The first {0} pauses are allowed",
                "本局最多可以暂停 {0} 次",
                "このプレイでは {0} 回まで一時停止可能"),
            ["mods.settings.custom_seed"] = new(
                "Custom seed",
                "自定义随机种子",
                "カスタムシード"),
            ["mods.settings.same_seed_shuffle"] = new(
                "The same seed produces the same lane shuffle",
                "相同种子会得到相同的按键排列",
                "同じシードなら同じレーン配置"),
            ["mods.settings.invalid_seed"] = new(
                "Enter an integer from -2147483648 to 2147483647",
                "请输入 -2147483648 到 2147483647 之间的整数",
                "-2147483648 から 2147483647 の整数を入力"),
            ["mods.settings.seed_applied"] = new(
                "Custom seed applied",
                "已应用这个随机种子",
                "カスタムシードを適用済み"),
            ["mods.settings.seed_placeholder"] = new(
                "Enter an integer",
                "输入一个整数",
                "整数を入力"),
            ["mods.settings.generate_seed"] = new(
                "Generate a new seed",
                "换一个随机种子",
                "新しいシードを生成"),
            ["mods.settings.key_conversion"] = new(
                "Key conversion",
                "按键数转换",
                "キー数変換"),
            ["mods.settings.regenerate_original"] = new(
                "Regenerates lanes from the original objects",
                "会根据原始物件重新生成轨道",
                "元のオブジェクトからレーンを再生成"),
            ["mods.settings.native_unchanged"] = new(
                "Native mania charts cannot be converted",
                "原生 mania 谱面不会被强行转换",
                "ネイティブ mania 譜面は変換しません"),
            ["mods.settings.dual_stages"] = new(
                "Split into two stages",
                "拆成前后两组轨道",
                "2 ステージに分割"),
            ["mods.settings.key_target"] = new(
                "Target: {0} keys",
                "目标：{0} 键",
                "変換先：{0} キー"),
            ["mods.settings.key_target_dual"] = new(
                "Target: two {0}-key stages",
                "目标：两组 {0} 键轨道",
                "変換先：{0} キーを 2 ステージ"),
            ["mods.settings.key_default"] = new(
                "Target: lazer default",
                "目标：使用 lazer 默认按键数",
                "変換先：lazer のデフォルト"),
            ["mods.settings.key_default_dual"] = new(
                "Target: two lazer-default stages",
                "目标：按 lazer 默认规则拆成两组",
                "変換先：lazer のデフォルトを 2 ステージ"),
            ["mods.settings.key_native_source"] = new(
                "Current chart is native mania",
                "当前是原生 mania 谱面，不能转换按键数",
                "現在の譜面はネイティブ mania のため変換不可"),
            ["mods.activate_hint"] = new("SPACE · ACTIVATE", "空格 · 启用", "SPACE · 有効化"),
            ["mods.back"] = new("BACK", "返回", "戻る"),
            ["mods.reset"] = new("RESET", "重置", "リセット"),
            ["mods.done"] = new("DONE", "完成", "完了"),
            ["mods.standard_only"] = new(
                "Available only for charts imported from osu!standard.",
                "仅适用于从 osu!standard 导入的谱面。",
                "osu!standard からインポートした譜面でのみ利用できます。"),
            ["mods.definition.easy.name"] = new("Easy", "降低难度", "イージー"),
            ["mods.definition.easy.description"] = new("Forgiving difficulty and gentler health drain.", "判定更宽松，掉血也更慢。", "難易度とライフ減少を緩和します。"),
            ["mods.definition.no-fail.name"] = new("No Fail", "不会失败", "ノーフェイル"),
            ["mods.definition.no-fail.description"] = new("Keep playing even when your health reaches zero.", "血量归零也不会中断游玩。", "ライフがゼロになってもプレイを続けられます。"),
            ["mods.definition.no-pause.name"] = new("No Pause", "限制暂停次数", "ノーポーズ"),
            ["mods.definition.no-pause.description"] = new("Limit how many times gameplay may be paused.", "限制一局内可暂停的次数；设为 0 时完全禁止暂停。", "プレイ中に一時停止できる回数を制限します。"),
            ["mods.no_pause.allowance"] = new("PAUSES: {0}", "可暂停：{0} 次", "一時停止：{0} 回"),
            ["mods.definition.half-time.name"] = new("Half Time", "歌曲减速", "ハーフタイム"),
            ["mods.definition.half-time.description"] = new("Slow the song down to 75% speed.", "将歌曲速度降低至 75%。", "楽曲速度を 75% に下げます。"),
            ["mods.definition.daycore.name"] = new("Daycore", "降调减速", "デイコア"),
            ["mods.definition.daycore.description"] = new("Slow down with a lower-pitched soundtrack.", "歌曲变慢，声音也会变低沉。", "速度と音程を下げます。"),
            ["mods.definition.no-release.name"] = new("No Release", "松键不判定", "ノーリリース"),
            ["mods.definition.no-release.description"] = new("Ignore judgements when hold notes are released.", "长按音符只看按下，松开时不判好坏。", "長押しノーツを離した時の判定を無視します。"),
            ["mods.definition.hard-rock.name"] = new("Hard Rock", "判定变严", "ハードロック"),
            ["mods.definition.hard-rock.description"] = new("Raise the difficulty and health drain.", "收紧判定并加快掉血。", "難易度とライフ減少を上げます。"),
            ["mods.definition.sudden-death.name"] = new("Sudden Death", "一失即败", "サドンデス"),
            ["mods.definition.sudden-death.description"] = new("A single miss ends the run.", "出现一次 Miss 就立即失败。", "1 回の Miss でプレイ終了になります。"),
            ["mods.definition.perfect.name"] = new("Perfect", "完美挑战", "パーフェクト"),
            ["mods.definition.perfect.description"] = new("Any judgement below Great ends the run.", "出现低于 Great 的判定就立即失败。", "Great 未満の判定でプレイ終了になります。"),
            ["mods.definition.double-time.name"] = new("Double Time", "加速", "ダブルタイム"),
            ["mods.definition.double-time.description"] = new("Speed the song up to 150%.", "将歌曲速度提高至 150%。", "楽曲速度を 150% に上げます。"),
            ["mods.definition.nightcore.name"] = new("Nightcore", "升调加速", "ナイトコア"),
            ["mods.definition.nightcore.description"] = new("Speed up with a higher-pitched soundtrack.", "提高速度，同时提高音调。", "速度と音程を上げます。"),
            ["mods.definition.fade-in.name"] = new("Fade In", "音符渐现", "フェードイン"),
            ["mods.definition.fade-in.description"] = new("Notes appear gradually as they approach.", "音符接近时逐渐出现。", "接近するノーツが徐々に現れます。"),
            ["mods.definition.hidden.name"] = new("Hidden", "音符提前消失", "ヒドゥン"),
            ["mods.definition.hidden.description"] = new("Notes fade before reaching the judgement line.", "音符到达判定线前逐渐消失。", "ノーツが判定ラインの前で消えていきます。"),
            ["mods.definition.cover.name"] = new("Cover", "遮住部分音符", "カバー"),
            ["mods.definition.cover.description"] = new("Hide part of the playfield with a cover.", "用遮罩挡住一部分音符区域。", "カバーでプレイフィールドの一部を隠します。"),
            ["mods.definition.flashlight.name"] = new("Flashlight", "缩小视野", "フラッシュライト"),
            ["mods.definition.flashlight.description"] = new("See notes only through a limited viewing area.", "只能在有限的可视范围内看到音符。", "限られた範囲内だけノーツが見えます。"),
            ["mods.definition.accuracy-challenge.name"] = new("Accuracy Challenge", "精度挑战", "精度チャレンジ"),
            ["mods.definition.accuracy-challenge.description"] = new("Fail when accuracy drops below your target.", "准确率低于设定目标时立即失败。", "精度が目標を下回ると失敗します。"),
            ["mods.definition.iidx-hard-gauge.name"] = new("IIDX Hard Gauge", "IIDX 硬血条", "IIDX ハードゲージ"),
            ["mods.definition.iidx-hard-gauge.description"] = new("Use IIDX Hard health changes independently of judgement timing.", "使用 IIDX Hard 的血量增减规则，不改变当前判定窗口。", "判定タイミングはそのまま、IIDX Hard のゲージ増減を使用します。"),
            ["mods.definition.lr2-hard-gauge.name"] = new("LR2 Hard Gauge", "LR2 硬血条", "LR2 ハードゲージ"),
            ["mods.definition.lr2-hard-gauge.description"] = new("Use LR2 Hard health changes independently of judgement timing.", "使用 LR2 Hard 的血量增减规则，不改变当前判定窗口。", "判定タイミングはそのまま、LR2 Hard のゲージ増減を使用します。"),
            ["mods.definition.beatoraja-hard-gauge.name"] = new("beatoraja Hard Gauge", "beatoraja 硬血条", "beatoraja ハードゲージ"),
            ["mods.definition.beatoraja-hard-gauge.description"] = new("Use beatoraja Hard health changes independently of judgement timing.", "使用 beatoraja Hard 的血量增减规则，不改变当前判定窗口。", "判定タイミングはそのまま、beatoraja Hard のゲージ増減を使用します。"),
            ["mods.definition.random.name"] = new("Random", "随机", "ランダム"),
            ["mods.definition.random.description"] = new("Shuffle note columns with a repeatable seed.", "随机打乱音符所在的轨道。", "再現可能なシードでノーツ列を並べ替えます。"),
            ["mods.definition.dual-stages.name"] = new("Dual Stages", "分成两个游玩区", "デュアルステージ"),
            ["mods.definition.dual-stages.description"] = new("Split converted charts across two playfields.", "把转换后的谱面拆到两个游玩区域。", "変換した譜面を 2 つのプレイフィールドに分けます。"),
            ["mods.definition.mirror.name"] = new("Mirror", "镜像", "ミラー"),
            ["mods.definition.mirror.description"] = new("Reverse every note column.", "把所有轨道左右翻转。", "すべてのノーツ列を反転します。"),
            ["mods.definition.difficulty-adjust.name"] = new("Difficulty Adjust", "自定义难度", "難易度調整"),
            ["mods.definition.difficulty-adjust.description"] = new("Customise health drain and judgement difficulty.", "自己调整判定宽松程度和掉血速度。", "ライフ減少と判定難易度を調整します。"),
            ["mods.definition.classic.name"] = new("Classic", "经典判定", "クラシック"),
            ["mods.definition.classic.description"] = new("Use classic mania scoring and behaviour.", "改用旧版 mania 的判定与计分规则。", "クラシックな mania のスコアと動作を使用します。"),
            ["mods.definition.invert.name"] = new("Invert", "短键长键互换", "インバート"),
            ["mods.definition.invert.description"] = new("Swap tap notes and hold-note bodies.", "把短按音符变成长按，长按音符变成短按。", "タップノーツと長押しノーツの本体を入れ替えます。"),
            ["mods.definition.constant-speed.name"] = new("Constant Speed", "去掉谱面变速", "コンスタントスピード"),
            ["mods.definition.constant-speed.description"] = new("Keep the visual scroll velocity constant.", "忽略谱面的忽快忽慢，让音符始终匀速移动；歌曲速度不变。", "見た目のスクロール速度を一定にします。"),
            ["mods.definition.hold-off.name"] = new("Hold Off", "长按变短按", "ホールドオフ"),
            ["mods.definition.hold-off.description"] = new("Convert hold notes into regular tap notes.", "把所有长按音符变成普通短按音符。", "長押しノーツを通常のタップノーツに変換します。"),
            ["mods.definition.key-count.name"] = new("{0} Keys", "{0} 键", "{0} キー"),
            ["mods.definition.key-count.description"] = new("Convert a standard-mode chart to this key count.", "将标准模式谱面转换为此键数。", "standard モードの譜面をこのキー数に変換します。"),
            ["mods.definition.autoplay.name"] = new("Developer Autoplay", "开发者自动打谱", "開発者オートプレイ"),
            ["mods.definition.autoplay.description"] = new("Automatically play and save the replay and score.", "自动打谱，保存回放和成绩。", "譜面を自動で完走し、リプレイとスコアを保存します。"),
            ["mods.definition.cinema.name"] = new("Cinema", "影院模式", "シネマ"),
            ["mods.definition.cinema.description"] = new("Watch an automated performance without the playfield.", "隐藏游玩区域并观看自动演示。", "プレイフィールドを隠して自動プレイを鑑賞します。"),
            ["mods.definition.wind-up.name"] = new("Wind Up", "逐渐加速", "ウインドアップ"),
            ["mods.definition.wind-up.description"] = new("Gradually increase playback speed.", "逐渐提高播放速度。", "再生速度を徐々に上げます。"),
            ["mods.definition.wind-down.name"] = new("Wind Down", "逐渐减速", "ウインドダウン"),
            ["mods.definition.wind-down.description"] = new("Gradually decrease playback speed.", "逐渐降低播放速度。", "再生速度を徐々に下げます。"),
            ["mods.definition.muted.name"] = new("Muted", "音乐逐渐静音", "ミュート"),
            ["mods.definition.muted.description"] = new("Play with configurable audio cues muted.", "按连击数逐步淡化音乐，并可保留节拍器或打击音。", "設定したオーディオキューを消してプレイします。"),
            ["mods.definition.adaptive-speed.name"] = new("Adaptive Speed", "按表现自动变速", "アダプティブスピード"),
            ["mods.definition.adaptive-speed.description"] = new("Change speed in response to recent accuracy.", "根据你最近打得准不准，自动调整歌曲速度。", "直近の精度に応じて速度を変えます。"),
            ["mods.definition.score-v2.name"] = new("Score V2", "新版计分", "スコア V2"),
            ["mods.definition.score-v2.description"] = new("Use the modern score calculation.", "使用现代计分方式。", "新しいスコア計算を使用します。"),

            ["gameplay.pause.title"] = new("Paused", "暂停", "一時停止"),
            ["gameplay.pause.subtitle"] = new("Catch your breath.", "先喘口气。", "ひと休み。"),
            ["gameplay.pause.bubble"] = new("Take a break!", "休息一下！", "ひと休み！"),
            ["gameplay.pause.bubble_alt1"] = new("Ready?", "随时继续！", "いつでも！"),
            ["gameplay.pause.bubble_alt2"] = new("Stretch!", "伸个懒腰！", "のびのび！"),
            ["gameplay.pause.bubble_alt3"] = new("Hydrate!", "喝口水吧！", "水分補給！"),
            ["gameplay.pause.resume"] = new("Resume", "继续游戏", "ゲームに戻る"),
            ["gameplay.pause.resume_hint"] = new("ESC  RESUME", "ESC  继续", "ESC  再開"),
            ["gameplay.pause.retry"] = new("Restart", "重新开始", "リスタート"),
            ["gameplay.retry.restarting"] = new("Restarting…", "正在重新开始…", "リスタート中…"),
            ["gameplay.pause.settings"] = new("Settings", "设置", "設定"),
            ["gameplay.pause.pause_settings"] = new("Pause settings", "暂停设置", "ポーズ設定"),
            ["gameplay.pause.resume_countdown"] = new("Resume countdown", "恢复倒计时", "再開カウントダウン"),
            ["gameplay.pause.seconds"] = new("{0} s", "{0} 秒", "{0} 秒"),
            ["gameplay.pause.countdown_off"] = new("Off", "关闭", "オフ"),
            ["gameplay.pause.exit"] = new("Exit play", "退出游玩", "選曲へ戻る"),
            ["gameplay.pause.hint_select"] = new("SELECT", "选择", "選択"),
            ["gameplay.pause.hint_confirm"] = new("CONFIRM", "确认", "決定"),
            ["gameplay.pause.hint_retry"] = new("RETRY", "重试", "リトライ"),

            ["gameplay.layout_editor.title"] = new(
                "HUD layout",
                "HUD 布局",
                "HUD レイアウト"),
            ["gameplay.layout_editor.hint"] = new(
                "Tab select · arrows move · Ctrl+S save",
                "Tab 切换 · 方向键移动 · Ctrl+S 保存",
                "Tab 切替 · 矢印移動 · Ctrl+S 保存"),
            ["gameplay.layout_editor.hide_hint"] = new(
                "Saved · {0} hide UI · Ctrl+arrows resize",
                "已保存 · {0} 隐藏界面 · Ctrl+方向键缩放",
                "保存済み · {0} UI 非表示 · Ctrl+矢印拡縮"),
            ["gameplay.layout_editor.unsaved_hint"] = new(
                "Unsaved changes · Esc asks before discarding",
                "未保存更改 · Esc 会先确认",
                "未保存の変更 · Esc で破棄確認"),
            ["gameplay.layout_editor.discard_confirm_hint"] = new(
                "Press Esc again or click Discard",
                "再次按 Esc 或点击“放弃”",
                "もう一度 Esc または「破棄」を押す"),
            ["gameplay.layout_editor.playfield"] = new(
                "Playfield · drag / resize",
                "轨道 · 拖动 / 拉伸边框",
                "レーン · ドラッグ / 拡縮"),
            ["gameplay.layout_editor.hud"] = new(
                "Info panel · drag / resize",
                "信息面板 · 拖动 / 拉伸边框",
                "情報パネル · ドラッグ / 拡縮"),
            ["gameplay.layout_editor.accuracy"] = new(
                "Accuracy · drag / resize",
                "ACC · 拖动 / 拉伸边框",
                "精度 · ドラッグ / 拡縮"),
            ["gameplay.layout_editor.progress"] = new(
                "Progress bar · drag / resize",
                "进度条 · 拖动 / 拉伸边框",
                "プログレスバー · ドラッグ / 拡縮"),
            ["gameplay.layout_editor.information"] = new(
                "Lower information · drag / resize",
                "下方信息 · 拖动 / 拉伸边框",
                "下部情報 · ドラッグ / 拡縮"),
            ["gameplay.layout_editor.timing_bar"] = new(
                "Timing bar · drag / resize",
                "判定条 · 拖动 / 拉伸边框",
                "判定バー · ドラッグ / 拡縮"),
            ["gameplay.layout_editor.combo"] = new(
                "Combo · drag / resize",
                "Combo · 拖动 / 拉伸边框",
                "コンボ · ドラッグ / 拡縮"),
            ["gameplay.layout_editor.judgement"] = new(
                "Judgement · drag / resize",
                "判定文字 · 拖动 / 拉伸边框",
                "判定表示 · ドラッグ / 拡縮"),
            ["gameplay.layout_editor.performance_readout"] = new(
                "Performance readout · drag",
                "性能读数 · 拖动",
                "パフォーマンス表示 · ドラッグ"),
            ["gameplay.layout_editor.cover_top_drag"] = new(
                "TOP COVER · DRAG TO ADD / RESIZE",
                "上挡板 · 拖动添加 / 调整",
                "上カバー · ドラッグで追加 / 調整"),
            ["gameplay.layout_editor.cover_bottom_drag"] = new(
                "BOTTOM COVER · DRAG TO ADD / RESIZE",
                "下挡板 · 拖动添加 / 调整",
                "下カバー · ドラッグで追加 / 調整"),
            ["gameplay.layout_editor.reset"] = new(
                "Reset",
                "重置",
                "リセット"),
            ["gameplay.layout_editor.undo"] = new(
                "Undo",
                "撤销",
                "元に戻す"),
            ["gameplay.layout_editor.redo"] = new(
                "Redo",
                "重做",
                "やり直す"),
            ["gameplay.layout_editor.cancel"] = new(
                "Cancel",
                "取消",
                "キャンセル"),
            ["gameplay.layout_editor.discard_confirm"] = new(
                "Discard",
                "放弃",
                "破棄"),
            ["gameplay.layout_editor.save"] = new(
                "Save & return",
                "保存并返回",
                "保存して戻る"),
            ["gameplay.layout_editor.test_play"] = new(
                "Test play",
                "试玩布局",
                "テストプレイ"),
            ["gameplay.layout_editor.autoplay_demo"] = new(
                "Autoplay demo",
                "自动演示",
                "オートデモ"),
            ["gameplay.layout_editor.autoplay_demo_active"] = new(
                "AUTOPLAY DEMO",
                "自动演示中",
                "オートデモ中"),
            ["gameplay.layout_editor.autoplay_demo_exit_hint"] = new(
                "Esc or Exit demo returns to the editor",
                "按 Esc 或退出演示返回编辑器",
                "Esc またはデモ終了でエディターに戻る"),
            ["gameplay.layout_editor.autoplay_demo_exit"] = new(
                "Exit demo",
                "退出演示",
                "デモ終了"),
            ["gameplay.layout_editor.window_controls"] = new(
                "Window controls",
                "窗口总控",
                "ウィンドウ管理"),
            ["gameplay.layout_editor.window_actions"] = new(
                "Actions",
                "操作窗口",
                "操作"),
            ["gameplay.layout_editor.drag_title_hint"] = new(
                "Drag title · click to show / hide",
                "拖动标题 · 点击显示 / 隐藏",
                "タイトルをドラッグ · クリックで表示切替"),
            ["gameplay.layout_editor.preview"] = new(
                "Full-page preview",
                "完整页面预览",
                "全体プレビュー"),
            ["gameplay.layout_editor.inspector"] = new(
                "ELEMENTS & GEOMETRY",
                "元素与精确位置",
                "要素と正確な位置"),
            ["gameplay.layout_editor.layer.playfield"] = new(
                "Playfield",
                "轨道",
                "レーン"),
            ["gameplay.layout_editor.layer.hud"] = new(
                "Info panel",
                "信息面板",
                "情報パネル"),
            ["gameplay.layout_editor.layer.accuracy"] = new(
                "Accuracy",
                "ACC",
                "精度"),
            ["gameplay.layout_editor.layer.progress"] = new(
                "Progress bar",
                "进度条",
                "プログレスバー"),
            ["gameplay.layout_editor.layer.information"] = new(
                "Lower information",
                "下方信息",
                "下部情報"),
            ["gameplay.layout_editor.layer.timing_bar"] = new(
                "Timing bar",
                "判定条",
                "判定バー"),
            ["gameplay.layout_editor.layer.combo"] = new(
                "Combo",
                "Combo",
                "コンボ"),
            ["gameplay.layout_editor.layer.judgement"] = new(
                "Judgement",
                "判定文字",
                "判定表示"),
            ["gameplay.layout_editor.layer.performance_readout"] = new(
                "Performance readout",
                "性能读数",
                "パフォーマンス表示"),
            ["gameplay.layout_editor.layer.hit_effects"] = new(
                "Hit effects",
                "击打特效",
                "ヒットエフェクト"),
            ["gameplay.layout_editor.centre_x"] = new(
                "Centre X",
                "水平居中",
                "水平中央"),
            ["gameplay.layout_editor.centre_y"] = new(
                "Centre Y",
                "垂直居中",
                "垂直中央"),
            ["gameplay.layout_editor.snap_hint"] = new(
                "Drag to move · Shift locks axis · Alt bypasses snap · Ctrl+wheel resizes lanes",
                "拖动即可移动 · Shift 锁定方向 · Alt 临时关闭吸附 · Ctrl+滚动缩放轨道",
                "ドラッグで移動 · Shift 軸固定 · Alt でスナップ無効 · Ctrl+ホイールでレーン拡縮"),
            ["gameplay.layout_editor.covers"] = new(
                "LANE GEOMETRY",
                "轨道几何",
                "レーン配置"),
            ["gameplay.layout_editor.top_cover"] = new(
                "Top",
                "上挡板",
                "上側"),
            ["gameplay.layout_editor.bottom_cover"] = new(
                "Bottom",
                "下挡板",
                "下側"),
            ["gameplay.layout_editor.judgement_line"] = new(
                "Hit line",
                "判定线",
                "判定ライン"),
            ["gameplay.layout_editor.judgement_line_drag"] = new(
                "Hit line · drag to adjust",
                "判定线 · 拖动调整",
                "判定ライン · ドラッグで調整"),
            ["gameplay.layout_editor.reset_line"] = new(
                "Reset",
                "复位",
                "リセット"),
            ["gameplay.layout_editor.add_cover"] = new(
                "Add",
                "添加",
                "追加"),
            ["gameplay.layout_editor.remove_cover"] = new(
                "Remove",
                "移除",
                "削除"),
            ["gameplay.layout_editor.cover_hint"] = new(
                "Enter px values or drag the highlighted bars on the lane.",
                "输入像素数值，或直接拖动轨道上的高亮条。",
                "px 値を入力、またはレーン上のバーをドラッグ。"),
            ["gameplay.layout_editor.live_settings"] = new(
                "LIVE PLAY SETTINGS",
                "实时游玩设置",
                "リアルタイム設定"),
            ["gameplay.layout_editor.feedback_settings"] = new(
                "JUDGEMENT FEEDBACK",
                "判定反馈",
                "判定フィードバック"),
            ["gameplay.layout_editor.judgement_duration"] = new(
                "Display time",
                "判定停留",
                "表示時間"),
            ["gameplay.layout_editor.judgement_opacity"] = new(
                "Opacity",
                "判定透明度",
                "不透明度"),
            ["gameplay.layout_editor.hit_error"] = new(
                "Hit error",
                "误差数值",
                "判定誤差"),
            ["gameplay.layout_editor.hit_error_size"] = new(
                "Hit error size",
                "误差大小",
                "判定誤差サイズ"),
            ["gameplay.layout_editor.timing_bar_visibility"] = new(
                "Timing bar",
                "判定条",
                "判定バー"),
            ["gameplay.layout_editor.show"] = new(
                "Show",
                "显示",
                "表示"),
            ["gameplay.layout_editor.hide"] = new(
                "Hide",
                "隐藏",
                "非表示"),
            ["gameplay.layout_editor.skin"] = new(
                "Skin",
                "皮肤",
                "スキン"),
            ["gameplay.layout_editor.default_skin"] = new(
                "Yokko default",
                "Yokko 默认",
                "Yokko デフォルト"),
            ["gameplay.layout_editor.scroll_speed"] = new(
                "Speed",
                "流速",
                "速度"),
            ["gameplay.layout_editor.downscroll"] = new(
                "Down",
                "下落式",
                "下向き"),
            ["gameplay.layout_editor.upscroll"] = new(
                "Up",
                "上升式",
                "上向き"),
            ["gameplay.layout_editor.background_dim"] = new(
                "Background dim",
                "背景暗度",
                "背景の暗さ"),
            ["gameplay.layout_editor.ln_cut"] = new(
                "Cut LN",
                "削 LN",
                "LN カット"),
            ["gameplay.layout_editor.on"] = new(
                "On",
                "开",
                "オン"),
            ["gameplay.layout_editor.off"] = new(
                "Off",
                "关",
                "オフ"),

            ["gameplay.result.title"] = new("RESULT", "结算", "リザルト"),
            ["gameplay.result.max_combo"] = new("MAX COMBO", "最大连击", "MAX COMBO"),
            ["gameplay.result.new_best"] = new("NEW BEST", "新纪录", "NEW BEST"),
            ["gameplay.result.retry"] = new("Retry", "再来一次", "リトライ"),
            ["gameplay.result.watch_replay"] = new("Watch Replay", "观看回放", "リプレイを見る"),
            ["gameplay.result.return"] = new("Song Select", "返回选曲", "曲選択へ"),

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

            ["editor.title"] = new("Yokko Editor", "Yokko 编辑器", "Yokko エディター"),
            ["editor.subtitle"] = new(
                "4K / 7K charting workstation",
                "4K / 7K 谱面工作台",
                "4K / 7K 譜面ワークスペース"),
            ["editor.new_4k"] = new("New 4K", "新建 4K", "新規 4K"),
            ["editor.new_7k"] = new("New 7K", "新建 7K", "新規 7K"),
            ["editor.import"] = new("Import", "导入", "インポート"),
            ["editor.export"] = new("Export", "导出", "エクスポート"),
            ["editor.playtest"] = new("Playtest", "试玩", "テストプレイ"),
            ["editor.ready"] = new(
                "Ready. Create a 4K/7K chart, import a supported chart, click grid cells, then Playtest.",
                "准备就绪。新建 4K/7K 谱面或导入支持的谱面，点击网格放置音符，然后试玩。",
                "準備完了。4K/7K 譜面を作成または対応譜面を読み込み、グリッドにノーツを置いてテストプレイできます。"),
            ["editor.status.new_draft"] = new(
                "New {0}K draft created.",
                "已新建 {0}K 草稿。",
                "新しい {0}K 下書きを作成しました。"),
            ["editor.status.timeline"] = new(
                "Timeline {0}-{1}",
                "时间轴 {0}-{1}",
                "タイムライン {0}-{1}"),
            ["editor.status.zoom"] = new(
                "Timeline zoom: {0} rows.",
                "时间轴缩放：{0} 行。",
                "タイムライン表示：{0} 行。"),
            ["editor.status.extended"] = new(
                "Extended chart to {0} rows.",
                "谱面已扩展至 {0} 行。",
                "譜面を {0} 行まで拡張しました。"),
            ["editor.status.imported"] = new(
                "Imported {0}.{1}",
                "已导入 {0}。{1}",
                "{0} をインポートしました。{1}"),
            ["editor.status.warning"] = new(
                " Warning: {0}{1}",
                " 警告：{0}{1}",
                " 警告：{0}{1}"),
            ["editor.status.more_warnings"] = new(
                " (+{0} more)",
                "（另有 {0} 条）",
                "（ほか {0} 件）"),
            ["editor.status.import_failed"] = new(
                "Import failed: {0}",
                "导入失败：{0}",
                "インポートに失敗しました：{0}"),
            ["editor.status.exported"] = new(
                "Exported {0}",
                "已导出至 {0}",
                "{0} にエクスポートしました"),
            ["editor.status.export_failed"] = new(
                "Export failed: {0}",
                "导出失败：{0}",
                "エクスポートに失敗しました：{0}"),
            ["editor.status.preview_playing"] = new(
                "Preview playing.",
                "正在播放预览。",
                "プレビュー再生中。"),
            ["editor.status.preview_paused"] = new(
                "Preview paused.",
                "预览已暂停。",
                "プレビューを一時停止しました。"),
            ["editor.status.preview_stopped"] = new(
                "Preview stopped.",
                "预览已停止。",
                "プレビューを停止しました。"),
            ["editor.status.preview_at"] = new(
                "Preview {0}.",
                "预览位置 {0}。",
                "プレビュー位置 {0}。"),
            ["editor.status.waveform_ready"] = new(
                "Waveform ready: {0}.",
                "波形已就绪：{0}。",
                "波形の準備完了：{0}。"),
            ["editor.status.waveform_unavailable"] = new(
                "Waveform unavailable: {0}",
                "波形不可用：{0}",
                "波形を利用できません：{0}"),
            ["editor.inspector.mode"] = new("Mode {0}K", "模式 {0}K", "モード {0}K"),
            ["editor.inspector.notes"] = new("Notes {0}", "音符 {0}", "ノーツ {0}"),
            ["editor.inspector.length"] = new("Length {0}s", "时长 {0} 秒", "長さ {0} 秒"),
            ["editor.inspector.window"] = new("Window {0}-{1}", "窗口 {0}-{1}", "範囲 {0}-{1}"),
            ["editor.inspector.scroll"] = new(
                "SV {0}x · {1} SV / {2} SSF / {3} groups",
                "SV {0}x · {1} 个 SV / {2} 个 SSF / {3} 组",
                "SV {0}x · SV {1} / SSF {2} / {3} グループ"),
            ["editor.inspector.grid"] = new(
                "Grid {0} rows • 1/{1} • {2} BPM",
                "网格 {0} 行 • 1/{1} • {2} BPM",
                "グリッド {0} 行 • 1/{1} • {2} BPM"),
            ["editor.inspector.audio_missing"] = new("Audio not linked", "未关联音频", "オーディオ未設定"),
            ["editor.inspector.audio"] = new("Audio {0}", "音频 {0}", "オーディオ {0}"),
            ["editor.inspector.source_draft"] = new("Source Yokko draft", "来源 Yokko 草稿", "ソース Yokko 下書き"),
            ["editor.inspector.source"] = new("Source {0}", "来源 {0}", "ソース {0}"),

            ["settings.title"] = new("Settings", "设置", "設定"),
            ["settings.back"] = new("Back", "返回", "戻る"),
            ["settings.search"] = new("Search settings", "搜索设置", "設定を検索"),
            ["settings.no_matches"] = new("No matching settings", "没有匹配的设置", "一致する設定がありません"),
            ["settings.group_core"] = new("CORE", "基础", "基本"),
            ["settings.group_creation"] = new("CREATION", "创作", "制作"),
            ["settings.group_system"] = new("SYSTEM", "系统", "システム"),
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
            ["settings.general.home_music"] = new(
                "Home screen music",
                "主页背景音乐",
                "ホーム画面の音楽"),
            ["settings.general.player_name"] = new(
                "Display name",
                "显示名称",
                "表示名"),
            ["settings.general.version"] = new(
                "Current version",
                "当前版本",
                "現在のバージョン"),
            ["settings.general.updates_note"] = new(
                "Visit About for credits and acknowledgements.",
                "完整版本信息与致谢请前往「关于」页面。",
                "クレジットと謝辞は「About」ページをご覧ください。"),
            ["settings.general.debug_console"] = new(
                "Live debug console · F12",
                "实时调试控制台 · F12",
                "リアルタイムデバッグコンソール · F12"),
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
            ["settings.display.ratio_resolution"] = new(
                "Ratio / resolution",
                "比例 / 分辨率",
                "比率 / 解像度"),
            ["settings.display.frame_limit"] = new("Frame limit", "帧率上限", "フレーム上限"),
            ["settings.display.interface_scale"] = new("Interface size", "界面大小", "UI サイズ"),
            ["settings.display.performance_readout"] = new(
                "Performance readout",
                "性能读数",
                "パフォーマンス表示"),
            ["settings.display.difficulty_rating"] = new(
                "Difficulty rating",
                "难度显示",
                "難易度表示"),
            ["settings.display.difficulty_rating.etterna"] = new(
                "Etterna MSD",
                "Etterna MSD",
                "Etterna MSD"),
            ["settings.display.difficulty_rating.rebirth"] = new(
                "Rebirth stars",
                "Rebirth 星级",
                "Rebirth 星評価"),
            ["settings.display.enabled"] = new("Enabled", "已开启", "オン"),
            ["settings.display.disabled"] = new("Disabled", "已关闭", "オフ"),
            ["settings.display.windowed"] = new("Windowed", "窗口化", "ウィンドウ"),
            ["settings.display.borderless"] = new("Borderless", "无边框", "ボーダーレス"),
            ["settings.display.fullscreen"] = new("Fullscreen", "全屏", "フルスクリーン"),
            ["settings.display.compact"] = new("80%", "80%", "80%"),
            ["settings.display.comfortable"] = new("90%", "90%", "90%"),
            ["settings.display.spacious"] = new("100%", "100%", "100%"),

            ["settings.desktop.title"] = new("Desktop", "\u684c\u9762", "\u30c7\u30b9\u30af\u30c8\u30c3\u30d7"),
            ["settings.desktop.subtitle"] = new(
                "Window switching and background behaviour",
                "\u7a97\u53e3\u5207\u6362\u4e0e\u540e\u53f0\u884c\u4e3a",
                "\u30a6\u30a3\u30f3\u30c9\u30a6\u5207\u66ff\u3068\u30d0\u30c3\u30af\u30b0\u30e9\u30a6\u30f3\u30c9\u52d5\u4f5c"),
            ["settings.desktop.description"] = new(
                "Tune Alt+Tab, inactive performance, audio and fullscreen output.",
                "\u8c03\u6574 Alt+Tab\u3001\u540e\u53f0\u6027\u80fd\u3001\u97f3\u9891\u4e0e\u5168\u5c4f\u8f93\u51fa\u3002",
                "Alt+Tab\u3001\u975e\u30a2\u30af\u30c6\u30a3\u30d6\u6642\u306e\u6027\u80fd\u3001\u97f3\u58f0\u3001\u30d5\u30eb\u30b9\u30af\u30ea\u30fc\u30f3\u51fa\u529b\u3092\u8abf\u6574\u3057\u307e\u3059\u3002"),
            ["settings.desktop.current_output"] = new("Current output", "\u5f53\u524d\u8f93\u51fa", "\u73fe\u5728\u306e\u51fa\u529b"),
            ["settings.desktop.fast_alt_tab"] = new("Fast Alt+Tab", "\u5feb\u901f Alt+Tab", "\u9ad8\u901f Alt+Tab"),
            ["settings.desktop.dynamic_fps"] = new("Dynamic frame rate", "\u52a8\u6001\u5e27\u6570", "\u52d5\u7684\u30d5\u30ec\u30fc\u30e0\u30ec\u30fc\u30c8"),
            ["settings.desktop.background_fps"] = new("Background frame rate", "\u540e\u53f0\u5e27\u7387", "\u30d0\u30c3\u30af\u30b0\u30e9\u30a6\u30f3\u30c9 FPS"),
            ["settings.desktop.background_audio"] = new("Background audio", "\u540e\u53f0\u97f3\u9891", "\u30d0\u30c3\u30af\u30b0\u30e9\u30a6\u30f3\u30c9\u97f3\u58f0"),
            ["settings.desktop.fullscreen_display"] = new("Fullscreen display", "\u5168\u5c4f\u663e\u793a\u5668", "\u30d5\u30eb\u30b9\u30af\u30ea\u30fc\u30f3\u8868\u793a\u5148"),
            ["settings.desktop.boss_key"] = new("Minimise shortcut", "\u4e00\u952e\u6700\u5c0f\u5316", "\u6700\u5c0f\u5316\u30b7\u30e7\u30fc\u30c8\u30ab\u30c3\u30c8"),
            ["settings.desktop.audio_keep"] = new("Keep", "\u4fdd\u6301", "\u7dad\u6301"),
            ["settings.desktop.audio_dim"] = new("20%", "20%", "20%"),
            ["settings.desktop.audio_mute"] = new("Mute", "\u9759\u97f3", "\u30df\u30e5\u30fc\u30c8"),

            ["debug_console.title"] = new("YOKKO LIVE DEBUG", "YOKKO 实时调试", "YOKKO ライブデバッグ"),
            ["debug_console.pause"] = new("Pause", "暂停", "一時停止"),
            ["debug_console.resume"] = new("Resume", "继续", "再開"),
            ["debug_console.clear"] = new("Clear", "清空", "クリア"),
            ["debug_console.copy"] = new("Copy", "复制", "コピー"),
            ["debug_console.export"] = new("Export", "导出", "エクスポート"),
            ["debug_console.open_logs"] = new("Open logs", "打开日志", "ログを開く"),
            ["debug_console.close"] = new("Close", "关闭", "閉じる"),
            ["debug_console.status_live"] = new("LIVE · {0} buffered", "实时 · 已缓存 {0} 条", "ライブ · {0} 件"),
            ["debug_console.status_paused"] = new("PAUSED · {0} pending", "已暂停 · {0} 条待显示", "一時停止 · {0} 件保留"),
            ["debug_console.status_exporting"] = new("EXPORTING…", "正在导出…", "エクスポート中…"),
            ["debug_console.status_exported"] = new("EXPORTED · path copied", "已导出 · 路径已复制", "エクスポート完了 · パスをコピー"),
            ["debug_console.status_export_failed"] = new("EXPORT FAILED · see log", "导出失败 · 请查看日志", "エクスポート失敗 · ログを確認"),

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
            ["settings.audio.master_volume"] = new("Master volume", "音量", "マスター音量"),
            ["settings.audio.hitsounds"] = new("Hitsounds", "按键音", "ヒットサウンド"),
            ["settings.audio.test"] = new("Test playback", "播放", "テスト再生"),
            ["settings.audio.test_failed"] = new("Audio test failed", "音频播放失败", "オーディオテスト失敗"),
            ["settings.audio.test_verified"] = new(
                "Actual output verified",
                "\u5df2\u9a8c\u8bc1\u5b9e\u9645\u8f93\u51fa",
                "\u5b9f\u969b\u306e\u51fa\u529b\u3092\u78ba\u8a8d\u6e08\u307f"),
            ["settings.audio.test_exclusive_fallback"] = new(
                "Exclusive unavailable \u00b7 Shared fallback measured",
                "\u72ec\u5360\u4e0d\u53ef\u7528 \u00b7 \u5df2\u6d4b\u91cf\u5171\u4eab\u56de\u9000",
                "\u6392\u4ed6\u30e2\u30fc\u30c9\u4e0d\u53ef \u00b7 \u5171\u6709\u30d5\u30a9\u30fc\u30eb\u30d0\u30c3\u30af\u3092\u8a08\u6e2c"),
            ["settings.audio.test_high_shared_latency"] = new(
                "High output latency \u00b7 try WASAPI Exclusive",
                "\u8f93\u51fa\u5ef6\u8fdf\u8f83\u9ad8 \u00b7 \u5efa\u8bae\u5c1d\u8bd5 WASAPI \u72ec\u5360",
                "\u51fa\u529b\u9045\u5ef6\u304c\u5927\u304d\u3044 \u00b7 WASAPI \u6392\u4ed6\u3092\u63a8\u5968"),
            ["settings.audio.test_high_latency"] = new(
                "High output latency \u00b7 try another device or buffer",
                "\u8f93\u51fa\u5ef6\u8fdf\u8f83\u9ad8 \u00b7 \u8bf7\u5c1d\u8bd5\u5176\u4ed6\u8bbe\u5907\u6216\u7f13\u51b2",
                "\u51fa\u529b\u9045\u5ef6\u304c\u5927\u304d\u3044 \u00b7 \u4ed6\u306e\u30c7\u30d0\u30a4\u30b9\u307e\u305f\u306f\u30d0\u30c3\u30d5\u30a1\u3092\u8a66\u884c"),
            ["settings.audio.test_result"] = new(
                "{0} \u00b7 req {1}f \u2192 buffer {2}f / period {3}f \u00b7 \u2264 {4:0.00} ms",
                "{0} \u00b7 \u8bf7\u6c42 {1}f \u2192 \u7f13\u51b2 {2}f / \u5468\u671f {3}f \u00b7 \u2264 {4:0.00} ms",
                "{0} \u00b7 \u8981\u6c42 {1}f \u2192 \u30d0\u30c3\u30d5\u30a1 {2}f / \u5468\u671f {3}f \u00b7 \u2264 {4:0.00} ms"),
            ["settings.audio.hitsounds_disabled"] = new(
                "Enable hitsounds before testing",
                "按键音已关闭",
                "ヒットサウンドはオフです"),
            ["settings.audio.enabled"] = new("Enabled", "已开启", "オン"),
            ["settings.audio.disabled"] = new("Disabled", "已关闭", "オフ"),
            ["settings.audio.offset"] = new("Timing offset", "时序偏移", "タイミングオフセット"),
            ["settings.audio.exclusive"] = new("WASAPI Exclusive", "WASAPI 独占", "WASAPI 排他"),
            ["settings.audio.shared"] = new("WASAPI Shared", "WASAPI 共享", "WASAPI 共有"),
            ["settings.audio.asio"] = new("ASIO", "ASIO", "ASIO"),
            ["settings.audio.asio_unavailable"] = new(
                "ASIO support is not included in this build",
                "当前构建未包含 ASIO 支持",
                "このビルドには ASIO サポートが含まれていません"),
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
            ["settings.gameplay.section_input"] = new("Input", "输入", "入力"),
            ["settings.gameplay.section_timing"] = new("Timing", "时序", "タイミング"),
            ["settings.gameplay.section_playback_rate"] = new("Rate", "倍速", "倍速"),
            ["settings.gameplay.section_judgement"] = new("Judgement", "判定", "判定"),
            ["settings.gameplay.section_feedback"] = new("Feedback", "反馈", "フィードバック"),

            ["settings.shortcuts.title"] = new("Shortcuts", "快捷键", "ショートカット"),
            ["settings.shortcuts.subtitle"] = new(
                "All Mania actions and key bindings",
                "集中管理全部 Mania 快捷键",
                "Mania の全ショートカットをまとめて管理"),
            ["settings.shortcuts.description"] = new(
                "Customise every Mania shortcut or restore the osu!lazer defaults.",
                "自定义全部 Mania 快捷键，或恢复 osu!lazer 默认设置。",
                "Mania の全ショートカットを変更し、osu!lazer の初期設定に戻せます。"),
            ["settings.shortcuts.status_title"] = new(
                "Mania shortcuts are ready",
                "Mania 快捷键已集中管理",
                "Mania ショートカットを一括管理"),
            ["settings.shortcuts.status_note"] = new(
                "{0} actions · saved instantly · individual and full reset available",
                "{0} 个快捷键 · 即时保存 · 支持单项和全部恢复默认",
                "{0} キー · 即時保存 · 個別・一括リセット対応"),
            ["settings.shortcuts.defaults_active"] = new(
                "Using osu!lazer defaults",
                "正在使用 osu!lazer 默认设置",
                "osu!lazer の初期設定を使用中"),
            ["settings.shortcuts.defaults_active_note"] = new(
                "Every Mania shortcut is on its default binding",
                "全部 Mania 快捷键均为默认设置",
                "全 Mania ショートカットが初期設定です"),
            ["settings.shortcuts.custom_active"] = new(
                "Custom Mania shortcuts active",
                "正在使用自定义 Mania 快捷键",
                "カスタム Mania ショートカットを使用中"),
            ["settings.shortcuts.modified_count"] = new(
                "{0} shortcuts use custom bindings",
                "{0} 个快捷键使用自定义设置",
                "{0} キーをカスタム設定中"),
            ["settings.shortcuts.modified"] = new(
                "CUSTOM",
                "已修改",
                "変更済み"),
            ["settings.shortcuts.system"] = new(
                "System",
                "\u7cfb\u7edf",
                "\u30b7\u30b9\u30c6\u30e0"),
            ["settings.shortcuts.system_fixed_hint"] = new(
                "Fixed system shortcut",
                "\u56fa\u5b9a\u7cfb\u7edf\u5feb\u6377\u952e",
                "\u56fa\u5b9a\u30b7\u30b9\u30c6\u30e0\u30b7\u30e7\u30fc\u30c8\u30ab\u30c3\u30c8"),
            ["settings.shortcuts.is_default"] = new(
                "Default",
                "默认",
                "初期設定"),
            ["settings.shortcuts.capture_title"] = new(
                "Setting: {0}",
                "正在设置：{0}",
                "設定中: {0}"),
            ["settings.shortcuts.capture_note"] = new(
                "Press the new key · Backspace cancels",
                "请按下新按键 · Backspace 取消",
                "新しいキーを入力 · Backspace でキャンセル"),
            ["settings.shortcuts.capture_cancelled"] = new(
                "Key capture cancelled",
                "已取消按键录入",
                "キー入力をキャンセルしました"),
            ["settings.shortcuts.capture_cancelled_note"] = new(
                "No shortcuts were changed",
                "没有修改任何快捷键",
                "ショートカットは変更されていません"),
            ["settings.shortcuts.binding_saved"] = new(
                "{0} updated",
                "{0} 已更新",
                "{0} を更新しました"),
            ["settings.shortcuts.binding_now"] = new(
                "Current key: {0}",
                "当前按键：{0}",
                "現在のキー: {0}"),
            ["settings.shortcuts.binding_swapped"] = new(
                "Swapped {0} and {1}",
                "已交换 {0} 与 {1}",
                "{0} と {1} を入れ替えました"),
            ["settings.shortcuts.binding_swapped_note"] = new(
                "{0}: {1} · {2}: {3}",
                "{0}：{1} · {2}：{3}",
                "{0}: {1} · {2}: {3}"),
            ["settings.shortcuts.reset_one_done"] = new(
                "{0} restored",
                "{0} 已恢复默认",
                "{0} を初期設定に戻しました"),
            ["settings.shortcuts.reset_all_confirm_title"] = new(
                "Restore every shortcut?",
                "确认恢复全部快捷键？",
                "全ショートカットを戻しますか？"),
            ["settings.shortcuts.reset_all_confirm_note"] = new(
                "Press the button again to confirm · you can undo afterwards",
                "再次点击确认 · 完成后可取消本次恢复",
                "もう一度押して確認 · 完了後も取り消せます"),
            ["settings.shortcuts.reset_all_confirm"] = new(
                "Confirm reset",
                "再次点击确认",
                "リセット確認"),
            ["settings.shortcuts.reset_all_done"] = new(
                "All shortcuts restored",
                "全部快捷键已恢复默认",
                "全ショートカットを初期設定に戻しました"),
            ["settings.shortcuts.undo_available"] = new(
                "Undo is available until the next shortcut change",
                "下次修改快捷键前可取消恢复",
                "次のキー変更までは取り消せます"),
            ["settings.shortcuts.undo_reset"] = new(
                "Undo reset",
                "取消恢复",
                "リセットを取り消す"),
            ["settings.shortcuts.reset_undone"] = new(
                "Reset undone",
                "已取消全部恢复",
                "リセットを取り消しました"),

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
            ["settings.skins.section_gameplay"] = new(
                "Gameplay",
                "游玩效果",
                "ゲームプレイ"),
            ["settings.skins.combo_bursts"] = new(
                "Combo bursts",
                "连击人物",
                "コンボバースト"),
            ["settings.skins.combo_bursts_note"] = new(
                "Show the skin's character when a combo milestone is reached",
                "达到连击里程碑时展示皮肤人物",
                "コンボ達成時にスキンのキャラクターを表示"),
            ["settings.skins.ln_cut_amount"] = new(
                "Additional LN cut",
                "额外投量",
                "追加 LN カット"),
            ["settings.skins.ln_cut_amount_note"] = new(
                "Optional extra cut on top of skin · visual only",
                "可选叠加皮肤原效果 · 仅影响显示",
                "スキン効果への追加は任意 · 表示のみ"),
            ["settings.skins.apply_next_gameplay"] = new(
                "Skin selection updates paused gameplay immediately · LN tweaks apply next load",
                "切换皮肤会在返回暂停界面时立即生效 · LN 调整下局生效",
                "スキン切替はポーズ画面へ戻ると即時反映 · LN 調整は次回プレイ時"),
            ["settings.gameplay.ready"] = new(
                "Gameplay controls are live",
                "游玩控制已实装",
                "ゲームプレイ設定が有効です"),
            ["settings.gameplay.ready_metadata"] = new(
                "4K  {0}   ·   7K  {1}",
                "4K  {0}   ·   7K  {1}",
                "4K  {0}   ·   7K  {1}"),
            ["settings.gameplay.selected_profile_ready"] = new(
                "{0}  ·  {1}",
                "{0}  ·  {1}",
                "{0}  ·  {1}"),
            ["settings.gameplay.selected_profile_ready_many"] = new(
                "{0} · {1} keys · keyboard, MIDI and HID supported",
                "{0} · {1} 个按键 · 支持键盘、MIDI 和 HID",
                "{0} · {1}キー · キーボード、MIDI、HIDに対応"),
            ["settings.gameplay.input_monitor"] = new(
                "Test your input",
                "输入测试",
                "入力テスト"),
            ["settings.gameplay.live"] = new("LIVE", "已启用", "有効"),
            ["settings.gameplay.calibration_start"] = new(
                "Calibrate input",
                "校准输入偏移",
                "入力を調整"),
            ["settings.gameplay.calibration_wait"] = new(
                "Preparing…",
                "准备中…",
                "準備中…"),
            ["settings.gameplay.calibration_preparing"] = new(
                "Preparing the timing test",
                "正在准备时序测试",
                "タイミングテストを準備中"),
            ["settings.gameplay.calibration_preparing_note"] = new(
                "The configured audio output will play a generated click track.",
                "将通过当前音频输出播放生成的节拍音。",
                "現在の出力で生成したクリック音を再生します。"),
            ["settings.gameplay.calibration_running"] = new(
                "Tap any bound key with the beat",
                "跟随节拍按任意已绑定按键",
                "拍に合わせて割り当て済みキーを押してください"),
            ["settings.gameplay.calibration_running_note"] = new(
                "The first beat is a one-second lead-in · Esc cancels",
                "第一拍前有 1 秒准备时间 · Esc 取消",
                "最初の拍まで1秒 · Escでキャンセル"),
            ["settings.gameplay.calibration_sample"] = new(
                "{0} samples · latest {1:+0;-0;0} ms",
                "{0} 个采样 · 本次 {1:+0;-0;0} ms",
                "{0} サンプル · 最新 {1:+0;-0;0} ms"),
            ["settings.gameplay.calibration_countdown"] = new(
                "{0}s remaining",
                "剩余 {0} 秒",
                "残り {0} 秒"),
            ["settings.gameplay.calibration_complete"] = new(
                "Calibration complete",
                "校准完成",
                "キャリブレーション完了"),
            ["settings.gameplay.calibration_result"] = new(
                "Suggested input offset {0:+0;-0;0} ms · based on {1} taps",
                "建议输入偏移 {0:+0;-0;0} ms · 基于 {1} 次按键",
                "推奨入力オフセット {0:+0;-0;0} ms · {1} 回の入力"),
            ["settings.gameplay.calibration_apply"] = new(
                "Apply {0:+0;-0;0} ms",
                "应用 {0:+0;-0;0} ms",
                "{0:+0;-0;0} msを適用"),
            ["settings.gameplay.calibration_applied"] = new(
                "Timing offset applied",
                "已应用时序偏移",
                "タイミングオフセットを適用しました"),
            ["settings.gameplay.calibration_applied_note"] = new(
                "Input offset is now {0:+0;-0;0} ms and shared with Audio.",
                "输入偏移现为 {0:+0;-0;0} ms，并与音频设置共享。",
                "入力オフセットは {0:+0;-0;0} ms でオーディオ設定と共有されます。"),
            ["settings.gameplay.calibration_again"] = new(
                "Run again",
                "重新校准",
                "もう一度"),
            ["settings.gameplay.calibration_incomplete"] = new(
                "More taps are needed",
                "需要更多按键采样",
                "入力サンプルが不足しています"),
            ["settings.gameplay.calibration_incomplete_note"] = new(
                "Captured {0} taps · at least {1} are needed for a recommendation.",
                "已采集 {0} 次 · 至少需要 {1} 次才能给出建议。",
                "{0} 回取得 · 推奨には最低 {1} 回必要です。"),
            ["settings.gameplay.calibration_failed"] = new(
                "Calibration audio failed",
                "校准音频播放失败",
                "キャリブレーション音声の再生に失敗"),
            ["settings.gameplay.calibration_failed_note"] = new(
                "Check the selected output device, then try again.",
                "请检查当前输出设备后重试。",
                "出力デバイスを確認して再試行してください。"),
            ["settings.gameplay.calibration_cancelled"] = new(
                "Calibration cancelled",
                "已取消校准",
                "キャリブレーションをキャンセルしました"),
            ["settings.gameplay.calibration_cancelled_note"] = new(
                "Your existing input offset was not changed.",
                "现有输入偏移没有改变。",
                "現在の入力オフセットは変更されていません。"),
            ["settings.gameplay.key_profile"] = new("Keys", "按键", "キー設定"),
            ["settings.gameplay.edit_all"] = new("Set all", "连续录入", "まとめて設定"),
            ["settings.gameplay.reset"] = new("Defaults", "恢复默认", "既定値"),
            ["settings.gameplay.presets"] = new("Layout", "布局", "配列"),
            ["settings.gameplay.preset_standard"] = new("Standard", "标准", "標準"),
            ["settings.gameplay.preset_left"] = new("Left hand", "左手", "左手"),
            ["settings.gameplay.preset_split"] = new("Split", "双手", "両手"),
            ["settings.gameplay.copy_other_mode"] = new(
                "Copy to other",
                "复制到另一模式",
                "他モードへコピー"),
            ["settings.gameplay.copy_to_mode"] = new(
                "Copy to {0}",
                "同步到 {0}",
                "{0}へ同期"),
            ["settings.gameplay.all_modes_hint"] = new(
                "All osu!mania layouts are editable · dual stages use two rows",
                "全部 osu!mania 键位均可修改 · 双舞台分两行显示",
                "すべての osu!mania 配列を編集可能 · デュアルは2段表示"),
            ["settings.gameplay.export_profile"] = new("Copy all", "复制全部", "すべてコピー"),
            ["settings.gameplay.import_profile"] = new("Paste all", "粘贴导入", "貼り付け"),
            ["settings.gameplay.preset_applied"] = new(
                "{0} preset applied.",
                "已应用“{0}”预设。",
                "「{0}」プリセットを適用しました。"),
            ["settings.gameplay.profile_copied"] = new(
                "{0} central lanes copied to {1}.",
                "已将 {0} 的核心键位同步到 {1}。",
                "{0} の中央レーンを {1} にコピーしました。"),
            ["settings.gameplay.profile_exported"] = new(
                "All mania key profiles copied to the clipboard.",
                "全部 Mania 键位已复制到剪贴板。",
                "すべてのManiaキー設定をクリップボードへコピーしました。"),
            ["settings.gameplay.profile_imported"] = new(
                "All mania key profiles imported.",
                "已导入全部 Mania 键位。",
                "すべてのManiaキー設定を読み込みました。"),
            ["settings.gameplay.profile_import_failed"] = new(
                "Clipboard does not contain a valid Yokko key profile.",
                "剪贴板里没有可用的 Yokko 键位配置。",
                "クリップボードに有効なYokkoキー設定がありません。"),
            ["settings.gameplay.shortcut_decrease_speed"] = new(
                "Decrease scroll speed",
                "降低滚速",
                "スクロール速度を下げる"),
            ["settings.gameplay.shortcut_decrease_speed_note"] = new(
                "osu!lazer default: F3",
                "osu!lazer 默认：F3",
                "osu!lazer 既定：F3"),
            ["settings.gameplay.shortcut_increase_speed"] = new(
                "Increase scroll speed",
                "提高滚速",
                "スクロール速度を上げる"),
            ["settings.gameplay.shortcut_increase_speed_note"] = new(
                "osu!lazer default: F4",
                "osu!lazer 默认：F4",
                "osu!lazer 既定：F4"),
            ["settings.gameplay.shortcuts_gameplay"] = new(
                "Gameplay",
                "游玩",
                "ゲームプレイ"),
            ["settings.gameplay.shortcuts_menu"] = new(
                "Pause menu",
                "暂停菜单",
                "ポーズメニュー"),
            ["settings.gameplay.shortcuts_results"] = new(
                "Results",
                "失败与结算",
                "リザルト"),
            ["settings.gameplay.shortcuts_editor"] = new(
                "Editor",
                "编辑器",
                "エディター"),
            ["settings.gameplay.shortcut_pause_back"] = new(
                "Pause / resume / back",
                "暂停 / 继续 / 返回",
                "一時停止 / 再開 / 戻る"),
            ["settings.gameplay.shortcut_toggle_layout_editor_ui"] = new(
                "Hide / show layout editor UI",
                "隐藏 / 显示布局编辑界面",
                "レイアウト編集 UI を隠す / 表示"),
            ["settings.gameplay.shortcut_skip_intro"] = new(
                "Skip intro",
                "跳过前奏",
                "イントロをスキップ"),
            ["settings.gameplay.shortcut_quick_retry"] = new(
                "Quick retry",
                "快速重试",
                "クイックリトライ"),
            ["settings.gameplay.shortcut_menu_previous"] = new(
                "Previous menu item",
                "上一菜单项",
                "前のメニュー項目"),
            ["settings.gameplay.shortcut_menu_previous_alt"] = new(
                "Previous menu item (alternate)",
                "上一菜单项（备用）",
                "前のメニュー項目（予備）"),
            ["settings.gameplay.shortcut_menu_next"] = new(
                "Next menu item",
                "下一菜单项",
                "次のメニュー項目"),
            ["settings.gameplay.shortcut_menu_next_alt"] = new(
                "Next menu item (alternate)",
                "下一菜单项（备用）",
                "次のメニュー項目（予備）"),
            ["settings.gameplay.shortcut_confirm"] = new(
                "Confirm",
                "确认",
                "決定"),
            ["settings.gameplay.shortcut_confirm_alt"] = new(
                "Confirm (alternate)",
                "确认（备用）",
                "決定（予備）"),
            ["settings.gameplay.shortcut_retry"] = new(
                "Retry",
                "重试",
                "リトライ"),
            ["settings.gameplay.shortcut_watch_replay"] = new(
                "Watch replay",
                "观看回放",
                "リプレイを見る"),
            ["settings.gameplay.shortcut_reset"] = new(
                "Reset F3 / F4",
                "恢复 F3 / F4",
                "F3 / F4 に戻す"),
            ["settings.gameplay.shortcut_reset_all"] = new(
                "Reset all defaults",
                "全部恢复默认",
                "すべて既定に戻す"),
            ["settings.gameplay.shortcut_default"] = new(
                "Default",
                "恢复默认",
                "既定に戻す"),
            ["settings.gameplay.shortcut_hint"] = new(
                "Click a shortcut, then press a key · duplicates swap automatically · Backspace cancels",
                "点击快捷键后按下新按键 · 重复时自动交换 · Backspace 取消",
                "項目を選んでキーを入力 · 重複時は自動交換 · Backspaceでキャンセル"),
            ["settings.gameplay.key_capture_hint"] = new(
                "Choose a lane, then press a keyboard key, MIDI note or HID button.",
                "选择一个轨道，然后按下键盘键、MIDI 音符或 HID 按钮。",
                "レーンを選び、キーボード、MIDI、HID の入力を押してください。"),
            ["settings.gameplay.key_swap_hint"] = new(
                "Already-bound inputs swap positions automatically · Esc cancels",
                "输入已被占用时会自动交换位置 · 按 Esc 取消",
                "重複した入力は自動で入れ替わります · Escでキャンセル"),
            ["settings.gameplay.capture_target"] = new(
                "Setting {0} · press a keyboard key, MIDI note or controller button · Esc cancels",
                "正在设置 {0} · 按下键盘按键、MIDI 音符或控制器按键 · Esc 取消",
                "{0}を設定中 · キーボード、MIDI、コントローラーを入力 · Escでキャンセル"),
            ["settings.gameplay.cancel_capture"] = new(
                "Cancel",
                "取消",
                "キャンセル"),
            ["settings.gameplay.lane"] = new("LANE {0}", "轨道 {0}", "レーン {0}"),
            ["settings.gameplay.bms_scratch"] = new("SCRATCH", "皿键", "スクラッチ"),
            ["settings.gameplay.bms_key"] = new("KEY {0}", "键 {0}", "キー {0}"),
            ["settings.gameplay.bms_stage_scratch"] = new(
                "{0}P SCRATCH",
                "{0}P 皿键",
                "{0}P スクラッチ"),
            ["settings.gameplay.bms_stage_key"] = new(
                "{0}P KEY {1}",
                "{0}P 键 {1}",
                "{0}P キー {1}"),
            ["settings.gameplay.bms_mode"] = new(
                "BMS mode",
                "BMS 模式",
                "BMSモード"),
            ["settings.gameplay.bms_single_play"] = new(
                "Single · SP",
                "单人 · SP",
                "シングル · SP"),
            ["settings.gameplay.bms_double_play"] = new(
                "Double · DP",
                "双人 · DP",
                "ダブル · DP"),
            ["settings.gameplay.bms_layout_note"] = new(
                "Independent scratch layout",
                "独立皿键布局",
                "独立スクラッチ配列"),
            ["settings.gameplay.bms_profile_hint"] = new(
                "BMS uses an independent scratch + 7-key layout.",
                "单人模式：1 个皿键 + 7 个按键。",
                "BMS は独立したスクラッチ + 7キー配列を使用します。"),
            ["settings.gameplay.bms_dp_profile_hint"] = new(
                "BMS DP uses two independent scratch + 7-key stages.",
                "双人模式：两组独立的皿键 + 7 键。",
                "BMS DP は2組の独立したスクラッチ + 7キー配列を使用します。"),
            ["settings.gameplay.click_to_change"] = new("Click to set", "点击设置", "クリックして設定"),
            ["settings.gameplay.press_key"] = new("WAITING", "等待输入", "入力待ち"),
            ["settings.gameplay.esc_cancel"] = new("Esc to cancel", "Esc 取消", "Esc でキャンセル"),
            ["settings.gameplay.sequence_hint"] = new(
                "{0}/{1} · setting {2} · Esc cancels",
                "{0}/{1} · 正在设置 {2} · Esc 取消",
                "{0}/{1} · {2}を設定中 · Escでキャンセル"),
            ["settings.gameplay.sequence_duplicate"] = new(
                "That input is already used · choose another",
                "该输入已被使用，请换一个",
                "その入力は使用済みです · 別の入力を選んでください"),
            ["settings.gameplay.sequence_captured"] = new(
                "Captured",
                "已录入",
                "入力済み"),
            ["settings.gameplay.sequence_saved"] = new(
                "{0}K profile saved · {1}",
                "{0}K 键位已保存 · {1}",
                "{0}K プロファイルを保存しました · {1}"),
            ["settings.gameplay.bms_profile_saved"] = new(
                "BMS profile saved · {0}",
                "BMS 键位已保存 · {0}",
                "BMS プロファイルを保存しました · {0}"),
            ["settings.gameplay.bms_dp_profile_saved"] = new(
                "BMS DP profile saved · {0}",
                "BMS DP 键位已保存 · {0}",
                "BMS DP プロファイルを保存しました · {0}"),
            ["settings.gameplay.single_saved"] = new(
                "{0} is now {1}.",
                "{0} 已设为 {1}。",
                "{0} を {1} に設定しました。"),
            ["settings.gameplay.key_swap_notice"] = new(
                "{0} was on {1}; {1} and {2} were swapped.",
                "{0} 原本属于 {1}；已交换 {1} 与 {2}。",
                "{0} は {1} にあり、{1} と {2} を入れ替えました。"),
            ["settings.gameplay.confirm_reset"] = new(
                "Confirm again",
                "再次确认",
                "もう一度確認"),
            ["settings.gameplay.undo_reset"] = new(
                "Undo",
                "撤销",
                "元に戻す"),
            ["settings.gameplay.binding_reset_confirm"] = new(
                "Click again to restore defaults. You can undo it afterwards.",
                "再次点击按钮确认恢复默认；完成后仍可撤销。",
                "もう一度押すと既定値に戻します。あとから取り消せます。"),
            ["settings.gameplay.binding_reset_done"] = new(
                "Defaults restored · you can undo until the next edit.",
                "已恢复默认；下次修改前可以撤销。",
                "既定値に戻しました · 次の変更までは取り消せます。"),
            ["settings.gameplay.binding_reset_undone"] = new(
                "Reset undone · your previous bindings are back.",
                "已撤销，原来的键位已恢复。",
                "リセットを取り消し、以前の設定を復元しました。"),
            ["settings.gameplay.binding_reset_cancelled"] = new(
                "Reset cancelled · bindings were not changed.",
                "已取消，键位没有变化。",
                "リセットをキャンセルしました · 設定は変更されていません。"),
            ["settings.gameplay.key_swapped"] = new("Swapped", "已交换", "入れ替え済み"),
            ["settings.gameplay.input_active"] = new("Pressed", "按下中", "入力中"),
            ["settings.gameplay.input_detected"] = new(
                "{0} detected · {1}",
                "检测到 {0} · {1}",
                "{0} を検出 · {1}"),
            ["settings.gameplay.input_unbound"] = new(
                "{0} is not bound in this profile",
                "{0} 未绑定到当前键位配置",
                "{0} は現在の設定に割り当てられていません"),
            ["settings.gameplay.input_chord"] = new(
                "{0}/{1} inputs pressed · hold combinations to test rollover",
                "当前按下 {0}/{1} 个输入 · 同时按住多个键可测试多键识别",
                "現在 {0}/{1} キーを検出 · 同時押しでロールオーバーを確認"),
            ["settings.gameplay.scroll_speed"] = new(
                "Note speed",
                "音符速度",
                "ノーツ速度"),
            ["settings.gameplay.scroll_speed_note"] = new(
                "Fine off: whole levels only. Fine on: drag by tenths; wheel or arrows adjust 1 ms · lower ms is faster.",
                "微调关闭：拖动、滚轮和方向键按整档；开启：拖动可细调，滚轮或方向键每次 1 ms · ms 越小越快。",
                "微調整オフ：整数段階のみ。オン：ドラッグで細かく、ホイール・方向キーは 1 ms ずつ調整 · 小さいほど高速。"),
            ["settings.gameplay.scroll_speed_mode_scale"] = new(
                "1–40 SCALE",
                "1–40 档位",
                "1–40 段階"),
            ["settings.gameplay.scroll_speed_mode_milliseconds"] = new(
                "ADVANCED · 1 MS",
                "进阶 · 1 MS",
                "上級 · 1 MS"),
            ["settings.gameplay.scroll_direction"] = new(
                "Scroll direction",
                "滚动方向",
                "スクロール方向"),
            ["settings.gameplay.scroll_direction_note"] = new(
                "Changes presentation only; timing and judgement stay the same.",
                "只改变音符移动方向，不影响谱面时间与判定。",
                "表示方向のみを変更し、タイミングと判定には影響しません。"),
            ["settings.gameplay.difficulty_rating"] = new(
                "Difficulty rating",
                "难度显示",
                "難易度表示"),
            ["settings.gameplay.difficulty_rating_note"] = new(
                "Controls how chart difficulty is shown in song select and results.",
                "控制选曲与结果界面的谱面难度展示方式。",
                "選曲とリザルトでの譜面難易度表示方式を切り替えます。"),
            ["settings.gameplay.difficulty_rating.etterna"] = new(
                "Etterna MSD",
                "Etterna MSD",
                "Etterna MSD"),
            ["settings.gameplay.difficulty_rating.rebirth"] = new(
                "Rebirth stars",
                "Rebirth 星级",
                "Rebirth 星評価"),
            ["settings.gameplay.scroll_direction_down"] = new(
                "Downscroll",
                "Downscroll · 向下",
                "Downscroll · 下向き"),
            ["settings.gameplay.scroll_direction_up"] = new(
                "Upscroll",
                "Upscroll · 向上",
                "Upscroll · 上向き"),
            ["settings.gameplay.playback_rate_heading"] = new(
                "Shortcut rate mode",
                "快捷键倍速模式",
                "ショートカット倍速モード"),
            ["settings.gameplay.playback_rate_note"] = new(
                "Choose how Alt + / Alt − changes the song audio.",
                "选择 Alt + / Alt − 调整歌曲倍速时的音调方式。",
                "Alt + / Alt − で曲の倍速を変えるときの音程を選びます。"),
            ["settings.gameplay.playback_rate_dt"] = new(
                "DT · Preserve pitch",
                "DT · 保持音调",
                "DT · 音程を維持"),
            ["settings.gameplay.playback_rate_nc"] = new(
                "NC · Raise pitch",
                "NC · 音调随速度升高",
                "NC · 速度と音程を上げます"),
            ["settings.gameplay.playback_rate_shortcut"] = new(
                "In-game shortcut",
                "游玩内快捷键",
                "プレイ中のショートカット"),
            ["settings.gameplay.playback_rate_shortcut_note"] = new(
                "Alt + / Alt − adjusts the rate in 0.05× steps.",
                "Alt + / Alt − 调整 0.05×。",
                "Alt + / Alt − で 0.05× 調整します。"),
            ["settings.gameplay.playback_rate_mod_priority"] = new(
                "Mod priority",
                "Mod 优先级",
                "Mod 設定"),
            ["settings.gameplay.playback_rate_mod_priority_note"] = new(
                "Explicit DT/NC and rate Mods keep their own pitch policy.",
                "显式 DT/NC 与倍速 Mod 仍使用各自的音调规则。",
                "DT/NC と倍速 Mod の設定を使用します。"),
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
            ["gameplay.timing.early"] = new("EARLY", "快", "早い"),
            ["gameplay.timing.late"] = new("LATE", "慢", "遅い"),
            ["gameplay.timing.title"] = new(
                "TIMING",
                "判定",
                "タイミング"),
            ["gameplay.timing.early_limit"] = new(
                "EARLY  -{0:0} ms",
                "快  -{0:0} ms",
                "早い  -{0:0} ms"),
            ["gameplay.timing.late_limit"] = new(
                "+{0:0} ms  LATE",
                "+{0:0} ms  慢",
                "+{0:0} ms  遅い"),
            ["gameplay.timing.on_time"] = new("ON TIME", "准", "正確"),
            ["gameplay.timing.press"] = new("PRESS", "按下", "押下"),
            ["gameplay.timing.release"] = new("RELEASE", "松开", "離す"),
            ["gameplay.timing.latest"] = new(
                "{0} · {1} · {2:+0.0;-0.0;0.0} ms",
                "{0} · {1} · {2:+0.0;-0.0;0.0} ms",
                "{0} · {1} · {2:+0.0;-0.0;0.0} ms"),
            ["gameplay.timing.latest_compact"] = new(
                "{0}  {1:+0.0;-0.0;0.0} ms",
                "{0}  {1:+0.0;-0.0;0.0} ms",
                "{0}  {1:+0.0;-0.0;0.0} ms"),
            ["gameplay.timing.trend_press"] = new(
                "TREND · PRESS {0:+0.0;-0.0;0.0} ms",
                "趋势 · 按下 {0:+0.0;-0.0;0.0} ms",
                "傾向 · 押下 {0:+0.0;-0.0;0.0} ms"),
            ["gameplay.timing.trend_release"] = new(
                "TREND · RELEASE {0:+0.0;-0.0;0.0} ms",
                "趋势 · 松开 {0:+0.0;-0.0;0.0} ms",
                "傾向 · 離す {0:+0.0;-0.0;0.0} ms"),
            ["gameplay.timing.trend_both"] = new(
                "TREND · PRESS {0:+0.0;-0.0;0.0} · RELEASE {1:+0.0;-0.0;0.0} ms",
                "趋势 · 按下 {0:+0.0;-0.0;0.0} · 松开 {1:+0.0;-0.0;0.0} ms",
                "傾向 · 押下 {0:+0.0;-0.0;0.0} · 離す {1:+0.0;-0.0;0.0} ms"),
            ["settings.gameplay.speed_presets"] = new("Quick presets", "快捷预设", "クイック設定"),
            ["settings.gameplay.quaver_rate_normalization"] = new(
                "Quaver rate normalization",
                "Quaver 倍率流速归一化",
                "Quaver レート正規化"),
            ["settings.gameplay.quaver_rate_normalization_note"] = new(
                "0% preserves Quaver's default real-time approach; 100% scales with rate.",
                "0% 保持 Quaver 默认的现实时间接近速度；100% 跟随倍率缩放。",
                "0% は Quaver の実時間接近を維持し、100% はレートに追従します。"),
            ["settings.gameplay.input_offset"] = new("Input offset", "输入偏移", "入力オフセット"),
            ["settings.gameplay.input_offset_note"] = new(
                "Shared with Audio so timing has one source of truth.",
                "与音频设置共享，确保时序只有一个真源。",
                "オーディオ設定と共有し、タイミングを一元管理します。"),
            ["settings.gameplay.judgement_heading"] = new(
                "Judgement system",
                "判定系统",
                "判定システム"),
            ["settings.gameplay.judgement_note"] = new(
                "Switch judgement rules. BMS follows beatoraja 5K/7K timing, scratch, empty MS, and traditional LN rules; score and gauge remain Yokko's.",
                "切换判定规则。BMS 模式对齐 beatoraja 的 5K/7K 时间窗、皿键、空按 MS 与传统 LN；计分和血条仍使用 Yokko 规则。",
                "判定ルールを切り替えます。BMS は beatoraja の5K/7K判定幅、スクラッチ、空押しMS、従来LNに準拠し、スコアとゲージは Yokko 仕様です。"),
            ["settings.gameplay.judgement_apply_next_game"] = new(
                "Changes are saved now and apply next play. The current play keeps its starting rules.",
                "更改会立即保存，并从下一局开始生效；当前局继续沿用开局时的判定规则。",
                "変更はすぐ保存され、次のプレイから反映されます。現在のプレイの判定は変わりません。"),
            ["settings.gameplay.judgement_yokko"] = new(
                "osu!lazer",
                "osu!lazer",
                "osu!lazer"),
            ["settings.gameplay.judgement_osu_stable"] = new(
                "osu!stable",
                "osu!stable",
                "osu!stable"),
            ["settings.gameplay.judgement_etterna"] = new(
                "Etterna",
                "Etterna",
                "Etterna"),
            ["settings.gameplay.judgement_bms_beatoraja"] = new(
                "BMS",
                "BMS",
                "BMS"),
            ["settings.gameplay.etterna_justice"] = new(
                "Etterna Judge",
                "Etterna 判定等级",
                "Etterna Judge"),
            ["settings.gameplay.etterna_justice_note"] = new(
                "J4 through Justice (J9). Only used in Etterna mode.",
                "可调 J4 至 Justice（J9），仅在 Etterna 模式生效。",
                "J4 から Justice（J9）。Etterna モードでのみ使用します。"),
            ["settings.gameplay.etterna_boundaries"] = new(
                "Etterna parity · W1–W4 scale with Judge · W5 and automatic miss stay at ±180 ms.",
                "Etterna 对齐 · W1–W4 随等级缩放 · W5 与自动 Miss 固定为 ±180 ms。",
                "Etterna 準拠 · W1～W4 は Judge で縮小 · W5 と自動 Miss は ±180 ms 固定。"),
            ["settings.gameplay.feedback_heading"] = new(
                "Playfield & window behavior",
                "游玩反馈与窗口行为",
                "プレイ画面とウィンドウ動作"),
            ["settings.gameplay.feedback_note"] = new(
                "Control feedback and automatic pause behavior while playing.",
                "控制游玩反馈与离开窗口时的自动暂停行为。",
                "プレイ中のフィードバックと自動停止を設定します。"),
            ["settings.gameplay.show_lane_feedback"] = new("Lane lighting", "轨道亮灯", "レーン点灯"),
            ["settings.gameplay.show_lane_feedback_note"] = new(
                "Light lanes on key press",
                "按住按键时提供反馈",
                "キー入力時に反応"),
            ["settings.gameplay.show_timing_bar"] = new(
                "Timing bar",
                "判定条",
                "タイミングバー"),
            ["settings.gameplay.show_timing_bar_note"] = new(
                "Show early, late and timing trends",
                "显示快慢与输入趋势",
                "早遅と入力傾向を表示"),
            ["settings.gameplay.keysounds"] = new(
                "Keysounds",
                "按键音",
                "キー音"),
            ["settings.gameplay.keysounds_note"] = new(
                "Play chart samples on hit",
                "命中时播放谱面采样",
                "ヒット時に譜面サンプルを再生"),
            ["settings.gameplay.mines"] = new(
                "Chart mines",
                "谱面炸弹",
                "譜面の地雷"),
            ["settings.gameplay.mines_note"] = new(
                "Etterna, Quaver and other charts",
                "适用于 Etterna、Quaver 等含炸弹的谱面",
                "Etterna・Quaver などの対応譜面に適用"),
            ["settings.gameplay.pause_when_unfocused"] = new(
                "Pause when unfocused",
                "离开窗口时暂停",
                "非アクティブ時に一時停止"),
            ["settings.gameplay.pause_when_unfocused_note"] = new(
                "Pause when Yokko loses focus",
                "切换到其他窗口时自动暂停",
                "別のウィンドウに切り替えると自動停止"),
            ["settings.gameplay.resume_countdown"] = new(
                "Resume countdown",
                "恢复倒计时",
                "再開カウントダウン"),
            ["settings.gameplay.resume_countdown_note"] = new(
                "Show a 3-2-1 buffer before resuming",
                "恢复前显示 3·2·1 缓冲",
                "再開前に 3・2・1 を表示"),
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
                "BMS samples",
                "BMS 采样",
                "BMS サンプル"),
            ["settings.import.prefer_keysounds_note"] = new(
                "Keep sample paths",
                "保留采样路径",
                "サンプルを保持"),
            ["settings.import.prefer_ssc"] = new(
                "Prefer SSC",
                "优先 SSC",
                "SSC を優先"),
            ["settings.import.prefer_ssc_note"] = new(
                "Use richer simfiles",
                "优先更完整的包内谱面",
                "詳細な譜面を優先"),
            ["settings.import.bms_scratch"] = new(
                "BMS scratch",
                "BMS 皿键",
                "BMS スクラッチ"),
            ["settings.import.bms_scratch_note"] = new(
                "Extra playable lane",
                "作为额外可玩轨道导入",
                "追加レーンとして読込"),
            ["settings.import.show_warnings"] = new(
                "Show warnings",
                "显示兼容提示",
                "警告を表示"),
            ["settings.import.show_warnings_note"] = new(
                "Report limitations",
                "报告降级处理的效果",
                "制限事項を通知"),
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
            ["settings.import.external_osu_title"] = new(
                "External osu! Songs (read only)",
                "外部 osu! 谱面库（只读）",
                "外部 osu! Songs（読み取り専用）"),
            ["settings.import.external_osu_unconfigured"] = new(
                "Auto-find or choose osu!stable Songs; Yokko will remember it",
                "自动查找或手动选择 osu!stable Songs，Yokko 会记住路径",
                "自動検索または手動で osu!stable Songs を選択すると、Yokko が記憶します"),
            ["settings.import.external_osu_auto_find"] = new(
                "Auto-find",
                "自动查找",
                "自動検索"),
            ["settings.import.external_osu_manual_select"] = new(
                "Choose",
                "手动选择",
                "手動選択"),
            ["settings.import.external_osu_not_found"] = new(
                "osu!stable not found",
                "未找到 osu!stable",
                "osu!stable が見つかりません"),
            ["settings.import.external_osu_count"] = new(
                "{0} mania charts",
                "{0} 张 mania 谱面",
                "mania {0} 譜面"),
            ["settings.import.external_osu_scanning"] = new(
                "Indexing...",
                "正在建立索引…",
                "インデックス作成中…"),
            ["settings.import.external_osu_failed"] = new(
                "Songs unavailable",
                "Songs 目录不可用",
                "Songs フォルダーを使用できません"),

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
                "Version, credits and acknowledgements",
                "版本、制作人员与致谢",
                "バージョン、クレジット、謝辞"),
            ["settings.about.description"] = new(
                "Project information and acknowledgements.",
                "项目资料与致谢信息。",
                "プロジェクト情報と謝辞。"),
            ["settings.about.section_version"] = new("Version & updates", "版本与更新", "バージョンと更新"),
            ["settings.about.section_credits"] = new("Credits", "制作人员", "クレジット"),
            ["settings.about.section_acknowledgements"] = new("Acknowledgements", "致谢", "謝辞"),
            ["settings.about.creator"] = new("Moekotori", "Moekotori", "Moekotori"),
            ["settings.about.acknowledgements"] = new(
                "osu!  ·  Etterna  ·  Quaver",
                "osu!  ·  Etterna  ·  Quaver",
                "osu!  ·  Etterna  ·  Quaver"),
            ["settings.safety.title"] = new("Safety", "安全", "セーフティ"),
            ["settings.safety.subtitle"] = new(
                "Crash reports and recovery tools",
                "崩溃报告与恢复工具",
                "クラッシュレポートと復旧ツール"),
            ["settings.safety.description"] = new(
                "Access diagnostics and recovery options.",
                "查看诊断信息与恢复选项。",
                "診断情報と復旧オプションを確認します。"),
            ["settings.safety.crash_reports"] = new(
                "Crash reports",
                "崩溃报告",
                "クラッシュレポート"),
            ["settings.safety.crash_reports_ready"] = new(
                "Reports are stored in Yokko's data folder",
                "报告保存在 Yokko 的数据目录中",
                "レポートは Yokko のデータフォルダーに保存されます"),
            ["settings.safety.open_crash_reports"] = new(
                "Open crash report folder",
                "打开崩溃报告目录",
                "クラッシュレポートフォルダーを開く"),
            ["settings.safety.opened"] = new(
                "Crash report folder opened",
                "已打开崩溃报告目录",
                "クラッシュレポートフォルダーを開きました"),
            ["settings.safety.open_failed"] = new(
                "Unable to open the crash report folder",
                "无法打开崩溃报告目录",
                "クラッシュレポートフォルダーを開けませんでした"),
            ["settings.safety.exit_hold_duration"] = new(
                "Hold Esc to exit",
                "主页按住 Esc 退出时间",
                "Esc 長押しで終了するまで"),
            ["settings.safety.exit_hold_duration_value"] = new(
                "{0:0.0} s",
                "{0:0.0} 秒",
                "{0:0.0} 秒"),
            ["settings.safety.note"] = new(
                "After a crash, send the newest crash-*.txt file when reporting the problem.",
                "发生闪退后，反馈问题时请附上时间最新的 crash-*.txt 文件。",
                "クラッシュ後、問題を報告する際は最新の crash-*.txt ファイルを添付してください。"),
            ["settings.safety.footer"] = new(
                "Safety tools do not change gameplay settings",
                "安全工具不会更改游戏设置",
                "セーフティツールはゲーム設定を変更しません"),
        };

    public static LocalisableString Get(string key, params object[] args)
    {
        if (!translations.TryGetValue(key, out Translation translation))
            throw new ArgumentException($"Unknown localisation key: {key}", nameof(key));

        return new TranslatableString(key, translation.English, args);
    }

    public static LocalisableString ModName(ManiaModDefinition definition)
    {
        if (definition.Id is >= ManiaModId.Key1 and <= ManiaModId.Key10)
        {
            int keys = (int)definition.Id - (int)ManiaModId.Key1 + 1;
            return Get("mods.definition.key-count.name", keys);
        }

        return Get($"mods.definition.{definition.Key}.name");
    }

    public static LocalisableString ModDescription(ManiaModDefinition definition)
    {
        if (definition.Id is >= ManiaModId.Key1 and <= ManiaModId.Key10)
            return Get("mods.definition.key-count.description");

        return Get($"mods.definition.{definition.Key}.description");
    }

    public static string SearchTerms(params string[] keys) =>
        string.Join(" ", keys.SelectMany(key =>
        {
            Translation translation = translations[key];
            return new[] { translation.English, translation.Chinese, translation.Japanese };
        }));

    public static string SearchTermsForPrefix(string prefix, params string[] aliases) =>
        string.Join(
            " ",
            translations
                .Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                .SelectMany(pair => new[]
                {
                    pair.Value.English,
                    pair.Value.Chinese,
                    pair.Value.Japanese,
                })
                .Concat(aliases));

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
