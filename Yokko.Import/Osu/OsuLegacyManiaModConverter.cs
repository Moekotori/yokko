using Yokko.Core.Mods;

namespace Yokko.Import.Osu;

/// <summary>
/// Legacy replay Mod flags used by the osu! binary score/replay format.
/// Numeric values are part of that external format.
/// </summary>
[Flags]
public enum OsuLegacyMods
{
    None = 0,
    NoFail = 1 << 0,
    Easy = 1 << 1,
    Hidden = 1 << 3,
    HardRock = 1 << 4,
    SuddenDeath = 1 << 5,
    DoubleTime = 1 << 6,
    HalfTime = 1 << 8,
    Nightcore = 1 << 9,
    Flashlight = 1 << 10,
    Autoplay = 1 << 11,
    Perfect = 1 << 14,
    Key4 = 1 << 15,
    Key5 = 1 << 16,
    Key6 = 1 << 17,
    Key7 = 1 << 18,
    Key8 = 1 << 19,
    FadeIn = 1 << 20,
    Random = 1 << 21,
    Cinema = 1 << 22,
    Key9 = 1 << 24,
    KeyCoop = 1 << 25,
    Key1 = 1 << 26,
    Key3 = 1 << 27,
    Key2 = 1 << 28,
    ScoreV2 = 1 << 29,
    Mirror = 1 << 30,
}

/// <summary>
/// Converts legacy osu!mania replay flags using osu!lazer's precedence and
/// mapping rules.
///
/// Source: ppy/osu,
/// osu.Game.Rulesets.Mania/ManiaRuleset.cs and
/// osu.Game.Rulesets.Mania.Tests/ManiaLegacyModConversionTest.cs,
/// commit 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0 (MIT).
/// </summary>
public static class OsuLegacyManiaModConverter
{
    public static ManiaModSet Convert(OsuLegacyMods legacyMods)
    {
        var mods = new List<ManiaModId>();

        if (legacyMods.HasFlag(OsuLegacyMods.Nightcore))
            mods.Add(ManiaModId.Nightcore);
        else if (legacyMods.HasFlag(OsuLegacyMods.DoubleTime))
            mods.Add(ManiaModId.DoubleTime);

        if (legacyMods.HasFlag(OsuLegacyMods.Perfect))
            mods.Add(ManiaModId.Perfect);
        else if (legacyMods.HasFlag(OsuLegacyMods.SuddenDeath))
            mods.Add(ManiaModId.SuddenDeath);

        if (legacyMods.HasFlag(OsuLegacyMods.Cinema))
            mods.Add(ManiaModId.Cinema);
        else if (legacyMods.HasFlag(OsuLegacyMods.Autoplay))
            mods.Add(ManiaModId.Autoplay);

        addIf(OsuLegacyMods.Easy, ManiaModId.Easy);
        addIf(OsuLegacyMods.FadeIn, ManiaModId.FadeIn);
        addIf(OsuLegacyMods.Flashlight, ManiaModId.Flashlight);
        addIf(OsuLegacyMods.HalfTime, ManiaModId.HalfTime);
        addIf(OsuLegacyMods.HardRock, ManiaModId.HardRock);
        addIf(OsuLegacyMods.Hidden, ManiaModId.Hidden);
        addIf(OsuLegacyMods.Key1, ManiaModId.Key1);
        addIf(OsuLegacyMods.Key2, ManiaModId.Key2);
        addIf(OsuLegacyMods.Key3, ManiaModId.Key3);
        addIf(OsuLegacyMods.Key4, ManiaModId.Key4);
        addIf(OsuLegacyMods.Key5, ManiaModId.Key5);
        addIf(OsuLegacyMods.Key6, ManiaModId.Key6);
        addIf(OsuLegacyMods.Key7, ManiaModId.Key7);
        addIf(OsuLegacyMods.Key8, ManiaModId.Key8);
        addIf(OsuLegacyMods.Key9, ManiaModId.Key9);
        addIf(OsuLegacyMods.KeyCoop, ManiaModId.DualStages);
        addIf(OsuLegacyMods.NoFail, ManiaModId.NoFail);
        addIf(OsuLegacyMods.Random, ManiaModId.Random);
        addIf(OsuLegacyMods.Mirror, ManiaModId.Mirror);
        addIf(OsuLegacyMods.ScoreV2, ManiaModId.ScoreV2);

        try
        {
            return mods.Count == 0
                ? ManiaModSet.Empty
                : new ManiaModSet(mods);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "The replay contains an incompatible osu!mania Mod combination.",
                exception);
        }

        void addIf(OsuLegacyMods flag, ManiaModId mod)
        {
            if (legacyMods.HasFlag(flag))
                mods.Add(mod);
        }
    }
}
