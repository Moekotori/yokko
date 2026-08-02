using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Timing;

namespace Yokko.Core.Mods;

/// <summary>
/// Applies deterministic osu!mania beatmap conversion Mods without mutating
/// the imported chart.
///
/// Mirror, Random and Hold Off follow ppy/osu's Mania implementations at
/// commit 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0 (MIT).
/// </summary>
public static class ManiaBeatmapModTransformer
{
    public static YokkoBeatmap Apply(
        YokkoBeatmap original,
        ManiaModSet? mods)
    {
        ArgumentNullException.ThrowIfNull(original);
        mods ??= ManiaModSet.Empty;
        if (mods.IsEmpty)
            return original;

        KeyMode keyMode = original.KeyMode;
        int stageCount = original.StageCount;
        IReadOnlyList<YokkoHitObject> hitObjects = original.HitObjects;
        IReadOnlyList<YokkoBreakPeriod> breakPeriods =
            original.BreakPeriods;
        if (original.ConversionSource is not null
            && (mods.KeyConversionTarget is not null
                || mods.HasDualStages))
        {
            int columnsPerStage = mods.KeyConversionTarget
                                  ?? OsuStandardManiaConverter
                                     .DetermineDefaultColumnCount(
                                         original.ConversionSource);
            stageCount = mods.HasDualStages ? 2 : 1;
            int totalColumns = columnsPerStage * stageCount;
            keyMode = (KeyMode)totalColumns;
            hitObjects = OsuStandardManiaConverter.Convert(
                original.ConversionSource,
                totalColumns,
                original.TimingPoints);
        }

        if (mods.Contains(ManiaModId.Invert))
        {
            var timing = new BeatTimingMap(
                original.TimingPoints,
                1);
            hitObjects = hitObjects
                                 .Where(static hitObject =>
                                     hitObject.Kind is
                                         HitObjectKind.Tap
                                         or HitObjectKind.Hold)
                                 .GroupBy(static hitObject =>
                                     hitObject.Lane)
                                 .SelectMany(column =>
                                 {
                                     YokkoHitObject[] locations =
                                         column.OrderBy(
                                                   static hitObject =>
                                                       hitObject
                                                           .StartTimeMilliseconds)
                                               .ToArray();
                                     return locations
                                            .Take(
                                                Math.Max(
                                                    0,
                                                    locations.Length - 1))
                                            .Select((current, index) =>
                                            {
                                                double nextTime =
                                                    locations[index + 1]
                                                        .StartTimeMilliseconds;
                                                double gap =
                                                    nextTime
                                                    - current
                                                        .StartTimeMilliseconds;
                                                double beatLength =
                                                    timing.TimingPointAt(
                                                        nextTime)
                                                          .BeatLengthMilliseconds;
                                                double duration = Math.Max(
                                                    gap / 2,
                                                    gap - beatLength / 4);
                                                return new YokkoHitObject(
                                                    current.Lane,
                                                    current
                                                        .StartTimeMilliseconds,
                                                    current
                                                        .StartTimeMilliseconds
                                                    + duration,
                                                    HitObjectKind.Hold,
                                                    current.SampleKey,
                                                    current.ScrollProfileId,
                                                    current.SamplePayload);
                                            });
                                 })
                                 .OrderBy(static hitObject =>
                                     hitObject.StartTimeMilliseconds)
                                 .ThenBy(static hitObject =>
                                     hitObject.Lane)
                                 .ToArray();
            // lazer's ManiaModInvert removes every break after replacing the
            // original objects with the continuous inverted hold pattern.
            breakPeriods = [];
        }
        else if (mods.Contains(ManiaModId.HoldOff))
        {
            hitObjects = hitObjects.Select(static hitObject =>
                    hitObject.Kind == HitObjectKind.Hold
                        ? new YokkoHitObject(
                            hitObject.Lane,
                            hitObject.StartTimeMilliseconds,
                            null,
                            HitObjectKind.Tap,
                            hitObject.SampleKey,
                            hitObject.ScrollProfileId,
                            hitObject.SamplePayload)
                        : hitObject)
                .ToArray();
        }

        int laneCount = (int)keyMode;
        HashSet<int> scratchLanes = original.ScratchLanes.ToHashSet();
        int[] movableLanes = Enumerable.Range(0, laneCount)
                                       .Where(lane => !scratchLanes.Contains(lane))
                                       .ToArray();
        int[]? laneMap = null;

        if (mods.Contains(ManiaModId.Random))
        {
            var random = new Random(mods.RandomSeed ?? 0);
            int[] shuffled = movableLanes
                             .OrderBy(_ => random.Next())
                             .ToArray();
            laneMap = Enumerable.Range(0, laneCount).ToArray();
            for (int index = 0; index < movableLanes.Length; index++)
                laneMap[movableLanes[index]] = shuffled[index];
        }

        if (mods.Contains(ManiaModId.Mirror))
        {
            laneMap ??= Enumerable.Range(0, laneCount).ToArray();
            var mirroredTargets = movableLanes
                                  .Select((lane, index) => (
                                      lane,
                                      target: movableLanes[
                                          movableLanes.Length - 1 - index]))
                                  .ToDictionary(
                                      static pair => pair.lane,
                                      static pair => pair.target);
            foreach (int lane in movableLanes)
                laneMap[lane] = mirroredTargets[laneMap[lane]];
        }

        if (laneMap != null)
        {
            hitObjects = hitObjects.Select(hitObject =>
            {
                if ((uint)hitObject.Lane >= laneMap.Length)
                    return hitObject;

                return new YokkoHitObject(
                    laneMap[hitObject.Lane],
                    hitObject.StartTimeMilliseconds,
                    hitObject.EndTimeMilliseconds,
                    hitObject.Kind,
                    hitObject.SampleKey,
                    hitObject.ScrollProfileId,
                    hitObject.SamplePayload,
                    hitObject.HoldType);
            }).ToArray();
        }

        YokkoBeatmap structurallyApplied =
            ReferenceEquals(hitObjects, original.HitObjects)
            && ReferenceEquals(breakPeriods, original.BreakPeriods)
            && keyMode == original.KeyMode
                ? original
                : original with
                {
                    KeyMode = keyMode,
                    HitObjects = hitObjects,
                    StageCount = stageCount,
                    BreakPeriods = breakPeriods,
                };
        if (!mods.Contains(ManiaModId.DifficultyAdjust))
            return structurallyApplied;

        return new YokkoBeatmap(
            structurallyApplied.Title,
            structurallyApplied.Artist,
            structurallyApplied.Creator,
            structurallyApplied.DifficultyName,
            structurallyApplied.KeyMode,
            structurallyApplied.SourceFormat,
            structurallyApplied.TimingPoints,
            structurallyApplied.AudioPath,
            structurallyApplied.HitObjects,
            mods.EffectiveOverallDifficulty(
                structurallyApplied.OverallDifficulty),
            structurallyApplied.ScrollVelocities,
            structurallyApplied.InitialScrollVelocity,
            structurallyApplied.ScrollSpeedFactors,
            structurallyApplied.ScrollProfiles,
            mods.EffectiveDrainRate(
                structurallyApplied.DrainRate),
            structurallyApplied.ConversionSource,
            structurallyApplied.StageCount,
            structurallyApplied.PreviewTimeMilliseconds,
            structurallyApplied.BreakPeriods,
            structurallyApplied.LegacyLongNoteRendering,
            structurallyApplied.ScheduledSamples,
            structurallyApplied.ScratchLane);
    }
}
