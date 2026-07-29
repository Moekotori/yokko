using System;
using System.Collections.Generic;
using System.Linq;
using osuTK.Input;
using Yokko.Core.Gameplay;

namespace Yokko.Game.Screens.Gameplay;

public sealed class KeyModeBindings
{
    private readonly Key[] keys;
    private readonly Dictionary<Key, int> lanesByKey;

    private KeyModeBindings(KeyMode keyMode, Key[] keys)
    {
        KeyMode = keyMode;
        this.keys = keys;
        lanesByKey = new Dictionary<Key, int>(keys.Length);

        for (int i = 0; i < keys.Length; i++)
            lanesByKey[keys[i]] = i;
    }

    public KeyMode KeyMode { get; }

    public int KeyCount => keys.Length;

    public static KeyModeBindings ForMode(KeyMode keyMode) =>
        new(keyMode, defaultKeys(keyMode));

    public static KeyModeBindings ForMode(
        KeyMode keyMode,
        int stageCount)
    {
        if (stageCount == 1)
            return ForMode(keyMode);
        if (stageCount != 2 || (int)keyMode % 2 != 0)
            throw new ArgumentOutOfRangeException(nameof(stageCount));

        return new KeyModeBindings(
            keyMode,
            dualStageKeys((int)keyMode / 2));
    }

    public static KeyModeBindings ForMode(
        KeyMode keyMode,
        IReadOnlyList<Key> configuredKeys)
    {
        int expectedCount = validateKeyMode(keyMode);

        if (configuredKeys == null || configuredKeys.Count != expectedCount)
            throw new ArgumentException(
                $"{keyMode} requires exactly {expectedCount} keys.",
                nameof(configuredKeys));

        if (configuredKeys.Distinct().Count() != expectedCount)
            throw new ArgumentException(
                "A gameplay key profile cannot contain duplicate keys.",
                nameof(configuredKeys));

        return new KeyModeBindings(keyMode, configuredKeys.ToArray());
    }

    public int GetLane(Key key) => lanesByKey.TryGetValue(key, out int lane) ? lane : -1;

    public string GetDisplayKey(int lane)
    {
        return FormatKey(keys[lane]);
    }

    public static string FormatKey(Key key) => key switch
    {
        Key.Space => "Space",
        Key.Period => ".",
        Key.Slash => "/",
        _ => key.ToString(),
    };

    private static Key[] defaultKeys(KeyMode keyMode) => keyMode switch
    {
        KeyMode.OneKey => [Key.Space],
        KeyMode.TwoKey => [Key.F, Key.J],
        KeyMode.ThreeKey => [Key.F, Key.Space, Key.J],
        KeyMode.FourKey => [Key.D, Key.F, Key.J, Key.K],
        KeyMode.FiveKey => [Key.D, Key.F, Key.Space, Key.J, Key.K],
        KeyMode.SixKey => [Key.S, Key.D, Key.F, Key.J, Key.K, Key.L],
        KeyMode.SevenKey => [Key.S, Key.D, Key.F, Key.Space, Key.J, Key.K, Key.L],
        KeyMode.EightKey => [Key.A, Key.S, Key.D, Key.F, Key.J, Key.K, Key.L, Key.Semicolon],
        KeyMode.NineKey => [Key.A, Key.S, Key.D, Key.F, Key.Space, Key.J, Key.K, Key.L, Key.Semicolon],
        KeyMode.TenKey => [Key.A, Key.S, Key.D, Key.F, Key.V, Key.N, Key.J, Key.K, Key.L, Key.Semicolon],
        KeyMode.TwelveKey
            or KeyMode.FourteenKey
            or KeyMode.SixteenKey
            or KeyMode.EighteenKey
            or KeyMode.TwentyKey =>
                dualStageKeys((int)keyMode / 2),
        _ => throw new ArgumentOutOfRangeException(
            nameof(keyMode),
            keyMode,
            "Mania supports between 1 and 10 keys."),
    };

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
        for (int i = left.Count - columns / 2;
             i < left.Count;
             i++)
        {
            yield return left[i];
        }
        if (columns % 2 == 1)
            yield return special;
        for (int i = 0; i < columns / 2; i++)
            yield return right[i];
    }

    private static int validateKeyMode(KeyMode keyMode)
    {
        int count = (int)keyMode;
        if (count is < 1 or > 20 || !Enum.IsDefined(keyMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(keyMode),
                keyMode,
                "Mania supports between 1 and 10 keys.");
        }

        return count;
    }
}
