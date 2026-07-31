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
    // System-provided text is not present in the localisation table, but still
    // needs glyphs in Yokko's deliberately subsetted bitmap font.
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
            ["main.utility_folder"] = new("Chart folder", "谱面文件夹", "譜面フォルダー"),
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
            ["mods.title"] = new("GAMEPLAY MODS", "游玩模组", "ゲームプレイ MOD"),
            ["mods.subtitle"] = new(
                "Customize your play experience.",
                "自定义你的游玩体验。",
                "プレイ体験をカスタマイズ。"),
            ["mods.category.difficulty_down"] = new("DIFFICULTY DOWN", "降低难度", "難易度を下げる"),
            ["mods.category.difficulty_up"] = new("DIFFICULTY UP", "提高难度", "難易度を上げる"),
            ["mods.category.conversion"] = new("CONVERSION", "谱面转换", "変換"),
            ["mods.category.automation"] = new("AUTOMATION", "自动游玩", "自動プレイ"),
            ["mods.category.fun"] = new("FUN", "趣味玩法", "お楽しみ"),
            ["mods.speed_multiplier"] = new("SPEED MULTIPLIER", "速度倍率", "スピード倍率"),
            ["mods.active_mods"] = new("ACTIVE MODS", "已启用模组", "有効な MOD"),
            ["mods.active"] = new("ACTIVE", "已启用", "有効"),
            ["mods.activate_hint"] = new("SPACE · ACTIVATE", "空格 · 启用", "SPACE · 有効化"),
            ["mods.back"] = new("BACK", "返回", "戻る"),
            ["mods.reset"] = new("RESET", "重置", "リセット"),
            ["mods.done"] = new("DONE", "完成", "完了"),
            ["mods.standard_only"] = new(
                "Available only for charts imported from osu!standard.",
                "仅适用于从 osu!standard 导入的谱面。",
                "osu!standard からインポートした譜面でのみ利用できます。"),
            ["mods.definition.easy.name"] = new("Easy", "简单", "イージー"),
            ["mods.definition.easy.description"] = new("Forgiving difficulty and gentler health drain.", "降低难度与生命值损耗。", "難易度とライフ減少を緩和します。"),
            ["mods.definition.no-fail.name"] = new("No Fail", "不会失败", "ノーフェイル"),
            ["mods.definition.no-fail.description"] = new("Keep playing even when your health reaches zero.", "生命值归零后仍可继续游玩。", "ライフがゼロになってもプレイを続けられます。"),
            ["mods.definition.half-time.name"] = new("Half Time", "半速", "ハーフタイム"),
            ["mods.definition.half-time.description"] = new("Slow the song down to 75% speed.", "将歌曲速度降低至 75%。", "楽曲速度を 75% に下げます。"),
            ["mods.definition.daycore.name"] = new("Daycore", "降调半速", "デイコア"),
            ["mods.definition.daycore.description"] = new("Slow down with a lower-pitched soundtrack.", "降低速度，同时降低音调。", "速度と音程を下げます。"),
            ["mods.definition.no-release.name"] = new("No Release", "忽略松键", "ノーリリース"),
            ["mods.definition.no-release.description"] = new("Ignore judgements when hold notes are released.", "忽略长按音符松键时的判定。", "長押しノーツを離した時の判定を無視します。"),
            ["mods.definition.hard-rock.name"] = new("Hard Rock", "困难", "ハードロック"),
            ["mods.definition.hard-rock.description"] = new("Raise the difficulty and health drain.", "提高难度与生命值损耗。", "難易度とライフ減少を上げます。"),
            ["mods.definition.sudden-death.name"] = new("Sudden Death", "突然死亡", "サドンデス"),
            ["mods.definition.sudden-death.description"] = new("A single miss ends the run.", "一次 Miss 即结束游玩。", "1 回の Miss でプレイ終了になります。"),
            ["mods.definition.perfect.name"] = new("Perfect", "完美", "パーフェクト"),
            ["mods.definition.perfect.description"] = new("Any judgement below Great ends the run.", "出现低于 Great 的判定即结束游玩。", "Great 未満の判定でプレイ終了になります。"),
            ["mods.definition.double-time.name"] = new("Double Time", "加速", "ダブルタイム"),
            ["mods.definition.double-time.description"] = new("Speed the song up to 150%.", "将歌曲速度提高至 150%。", "楽曲速度を 150% に上げます。"),
            ["mods.definition.nightcore.name"] = new("Nightcore", "升调加速", "ナイトコア"),
            ["mods.definition.nightcore.description"] = new("Speed up with a higher-pitched soundtrack.", "提高速度，同时提高音调。", "速度と音程を上げます。"),
            ["mods.definition.fade-in.name"] = new("Fade In", "渐入", "フェードイン"),
            ["mods.definition.fade-in.description"] = new("Notes appear gradually as they approach.", "音符接近时逐渐出现。", "接近するノーツが徐々に現れます。"),
            ["mods.definition.hidden.name"] = new("Hidden", "隐藏", "ヒドゥン"),
            ["mods.definition.hidden.description"] = new("Notes fade before reaching the judgement line.", "音符到达判定线前逐渐消失。", "ノーツが判定ラインの前で消えていきます。"),
            ["mods.definition.cover.name"] = new("Cover", "遮挡", "カバー"),
            ["mods.definition.cover.description"] = new("Hide part of the playfield with a cover.", "用遮罩隐藏部分游玩区域。", "カバーでプレイフィールドの一部を隠します。"),
            ["mods.definition.flashlight.name"] = new("Flashlight", "手电筒", "フラッシュライト"),
            ["mods.definition.flashlight.description"] = new("See notes only through a limited viewing area.", "只能在有限的可视范围内看到音符。", "限られた範囲内だけノーツが見えます。"),
            ["mods.definition.accuracy-challenge.name"] = new("Accuracy Challenge", "准确率挑战", "精度チャレンジ"),
            ["mods.definition.accuracy-challenge.description"] = new("Fail when accuracy drops below your target.", "准确率低于目标时失败。", "精度が目標を下回ると失敗します。"),
            ["mods.definition.random.name"] = new("Random", "随机", "ランダム"),
            ["mods.definition.random.description"] = new("Shuffle note columns with a repeatable seed.", "使用可复现的种子随机排列音符轨道。", "再現可能なシードでノーツ列を並べ替えます。"),
            ["mods.definition.dual-stages.name"] = new("Dual Stages", "双舞台", "デュアルステージ"),
            ["mods.definition.dual-stages.description"] = new("Split converted charts across two playfields.", "将转换后的谱面分布到两个游玩区域。", "変換した譜面を 2 つのプレイフィールドに分けます。"),
            ["mods.definition.mirror.name"] = new("Mirror", "镜像", "ミラー"),
            ["mods.definition.mirror.description"] = new("Reverse every note column.", "反转所有音符轨道。", "すべてのノーツ列を反転します。"),
            ["mods.definition.difficulty-adjust.name"] = new("Difficulty Adjust", "难度调整", "難易度調整"),
            ["mods.definition.difficulty-adjust.description"] = new("Customise health drain and judgement difficulty.", "自定义生命值损耗与判定难度。", "ライフ減少と判定難易度を調整します。"),
            ["mods.definition.classic.name"] = new("Classic", "经典", "クラシック"),
            ["mods.definition.classic.description"] = new("Use classic mania scoring and behaviour.", "使用经典 mania 计分与行为。", "クラシックな mania のスコアと動作を使用します。"),
            ["mods.definition.invert.name"] = new("Invert", "反转音符", "インバート"),
            ["mods.definition.invert.description"] = new("Swap tap notes and hold-note bodies.", "交换短按音符与长按音符主体。", "タップノーツと長押しノーツの本体を入れ替えます。"),
            ["mods.definition.constant-speed.name"] = new("Constant Speed", "恒定流速", "コンスタントスピード"),
            ["mods.definition.constant-speed.description"] = new("Keep the visual scroll velocity constant.", "保持视觉滚动速度恒定。", "見た目のスクロール速度を一定にします。"),
            ["mods.definition.hold-off.name"] = new("Hold Off", "移除长按", "ホールドオフ"),
            ["mods.definition.hold-off.description"] = new("Convert hold notes into regular tap notes.", "将长按音符转换为普通短按音符。", "長押しノーツを通常のタップノーツに変換します。"),
            ["mods.definition.key-count.name"] = new("{0} Keys", "{0} 键", "{0} キー"),
            ["mods.definition.key-count.description"] = new("Convert a standard-mode chart to this key count.", "将标准模式谱面转换为此键数。", "standard モードの譜面をこのキー数に変換します。"),
            ["mods.definition.autoplay.name"] = new("Autoplay", "自动游玩", "オートプレイ"),
            ["mods.definition.autoplay.description"] = new("Watch a perfect automated performance.", "观看完美的自动演示。", "完璧な自動プレイを鑑賞します。"),
            ["mods.definition.cinema.name"] = new("Cinema", "影院模式", "シネマ"),
            ["mods.definition.cinema.description"] = new("Watch an automated performance without the playfield.", "隐藏游玩区域并观看自动演示。", "プレイフィールドを隠して自動プレイを鑑賞します。"),
            ["mods.definition.wind-up.name"] = new("Wind Up", "逐渐加速", "ウインドアップ"),
            ["mods.definition.wind-up.description"] = new("Gradually increase playback speed.", "逐渐提高播放速度。", "再生速度を徐々に上げます。"),
            ["mods.definition.wind-down.name"] = new("Wind Down", "逐渐减速", "ウインドダウン"),
            ["mods.definition.wind-down.description"] = new("Gradually decrease playback speed.", "逐渐降低播放速度。", "再生速度を徐々に下げます。"),
            ["mods.definition.muted.name"] = new("Muted", "静音玩法", "ミュート"),
            ["mods.definition.muted.description"] = new("Play with configurable audio cues muted.", "游玩时按设置静音部分音频提示。", "設定したオーディオキューを消してプレイします。"),
            ["mods.definition.adaptive-speed.name"] = new("Adaptive Speed", "自适应速度", "アダプティブスピード"),
            ["mods.definition.adaptive-speed.description"] = new("Change speed in response to recent accuracy.", "根据近期准确率动态调整速度。", "直近の精度に応じて速度を変えます。"),
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
            ["gameplay.pause.settings"] = new("Settings", "设置", "設定"),
            ["gameplay.pause.exit"] = new("Exit play", "退出游玩", "選曲へ戻る"),
            ["gameplay.pause.hint_select"] = new("SELECT", "选择", "選択"),
            ["gameplay.pause.hint_confirm"] = new("CONFIRM", "确认", "決定"),
            ["gameplay.pause.hint_retry"] = new("RETRY", "重试", "リトライ"),

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
            ["settings.display.interface_scale"] = new("Interface size", "界面大小", "UI サイズ"),
            ["settings.display.performance_readout"] = new(
                "Performance readout",
                "性能读数",
                "パフォーマンス表示"),
            ["settings.display.enabled"] = new("Enabled", "已开启", "オン"),
            ["settings.display.disabled"] = new("Disabled", "已关闭", "オフ"),
            ["settings.display.windowed"] = new("Windowed", "窗口化", "ウィンドウ"),
            ["settings.display.borderless"] = new("Borderless", "无边框", "ボーダーレス"),
            ["settings.display.fullscreen"] = new("Fullscreen", "全屏", "フルスクリーン"),
            ["settings.display.compact"] = new("80%", "80%", "80%"),
            ["settings.display.comfortable"] = new("90%", "90%", "90%"),
            ["settings.display.spacious"] = new("100%", "100%", "100%"),

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
            ["settings.gameplay.ready"] = new(
                "Gameplay controls are live",
                "游玩控制已实装",
                "ゲームプレイ設定が有効です"),
            ["settings.gameplay.ready_metadata"] = new(
                "4K  {0}   ·   7K  {1}",
                "4K  {0}   ·   7K  {1}",
                "4K  {0}   ·   7K  {1}"),
            ["settings.gameplay.input_monitor"] = new(
                "Live input monitor",
                "实时输入监测",
                "リアルタイム入力モニター"),
            ["settings.gameplay.live"] = new("LIVE", "已启用", "有効"),
            ["settings.gameplay.calibration_start"] = new(
                "30s calibration",
                "30 秒校准",
                "30秒キャリブレーション"),
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
            ["settings.gameplay.key_profile"] = new("Key profile", "键位配置", "キープロファイル"),
            ["settings.gameplay.edit_all"] = new("Edit all keys", "修改键位", "キーを一括変更"),
            ["settings.gameplay.reset"] = new("Reset keys", "重置键位", "キーをリセット"),
            ["settings.gameplay.presets"] = new("Presets", "预设", "プリセット"),
            ["settings.gameplay.preset_standard"] = new("Standard", "标准", "標準"),
            ["settings.gameplay.preset_left"] = new("Left hand", "左手", "左手"),
            ["settings.gameplay.preset_split"] = new("Split", "双手", "両手"),
            ["settings.gameplay.copy_other_mode"] = new(
                "Copy to other",
                "复制到另一模式",
                "他モードへコピー"),
            ["settings.gameplay.all_modes_hint"] = new(
                "All osu!mania layouts are editable · dual stages use two rows",
                "全部 osu!mania 键位均可修改 · 双舞台分两行显示",
                "すべての osu!mania 配列を編集可能 · デュアルは2段表示"),
            ["settings.gameplay.export_profile"] = new("Export", "导出", "書き出し"),
            ["settings.gameplay.import_profile"] = new("Import", "导入", "読み込み"),
            ["settings.gameplay.preset_applied"] = new(
                "{0} preset applied.",
                "已应用“{0}”预设。",
                "「{0}」プリセットを適用しました。"),
            ["settings.gameplay.profile_copied"] = new(
                "{0} central lanes copied to {1}.",
                "已将 {0} 中央轨道复制到 {1}。",
                "{0} の中央レーンを {1} にコピーしました。"),
            ["settings.gameplay.profile_exported"] = new(
                "All mania key profiles copied to the clipboard.",
                "全部 Mania 键位方案已复制到剪贴板。",
                "すべてのManiaキー設定をクリップボードへコピーしました。"),
            ["settings.gameplay.profile_imported"] = new(
                "All mania key profiles imported.",
                "已导入全部 Mania 键位方案。",
                "すべてのManiaキー設定を読み込みました。"),
            ["settings.gameplay.profile_import_failed"] = new(
                "Clipboard does not contain a valid Yokko key profile.",
                "剪贴板中没有有效的 Yokko 键位方案。",
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
            ["settings.gameplay.shortcut_pause_back"] = new(
                "Pause / resume / back",
                "暂停 / 继续 / 返回",
                "一時停止 / 再開 / 戻る"),
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
            ["settings.gameplay.single_saved"] = new(
                "Lane {0} is now {1}.",
                "轨道 {0} 已设为 {1}。",
                "レーン {0} を {1} に設定しました。"),
            ["settings.gameplay.key_swap_notice"] = new(
                "{0} was on lane {1}; lanes {1} and {2} were swapped.",
                "{0} 原本属于轨道 {1}；已交换轨道 {1} 与 {2}。",
                "{0} はレーン {1} にあり、レーン {1} と {2} を入れ替えました。"),
            ["settings.gameplay.key_swapped"] = new("Swapped", "已交换", "入れ替え済み"),
            ["settings.gameplay.input_active"] = new("Detected", "已检测", "検出"),
            ["settings.gameplay.input_detected"] = new(
                "{0} detected · lane {1}",
                "检测到 {0} · 轨道 {1}",
                "{0} を検出 · レーン {1}"),
            ["settings.gameplay.input_unbound"] = new(
                "{0} is not bound in this profile",
                "{0} 未绑定到当前方案",
                "{0} は現在の設定に割り当てられていません"),
            ["settings.gameplay.input_chord"] = new(
                "{0}/{1} keys currently detected · hold combinations to check rollover",
                "当前检测到 {0}/{1} 键 · 可按住组合键检查多键识别",
                "現在 {0}/{1} キーを検出 · 同時押しでロールオーバーを確認"),
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
                "Choose Yokko's osu!mania-compatible rules or Etterna's timing and closest-note behavior.",
                "选择 Yokko 的 osu!mania 兼容规则，或 Etterna 的窗口与最近音符判定。",
                "Yokko の osu!mania 互換ルール、または Etterna の判定幅と最近ノーツ判定を選びます。"),
            ["settings.gameplay.judgement_yokko"] = new(
                "Yokko · osu!mania",
                "Yokko · osu!mania",
                "Yokko · osu!mania"),
            ["settings.gameplay.judgement_etterna"] = new(
                "Etterna",
                "Etterna",
                "Etterna"),
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
                "Mines",
                "炸弹",
                "地雷"),
            ["settings.gameplay.mines_note"] = new(
                "Hit one to lose health",
                "踩中会爆炸并扣除生命",
                "踏むと爆発してゲージが減少"),
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
