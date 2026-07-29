using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osuTK.Input;
using Yokko.Core.Gameplay;

namespace Yokko.Game.Gameplay;

/// <summary>
/// Application-owned gameplay preferences. Chart rules and scoring windows stay
/// in Yokko.Core; this type only owns player input and presentation choices.
/// </summary>
public sealed class YokkoGameplaySettings
{
    private static readonly Key[] defaultFourKeyBindings =
    {
        Key.D,
        Key.F,
        Key.J,
        Key.K,
    };

    private static readonly Key[] defaultSevenKeyBindings =
    {
        Key.S,
        Key.D,
        Key.F,
        Key.Space,
        Key.J,
        Key.K,
        Key.L,
    };

    private readonly Bindable<Key>[] fourKeyBindings =
        createBindings(defaultFourKeyBindings);

    private readonly Bindable<Key>[] sevenKeyBindings =
        createBindings(defaultSevenKeyBindings);

    public IReadOnlyList<Bindable<Key>> FourKeyBindings => fourKeyBindings;

    public IReadOnlyList<Bindable<Key>> SevenKeyBindings => sevenKeyBindings;

    public readonly Bindable<double> ScrollSpeed =
        new(OsuManiaScrollSpeed.Default);

    public readonly BindableBool ShowLanePressFeedback = new(true);

    public readonly BindableBool KeysoundsEnabled = new(true);

    public IReadOnlyList<Bindable<Key>> GetBindableKeys(KeyMode keyMode) =>
        keyMode switch
        {
            KeyMode.FourKey => fourKeyBindings,
            KeyMode.SevenKey => sevenKeyBindings,
            _ => throw new ArgumentOutOfRangeException(
                nameof(keyMode),
                keyMode,
                "Unsupported key mode."),
        };

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
        Key[] defaults = keyMode switch
        {
            KeyMode.FourKey => defaultFourKeyBindings,
            KeyMode.SevenKey => defaultSevenKeyBindings,
            _ => throw new ArgumentOutOfRangeException(
                nameof(keyMode),
                keyMode,
                "Unsupported key mode."),
        };
        IReadOnlyList<Bindable<Key>> bindings = GetBindableKeys(keyMode);

        for (int index = 0; index < defaults.Length; index++)
            bindings[index].Value = defaults[index];
    }

    public void SetScrollSpeed(double speed) =>
        ScrollSpeed.Value = OsuManiaScrollSpeed.Clamp(speed);

    public void AdjustScrollSpeed(double amount) =>
        ScrollSpeed.Value = OsuManiaScrollSpeed.Adjust(
            ScrollSpeed.Value,
            amount);

    private static Bindable<Key>[] createBindings(IEnumerable<Key> defaults) =>
        defaults.Select(key => new Bindable<Key>(key)).ToArray();
}
