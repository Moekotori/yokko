using System;

namespace Yokko.Game.Screens.SongSelect;

internal static class SongSelectArtworkPolicy
{
    internal const string FallbackTexture = "SongSelect/blue-signal";
    // The beatmap artwork is deliberately allowed to vary wildly. A strong,
    // neutral navy veil keeps the selection UI readable on bright, dark and
    // highly saturated backgrounds without adapting the palette per chart.
    internal const float IsolationOpacity = 0.68f;

    internal static string Resolve(string artworkPath) =>
        string.IsNullOrWhiteSpace(artworkPath)
            ? FallbackTexture
            : artworkPath;
}
