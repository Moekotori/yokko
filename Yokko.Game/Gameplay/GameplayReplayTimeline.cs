using System;
using System.Collections.Generic;

namespace Yokko.Game.Gameplay;

/// <summary>
/// Stable cursor over full-state replay frames. The API is intentionally
/// rewind-capable so gameplay can later rebuild deterministic judgement state
/// before exposing arbitrary seek controls.
/// </summary>
internal sealed class GameplayReplayTimeline
{
    private readonly IReadOnlyList<GameplayReplayFrame> frames;
    private int currentFrameIndex = -1;

    public GameplayReplayTimeline(
        IReadOnlyList<GameplayReplayFrame> frames)
    {
        this.frames = frames
                      ?? throw new ArgumentNullException(nameof(frames));

        for (int index = 0; index < frames.Count; index++)
        {
            GameplayReplayFrame frame = frames[index];
            if (!double.IsFinite(frame.TimeMilliseconds))
                throw new ArgumentOutOfRangeException(nameof(frames));
            if (index > 0
                && frame.TimeMilliseconds
                < frames[index - 1].TimeMilliseconds)
            {
                throw new ArgumentException(
                    "Replay frames must be ordered by gameplay time.",
                    nameof(frames));
            }
        }
    }

    public int CurrentFrameIndex => currentFrameIndex;

    public GameplayReplayFrame? CurrentFrame =>
        currentFrameIndex >= 0
            ? frames[currentFrameIndex]
            : null;

    public GameplayReplayFrame? NextFrame =>
        currentFrameIndex + 1 < frames.Count
            ? frames[currentFrameIndex + 1]
            : null;

    public ulong PressedLanes => CurrentFrame?.PressedLanes ?? 0;

    public bool MoveNext(double gameplayTime,
        out GameplayReplayFrame frame)
    {
        int nextIndex = currentFrameIndex + 1;
        if (nextIndex >= frames.Count
            || frames[nextIndex].TimeMilliseconds > gameplayTime)
        {
            frame = default;
            return false;
        }

        currentFrameIndex = nextIndex;
        frame = frames[nextIndex];
        return true;
    }

    public ulong Seek(double gameplayTime)
    {
        if (!double.IsFinite(gameplayTime))
            throw new ArgumentOutOfRangeException(nameof(gameplayTime));

        int low = 0;
        int high = frames.Count;
        while (low < high)
        {
            int middle = low + (high - low) / 2;
            if (frames[middle].TimeMilliseconds <= gameplayTime)
                low = middle + 1;
            else
                high = middle;
        }

        currentFrameIndex = low - 1;
        return PressedLanes;
    }

    public void Reset() => currentFrameIndex = -1;
}
