using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Import.Osu;

namespace Yokko.Game.Gameplay;

internal readonly record struct GameplayReplayInput(
    int Lane,
    bool IsPressed,
    double TimeMilliseconds);

/// <summary>
/// Full mania key state after a replay transition at a specific gameplay
/// time. This mirrors ppy/osu's ManiaReplayFrame at commit
/// 83b8a64bec19e1463353645c2d6d10c75e275b43 (MIT): playback owns the
/// complete pressed-action state rather than depending on loose input edges.
/// </summary>
internal readonly record struct GameplayReplayFrame(
    double TimeMilliseconds,
    ulong PressedLanes);

internal sealed class GameplayReplay
{
    private readonly GameplayReplayInput[] inputs;
    private readonly GameplayReplayFrame[] frames;

    public IReadOnlyList<GameplayReplayInput> Inputs => inputs;

    public IReadOnlyList<GameplayReplayFrame> Frames => frames;

    public ManiaModSet Mods { get; }

    public JudgementConfiguration? JudgementConfiguration { get; }

    public GameplayReplay(
        IEnumerable<GameplayReplayInput> inputs,
        ManiaModSet mods = null,
        JudgementConfiguration? judgementConfiguration = null)
    {
        this.inputs = inputs.ToArray();
        Mods = mods ?? ManiaModSet.Empty;
        JudgementConfiguration = judgementConfiguration;

        ulong pressedLanes = 0;
        var replayFrames = new List<GameplayReplayFrame>(
            this.inputs.Length);

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

            if (input.Lane >= 64)
                throw new ArgumentOutOfRangeException(nameof(inputs));

            ulong laneMask = 1UL << input.Lane;
            ulong nextPressedLanes = input.IsPressed
                ? pressedLanes | laneMask
                : pressedLanes & ~laneMask;
            if (nextPressedLanes == pressedLanes)
                continue;

            pressedLanes = nextPressedLanes;
            replayFrames.Add(new GameplayReplayFrame(
                input.TimeMilliseconds,
                pressedLanes));
        }

        frames = replayFrames.ToArray();
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
            OsuLegacyManiaModConverter.Convert(replay.Mods),
            Yokko.Core.Scoring.JudgementConfiguration.YokkoDefault);
    }
}
