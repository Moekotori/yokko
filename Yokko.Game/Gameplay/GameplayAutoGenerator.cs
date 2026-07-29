using System;
using System.Collections.Generic;
using System.Linq;
using Yokko.Core.Beatmaps;

namespace Yokko.Game.Gameplay;

/// <summary>
/// Generates deterministic osu!mania-style automatic input.
///
/// Adapted from ppy/osu,
/// osu.Game.Rulesets.Mania/Replays/ManiaAutoGenerator.cs,
/// commit 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0 (MIT).
/// </summary>
internal static class GameplayAutoGenerator
{
    internal const double ReleaseDelayMilliseconds = 20;

    public static GameplayReplay Generate(YokkoBeatmap beatmap)
    {
        ArgumentNullException.ThrowIfNull(beatmap);

        YokkoHitObject[] playable = beatmap.HitObjects
                                              .Where(static hitObject =>
                                                  hitObject.Kind
                                                  is HitObjectKind.Tap
                                                  or HitObjectKind.Hold)
                                              .ToArray();
        var actionPoints = new List<ActionPoint>(playable.Length * 2);
        int sequence = 0;

        for (int index = 0; index < playable.Length; index++)
        {
            YokkoHitObject current = playable[index];
            YokkoHitObject next = findNextInLane(
                playable,
                index,
                current.Lane);
            double releaseTime = calculateReleaseTime(current, next);

            actionPoints.Add(new ActionPoint(
                current.StartTimeMilliseconds,
                sequence++,
                current.Lane,
                true));
            actionPoints.Add(new ActionPoint(
                releaseTime,
                sequence++,
                current.Lane,
                false));
        }

        return new GameplayReplay(
            actionPoints.OrderBy(static point => point.TimeMilliseconds)
                        .ThenBy(static point => point.Sequence)
                        .Select(static point => new GameplayReplayInput(
                            point.Lane,
                            point.IsPressed,
                            point.TimeMilliseconds)));
    }

    private static YokkoHitObject findNextInLane(
        IReadOnlyList<YokkoHitObject> hitObjects,
        int currentIndex,
        int lane)
    {
        for (int index = currentIndex + 1;
             index < hitObjects.Count;
             index++)
        {
            if (hitObjects[index].Lane == lane)
                return hitObjects[index];
        }

        return null;
    }

    private static double calculateReleaseTime(
        YokkoHitObject current,
        YokkoHitObject next)
    {
        double endTime = current.EndTimeMilliseconds
                         ?? current.StartTimeMilliseconds;
        double releaseDelay = ReleaseDelayMilliseconds;

        if (current.Kind == HitObjectKind.Hold)
        {
            if (endTime > current.StartTimeMilliseconds)
                return endTime;

            releaseDelay = 1;
        }

        bool canDelayFully = next == null
                             || next.StartTimeMilliseconds
                             > endTime + releaseDelay;
        return canDelayFully
            ? endTime + releaseDelay
            : endTime
              + (next!.StartTimeMilliseconds - endTime) * 0.9;
    }

    private readonly record struct ActionPoint(
        double TimeMilliseconds,
        int Sequence,
        int Lane,
        bool IsPressed);
}
