using System;

namespace Yokko.Game.Screens.SongSelect;

internal static class SongSelectArtworkPolicy
{
    internal const string FallbackTexture = "SongSelect/blue-signal";
    internal const float IsolationOpacity = 0.36f;

    internal static string Resolve(string artworkPath) =>
        string.IsNullOrWhiteSpace(artworkPath)
            ? FallbackTexture
            : artworkPath;
}
