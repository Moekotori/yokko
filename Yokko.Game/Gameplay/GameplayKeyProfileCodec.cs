using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.Bindings;
using osuTK.Input;
using Yokko.Core.Gameplay;

namespace Yokko.Game.Gameplay;

internal static class GameplayKeyProfileCodec
{
    private const string currentPrefix = "YOKKO-KEYS-V5";
    private const string versionFourPrefix = "YOKKO-KEYS-V4";
    private const string versionThreePrefix = "YOKKO-KEYS-V3";
    private const string versionTwoPrefix = "YOKKO-KEYS-V2";
    private const string legacyPrefix = "YOKKO-KEYS-V1";

    public static string Encode(YokkoGameplaySettings settings)
    {
        IEnumerable<string> laneProfiles =
            settings.SupportedKeyModes.Select(mode =>
                $"{(int)mode}K={encodeInputKeys(settings.GetInputKeys(mode))}");
        string shortcuts = "SHORTCUTS=" + string.Join(
            ",",
            settings.SupportedShortcutActions.Select(action =>
                $"{action}:{settings.GetShortcutBinding(action)}"));
        return string.Join(
            "|",
            new[] { currentPrefix }.Concat(laneProfiles)
                                   .Append($"BMS={encodeInputKeys(settings.GetBmsInputKeys())}")
                                   .Append(shortcuts));
    }

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

        if (parts[0].Equals(versionTwoPrefix, StringComparison.Ordinal))
        {
            decodeLaneProfiles(parts, settings, 1);
            return;
        }

        if (parts[0].Equals(versionThreePrefix, StringComparison.Ordinal))
        {
            if (parts.Length != settings.SupportedKeyModes.Count + 2)
                throw new FormatException("The key profile is incomplete.");

            Dictionary<KeyMode, IReadOnlyList<Key>> legacyProfiles =
                decodeLaneProfiles(parts, settings, 1, apply: false);
            IReadOnlyDictionary<ManiaShortcutAction, Key> legacyShortcuts =
                decodeShortcuts(parts[^1], settings);

            foreach ((KeyMode mode, IReadOnlyList<Key> keys) in legacyProfiles)
                settings.SetBindings(mode, keys);
            foreach ((ManiaShortcutAction action, Key key) in legacyShortcuts)
                settings.SetShortcutBinding(action, key);
            return;
        }

        if (parts[0].Equals(versionFourPrefix, StringComparison.Ordinal))
        {
            if (parts.Length != settings.SupportedKeyModes.Count + 2)
                throw new FormatException("The input profile is incomplete.");

            Dictionary<KeyMode, IReadOnlyList<InputKey>> versionFourProfiles =
                decodeInputLaneProfiles(parts, settings, 1);
            IReadOnlyDictionary<ManiaShortcutAction, Key> versionFourShortcuts =
                decodeShortcuts(parts[^1], settings);
            foreach ((KeyMode mode, IReadOnlyList<InputKey> keys) in versionFourProfiles)
                settings.SetInputBindings(mode, keys);
            foreach ((ManiaShortcutAction action, Key key) in versionFourShortcuts)
                settings.SetShortcutBinding(action, key);
            return;
        }

        if (!parts[0].Equals(currentPrefix, StringComparison.Ordinal)
            || parts.Length != settings.SupportedKeyModes.Count + 3)
        {
            throw new FormatException("The key profile version is unsupported.");
        }

        Dictionary<KeyMode, IReadOnlyList<InputKey>> decoded =
            decodeInputLaneProfiles(parts, settings, 1);
        IReadOnlyList<InputKey> bms = decodeInputPart(
            parts[settings.SupportedKeyModes.Count + 1],
            "BMS",
            8);
        IReadOnlyDictionary<ManiaShortcutAction, Key> shortcuts =
            decodeShortcuts(parts[^1], settings);

        foreach ((KeyMode mode, IReadOnlyList<InputKey> keys) in decoded)
            settings.SetInputBindings(mode, keys);
        settings.SetBmsInputBindings(bms);
        foreach ((ManiaShortcutAction action, Key key) in shortcuts)
            settings.SetShortcutBinding(action, key);
    }

    private static Dictionary<KeyMode, IReadOnlyList<Key>>
        decodeLaneProfiles(
            IReadOnlyList<string> parts,
            YokkoGameplaySettings settings,
            int offset,
            bool apply = true)
    {
        if (parts.Count < settings.SupportedKeyModes.Count + offset)
            throw new FormatException("The key profile is incomplete.");

        var decoded = new Dictionary<KeyMode, IReadOnlyList<Key>>();
        for (int index = 0; index < settings.SupportedKeyModes.Count; index++)
        {
            KeyMode mode = settings.SupportedKeyModes[index];
            decoded[mode] = decodePart(
                parts[index + offset],
                $"{(int)mode}K",
                (int)mode);
        }

        if (apply)
        {
            foreach ((KeyMode mode, IReadOnlyList<Key> keys) in decoded)
                settings.SetBindings(mode, keys);
        }

        return decoded;
    }

    private static string encodeKeys(IReadOnlyList<Key> keys) =>
        string.Join(",", keys.Select(key => key.ToString()));

    private static string encodeInputKeys(IReadOnlyList<InputKey> keys) =>
        string.Join(",", keys.Select(key => key.ToString()));

    private static Dictionary<KeyMode, IReadOnlyList<InputKey>>
        decodeInputLaneProfiles(
            IReadOnlyList<string> parts,
            YokkoGameplaySettings settings,
            int offset)
    {
        if (parts.Count < settings.SupportedKeyModes.Count + offset)
            throw new FormatException("The input profile is incomplete.");

        var decoded = new Dictionary<KeyMode, IReadOnlyList<InputKey>>();
        for (int index = 0; index < settings.SupportedKeyModes.Count; index++)
        {
            KeyMode mode = settings.SupportedKeyModes[index];
            decoded[mode] = decodeInputPart(
                parts[index + offset],
                $"{(int)mode}K",
                (int)mode);
        }

        return decoded;
    }

    private static IReadOnlyList<InputKey> decodeInputPart(
        string part,
        string name,
        int expectedCount)
    {
        string marker = $"{name}=";
        if (!part.StartsWith(marker, StringComparison.Ordinal))
            throw new FormatException($"The {name} input profile is missing.");

        string[] values = part[marker.Length..].Split(',');
        if (values.Length != expectedCount)
            throw new FormatException($"{name} requires {expectedCount} inputs.");

        var keys = new List<InputKey>(expectedCount);
        foreach (string value in values)
        {
            if (!Enum.TryParse(value, true, out InputKey key)
                || !YokkoGameplaySettings.IsSupportedInputKey(key))
            {
                throw new FormatException($"Invalid gameplay input: {value}.");
            }

            keys.Add(key);
        }

        if (keys.Distinct().Count() != keys.Count)
            throw new FormatException($"{name} contains duplicate inputs.");

        return keys;
    }

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

    private static IReadOnlyDictionary<ManiaShortcutAction, Key>
        decodeShortcuts(
            string part,
            YokkoGameplaySettings settings)
    {
        const string marker = "SHORTCUTS=";
        if (!part.StartsWith(marker, StringComparison.Ordinal))
            throw new FormatException("The Mania shortcut profile is missing.");

        var decoded = new Dictionary<ManiaShortcutAction, Key>();
        foreach (string entry in part[marker.Length..].Split(','))
        {
            string[] pair = entry.Split(':');
            if (pair.Length != 2
                || !Enum.TryParse(
                    pair[0],
                    true,
                    out ManiaShortcutAction action)
                || !Enum.TryParse(pair[1], true, out Key key)
                || key == Key.Unknown
                || !decoded.TryAdd(action, key))
            {
                throw new FormatException(
                    $"Invalid Mania shortcut entry: {entry}.");
            }
        }

        ManiaShortcutAction[] missingActions =
            settings.SupportedShortcutActions
                .Where(action => !decoded.ContainsKey(action))
                .ToArray();
        bool isLegacyLayoutEditorProfile =
            missingActions.Length == 1
            && missingActions[0]
                == ManiaShortcutAction.ToggleLayoutEditorUi;
        if (decoded.Count > settings.SupportedShortcutActions.Count
            || (!isLegacyLayoutEditorProfile
                && missingActions.Length > 0))
        {
            throw new FormatException(
                "The Mania shortcut profile is incomplete.");
        }

        if (isLegacyLayoutEditorProfile)
        {
            decoded[ManiaShortcutAction.ToggleLayoutEditorUi] =
                settings.GetDefaultShortcutBinding(
                    ManiaShortcutAction.ToggleLayoutEditorUi);
        }

        return decoded;
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
