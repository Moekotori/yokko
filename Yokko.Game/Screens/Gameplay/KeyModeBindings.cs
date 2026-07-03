using System;
using System.Collections.Generic;
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

    public static KeyModeBindings ForMode(KeyMode keyMode) => keyMode switch
    {
        KeyMode.FourKey => new KeyModeBindings(keyMode, [Key.D, Key.F, Key.J, Key.K]),
        KeyMode.SevenKey => new KeyModeBindings(keyMode, [Key.S, Key.D, Key.F, Key.Space, Key.J, Key.K, Key.L]),
        _ => throw new ArgumentOutOfRangeException(nameof(keyMode), keyMode, "Unsupported key mode."),
    };

    public int GetLane(Key key) => lanesByKey.TryGetValue(key, out int lane) ? lane : -1;

    public string GetDisplayKey(int lane)
    {
        Key key = keys[lane];
        return key == Key.Space ? "Space" : key.ToString();
    }
}
