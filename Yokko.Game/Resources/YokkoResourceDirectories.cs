using System.IO;

namespace Yokko.Game.Resources;

/// <summary>
/// Defines the persistent, user-managed resource layout beneath the game
/// storage directory.
/// </summary>
internal static class YokkoResourceDirectories
{
    public const string Root = "Resources";
    public static readonly string Beatmaps = Path.Combine(Root, "Beatmaps");
    public static readonly string Skins = Path.Combine(Root, "Skins");
}
