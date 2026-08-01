using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.Bindings;
using osuTK.Input;
using Yokko.Core.Gameplay;
using Yokko.Game.Gameplay;

namespace Yokko.Game.Screens.Gameplay;

public sealed class KeyModeBindings
{
    private readonly InputKey[] keys;
    private readonly Dictionary<InputKey, int> lanesByKey;

    private KeyModeBindings(KeyMode keyMode, InputKey[] keys)
    {
        KeyMode = keyMode;
        this.keys = keys;
        lanesByKey = new Dictionary<InputKey, int>(keys.Length);

        for (int i = 0; i < keys.Length; i++)
            lanesByKey[keys[i]] = i;
    }

    public KeyMode KeyMode { get; }

    public int KeyCount => keys.Length;

    public static KeyModeBindings ForMode(KeyMode keyMode) =>
        new(
            keyMode,
            OsuManiaKeyLayout.GetDefaultKeys(keyMode)
                              .Select(KeyCombination.FromKey)
                              .ToArray());

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
            OsuManiaKeyLayout.GetDefaultKeys(keyMode, stageCount)
                              .Select(KeyCombination.FromKey)
                              .ToArray());
    }

    public static KeyModeBindings ForMode(
        KeyMode keyMode,
        IReadOnlyList<InputKey> configuredKeys)
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

    public static KeyModeBindings ForMode(
        KeyMode keyMode,
        IReadOnlyList<Key> configuredKeys) =>
        ForMode(
            keyMode,
            configuredKeys?.Select(KeyCombination.FromKey).ToArray());

    public int GetLane(Key key) => GetLane(KeyCombination.FromKey(key));

    public int GetLane(InputKey key) =>
        lanesByKey.TryGetValue(key, out int lane) ? lane : -1;

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

    public static string FormatKey(InputKey key)
    {
        string name = key.ToString();
        if (name.StartsWith("Midi", StringComparison.Ordinal))
            return "MIDI " + name[4..].Replace("Sharp", "#", StringComparison.Ordinal);
        if (name.StartsWith("Joystick", StringComparison.Ordinal))
            return "HID " + name[8..];

        return FormatKey((Key)(int)key);
    }

    private static int validateKeyMode(KeyMode keyMode)
    {
        if (!OsuManiaKeyLayout.SupportedModes.Contains(keyMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(keyMode),
                keyMode,
                "Unsupported osu!mania key mode.");
        }

        return (int)keyMode;
    }
}
