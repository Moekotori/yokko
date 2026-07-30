using System;
using System.Linq;
using Yokko.Core.Beatmaps;
using Yokko.Core.Scoring;

namespace Yokko.Game.Gameplay;

internal readonly record struct GameplayKeysoundFastSelection(
    int Selected,
    bool SelectedIsUnresolved,
    double SelectedSafeUntil,
    int Candidate,
    double CandidateThreshold,
    double CandidateSafeUntil,
    int First,
    int Last)
{
    internal int Select(double inputTime)
    {
        if (SelectedIsUnresolved)
            return inputTime <= SelectedSafeUntil ? Selected : -1;
        if (Candidate < 0)
            return Last;
        if (inputTime >= CandidateThreshold)
            return inputTime <= CandidateSafeUntil ? Candidate : -1;
        return Selected >= 0 ? Selected : First;
    }

    internal bool TrySelectSafely(
        double inputTime,
        double guardMilliseconds,
        out int selected)
    {
        selected = -1;
        if (SelectedIsUnresolved)
        {
            if (inputTime > SelectedSafeUntil - guardMilliseconds)
                return false;
            selected = Selected;
            return selected >= 0;
        }

        if (Candidate < 0)
        {
            selected = Last;
            return selected >= 0;
        }

        if (Math.Abs(inputTime - CandidateThreshold) <= guardMilliseconds)
            return false;
        if (inputTime > CandidateThreshold)
        {
            if (inputTime > CandidateSafeUntil - guardMilliseconds)
                return false;
            selected = Candidate;
            return true;
        }

        selected = Selected >= 0 ? Selected : First;
        return selected >= 0;
    }
}

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

    internal GameplayKeysoundFastSelection CaptureFastSelection(int lane)
    {
        if ((uint)lane >= objectIndicesByLane.Length)
            return default;

        int[] laneObjects = objectIndicesByLane[lane];
        if (laneObjects.Length == 0)
        {
            return new GameplayKeysoundFastSelection(
                -1,
                false,
                0,
                -1,
                0,
                0,
                -1,
                -1);
        }

        int selected = mostValidObjects[lane];
        bool selectedIsUnresolved =
            selected >= 0 && !judgementState.IsResolved(selected);
        double selectedSafeUntil = selectedIsUnresolved
            ? safeUntil(selected)
            : 0;

        int candidatePosition = nextUnresolvedPositions[lane];
        while (candidatePosition < laneObjects.Length
               && judgementState.IsResolved(laneObjects[candidatePosition]))
        {
            candidatePosition++;
        }
        nextUnresolvedPositions[lane] = candidatePosition;

        int candidate = candidatePosition < laneObjects.Length
            ? laneObjects[candidatePosition]
            : -1;
        return new GameplayKeysoundFastSelection(
            selected,
            selectedIsUnresolved,
            selectedSafeUntil,
            candidate,
            candidate < 0
                ? 0
                : beatmap.HitObjects[candidate].StartTimeMilliseconds
                  - judgementState.Windows.MehMilliseconds * 2,
            candidate < 0 ? 0 : safeUntil(candidate),
            laneObjects[0],
            laneObjects[^1]);
    }

    private double safeUntil(int hitObjectIndex)
    {
        YokkoHitObject hitObject = beatmap.HitObjects[hitObjectIndex];
        return (hitObject.EndTimeMilliseconds
                ?? hitObject.StartTimeMilliseconds)
               + judgementState.Windows.MehMilliseconds;
    }
}
