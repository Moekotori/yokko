using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osuTK.Graphics;

namespace Yokko.Game.Presentation;

/// <summary>
/// The semantic design tokens used by Yokko-owned interface components.
/// </summary>
/// <remarks>
/// Pages still own their layout and interaction. Replacing this value only
/// changes presentation, which keeps future user themes away from game logic.
/// </remarks>
public sealed record YokkoUiTheme(
    YokkoUiColourTokens Colours,
    YokkoUiTypographyTokens Typography,
    YokkoUiMetricTokens Metrics,
    YokkoUiMotionTokens Motion)
{
    public static YokkoUiTheme Default { get; } = new(
        YokkoUiColourTokens.Default,
        YokkoUiTypographyTokens.Default,
        YokkoUiMetricTokens.Default,
        YokkoUiMotionTokens.Default);
}

public sealed record YokkoUiColourTokens(
    YokkoDarkColourTokens Dark,
    YokkoBrandColourTokens Brand,
    YokkoSettingsColourTokens Settings,
    YokkoSongSelectColourTokens SongSelect)
{
    public static YokkoUiColourTokens Default { get; } = new(
        YokkoDarkColourTokens.Default,
        YokkoBrandColourTokens.Default,
        YokkoSettingsColourTokens.Default,
        YokkoSongSelectColourTokens.Default);
}

public sealed record YokkoDarkColourTokens(
    Color4 Background,
    Color4 Surface,
    Color4 SurfaceElevated,
    Color4 SurfaceHover,
    Color4 Panel,
    Color4 PanelAlt,
    Color4 Chip,
    Color4 Border,
    Color4 Text,
    Color4 TextMuted,
    Color4 TextDim,
    Color4 Cyan,
    Color4 Rose,
    Color4 Lime,
    Color4 Violet,
    Color4 AccentControl)
{
    public static YokkoDarkColourTokens Default { get; } = new(
        new Color4(0.018f, 0.023f, 0.036f, 1f),
        new Color4(0.045f, 0.058f, 0.082f, 0.98f),
        new Color4(0.065f, 0.082f, 0.112f, 1f),
        new Color4(0.085f, 0.108f, 0.145f, 1f),
        new Color4(0.045f, 0.058f, 0.082f, 0.98f),
        new Color4(0.035f, 0.046f, 0.066f, 0.98f),
        new Color4(0.085f, 0.108f, 0.145f, 1f),
        new Color4(0.28f, 0.34f, 0.43f, 0.34f),
        Color4.White,
        new Color4(0.78f, 0.84f, 0.92f, 1f),
        new Color4(0.57f, 0.64f, 0.73f, 1f),
        new Color4(0.25f, 0.88f, 0.96f, 1f),
        new Color4(1f, 0.43f, 0.58f, 1f),
        new Color4(0.69f, 0.94f, 0.43f, 1f),
        new Color4(0.65f, 0.56f, 1f, 1f),
        new Color4(0.09f, 0.11f, 0.145f, 0.98f));
}

public sealed record YokkoBrandColourTokens(
    Color4 Ink,
    Color4 Cyan,
    Color4 PaleCyan,
    Color4 Yellow,
    Color4 Pink,
    Color4 Ivory)
{
    public static YokkoBrandColourTokens Default { get; } = new(
        new Color4(0.035f, 0.085f, 0.54f, 1f),
        new Color4(0.18f, 0.78f, 0.94f, 1f),
        new Color4(0.78f, 0.96f, 1f, 1f),
        new Color4(1f, 0.91f, 0.42f, 1f),
        new Color4(1f, 0.22f, 0.65f, 1f),
        new Color4(0.992f, 0.992f, 0.988f, 1f));
}

public sealed record YokkoSettingsColourTokens(
    Color4 MutedInk,
    Color4 Divider,
    Color4 StatusCyan,
    Color4 PaleCyan,
    Color4 HoverInk,
    Color4 SoftShadow)
{
    public static YokkoSettingsColourTokens Default { get; } = new(
        new Color4(0.34f, 0.39f, 0.64f, 1f),
        new Color4(0.12f, 0.22f, 0.55f, 0.18f),
        new Color4(0.36f, 0.84f, 0.96f, 1f),
        new Color4(0.87f, 0.98f, 1f, 1f),
        new Color4(0.055f, 0.15f, 0.7f, 1f),
        new Color4(0.05f, 0.12f, 0.35f, 0.08f));
}

public sealed record YokkoSongSelectColourTokens(
    Color4 Ivory,
    Color4 Navy,
    Color4 DeepNavy,
    Color4 Surface,
    Color4 SurfaceRaised,
    Color4 Cyan,
    Color4 PaleCyan,
    Color4 Yellow,
    Color4 Pink,
    Color4 Muted)
{
    public static YokkoSongSelectColourTokens Default { get; } = new(
        new Color4(0.992f, 0.992f, 0.988f, 1f),
        new Color4(0.035f, 0.085f, 0.37f, 1f),
        new Color4(0.012f, 0.035f, 0.18f, 1f),
        new Color4(0.02f, 0.055f, 0.23f, 1f),
        new Color4(0.035f, 0.085f, 0.31f, 1f),
        new Color4(0.29f, 0.81f, 0.94f, 1f),
        new Color4(0.78f, 0.96f, 1f, 1f),
        new Color4(1f, 0.91f, 0.42f, 1f),
        new Color4(1f, 0.22f, 0.65f, 1f),
        new Color4(0.58f, 0.68f, 0.86f, 1f));
}

public sealed record YokkoUiTypographyTokens(
    string PrimaryFont,
    string InputFont,
    string StickerFont,
    float MinimumReadableSize)
{
    public const string CompleteFamily = "PlusJakartaSans";

    // A deliberately small, shared type ramp keeps neighbouring pieces of UI
    // from ending up at subtly different fractional sizes. The larger steps
    // open up as the text grows so headings keep a clear visual hierarchy.
    private static readonly float[] readable_type_ramp =
    {
        14, 16, 18, 20, 22, 24, 28, 32, 36, 40,
        48, 56, 64, 72, 80, 88, 96, 104, 112, 120,
        128, 144, 160,
    };

    public static YokkoUiTypographyTokens Default { get; } = new(
        CompleteFamily,
        CompleteFamily,
        CompleteFamily,
        18);

    public FontUsage Display(float size) =>
        new FontUsage(PrimaryFont, ReadableSize(size))
            .With(weight: "SemiBold");

    public FontUsage Body(float size) =>
        new FontUsage(PrimaryFont, ReadableSize(size))
            .With(weight: "Regular");

    public FontUsage SearchInput(float size) =>
        new(InputFont, ReadableSize(size));

    public FontUsage Brand(float size) =>
        new FontUsage(PrimaryFont, ReadableSize(size))
            .With(weight: "Bold");

    public FontUsage Sticker(float size) =>
        new FontUsage(StickerFont, ReadableSize(size))
            .With(weight: "Bold");

    public FontUsage Interface(float size, string weight = null) =>
        new FontUsage(PrimaryFont, ReadableSize(size))
            .With(weight: weight ?? "Regular");

    public FontUsage Control(float size = 16, string weight = null) =>
        new FontUsage(PrimaryFont, ReadableSize(MathF.Max(14, size)))
            .With(weight: weight ?? "SemiBold");

    public float ReadableSize(float size)
    {
        float requestedSize = MathF.Max(
            MinimumReadableSize,
            size + MathF.Min(6, 4 + size * 0.05f));

        float closestSize = readable_type_ramp[0];
        float closestDistance = MathF.Abs(requestedSize - closestSize);

        foreach (float candidate in readable_type_ramp)
        {
            if (candidate < MinimumReadableSize)
                continue;

            float distance = MathF.Abs(requestedSize - candidate);
            if (distance < closestDistance)
            {
                closestSize = candidate;
                closestDistance = distance;
            }
        }

        return MathF.Max(MinimumReadableSize, closestSize);
    }
}

public sealed record YokkoUiMetricTokens(
    float ControlCornerRadius,
    float CardCornerRadius,
    float AccentBarWidth,
    float InlineSpacing,
    float HoverScale,
    float PressedScale,
    float DisabledAlpha,
    float FocusLineHeight)
{
    public static YokkoUiMetricTokens Default { get; } = new(
        8,
        12,
        4,
        8,
        1.025f,
        0.98f,
        0.45f,
        3);
}

public sealed record YokkoUiMotionTokens(
    double HoverInDuration,
    double HoverOutDuration,
    double StateChangeDuration,
    double FocusDuration,
    Easing HoverEasing)
{
    public static YokkoUiMotionTokens Default { get; } = new(
        120,
        140,
        120,
        100,
        Easing.OutQuint);
}

/// <summary>
/// Holds the active UI theme and broadcasts replacements to migrated
/// components. This is cached by <see cref="YokkoGameBase"/>.
/// </summary>
public sealed class YokkoUiThemeStore
{
    private readonly Bindable<YokkoUiTheme> current = new(YokkoUiTheme.Default);
    private readonly Bindable<string> activeName = new("Default");
    private readonly Bindable<string> sourcePath = new(string.Empty);
    private readonly Bindable<string> lastError = new(string.Empty);
    private readonly Bindable<int> revision = new();

    public IBindable<YokkoUiTheme> Current => current;
    public IBindable<string> ActiveName => activeName;
    public IBindable<string> SourcePath => sourcePath;
    public IBindable<string> LastError => lastError;
    public IBindable<int> Revision => revision;

    public void Apply(
        YokkoUiTheme theme,
        string name = null,
        string loadedFrom = null)
    {
        Validate(theme);
        current.Value = theme;
        activeName.Value = string.IsNullOrWhiteSpace(name)
            ? "Custom"
            : name.Trim();
        sourcePath.Value = loadedFrom?.Trim() ?? string.Empty;
        lastError.Value = string.Empty;
        revision.Value++;
    }

    public void ReportLoadError(string loadedFrom, string error)
    {
        sourcePath.Value = loadedFrom?.Trim() ?? string.Empty;
        lastError.Value = string.IsNullOrWhiteSpace(error)
            ? "Unknown theme loading error."
            : error.Trim();
    }

    public void Reset()
    {
        current.Value = YokkoUiTheme.Default;
        activeName.Value = "Default";
        sourcePath.Value = string.Empty;
        lastError.Value = string.Empty;
        revision.Value++;
    }

    public static void Validate(YokkoUiTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(theme.Colours);
        ArgumentNullException.ThrowIfNull(theme.Colours.Dark);
        ArgumentNullException.ThrowIfNull(theme.Colours.Brand);
        ArgumentNullException.ThrowIfNull(theme.Colours.Settings);
        ArgumentNullException.ThrowIfNull(theme.Colours.SongSelect);
        ArgumentNullException.ThrowIfNull(theme.Typography);
        ArgumentNullException.ThrowIfNull(theme.Metrics);
        ArgumentNullException.ThrowIfNull(theme.Motion);

        if (string.IsNullOrWhiteSpace(theme.Typography.PrimaryFont)
            || string.IsNullOrWhiteSpace(theme.Typography.InputFont)
            || string.IsNullOrWhiteSpace(theme.Typography.StickerFont))
        {
            throw new ArgumentException(
                "Theme font names must not be empty.",
                nameof(theme));
        }

        if (!string.Equals(
                theme.Typography.PrimaryFont,
                YokkoUiTypographyTokens.CompleteFamily,
                StringComparison.Ordinal)
            || !string.Equals(
                theme.Typography.InputFont,
                YokkoUiTypographyTokens.CompleteFamily,
                StringComparison.Ordinal)
            || theme.Typography.StickerFont
                    != YokkoUiTypographyTokens.CompleteFamily)
        {
            throw new ArgumentException(
                "Theme text fonts must use Yokko's registered UI family.",
                nameof(theme));
        }

        requireRange(
            theme.Typography.MinimumReadableSize,
            8,
            64,
            nameof(theme.Typography.MinimumReadableSize));
        requireRange(
            theme.Metrics.ControlCornerRadius,
            0,
            64,
            nameof(theme.Metrics.ControlCornerRadius));
        requireRange(
            theme.Metrics.CardCornerRadius,
            0,
            96,
            nameof(theme.Metrics.CardCornerRadius));
        requireRange(
            theme.Metrics.AccentBarWidth,
            0,
            24,
            nameof(theme.Metrics.AccentBarWidth));
        requireRange(
            theme.Metrics.InlineSpacing,
            0,
            64,
            nameof(theme.Metrics.InlineSpacing));
        requireRange(
            theme.Metrics.HoverScale,
            1,
            1.15f,
            nameof(theme.Metrics.HoverScale));
        requireRange(
            theme.Metrics.PressedScale,
            0.8f,
            1,
            nameof(theme.Metrics.PressedScale));
        requireRange(
            theme.Metrics.DisabledAlpha,
            0.1f,
            1,
            nameof(theme.Metrics.DisabledAlpha));
        requireRange(
            theme.Metrics.FocusLineHeight,
            1,
            12,
            nameof(theme.Metrics.FocusLineHeight));
        requireRange(
            theme.Motion.HoverInDuration,
            0,
            1000,
            nameof(theme.Motion.HoverInDuration));
        requireRange(
            theme.Motion.HoverOutDuration,
            0,
            1000,
            nameof(theme.Motion.HoverOutDuration));
        requireRange(
            theme.Motion.StateChangeDuration,
            0,
            1000,
            nameof(theme.Motion.StateChangeDuration));
        requireRange(
            theme.Motion.FocusDuration,
            0,
            1000,
            nameof(theme.Motion.FocusDuration));
        if (!Enum.IsDefined(
                typeof(Easing),
                theme.Motion.HoverEasing))
        {
            throw new ArgumentException(
                "Theme hover easing must be a defined value.",
                nameof(theme));
        }
    }

    private static void requireRange(
        double value,
        double minimum,
        double maximum,
        string token)
    {
        if (!double.IsFinite(value)
            || value < minimum
            || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                token,
                value,
                $"Theme token must be between {minimum} and {maximum}.");
        }
    }
}
