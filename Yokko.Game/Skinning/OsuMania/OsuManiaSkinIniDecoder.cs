using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using osuTK.Graphics;

namespace Yokko.Game.Skinning.OsuMania;

internal static class OsuManiaSkinIniDecoder
{
    public static OsuManiaSkinInfo Decode(
        string contents,
        bool skinIniPresent = true,
        bool forceLatestVersion = false)
    {
        string name = "Unknown";
        string author = string.Empty;
        string version = forceLatestVersion
            ? "latest"
            : readVersion(contents, skinIniPresent);
        int animationFrameRate = 0;
        bool layeredHitSounds = true;
        bool comboBurstRandom = false;
        string comboPrefix = "score";
        int comboOverlap = 0;
        string scorePrefix = "score";
        int scoreOverlap = 0;
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
                commitManiaSection(maniaValues, configurations, version);
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
                else if (key.Equals(
                             "AnimationFramerate",
                             StringComparison.OrdinalIgnoreCase)
                         && int.TryParse(
                             value,
                             NumberStyles.Integer,
                             CultureInfo.InvariantCulture,
                             out int parsedFrameRate))
                    animationFrameRate = Math.Max(0, parsedFrameRate);
                else if (key.Equals(
                             "LayeredHitSounds",
                             StringComparison.OrdinalIgnoreCase)
                         && int.TryParse(
                             value,
                             NumberStyles.Integer,
                             CultureInfo.InvariantCulture,
                             out int parsedLayeredHitSounds))
                    layeredHitSounds = parsedLayeredHitSounds == 1;
                else if (key.Equals(
                             "ComboBurstRandom",
                             StringComparison.OrdinalIgnoreCase)
                         && int.TryParse(
                             value,
                             NumberStyles.Integer,
                             CultureInfo.InvariantCulture,
                             out int parsedComboBurstRandom))
                    comboBurstRandom = parsedComboBurstRandom == 1;
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
                else if (key.Equals("ScorePrefix", StringComparison.OrdinalIgnoreCase)
                         && !string.IsNullOrWhiteSpace(value))
                    scorePrefix = value;
                else if (key.Equals("ScoreOverlap", StringComparison.OrdinalIgnoreCase)
                         && int.TryParse(
                             value,
                             NumberStyles.Integer,
                             CultureInfo.InvariantCulture,
                             out int parsedScoreOverlap))
                    scoreOverlap = parsedScoreOverlap;
            }
            else if (maniaValues != null)
                maniaValues[key] = value;
        }

        commitManiaSection(maniaValues, configurations, version);
        return new OsuManiaSkinInfo(
            name,
            author,
            version,
            animationFrameRate,
            layeredHitSounds,
            comboBurstRandom,
            comboPrefix,
            comboOverlap,
            configurations)
        {
            ScorePrefix = scorePrefix,
            ScoreOverlap = scoreOverlap,
        };
    }

    private static string readVersion(
        string contents,
        bool skinIniPresent)
    {
        string version = skinIniPresent ? "1.0" : "latest";
        string section = string.Empty;

        using var reader = new StringReader(contents ?? string.Empty);

        while (reader.ReadLine() is string rawLine)
        {
            string line = rawLine.Trim();

            if (line.Length == 0
                || line.StartsWith("//", StringComparison.Ordinal)
                || line.StartsWith(";", StringComparison.Ordinal)
                || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("[", StringComparison.Ordinal)
                && line.EndsWith("]", StringComparison.Ordinal))
            {
                section = line[1..^1].Trim();
                continue;
            }

            if (!section.Equals(
                    "General",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int separator = line.IndexOf(':');
            if (separator < 0
                || !line[..separator].Trim().Equals(
                    "Version",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string value = line[(separator + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(value))
                version = value;
        }

        return version;
    }

    private static void commitManiaSection(
        IReadOnlyDictionary<string, string> values,
        IDictionary<int, OsuManiaSkinConfiguration> configurations,
        string version)
    {
        if (values == null || !tryInt(values, "Keys", out int keys) || keys <= 0 || keys > 18)
            return;

        bool? splitStages = nullableBoolean(values, "SplitStages");
        int specialStyle = namedInteger(
            values,
            "SpecialStyle",
            0,
            ("None", 0),
            ("Left", 1),
            ("Right", 2));
        OsuManiaSkinConfiguration defaults =
            OsuManiaSkinConfiguration.CreateDefault(
                keys,
                version,
                splitStages,
                specialStyle);
        float[] widths = floatList(values, "ColumnWidth", defaults.ColumnWidths, keys);
        float[] spacings = floatList(values, "ColumnSpacing", defaults.ColumnSpacings, keys);
        float[] lineWidths = floatList(
            values,
            "ColumnLineWidth",
            defaults.ColumnLineWidths,
            keys + 1);
        float[] explosionWidths = floatList(
            values,
            "LightingNWidth",
            defaults.ExplosionWidths,
            keys);
        float[] holdNoteLightWidths = floatList(
            values,
            "LightingLWidth",
            defaults.HoldNoteLightWidths,
            keys);

        for (int lane = 0; lane < keys; lane++)
            widths[lane] = Math.Clamp(widths[lane], 5, 100);

        for (int lane = 0; lane < keys - 1; lane++)
            spacings[lane] = Math.Max(spacings[lane], -widths[lane + 1]);

        for (int edge = 0; edge < lineWidths.Length; edge++)
        {
            if (lineWidths[edge] > 0 && lineWidths[edge] < 2)
                lineWidths[edge] = 2;
        }
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
        int defaultBodyStyle = Math.Clamp(
            noteBodyStyle(
                values,
                "NoteBodyStyle",
                defaults.NoteBodyStyles[0]),
            0,
            4);
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
            noteBodyStyles[lane] = Math.Clamp(
                noteBodyStyle(
                    values,
                    $"NoteBodyStyle{lane}",
                    defaultBodyStyle),
                0,
                4);
            keyFlips[lane] = boolean(values, $"KeyFlipWhenUpsideDown{lane}", defaultKeyFlip);
            pressedKeyFlips[lane] = boolean(values, $"KeyFlipWhenUpsideDown{lane}D", keyFlips[lane]);
            noteFlips[lane] = boolean(values, $"NoteFlipWhenUpsideDown{lane}", defaultNoteFlip);
            holdHeadFlips[lane] = boolean(values, $"NoteFlipWhenUpsideDown{lane}H", noteFlips[lane]);
            holdBodyFlips[lane] = boolean(values, $"NoteFlipWhenUpsideDown{lane}L", noteFlips[lane]);
            holdTailFlips[lane] = boolean(values, $"NoteFlipWhenUpsideDown{lane}T", noteFlips[lane]);
        }

        if (configurations.ContainsKey(keys))
            return;

        configurations.Add(keys, new OsuManiaSkinConfiguration(
            keys,
            widths,
            spacings,
            lineWidths,
            Math.Clamp(
                number(values, "HitPosition", defaults.HitPosition),
                240,
                480),
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
            HoldNoteLightImage = text(
                values,
                "LightingL",
                defaults.HoldNoteLightImage),
            LightPosition = number(
                values,
                "LightPosition",
                defaults.LightPosition),
            LightFramePerSecond = positiveFrameRate(
                integer(values, "LightFramePerSecond", defaults.LightFramePerSecond)),
            ExplosionWidths = explosionWidths,
            HoldNoteLightWidths = holdNoteLightWidths,
            LaneLightColours = laneLightColours,
            BarLineHeight = Math.Max(
                0,
                number(values, "BarlineHeight", defaults.BarLineHeight)),
            ShowJudgementLine = boolean(
                values,
                "JudgementLine",
                defaults.ShowJudgementLine),
            BarLineColour = colour(
                values,
                "ColourBarline",
                defaults.BarLineColour),
            JudgementLineColour = colour(
                values,
                "ColourJudgementLine",
                defaults.JudgementLineColour),
            StageLeft = text(values, "StageLeft", defaults.StageLeft),
            StageRight = text(values, "StageRight", defaults.StageRight),
            StageBottom = text(values, "StageBottom", defaults.StageBottom),
            WarningArrow = text(
                values,
                "WarningArrow",
                defaults.WarningArrow),
            SplitStages = splitStages,
            StageSeparation = Math.Max(
                5,
                number(values, "StageSeparation", defaults.StageSeparation)),
            SeparateScore = boolean(
                values,
                "SeparateScore",
                defaults.SeparateScore),
            SpecialStyle = specialStyle,
            ColumnStart = number(
                values,
                "ColumnStart",
                defaults.ColumnStart),
            ColumnRight = number(
                values,
                "ColumnRight",
                defaults.ColumnRight),
            ComboBurstStyle = namedInteger(
                values,
                "ComboBurstStyle",
                defaults.ComboBurstStyle,
                ("Left", 0),
                ("Right", 1),
                ("Both", 2)),
            KeyWarningColour = colour(
                values,
                "ColourKeyWarning",
                defaults.KeyWarningColour),
            HoldColour = colour(
                values,
                "ColourHold",
                defaults.HoldColour),
            ComboBreakColour = colour(
                values,
                "ColourBreak",
                defaults.ComboBreakColour),
            SkinVersion = defaults.SkinVersion,
        });
    }

    private static string text(IReadOnlyDictionary<string, string> values, string key, string fallback) =>
        values.TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;

    private static bool boolean(IReadOnlyDictionary<string, string> values, string key, bool fallback) =>
        tryInt(values, key, out int value) ? value == 1 : fallback;

    private static int integer(IReadOnlyDictionary<string, string> values, string key, int fallback) =>
        tryInt(values, key, out int value) ? value : fallback;

    private static int positiveFrameRate(int value) =>
        value <= 0 ? 24 : Math.Min(value, 1000);

    private static int noteBodyStyle(
        IReadOnlyDictionary<string, string> values,
        string key,
        int fallback) =>
        namedInteger(
            values,
            key,
            fallback,
            ("Stretch", 0),
            ("Repeat", 1),
            ("RepeatTop", 2),
            ("RepeatBottom", 3),
            ("RepeatTopAndBottom", 4));

    private static int namedInteger(
        IReadOnlyDictionary<string, string> values,
        string key,
        int fallback,
        params (string Name, int Value)[] names)
    {
        if (!values.TryGetValue(key, out string raw))
            return fallback;

        if (int.TryParse(
                raw,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int value))
            return value;

        foreach ((string name, int namedValue) in names)
        {
            if (raw.Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                return namedValue;
        }

        return fallback;
    }

    private static float number(IReadOnlyDictionary<string, string> values, string key, float fallback) =>
        values.TryGetValue(key, out string value) &&
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) &&
        float.IsFinite(parsed)
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
            result[i] = float.TryParse(
                            parts[i].Trim(),
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out float parsed)
                        && float.IsFinite(parsed)
                ? parsed
                : 0;
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

    private static bool? nullableBoolean(
        IReadOnlyDictionary<string, string> values,
        string key) =>
        tryInt(values, key, out int value) ? value == 1 : null;
}
