using System;
using System.Collections.Generic;
using System.Linq;

namespace Yokko.Game.Gameplay;

internal readonly record struct GameplayReplayInput(
    int Lane,
    bool IsPressed,
    double TimeMilliseconds);

internal sealed class GameplayReplay
{
    private readonly GameplayReplayInput[] inputs;

    public IReadOnlyList<GameplayReplayInput> Inputs => inputs;

    public GameplayReplay(IEnumerable<GameplayReplayInput> inputs)
    {
        this.inputs = inputs.ToArray();

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
}
