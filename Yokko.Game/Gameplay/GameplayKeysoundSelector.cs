using System.Linq;
using Yokko.Core.Beatmaps;
using Yokko.Core.Scoring;

namespace Yokko.Game.Gameplay;

/// <summary>
/// Selects the sample associated with the most appropriate object for a lane
/// press, including early presses and spam presses.
/// Mirrors ppy/osu GameplaySampleTriggerSource at
/// osu.Game/Rulesets/UI/GameplaySampleTriggerSource.cs,
/// commit 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0 (MIT).
/// </summary>
internal sealed class GameplayKeysoundSelector
{
    private readonly YokkoBeatmap beatmap;
    private readonly BeatmapJudgementState judgementState;
    private readonly int[][] objectIndicesByLane;
    private readonly int[] mostValidObjects;
    private readonly int[] nextUnresolvedPositions;

    internal GameplayKeysoundSelector(
        YokkoBeatmap beatmap,
        BeatmapJudgementState judgementState)
    {
        this.beatmap = beatmap;
        this.judgementState = judgementState;
        objectIndicesByLane = Enumerable
                .Range(0, (int)beatmap.KeyMode)
            .Select(lane => beatmap.HitObjects
                .Select((hitObject, index) => (hitObject, index))
                .Where(item =>
                    item.hitObject.Lane == lane
                    && item.hitObject.Kind != HitObjectKind.Mine)
                .OrderBy(item => item.hitObject.StartTimeMilliseconds)
                .ThenBy(item => item.index)
                .Select(item => item.index)
                .ToArray())
            .ToArray();
        mostValidObjects =
            Enumerable.Repeat(-1, (int)beatmap.KeyMode).ToArray();
        nextUnresolvedPositions = new int[(int)beatmap.KeyMode];
    }

    internal int Select(int lane, double inputTime)
    {
        if ((uint)lane >= objectIndicesByLane.Length)
            return -1;

        int selected = mostValidObjects[lane];
        if (selected >= 0 && !judgementState.IsResolved(selected))
            return selected;

        int[] laneObjects = objectIndicesByLane[lane];
        int candidatePosition = nextUnresolvedPositions[lane];
        while (candidatePosition < laneObjects.Length
               && judgementState.IsResolved(
                   laneObjects[candidatePosition]))
        {
            candidatePosition++;
        }

        nextUnresolvedPositions[lane] = candidatePosition;
        int candidate = candidatePosition < laneObjects.Length
            ? laneObjects[candidatePosition]
            : -1;

        if (candidate < 0)
        {
            selected = laneObjects.Length == 0
                ? -1
                : laneObjects[^1];
        }
        else if (inputTime >= beatmap.HitObjects[candidate]
                                     .StartTimeMilliseconds
                              - judgementState.Windows.MehMilliseconds * 2)
        {
            selected = candidate;
        }
        else if (selected < 0)
        {
            selected = laneObjects.Length == 0
                ? -1
                : laneObjects[0];
        }

        mostValidObjects[lane] = selected;
        return selected;
    }
}
