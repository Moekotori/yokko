namespace Yokko.Core.Mods;

/// <summary>
/// Snapshot of the mods returned by osu!lazer's ManiaRuleset.GetModsFor().
///
/// Source: ppy/osu, osu.Game.Rulesets.Mania/ManiaRuleset.cs
/// commit 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0 (MIT).
///
/// This is a parity target only. A mod must also have a registered runtime
/// implementation before Yokko may expose it as selectable.
/// </summary>
public static class OsuManiaModParityCatalog
{
    public const string UpstreamCommit =
        "9f227ed28b6c8ba46dfea1f000f778d8b2827ad0";

    private static readonly ManiaModDefinition[] definitions =
    [
        mod(ManiaModId.Easy, "easy", "EZ", "Easy", ManiaModCategory.DifficultyReduction),
        mod(ManiaModId.NoFail, "no-fail", "NF", "No Fail", ManiaModCategory.DifficultyReduction),
        mod(ManiaModId.NoPause, "no-pause", "NP", "No Pause", ManiaModCategory.DifficultyIncrease),
        mod(ManiaModId.HalfTime, "half-time", "HT", "Half Time", ManiaModCategory.DifficultyReduction),
        mod(ManiaModId.Daycore, "daycore", "DC", "Daycore", ManiaModCategory.DifficultyReduction),
        mod(ManiaModId.NoRelease, "no-release", "NR", "No Release", ManiaModCategory.DifficultyReduction),

        mod(ManiaModId.HardRock, "hard-rock", "HR", "Hard Rock", ManiaModCategory.DifficultyIncrease),
        mod(ManiaModId.SuddenDeath, "sudden-death", "SD", "Sudden Death", ManiaModCategory.DifficultyIncrease),
        mod(ManiaModId.Perfect, "perfect", "PF", "Perfect", ManiaModCategory.DifficultyIncrease),
        mod(ManiaModId.DoubleTime, "double-time", "DT", "Double Time", ManiaModCategory.DifficultyIncrease),
        mod(ManiaModId.Nightcore, "nightcore", "NC", "Nightcore", ManiaModCategory.DifficultyIncrease),
        mod(ManiaModId.FadeIn, "fade-in", "FI", "Fade In", ManiaModCategory.DifficultyIncrease),
        mod(ManiaModId.Hidden, "hidden", "HD", "Hidden", ManiaModCategory.DifficultyIncrease),
        mod(ManiaModId.Cover, "cover", "CO", "Cover", ManiaModCategory.DifficultyIncrease),
        mod(ManiaModId.Flashlight, "flashlight", "FL", "Flashlight", ManiaModCategory.DifficultyIncrease),
        mod(
            ManiaModId.AccuracyChallenge,
            "accuracy-challenge",
            "AC",
            "Accuracy Challenge",
            ManiaModCategory.DifficultyIncrease),

        mod(ManiaModId.Random, "random", "RD", "Random", ManiaModCategory.Conversion),
        mod(ManiaModId.DualStages, "dual-stages", "DS", "Dual Stages", ManiaModCategory.Conversion),
        mod(ManiaModId.Mirror, "mirror", "MR", "Mirror", ManiaModCategory.Conversion),
        mod(
            ManiaModId.DifficultyAdjust,
            "difficulty-adjust",
            "DA",
            "Difficulty Adjust",
            ManiaModCategory.Conversion),
        mod(ManiaModId.Classic, "classic", "CL", "Classic", ManiaModCategory.Conversion),
        mod(ManiaModId.Invert, "invert", "IN", "Invert", ManiaModCategory.Conversion),
        mod(
            ManiaModId.ConstantSpeed,
            "constant-speed",
            "CS",
            "Constant Speed",
            ManiaModCategory.Conversion),
        mod(ManiaModId.HoldOff, "hold-off", "HO", "Hold Off", ManiaModCategory.Conversion),
        mod(ManiaModId.Key1, "key-1", "1K", "1 Key", ManiaModCategory.Conversion),
        mod(ManiaModId.Key2, "key-2", "2K", "2 Keys", ManiaModCategory.Conversion),
        mod(ManiaModId.Key3, "key-3", "3K", "3 Keys", ManiaModCategory.Conversion),
        mod(ManiaModId.Key4, "key-4", "4K", "4 Keys", ManiaModCategory.Conversion),
        mod(ManiaModId.Key5, "key-5", "5K", "5 Keys", ManiaModCategory.Conversion),
        mod(ManiaModId.Key6, "key-6", "6K", "6 Keys", ManiaModCategory.Conversion),
        mod(ManiaModId.Key7, "key-7", "7K", "7 Keys", ManiaModCategory.Conversion),
        mod(ManiaModId.Key8, "key-8", "8K", "8 Keys", ManiaModCategory.Conversion),
        mod(ManiaModId.Key9, "key-9", "9K", "9 Keys", ManiaModCategory.Conversion),
        mod(ManiaModId.Key10, "key-10", "10K", "10 Keys", ManiaModCategory.Conversion),

        mod(
            ManiaModId.Autoplay,
            "autoplay",
            "AD",
            "Developer Autoplay",
            ManiaModCategory.Automation),
        mod(ManiaModId.Cinema, "cinema", "CN", "Cinema", ManiaModCategory.Automation),

        mod(ManiaModId.WindUp, "wind-up", "WU", "Wind Up", ManiaModCategory.Fun),
        mod(ManiaModId.WindDown, "wind-down", "WD", "Wind Down", ManiaModCategory.Fun),
        mod(ManiaModId.Muted, "muted", "MU", "Muted", ManiaModCategory.Fun),
        mod(
            ManiaModId.AdaptiveSpeed,
            "adaptive-speed",
            "AS",
            "Adaptive Speed",
            ManiaModCategory.Fun),

        mod(ManiaModId.ScoreV2, "score-v2", "SV2", "Score V2", ManiaModCategory.System),
    ];

    private static readonly IReadOnlyDictionary<ManiaModId, ManiaModDefinition>
        byId = definitions.ToDictionary(static definition => definition.Id);

    private static readonly IReadOnlyDictionary<string, ManiaModDefinition>
        byKey = definitions.ToDictionary(
            static definition => definition.Key,
            StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<ManiaModDefinition> All => definitions;

    public static ManiaModDefinition Get(ManiaModId id) => byId[id];

    public static bool TryGet(
        string key,
        out ManiaModDefinition? definition)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            definition = null;
            return false;
        }

        return byKey.TryGetValue(key, out definition);
    }

    private static ManiaModDefinition mod(
        ManiaModId id,
        string key,
        string acronym,
        string name,
        ManiaModCategory category) =>
        new(id, key, acronym, name, category);
}
