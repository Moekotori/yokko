using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using osu.Framework.Graphics;
using osuTK.Graphics;

namespace Yokko.Game.Presentation;

public sealed record YokkoUiThemeFileResult(
    string Name,
    YokkoUiTheme Theme);

/// <summary>
/// Reads a strict, versioned JSON overlay on top of Yokko's complete default
/// theme. Omitted tokens safely retain their built-in values.
/// </summary>
public static class YokkoUiThemeFile
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions json_options = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static YokkoUiThemeFileResult Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(
            Environment.ExpandEnvironmentVariables(path.Trim()));
        return Parse(File.ReadAllText(fullPath));
    }

    public static YokkoUiThemeFileResult Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        YokkoUiThemeDocument document =
            JsonSerializer.Deserialize<YokkoUiThemeDocument>(
                json,
                json_options)
            ?? throw new InvalidDataException("Theme document is empty.");

        if (document.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported theme schema version {document.SchemaVersion}. "
                + $"Expected {CurrentSchemaVersion}.");
        }

        YokkoUiTheme theme = YokkoUiTheme.Default;
        YokkoUiColourTokens colours = theme.Colours;
        colours = colours with
        {
            Dark = apply(document.Colours?.Dark, colours.Dark),
            Brand = apply(document.Colours?.Brand, colours.Brand),
            Settings = apply(document.Colours?.Settings, colours.Settings),
            SongSelect = apply(
                document.Colours?.SongSelect,
                colours.SongSelect),
        };

        YokkoUiTypographyTokens typography = theme.Typography;
        if (document.Typography != null)
        {
            typography = typography with
            {
                PrimaryFont = text(
                    document.Typography.PrimaryFont,
                    typography.PrimaryFont,
                    "typography.primaryFont"),
                InputFont = text(
                    document.Typography.InputFont,
                    typography.InputFont,
                    "typography.inputFont"),
                StickerFont = text(
                    document.Typography.StickerFont,
                    typography.StickerFont,
                    "typography.stickerFont"),
                MinimumReadableSize = document.Typography.MinimumReadableSize
                                      ?? typography.MinimumReadableSize,
            };
        }

        YokkoUiMetricTokens metrics = theme.Metrics;
        if (document.Metrics != null)
        {
            metrics = metrics with
            {
                ControlCornerRadius = document.Metrics.ControlCornerRadius
                                      ?? metrics.ControlCornerRadius,
                CardCornerRadius = document.Metrics.CardCornerRadius
                                   ?? metrics.CardCornerRadius,
                AccentBarWidth = document.Metrics.AccentBarWidth
                                 ?? metrics.AccentBarWidth,
                InlineSpacing = document.Metrics.InlineSpacing
                                ?? metrics.InlineSpacing,
                HoverScale = document.Metrics.HoverScale
                             ?? metrics.HoverScale,
                PressedScale = document.Metrics.PressedScale
                               ?? metrics.PressedScale,
                DisabledAlpha = document.Metrics.DisabledAlpha
                                ?? metrics.DisabledAlpha,
                FocusLineHeight = document.Metrics.FocusLineHeight
                                  ?? metrics.FocusLineHeight,
            };
        }

        YokkoUiMotionTokens motion = theme.Motion;
        if (document.Motion != null)
        {
            motion = motion with
            {
                HoverInDuration = document.Motion.HoverInDuration
                                  ?? motion.HoverInDuration,
                HoverOutDuration = document.Motion.HoverOutDuration
                                   ?? motion.HoverOutDuration,
                StateChangeDuration = document.Motion.StateChangeDuration
                                      ?? motion.StateChangeDuration,
                FocusDuration = document.Motion.FocusDuration
                                ?? motion.FocusDuration,
                HoverEasing = easing(
                    document.Motion.HoverEasing,
                    motion.HoverEasing),
            };
        }

        theme = theme with
        {
            Colours = colours,
            Typography = typography,
            Metrics = metrics,
            Motion = motion,
        };
        YokkoUiThemeStore.Validate(theme);

        return new YokkoUiThemeFileResult(
            text(document.Name, "Development theme", "name"),
            theme);
    }

    private static YokkoDarkColourTokens apply(
        YokkoDarkColoursDocument document,
        YokkoDarkColourTokens value)
    {
        if (document == null)
            return value;

        return value with
        {
            Background = colour(document.Background, value.Background, "colours.dark.background"),
            Surface = colour(document.Surface, value.Surface, "colours.dark.surface"),
            SurfaceElevated = colour(document.SurfaceElevated, value.SurfaceElevated, "colours.dark.surfaceElevated"),
            SurfaceHover = colour(document.SurfaceHover, value.SurfaceHover, "colours.dark.surfaceHover"),
            Panel = colour(document.Panel, value.Panel, "colours.dark.panel"),
            PanelAlt = colour(document.PanelAlt, value.PanelAlt, "colours.dark.panelAlt"),
            Chip = colour(document.Chip, value.Chip, "colours.dark.chip"),
            Border = colour(document.Border, value.Border, "colours.dark.border"),
            Text = colour(document.Text, value.Text, "colours.dark.text"),
            TextMuted = colour(document.TextMuted, value.TextMuted, "colours.dark.textMuted"),
            TextDim = colour(document.TextDim, value.TextDim, "colours.dark.textDim"),
            Cyan = colour(document.Cyan, value.Cyan, "colours.dark.cyan"),
            Rose = colour(document.Rose, value.Rose, "colours.dark.rose"),
            Lime = colour(document.Lime, value.Lime, "colours.dark.lime"),
            Violet = colour(document.Violet, value.Violet, "colours.dark.violet"),
            AccentControl = colour(document.AccentControl, value.AccentControl, "colours.dark.accentControl"),
        };
    }

    private static YokkoBrandColourTokens apply(
        YokkoBrandColoursDocument document,
        YokkoBrandColourTokens value)
    {
        if (document == null)
            return value;

        return value with
        {
            Ink = colour(document.Ink, value.Ink, "colours.brand.ink"),
            Cyan = colour(document.Cyan, value.Cyan, "colours.brand.cyan"),
            PaleCyan = colour(document.PaleCyan, value.PaleCyan, "colours.brand.paleCyan"),
            Yellow = colour(document.Yellow, value.Yellow, "colours.brand.yellow"),
            Pink = colour(document.Pink, value.Pink, "colours.brand.pink"),
            Ivory = colour(document.Ivory, value.Ivory, "colours.brand.ivory"),
        };
    }

    private static YokkoSettingsColourTokens apply(
        YokkoSettingsColoursDocument document,
        YokkoSettingsColourTokens value)
    {
        if (document == null)
            return value;

        return value with
        {
            MutedInk = colour(document.MutedInk, value.MutedInk, "colours.settings.mutedInk"),
            Divider = colour(document.Divider, value.Divider, "colours.settings.divider"),
            StatusCyan = colour(document.StatusCyan, value.StatusCyan, "colours.settings.statusCyan"),
            PaleCyan = colour(document.PaleCyan, value.PaleCyan, "colours.settings.paleCyan"),
            HoverInk = colour(document.HoverInk, value.HoverInk, "colours.settings.hoverInk"),
            SoftShadow = colour(document.SoftShadow, value.SoftShadow, "colours.settings.softShadow"),
        };
    }

    private static YokkoSongSelectColourTokens apply(
        YokkoSongSelectColoursDocument document,
        YokkoSongSelectColourTokens value)
    {
        if (document == null)
            return value;

        return value with
        {
            Ivory = colour(document.Ivory, value.Ivory, "colours.songSelect.ivory"),
            Navy = colour(document.Navy, value.Navy, "colours.songSelect.navy"),
            DeepNavy = colour(document.DeepNavy, value.DeepNavy, "colours.songSelect.deepNavy"),
            Surface = colour(document.Surface, value.Surface, "colours.songSelect.surface"),
            SurfaceRaised = colour(document.SurfaceRaised, value.SurfaceRaised, "colours.songSelect.surfaceRaised"),
            Cyan = colour(document.Cyan, value.Cyan, "colours.songSelect.cyan"),
            PaleCyan = colour(document.PaleCyan, value.PaleCyan, "colours.songSelect.paleCyan"),
            Yellow = colour(document.Yellow, value.Yellow, "colours.songSelect.yellow"),
            Pink = colour(document.Pink, value.Pink, "colours.songSelect.pink"),
            Muted = colour(document.Muted, value.Muted, "colours.songSelect.muted"),
        };
    }

    private static Color4 colour(
        string encoded,
        Color4 fallback,
        string token)
    {
        if (encoded == null)
            return fallback;

        string value = encoded.Trim();
        if (value.Length is not (7 or 9) || value[0] != '#')
        {
            throw new InvalidDataException(
                $"Theme token '{token}' must use #RRGGBB or #RRGGBBAA.");
        }

        try
        {
            byte red = hex(value.AsSpan(1, 2));
            byte green = hex(value.AsSpan(3, 2));
            byte blue = hex(value.AsSpan(5, 2));
            byte alpha = value.Length == 9
                ? hex(value.AsSpan(7, 2))
                : byte.MaxValue;
            return new Color4(
                red / 255f,
                green / 255f,
                blue / 255f,
                alpha / 255f);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException(
                $"Theme token '{token}' contains an invalid hex colour.",
                exception);
        }
    }

    private static byte hex(ReadOnlySpan<char> value) =>
        byte.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static string text(
        string value,
        string fallback,
        string token)
    {
        if (value == null)
            return fallback;

        string trimmed = value.Trim();
        if (trimmed.Length is 0 or > 128)
        {
            throw new InvalidDataException(
                $"Theme token '{token}' must contain 1 to 128 characters.");
        }

        return trimmed;
    }

    private static Easing easing(string value, Easing fallback)
    {
        if (value == null)
            return fallback;

        if (!Enum.TryParse(value.Trim(), true, out Easing result)
            || !Enum.IsDefined(typeof(Easing), result))
        {
            throw new InvalidDataException(
                $"Unknown theme hover easing '{value}'.");
        }

        return result;
    }
}

internal sealed class YokkoUiThemeDocument
{
    public int SchemaVersion { get; init; }
    public string Name { get; init; }
    public YokkoColoursDocument Colours { get; init; }
    public YokkoTypographyDocument Typography { get; init; }
    public YokkoMetricsDocument Metrics { get; init; }
    public YokkoMotionDocument Motion { get; init; }
}

internal sealed class YokkoColoursDocument
{
    public YokkoDarkColoursDocument Dark { get; init; }
    public YokkoBrandColoursDocument Brand { get; init; }
    public YokkoSettingsColoursDocument Settings { get; init; }
    public YokkoSongSelectColoursDocument SongSelect { get; init; }
}

internal sealed class YokkoDarkColoursDocument
{
    public string Background { get; init; }
    public string Surface { get; init; }
    public string SurfaceElevated { get; init; }
    public string SurfaceHover { get; init; }
    public string Panel { get; init; }
    public string PanelAlt { get; init; }
    public string Chip { get; init; }
    public string Border { get; init; }
    public string Text { get; init; }
    public string TextMuted { get; init; }
    public string TextDim { get; init; }
    public string Cyan { get; init; }
    public string Rose { get; init; }
    public string Lime { get; init; }
    public string Violet { get; init; }
    public string AccentControl { get; init; }
}

internal sealed class YokkoBrandColoursDocument
{
    public string Ink { get; init; }
    public string Cyan { get; init; }
    public string PaleCyan { get; init; }
    public string Yellow { get; init; }
    public string Pink { get; init; }
    public string Ivory { get; init; }
}

internal sealed class YokkoSettingsColoursDocument
{
    public string MutedInk { get; init; }
    public string Divider { get; init; }
    public string StatusCyan { get; init; }
    public string PaleCyan { get; init; }
    public string HoverInk { get; init; }
    public string SoftShadow { get; init; }
}

internal sealed class YokkoSongSelectColoursDocument
{
    public string Ivory { get; init; }
    public string Navy { get; init; }
    public string DeepNavy { get; init; }
    public string Surface { get; init; }
    public string SurfaceRaised { get; init; }
    public string Cyan { get; init; }
    public string PaleCyan { get; init; }
    public string Yellow { get; init; }
    public string Pink { get; init; }
    public string Muted { get; init; }
}

internal sealed class YokkoTypographyDocument
{
    public string PrimaryFont { get; init; }
    public string InputFont { get; init; }
    public string StickerFont { get; init; }
    public float? MinimumReadableSize { get; init; }
}

internal sealed class YokkoMetricsDocument
{
    public float? ControlCornerRadius { get; init; }
    public float? CardCornerRadius { get; init; }
    public float? AccentBarWidth { get; init; }
    public float? InlineSpacing { get; init; }
    public float? HoverScale { get; init; }
    public float? PressedScale { get; init; }
    public float? DisabledAlpha { get; init; }
    public float? FocusLineHeight { get; init; }
}

internal sealed class YokkoMotionDocument
{
    public double? HoverInDuration { get; init; }
    public double? HoverOutDuration { get; init; }
    public double? StateChangeDuration { get; init; }
    public double? FocusDuration { get; init; }
    public string HoverEasing { get; init; }
}
