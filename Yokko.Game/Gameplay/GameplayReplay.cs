using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Yokko.Core.Mods;
using Yokko.Import.Osu;

namespace Yokko.Game.Gameplay;

internal readonly record struct GameplayReplayInput(
    int Lane,
    bool IsPressed,
    double TimeMilliseconds);

internal sealed class GameplayReplay
{
    private readonly GameplayReplayInput[] inputs;

    public IReadOnlyList<GameplayReplayInput> Inputs => inputs;

    public ManiaModSet Mods { get; }

    public GameplayReplay(
        IEnumerable<GameplayReplayInput> inputs,
        ManiaModSet mods = null)
    {
        this.inputs = inputs.ToArray();
        Mods = mods ?? ManiaModSet.Empty;

        for (int i = 0; i < this.inputs.Length; i++)
        {
            GameplayReplayInput input = this.inputs[i];

            if (input.Lane < 0)
                throw new ArgumentOutOfRangeException(nameof(inputs));

            if (!double.IsFinite(input.TimeMilliseconds))
                throw new ArgumentOutOfRangeException(nameof(inputs));

            if (i > 0
                && input.TimeMilliseconds
                < this.inputs[i - 1].TimeMilliseconds)
            {
                throw new ArgumentException(
                    "Replay inputs must be ordered by gameplay time.",
                    nameof(inputs));
            }
        }
    }

    public static GameplayReplay FromOsuReplay(
        OsuReplay replay,
        int keyCount)
    {
        ArgumentNullException.ThrowIfNull(replay);
        if (keyCount is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(keyCount));

        int supportedKeys = (1 << keyCount) - 1;
        int previousKeys = 0;
        var converted = new List<GameplayReplayInput>();

        foreach (OsuReplayFrame frame in replay.Frames)
        {
            if ((frame.PressedKeys & ~supportedKeys) != 0)
            {
                throw new InvalidDataException(
                    $"The replay uses keys outside this {keyCount}K beatmap.");
            }

            int changedKeys = previousKeys ^ frame.PressedKeys;

            for (int lane = 0; lane < keyCount; lane++)
            {
                int laneMask = 1 << lane;
                if ((changedKeys & laneMask) == 0)
                    continue;

                converted.Add(new GameplayReplayInput(
                    lane,
                    (frame.PressedKeys & laneMask) != 0,
                    frame.TimeMilliseconds));
            }

            previousKeys = frame.PressedKeys;
        }

        return new GameplayReplay(
            converted,
            OsuLegacyManiaModConverter.Convert(replay.Mods));
    }
}
