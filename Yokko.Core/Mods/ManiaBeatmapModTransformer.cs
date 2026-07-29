using Yokko.Core.Beatmaps;

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

        IReadOnlyList<YokkoHitObject> hitObjects = original.HitObjects;

        if (mods.Contains(ManiaModId.HoldOff))
        {
            hitObjects = hitObjects.Select(static hitObject =>
                    hitObject.Kind == HitObjectKind.Hold
                        ? new YokkoHitObject(
                            hitObject.Lane,
                            hitObject.StartTimeMilliseconds,
                            null,
                            HitObjectKind.Tap,
                            hitObject.SampleKey,
                            hitObject.ScrollProfileId)
                        : hitObject)
                .ToArray();
        }

        int laneCount = (int)original.KeyMode;
        int[]? laneMap = null;

        if (mods.Contains(ManiaModId.Random))
        {
            var random = new Random(mods.RandomSeed ?? 0);
            laneMap = Enumerable.Range(0, laneCount)
                                .OrderBy(_ => random.Next())
                                .ToArray();
        }

        if (mods.Contains(ManiaModId.Mirror))
        {
            laneMap ??= Enumerable.Range(0, laneCount).ToArray();
            for (int lane = 0; lane < laneMap.Length; lane++)
                laneMap[lane] = laneCount - 1 - laneMap[lane];
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
                    hitObject.ScrollProfileId);
            }).ToArray();
        }

        return ReferenceEquals(hitObjects, original.HitObjects)
            ? original
            : original with { HitObjects = hitObjects };
    }
}
