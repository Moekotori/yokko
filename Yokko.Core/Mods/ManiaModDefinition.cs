namespace Yokko.Core.Mods;

/// <summary>
/// Metadata for one entry in the osu!mania parity target catalogue.
/// Runtime implementations and configurable values intentionally live outside
/// this type so listing a target never implies that it is already playable.
/// </summary>
public sealed record ManiaModDefinition(
    ManiaModId Id,
    string Key,
    string Acronym,
    string Name,
    ManiaModCategory Category);
