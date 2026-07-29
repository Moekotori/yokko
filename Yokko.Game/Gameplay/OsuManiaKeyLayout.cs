using System;
using System.Collections.Generic;
using System.Linq;
using osuTK.Input;
using Yokko.Core.Gameplay;

namespace Yokko.Game.Gameplay;

/// <summary>
/// Default osu!mania keyboard layouts shared by gameplay and configuration.
/// </summary>
/// <remarks>
/// Ported from ppy/osu at commit 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0:
/// osu.Game.Rulesets.Mania/SingleStageVariantGenerator.cs,
/// DualStageVariantGenerator.cs, and VariantMappingGenerator.cs.
/// </remarks>
internal static class OsuManiaKeyLayout
{
    private static readonly KeyMode[] supportedModes =
    {
        KeyMode.OneKey,
        KeyMode.TwoKey,
        KeyMode.ThreeKey,
        KeyMode.FourKey,
        KeyMode.FiveKey,
        KeyMode.SixKey,
        KeyMode.SevenKey,
        KeyMode.EightKey,
        KeyMode.NineKey,
        KeyMode.TenKey,
        KeyMode.TwelveKey,
        KeyMode.FourteenKey,
        KeyMode.SixteenKey,
        KeyMode.EighteenKey,
        KeyMode.TwentyKey,
    };

    public static IReadOnlyList<KeyMode> SupportedModes => supportedModes;

    public static Key[] GetDefaultKeys(KeyMode keyMode)
    {
        int keyCount = validateKeyMode(keyMode);
        return keyCount <= 10
            ? singleStageKeys(keyCount)
            : dualStageKeys(keyCount / 2);
    }

    public static Key[] GetDefaultKeys(KeyMode keyMode, int stageCount)
    {
        int keyCount = validateKeyMode(keyMode);
        if (stageCount == 1)
            return singleStageKeys(keyCount);
        if (stageCount != 2 || keyCount % 2 != 0)
            throw new ArgumentOutOfRangeException(nameof(stageCount));

        return dualStageKeys(keyCount / 2);
    }

    public static string GetDisplayName(KeyMode keyMode)
    {
        int keyCount = validateKeyMode(keyMode);
        return keyCount <= 10
            ? $"{keyCount}K"
            : $"{keyCount / 2}K + {keyCount / 2}K";
    }

    private static Key[] singleStageKeys(int columns)
    {
        Key[] left = columns == 10
            ? [Key.A, Key.S, Key.D, Key.F, Key.V]
            : [Key.A, Key.S, Key.D, Key.F];
        Key[] right = columns == 10
            ? [Key.N, Key.J, Key.K, Key.L, Key.Semicolon]
            : [Key.J, Key.K, Key.L, Key.Semicolon];

        return generateStage(columns, left, right, Key.Space).ToArray();
    }

    private static Key[] dualStageKeys(int keysPerStage)
    {
        Key[] stage1Left;
        Key[] stage1Right;
        Key[] stage2Left;
        Key[] stage2Right;
        if (keysPerStage == 10)
        {
            stage1Left = [Key.Q, Key.W, Key.E, Key.R, Key.V];
            stage1Right = [Key.M, Key.I, Key.O, Key.P, Key.BracketLeft];
            stage2Left = [Key.S, Key.D, Key.F, Key.G, Key.B];
            stage2Right = [Key.N, Key.J, Key.K, Key.L, Key.Semicolon];
        }
        else
        {
            stage1Left = [Key.Q, Key.W, Key.E, Key.R];
            stage1Right = [Key.I, Key.O, Key.P, Key.BracketLeft];
            stage2Left = [Key.S, Key.D, Key.F, Key.G];
            stage2Right = [Key.J, Key.K, Key.L, Key.Semicolon];
        }

        return generateStage(
                keysPerStage,
                stage1Left,
                stage1Right,
                Key.V)
            .Concat(generateStage(
                keysPerStage,
                stage2Left,
                stage2Right,
                Key.B))
            .ToArray();
    }

    private static IEnumerable<Key> generateStage(
        int columns,
        IReadOnlyList<Key> left,
        IReadOnlyList<Key> right,
        Key special)
    {
        for (int i = left.Count - columns / 2; i < left.Count; i++)
            yield return left[i];

        if (columns % 2 == 1)
            yield return special;

        for (int i = 0; i < columns / 2; i++)
            yield return right[i];
    }

    private static int validateKeyMode(KeyMode keyMode)
    {
        if (!supportedModes.Contains(keyMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(keyMode),
                keyMode,
                "osu!mania supports 1K-10K and 6K-10K dual-stage layouts.");
        }

        return (int)keyMode;
    }
}
