using System;
using System.Collections.Generic;
using System.Linq;
using osuTK.Input;
using Yokko.Core.Gameplay;

namespace Yokko.Game.Gameplay;

internal static class GameplayKeyProfileCodec
{
    private const string currentPrefix = "YOKKO-KEYS-V2";
    private const string legacyPrefix = "YOKKO-KEYS-V1";

    public static string Encode(YokkoGameplaySettings settings) =>
        string.Join(
            "|",
            new[] { currentPrefix }.Concat(
                settings.SupportedKeyModes.Select(mode =>
                    $"{(int)mode}K={encodeKeys(settings.GetKeys(mode))}")));

    public static void DecodeAndApply(
        string text,
        YokkoGameplaySettings settings)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException("The key profile is empty.");

        string[] parts = text.Trim().Split('|');
        if (parts[0].Equals(legacyPrefix, StringComparison.Ordinal))
        {
            decodeLegacy(parts, settings);
            return;
        }

        if (!parts[0].Equals(currentPrefix, StringComparison.Ordinal)
            || parts.Length != settings.SupportedKeyModes.Count + 1)
        {
            throw new FormatException("The key profile version is unsupported.");
        }

        var decoded = new Dictionary<KeyMode, IReadOnlyList<Key>>();
        for (int index = 0; index < settings.SupportedKeyModes.Count; index++)
        {
            KeyMode mode = settings.SupportedKeyModes[index];
            decoded[mode] = decodePart(
                parts[index + 1],
                $"{(int)mode}K",
                (int)mode);
        }

        foreach ((KeyMode mode, IReadOnlyList<Key> keys) in decoded)
            settings.SetBindings(mode, keys);
    }

    private static string encodeKeys(IReadOnlyList<Key> keys) =>
        string.Join(",", keys.Select(key => key.ToString()));

    private static IReadOnlyList<Key> decodePart(
        string part,
        string name,
        int expectedCount)
    {
        string marker = $"{name}=";
        if (!part.StartsWith(marker, StringComparison.Ordinal))
            throw new FormatException($"The {name} key profile is missing.");

        string[] values = part[marker.Length..].Split(',');
        if (values.Length != expectedCount)
            throw new FormatException($"{name} requires {expectedCount} keys.");

        var keys = new List<Key>(expectedCount);
        foreach (string value in values)
        {
            if (!Enum.TryParse(value, true, out Key key)
                || key == Key.Unknown
                || key == Key.Escape)
            {
                throw new FormatException($"Invalid gameplay key: {value}.");
            }

            keys.Add(key);
        }

        if (keys.Distinct().Count() != keys.Count)
            throw new FormatException($"{name} contains duplicate keys.");

        return keys;
    }

    private static void decodeLegacy(
        IReadOnlyList<string> parts,
        YokkoGameplaySettings settings)
    {
        if (parts.Count != 3)
            throw new FormatException("The legacy key profile is invalid.");

        IReadOnlyList<Key> fourKeys = decodePart(parts[1], "4K", 4);
        IReadOnlyList<Key> sevenKeys = decodePart(parts[2], "7K", 7);

        settings.SetBindings(KeyMode.FourKey, fourKeys);
        settings.SetBindings(KeyMode.SevenKey, sevenKeys);
    }
}
