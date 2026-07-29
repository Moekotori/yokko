using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osuTK.Input;
using Yokko.Core.Gameplay;

namespace Yokko.Game.Gameplay;

public enum GameplayKeyPreset
{
    Standard,
    LeftHanded,
    Split,
}

public enum ManiaShortcutAction
{
    DecreaseScrollSpeed,
    IncreaseScrollSpeed,
}

/// <summary>
/// Application-owned gameplay preferences. Chart rules and scoring windows stay
/// in Yokko.Core; this type only owns player input and presentation choices.
/// </summary>
public sealed class YokkoGameplaySettings
{
    private static readonly Key[] defaultFourKeyBindings =
        OsuManiaKeyLayout.GetDefaultKeys(KeyMode.FourKey);

    private static readonly Key[] defaultSevenKeyBindings =
        OsuManiaKeyLayout.GetDefaultKeys(KeyMode.SevenKey);

    private static readonly IReadOnlyDictionary<
        (KeyMode Mode, GameplayKeyPreset Preset),
        Key[]> bindingPresets =
        new Dictionary<(KeyMode, GameplayKeyPreset), Key[]>
        {
            [(KeyMode.FourKey, GameplayKeyPreset.Standard)] =
                defaultFourKeyBindings,
            [(KeyMode.FourKey, GameplayKeyPreset.LeftHanded)] =
                new[] { Key.A, Key.S, Key.D, Key.F },
            [(KeyMode.FourKey, GameplayKeyPreset.Split)] =
                new[] { Key.Z, Key.X, Key.Period, Key.Slash },
            [(KeyMode.SevenKey, GameplayKeyPreset.Standard)] =
                defaultSevenKeyBindings,
            [(KeyMode.SevenKey, GameplayKeyPreset.LeftHanded)] =
                new[] { Key.A, Key.S, Key.D, Key.F, Key.G, Key.H, Key.J },
            [(KeyMode.SevenKey, GameplayKeyPreset.Split)] =
                new[]
                {
                    Key.Z,
                    Key.X,
                    Key.C,
                    Key.Space,
                    Key.Comma,
                    Key.Period,
                    Key.Slash,
                },
        };

    private readonly Dictionary<KeyMode, Bindable<Key>[]> bindingsByMode;

    public IReadOnlyList<KeyMode> SupportedKeyModes =>
        OsuManiaKeyLayout.SupportedModes;

    public IReadOnlyList<Bindable<Key>> FourKeyBindings =>
        bindingsByMode[KeyMode.FourKey];

    public IReadOnlyList<Bindable<Key>> SevenKeyBindings =>
        bindingsByMode[KeyMode.SevenKey];

    internal event Action BindingsChanged;

    public readonly Bindable<double> ScrollSpeed =
        new(OsuManiaScrollSpeed.Default);

    /// <summary>
    /// Matches Quaver's optional playback-rate scroll normalization.
    /// Zero keeps the same real-time approach duration while 100% makes
    /// rate mods scale the visual approach together with chart time.
    /// </summary>
    public readonly Bindable<double> QuaverScrollRateNormalization =
        new(0);

    public readonly BindableBool ShowLanePressFeedback = new(true);

    public readonly BindableBool KeysoundsEnabled = new(true);

    public readonly BindableBool PauseWhenUnfocused = new(true);

    public readonly Bindable<Key> DecreaseScrollSpeedKey = new(Key.F3);

    public readonly Bindable<Key> IncreaseScrollSpeedKey = new(Key.F4);

    public YokkoGameplaySettings()
    {
        bindingsByMode = OsuManiaKeyLayout.SupportedModes.ToDictionary(
            mode => mode,
            mode => createBindings(OsuManiaKeyLayout.GetDefaultKeys(mode)));

        foreach (Bindable<Key> binding in bindingsByMode.Values.SelectMany(
                     modeBindings => modeBindings))
        {
            binding.ValueChanged += _ => BindingsChanged?.Invoke();
        }
    }

    public IReadOnlyList<Bindable<Key>> GetBindableKeys(KeyMode keyMode) =>
        bindingsByMode.TryGetValue(keyMode, out Bindable<Key>[] bindings)
            ? bindings
            : throw new ArgumentOutOfRangeException(
                nameof(keyMode),
                keyMode,
                "Unsupported key mode.");

    public IReadOnlyList<Key> GetKeys(KeyMode keyMode) =>
        GetBindableKeys(keyMode)
            .Select(binding => binding.Value)
            .ToArray();

    /// <summary>
    /// Assigns a key without allowing duplicate lanes. When the requested key is
    /// already in the active profile, both lanes are swapped so the operation is
    /// immediate and never leaves the profile unusable.
    /// </summary>
    public void SetBinding(KeyMode keyMode, int lane, Key key)
    {
        if (key == Key.Escape)
            throw new ArgumentException("Escape is reserved for navigation.", nameof(key));

        IReadOnlyList<Bindable<Key>> bindings = GetBindableKeys(keyMode);

        if ((uint)lane >= bindings.Count)
            throw new ArgumentOutOfRangeException(nameof(lane));

        int duplicateLane = -1;

        for (int index = 0; index < bindings.Count; index++)
        {
            if (index != lane && bindings[index].Value == key)
            {
                duplicateLane = index;
                break;
            }
        }

        Key previous = bindings[lane].Value;
        bindings[lane].Value = key;

        if (duplicateLane >= 0)
            bindings[duplicateLane].Value = previous;
    }

    /// <summary>
    /// Replaces a complete key profile as one validated operation. This is used
    /// by sequential capture so cancelling halfway never leaves a partial map.
    /// </summary>
    public void SetBindings(KeyMode keyMode, IReadOnlyList<Key> keys)
    {
        IReadOnlyList<Bindable<Key>> bindings = GetBindableKeys(keyMode);

        if (keys == null || keys.Count != bindings.Count)
            throw new ArgumentException(
                $"{keyMode} requires exactly {bindings.Count} keys.",
                nameof(keys));

        if (keys.Any(key => key == Key.Escape))
            throw new ArgumentException(
                "Escape is reserved for navigation.",
                nameof(keys));

        if (keys.Distinct().Count() != keys.Count)
            throw new ArgumentException(
                "A gameplay key profile cannot contain duplicate keys.",
                nameof(keys));

        for (int index = 0; index < bindings.Count; index++)
            bindings[index].Value = keys[index];
    }

    public void ResetBindings(KeyMode keyMode)
    {
        Key[] defaults = OsuManiaKeyLayout.GetDefaultKeys(keyMode);
        IReadOnlyList<Bindable<Key>> bindings = GetBindableKeys(keyMode);

        for (int index = 0; index < defaults.Length; index++)
            bindings[index].Value = defaults[index];
    }

    public Key GetShortcutBinding(ManiaShortcutAction action) =>
        action switch
        {
            ManiaShortcutAction.DecreaseScrollSpeed =>
                DecreaseScrollSpeedKey.Value,
            ManiaShortcutAction.IncreaseScrollSpeed =>
                IncreaseScrollSpeedKey.Value,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

    /// <summary>
    /// Changes a Mania action key while keeping the two actions unique. A
    /// duplicate assignment swaps the actions instead of silently unbinding one.
    /// </summary>
    public void SetShortcutBinding(ManiaShortcutAction action, Key key)
    {
        if (key is Key.Escape or Key.Unknown)
        {
            throw new ArgumentException(
                "Escape and unknown keys cannot be Mania shortcuts.",
                nameof(key));
        }

        Bindable<Key> target = shortcutBindable(action);
        Bindable<Key> other = shortcutBindable(
            action == ManiaShortcutAction.DecreaseScrollSpeed
                ? ManiaShortcutAction.IncreaseScrollSpeed
                : ManiaShortcutAction.DecreaseScrollSpeed);
        Key previous = target.Value;
        target.Value = key;

        if (other.Value == key)
            other.Value = previous;
    }

    public void ResetShortcutBindings()
    {
        DecreaseScrollSpeedKey.Value = Key.F3;
        IncreaseScrollSpeedKey.Value = Key.F4;
    }

    public void ApplyBindingPreset(
        KeyMode keyMode,
        GameplayKeyPreset preset)
    {
        if (!bindingPresets.TryGetValue((keyMode, preset), out Key[] keys))
        {
            throw new ArgumentOutOfRangeException(
                nameof(preset),
                preset,
                $"No {preset} preset exists for {keyMode}.");
        }

        SetBindings(keyMode, keys);
    }

    /// <summary>
    /// Copies the four central gameplay columns between 4K and 7K. Expanding to
    /// 7K preserves the current outer and centre keys wherever possible.
    /// </summary>
    public void CopyBindingsToOtherMode(KeyMode sourceMode)
    {
        switch (sourceMode)
        {
            case KeyMode.FourKey:
                copyFourKeyToSevenKey();
                break;

            case KeyMode.SevenKey:
                SetBindings(
                    KeyMode.FourKey,
                    new[]
                    {
                        SevenKeyBindings[1].Value,
                        SevenKeyBindings[2].Value,
                        SevenKeyBindings[4].Value,
                        SevenKeyBindings[5].Value,
                    });
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(sourceMode),
                    sourceMode,
                    "Only 4K and 7K profiles can be copied.");
        }
    }

    public void SetScrollSpeed(double speed) =>
        ScrollSpeed.Value = OsuManiaScrollSpeed.Clamp(speed);

    public void AdjustScrollSpeed(double amount) =>
        ScrollSpeed.Value = OsuManiaScrollSpeed.Adjust(
            ScrollSpeed.Value,
            amount);

    private static Bindable<Key>[] createBindings(IEnumerable<Key> defaults) =>
        defaults.Select(key => new Bindable<Key>(key)).ToArray();

    private Bindable<Key> shortcutBindable(ManiaShortcutAction action) =>
        action switch
        {
            ManiaShortcutAction.DecreaseScrollSpeed =>
                DecreaseScrollSpeedKey,
            ManiaShortcutAction.IncreaseScrollSpeed =>
                IncreaseScrollSpeedKey,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

    private void copyFourKeyToSevenKey()
    {
        Key[] fourKeys = GetKeys(KeyMode.FourKey).ToArray();
        var used = new HashSet<Key>(fourKeys);
        Key[] fallback =
        {
            SevenKeyBindings[0].Value,
            SevenKeyBindings[3].Value,
            SevenKeyBindings[6].Value,
            Key.S,
            Key.Space,
            Key.L,
            Key.A,
            Key.Semicolon,
            Key.G,
            Key.H,
        };
        var extras = fallback.Where(used.Add).Take(3).ToArray();

        if (extras.Length != 3)
        {
            throw new InvalidOperationException(
                "Could not create a unique 7K profile from the 4K keys.");
        }

        SetBindings(
            KeyMode.SevenKey,
            new[]
            {
                extras[0],
                fourKeys[0],
                fourKeys[1],
                extras[1],
                fourKeys[2],
                fourKeys[3],
                extras[2],
            });
    }
}
