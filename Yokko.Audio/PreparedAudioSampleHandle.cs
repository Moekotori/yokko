namespace Yokko.Audio;

/// <summary>
/// Identifies a sample prepared by a specific audio engine. Handles remain
/// valid while the same prepared sample set is rebound to a restarted core.
/// </summary>
public readonly struct PreparedAudioSampleHandle
{
    private readonly object? owner;
    private readonly int generation;
    private readonly int slot;

    internal PreparedAudioSampleHandle(object owner, int generation, int slot)
    {
        this.owner = owner;
        this.generation = generation;
        this.slot = slot;
    }

    internal bool BelongsTo(object expectedOwner) =>
        ReferenceEquals(owner, expectedOwner);

    internal int Generation => generation;

    internal int Slot => slot;
}
