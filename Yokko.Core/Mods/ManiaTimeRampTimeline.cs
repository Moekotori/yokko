using Yokko.Core.Beatmaps;

namespace Yokko.Core.Mods;

public static class ManiaTimeRampTimeline
{
    public static double ToRealTime(
        double chartTimeMilliseconds,
        double firstObjectTimeMilliseconds,
        double lastObjectTimeMilliseconds,
        double initialRate,
        double finalRate)
    {
        if (chartTimeMilliseconds <= 0)
            return chartTimeMilliseconds / initialRate;

        double rampStart = Math.Max(0, firstObjectTimeMilliseconds);
        double rampEnd = rampStart
                         + 0.75
                         * Math.Max(
                             0,
                             lastObjectTimeMilliseconds - rampStart);
        double beforeRamp =
            Math.Min(chartTimeMilliseconds, rampStart) / initialRate;
        if (chartTimeMilliseconds <= rampStart)
            return beforeRamp;

        double clampedRampEnd =
            Math.Min(chartTimeMilliseconds, rampEnd);
        double rampDuration = rampEnd - rampStart;
        double withinRamp;
        if (rampDuration <= 0
            || Math.Abs(finalRate - initialRate) < 0.0000001)
        {
            withinRamp =
                (clampedRampEnd - rampStart) / initialRate;
        }
        else
        {
            double slope =
                (finalRate - initialRate) / rampDuration;
            double endingRate = initialRate
                                + slope
                                * (clampedRampEnd - rampStart);
            withinRamp =
                Math.Log(endingRate / initialRate) / slope;
        }

        double afterRamp = chartTimeMilliseconds > rampEnd
            ? (chartTimeMilliseconds - rampEnd) / finalRate
            : 0;
        return beforeRamp + withinRamp + afterRamp;
    }

    public static YokkoBeatmap TransformForDifficulty(
        YokkoBeatmap beatmap,
        ManiaModSet mods)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        ArgumentNullException.ThrowIfNull(mods);
        if (!mods.HasTimeRamp || beatmap.HitObjects.Count == 0)
            return beatmap;

        double first = beatmap.HitObjects.Min(
            static hitObject => hitObject.StartTimeMilliseconds);
        double last = beatmap.HitObjects.Max(
            static hitObject =>
                hitObject.EndTimeMilliseconds
                ?? hitObject.StartTimeMilliseconds);
        return beatmap with
        {
            HitObjects = beatmap.HitObjects
                .Select(hitObject => new YokkoHitObject(
                    hitObject.Lane,
                    ToRealTime(
                        hitObject.StartTimeMilliseconds,
                        first,
                        last,
                        mods.TimeRampInitialRate,
                        mods.TimeRampFinalRate),
                    hitObject.EndTimeMilliseconds is double end
                        ? ToRealTime(
                            end,
                            first,
                            last,
                            mods.TimeRampInitialRate,
                            mods.TimeRampFinalRate)
                        : null,
                    hitObject.Kind,
                    hitObject.SampleKey,
                    hitObject.ScrollProfileId,
                    hitObject.SamplePayload))
                .ToArray(),
        };
    }
}
