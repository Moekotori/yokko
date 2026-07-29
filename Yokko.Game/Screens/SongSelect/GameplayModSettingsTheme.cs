using osuTK.Graphics;

namespace Yokko.Game.Screens.SongSelect;

/// <summary>
/// Light workspace palette used by the Gameplay Mods configuration card.
/// Kept separate from the dark Song Select palette so the card belongs to
/// the ivory Gameplay Mods screen without changing the rest of Song Select.
/// </summary>
internal static class GameplayModSettingsTheme
{
    public static readonly Color4 Text =
        new(0.035f, 0.085f, 0.37f, 1f);
    public static readonly Color4 Control =
        new(0.87f, 0.96f, 0.985f, 1f);
    public static readonly Color4 AccentOn =
        new(0.012f, 0.035f, 0.18f, 1f);
    public static readonly Color4 Surface =
        new(0.965f, 0.99f, 1f, 1f);
    public static readonly Color4 Accent =
        new(0.055f, 0.67f, 0.84f, 1f);
    public static readonly Color4 Selection =
        new(1f, 0.22f, 0.65f, 1f);
    public static readonly Color4 Muted =
        new(0.31f, 0.39f, 0.61f, 1f);
}
