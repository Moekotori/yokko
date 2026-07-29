using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using osuTK.Graphics;

namespace Yokko.Game.Skinning.OsuMania;

internal static class OsuManiaSkinIniDecoder
{
    public static OsuManiaSkinInfo Decode(string contents)
    {
        string name = "Unknown";
        string author = string.Empty;
        string version = "1.0";
        string comboPrefix = "score";
        int comboOverlap = 0;
        string section = string.Empty;
        Dictionary<string, string> maniaValues = null;
        var configurations = new Dictionary<int, OsuManiaSkinConfiguration>();

        using var reader = new StringReader(contents ?? string.Empty);

        while (reader.ReadLine() is string rawLine)
        {
            string line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal) ||
                line.StartsWith(";", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
                commitManiaSection(maniaValues, configurations);
                section = line[1..^1].Trim();
                maniaValues = section.Equals("Mania", StringComparison.OrdinalIgnoreCase)
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : null;
                continue;
            }

            int separator = line.IndexOf(':');

            if (separator < 0)
                continue;

            string key = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();

            if (section.Equals("General", StringComparison.OrdinalIgnoreCase))
            {
                if (key.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    name = value;
                else if (key.Equals("Author", StringComparison.OrdinalIgnoreCase))
                    author = value;
                else if (key.Equals("Version", StringComparison.OrdinalIgnoreCase))
                    version = value;
            }
            else if (section.Equals("Fonts", StringComparison.OrdinalIgnoreCase))
            {
                if (key.Equals("ComboPrefix", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(value))
                    comboPrefix = value;
                else if (key.Equals("ComboOverlap", StringComparison.OrdinalIgnoreCase)
                         && int.TryParse(
                             value,
                             NumberStyles.Integer,
                             CultureInfo.InvariantCulture,
                             out int parsedOverlap))
                    comboOverlap = parsedOverlap;
            }
            else if (maniaValues != null)
                maniaValues[key] = value;
        }

        commitManiaSection(maniaValues, configurations);
        return new OsuManiaSkinInfo(
            name,
            author,
            version,
            comboPrefix,
            comboOverlap,
            configurations);
    }

    private static void commitManiaSection(
        IReadOnlyDictionary<string, string> values,
        IDictionary<int, OsuManiaSkinConfiguration> configurations)
    {
        if (values == null || !tryInt(values, "Keys", out int keys) || keys <= 0 || keys > 18)
            return;

        OsuManiaSkinConfiguration defaults = OsuManiaSkinConfiguration.CreateDefault(keys);
        float[] widths = floatList(values, "ColumnWidth", defaults.ColumnWidths, keys);
        float[] spacings = floatList(values, "ColumnSpacing", defaults.ColumnSpacings, keys);
        float[] lineWidths = floatList(values, "ColumnLineWidth", defaults.ColumnLineWidths, keys);
        var laneColours = new Color4[keys];
        var laneLightColours = new Color4[keys];
        var keyImages = new string[keys];
        var pressedKeyImages = new string[keys];
        var noteImages = new string[keys];
        var holdHeadImages = new string[keys];
        var holdBodyImages = new string[keys];
        var holdTailImages = new string[keys];
        var noteBodyStyles = new int[keys];
        var keyFlips = new bool[keys];
        var pressedKeyFlips = new bool[keys];
        var noteFlips = new bool[keys];
        var holdHeadFlips = new bool[keys];
        var holdBodyFlips = new bool[keys];
        var holdTailFlips = new bool[keys];
        int defaultBodyStyle = Math.Clamp(integer(values, "NoteBodyStyle", 1), 0, 4);
        bool defaultKeyFlip = boolean(values, "KeyFlipWhenUpsideDown", true);
        bool defaultNoteFlip = boolean(values, "NoteFlipWhenUpsideDown", true);

        for (int lane = 0; lane < keys; lane++)
        {
            laneColours[lane] = colour(values, $"Colour{lane + 1}", defaults.LaneColours[lane]);
            laneLightColours[lane] = colour(
                values,
                $"ColourLight{lane + 1}",
                defaults.LaneLightColours[lane]);
            keyImages[lane] = text(values, $"KeyImage{lane}", defaults.KeyImages[lane]);
            pressedKeyImages[lane] = text(values, $"KeyImage{lane}D", defaults.PressedKeyImages[lane]);
            noteImages[lane] = text(values, $"NoteImage{lane}", defaults.NoteImages[lane]);
            holdHeadImages[lane] = text(values, $"NoteImage{lane}H", defaults.HoldHeadImages[lane]);
            holdBodyImages[lane] = text(values, $"NoteImage{lane}L", defaults.HoldBodyImages[lane]);
            holdTailImages[lane] = text(values, $"NoteImage{lane}T", holdHeadImages[lane]);
            noteBodyStyles[lane] = Math.Clamp(integer(values, $"NoteBodyStyle{lane}", defaultBodyStyle), 0, 4);
            keyFlips[lane] = boolean(values, $"KeyFlipWhenUpsideDown{lane}", defaultKeyFlip);
            pressedKeyFlips[lane] = boolean(values, $"KeyFlipWhenUpsideDown{lane}D", keyFlips[lane]);
            noteFlips[lane] = boolean(values, $"NoteFlipWhenUpsideDown{lane}", defaultNoteFlip);
            holdHeadFlips[lane] = boolean(values, $"NoteFlipWhenUpsideDown{lane}H", noteFlips[lane]);
            holdBodyFlips[lane] = boolean(values, $"NoteFlipWhenUpsideDown{lane}L", noteFlips[lane]);
            holdTailFlips[lane] = boolean(values, $"NoteFlipWhenUpsideDown{lane}T", noteFlips[lane]);
        }

        configurations[keys] = new OsuManiaSkinConfiguration(
            keys,
            widths,
            spacings,
            lineWidths,
            number(values, "HitPosition", defaults.HitPosition),
            number(values, "ScorePosition", defaults.ScorePosition),
            number(values, "ComboPosition", defaults.ComboPosition),
            boolean(values, "UpsideDown", defaults.UpsideDown),
            boolean(values, "KeysUnderNotes", defaults.KeysUnderNotes),
            defaultBodyStyle,
            laneColours,
            colour(values, "ColourColumnLine", defaults.ColumnLineColour),
            keyImages,
            pressedKeyImages,
            noteImages,
            holdHeadImages,
            holdBodyImages,
            holdTailImages,
            text(values, "StageHint", defaults.StageHint),
            text(values, "Hit0", defaults.Hit0),
            text(values, "Hit50", defaults.Hit50),
            text(values, "Hit100", defaults.Hit100),
            text(values, "Hit200", defaults.Hit200),
            text(values, "Hit300", defaults.Hit300),
            text(values, "Hit300g", defaults.Hit300g))
        {
            NoteBodyStyles = noteBodyStyles,
            WidthForNoteHeightScale = number(values, "WidthForNoteHeightScale", widths.Min()),
            KeyFlipWhenUpsideDown = keyFlips,
            PressedKeyFlipWhenUpsideDown = pressedKeyFlips,
            NoteFlipWhenUpsideDown = noteFlips,
            HoldHeadFlipWhenUpsideDown = holdHeadFlips,
            HoldBodyFlipWhenUpsideDown = holdBodyFlips,
            HoldTailFlipWhenUpsideDown = holdTailFlips,
            LightImage = text(values, "StageLight", defaults.LightImage),
            ExplosionImage = text(
                values,
                "LightingN",
                defaults.ExplosionImage),
            LightPosition = number(
                values,
                "LightPosition",
                defaults.LightPosition),
            LightFramePerSecond = Math.Clamp(
                integer(values, "LightFramePerSecond", defaults.LightFramePerSecond),
                1,
                1000),
            ExplosionWidth = Math.Max(
                0,
                number(values, "LightingNWidth", defaults.ExplosionWidth)),
            LaneLightColours = laneLightColours,
        };
    }

    private static string text(IReadOnlyDictionary<string, string> values, string key, string fallback) =>
        values.TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;

    private static bool boolean(IReadOnlyDictionary<string, string> values, string key, bool fallback) =>
        tryInt(values, key, out int value) ? value == 1 : fallback;

    private static int integer(IReadOnlyDictionary<string, string> values, string key, int fallback) =>
        tryInt(values, key, out int value) ? value : fallback;

    private static float number(IReadOnlyDictionary<string, string> values, string key, float fallback) =>
        values.TryGetValue(key, out string value) &&
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
            ? parsed
            : fallback;

    private static float[] floatList(
        IReadOnlyDictionary<string, string> values,
        string key,
        IReadOnlyList<float> defaults,
        int count)
    {
        var result = new float[count];

        for (int i = 0; i < count; i++)
            result[i] = defaults[i];

        if (!values.TryGetValue(key, out string raw))
            return result;

        string[] parts = raw.Split(',');

        for (int i = 0; i < Math.Min(count, parts.Length); i++)
        {
            if (float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                result[i] = parsed;
        }

        return result;
    }

    private static Color4 colour(
        IReadOnlyDictionary<string, string> values,
        string key,
        Color4 fallback)
    {
        if (!values.TryGetValue(key, out string raw))
            return fallback;

        string[] parts = raw.Split(',');

        if (parts.Length < 3 ||
            !byte.TryParse(parts[0].Trim(), out byte red) ||
            !byte.TryParse(parts[1].Trim(), out byte green) ||
            !byte.TryParse(parts[2].Trim(), out byte blue))
            return fallback;

        byte alpha = 255;

        if (parts.Length >= 4 && byte.TryParse(parts[3].Trim(), out byte parsedAlpha))
            alpha = parsedAlpha;

        return new Color4(red, green, blue, alpha);
    }

    private static bool tryInt(IReadOnlyDictionary<string, string> values, string key, out int result)
    {
        result = 0;
        return values.TryGetValue(key, out string raw) &&
               int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }
}
