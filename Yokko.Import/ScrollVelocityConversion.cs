using Yokko.Core.Beatmaps;
using Yokko.Core.Timing;

namespace Yokko.Import;

internal static class ScrollVelocityConversion
{
    public static ScrollVelocityProfile FromOsu(
        IReadOnlyList<YokkoTimingPoint> timingPoints,
        IReadOnlyList<YokkoHitObject> hitObjects,
        bool applyInheritedScrollSpeed = true)
    {
        YokkoTimingPoint[] relevantPoints = relevantTimingPoints(timingPoints, hitObjects);

        if (relevantPoints.Length == 0)
            return ScrollVelocityProfile.Default;

        double baseBeatLength = MostCommonBeatLength(relevantPoints, hitObjects);
        double currentBeatLength = 1000;
        double currentInheritedMultiplier = 1;
        var changes = new List<YokkoScrollVelocity>();
        double? initialMultiplier = null;
        double? previousMultiplier = null;

        foreach (IGrouping<double, YokkoTimingPoint> group in relevantPoints
                                                               .OrderBy(static point => point.TimeMilliseconds)
                                                               .GroupBy(static point => point.TimeMilliseconds))
        {
            foreach (YokkoTimingPoint point in group)
            {
                if (point.Uninherited)
                {
                    if (double.IsFinite(point.BeatLengthMilliseconds)
                        && point.BeatLengthMilliseconds > 0)
                    {
                        currentBeatLength = point.BeatLengthMilliseconds;
                    }

                    currentInheritedMultiplier = 1;
                }
                else if (applyInheritedScrollSpeed
                         && double.IsFinite(point.BeatLengthMilliseconds)
                         && point.BeatLengthMilliseconds < 0)
                {
                    currentInheritedMultiplier = Math.Clamp(
                        100 / -point.BeatLengthMilliseconds,
                        0.01,
                        10);
                }
            }

            double multiplier =
                currentInheritedMultiplier * baseBeatLength / currentBeatLength;

            if (initialMultiplier == null)
            {
                initialMultiplier = multiplier;
                previousMultiplier = multiplier;
                continue;
            }

            if (multiplier.Equals(previousMultiplier))
                continue;

            changes.Add(new YokkoScrollVelocity(group.Key, multiplier));
            previousMultiplier = multiplier;
        }

        return new ScrollVelocityProfile(initialMultiplier ?? 1, changes);
    }

    public static ScrollVelocityProfile FromQuaver(
        IReadOnlyList<YokkoTimingPoint> timingPoints,
        IReadOnlyList<YokkoHitObject> hitObjects,
        IReadOnlyList<YokkoScrollVelocity> sliderVelocities,
        bool bpmDoesNotAffectScrollVelocity,
        double initialScrollVelocity)
    {
        if (bpmDoesNotAffectScrollVelocity)
        {
            return new ScrollVelocityProfile(
                initialScrollVelocity,
                sliderVelocities.OrderBy(static velocity => velocity.TimeMilliseconds)
                                .GroupBy(static velocity => velocity.TimeMilliseconds)
                                .Select(static group => group.Last())
                                .ToArray());
        }

        YokkoTimingPoint[] relevantPoints = relevantTimingPoints(
                                                 timingPoints,
                                                 hitObjects)
                                             .Where(static point =>
                                                 point.Uninherited
                                                 && !double.IsNaN(
                                                     point.BeatLengthMilliseconds)
                                                 && point.BeatLengthMilliseconds
                                                 >= 0)
                                             .OrderBy(static point =>
                                                 point.TimeMilliseconds)
                                             .ToArray();

        if (relevantPoints.Length == 0)
            relevantPoints = [YokkoTimingPoint.Default];

        YokkoScrollVelocity[] orderedSliderVelocities = sliderVelocities
                                                        .OrderBy(static velocity =>
                                                            velocity.TimeMilliseconds)
                                                        .ToArray();
        double baseBeatLength = mostCommonQuaverBeatLength(
            relevantPoints,
            hitObjects);
        double currentBeatLength =
            relevantPoints[0].BeatLengthMilliseconds;
        int currentSliderVelocityIndex = 0;
        double? currentSliderVelocityStartTime = null;
        double currentSliderVelocity = 1;
        double? currentAdjustedMultiplier = null;
        double? normalizedInitialMultiplier = null;
        var changes = new List<YokkoScrollVelocity>();

        for (int i = 0; i < relevantPoints.Length; i++)
        {
            YokkoTimingPoint timingPoint = relevantPoints[i];
            bool nextTimingPointHasSameTimestamp =
                i + 1 < relevantPoints.Length
                && relevantPoints[i + 1].TimeMilliseconds
                == timingPoint.TimeMilliseconds;

            while (currentSliderVelocityIndex
                   < orderedSliderVelocities.Length)
            {
                YokkoScrollVelocity sliderVelocity =
                    orderedSliderVelocities[currentSliderVelocityIndex];

                if (sliderVelocity.TimeMilliseconds
                    > timingPoint.TimeMilliseconds)
                {
                    break;
                }

                if (nextTimingPointHasSameTimestamp
                    && sliderVelocity.TimeMilliseconds
                    == timingPoint.TimeMilliseconds)
                {
                    break;
                }

                if (sliderVelocity.TimeMilliseconds
                    < timingPoint.TimeMilliseconds)
                {
                    double multiplier =
                        adjustedQuaverMultiplier(
                            sliderVelocity.Multiplier,
                            baseBeatLength,
                            currentBeatLength);
                    recordMultiplier(
                        sliderVelocity.TimeMilliseconds,
                        multiplier);
                }

                currentSliderVelocityStartTime =
                    sliderVelocity.TimeMilliseconds;
                currentSliderVelocity = sliderVelocity.Multiplier;
                currentSliderVelocityIndex++;
            }

            if (currentSliderVelocityStartTime == null
                || currentSliderVelocityStartTime
                < timingPoint.TimeMilliseconds)
            {
                currentSliderVelocity = 1;
            }

            currentBeatLength = timingPoint.BeatLengthMilliseconds;
            recordMultiplier(
                timingPoint.TimeMilliseconds,
                adjustedQuaverMultiplier(
                    currentSliderVelocity,
                    baseBeatLength,
                    currentBeatLength));
        }

        for (; currentSliderVelocityIndex
               < orderedSliderVelocities.Length;
             currentSliderVelocityIndex++)
        {
            YokkoScrollVelocity sliderVelocity =
                orderedSliderVelocities[currentSliderVelocityIndex];
            recordMultiplier(
                sliderVelocity.TimeMilliseconds,
                adjustedQuaverMultiplier(
                    sliderVelocity.Multiplier,
                    baseBeatLength,
                    currentBeatLength));
        }

        return new ScrollVelocityProfile(
            normalizedInitialMultiplier ?? 1,
            changes.OrderBy(static velocity => velocity.TimeMilliseconds)
                   .GroupBy(static velocity => velocity.TimeMilliseconds)
                   .Select(static group => group.Last())
                   .ToArray());

        void recordMultiplier(double timeMilliseconds, double multiplier)
        {
            if (currentAdjustedMultiplier == null)
            {
                currentAdjustedMultiplier = multiplier;
                normalizedInitialMultiplier = multiplier;
                return;
            }

            if (multiplier.Equals(currentAdjustedMultiplier.Value))
                return;

            changes.Add(new YokkoScrollVelocity(
                timeMilliseconds,
                multiplier));
            currentAdjustedMultiplier = multiplier;
        }
    }

    private static YokkoTimingPoint[] relevantTimingPoints(
        IReadOnlyList<YokkoTimingPoint> timingPoints,
        IReadOnlyList<YokkoHitObject> hitObjects)
    {
        double lastTime = hitObjects.Count == 0
            ? timingPoints.LastOrDefault()?.TimeMilliseconds ?? 0
            : hitObjects.Max(static hitObject =>
                hitObject.EndTimeMilliseconds ?? hitObject.StartTimeMilliseconds);

        return timingPoints.Where(point => point.TimeMilliseconds <= lastTime)
                           .ToArray();
    }

    internal static double MostCommonBeatLength(
        IReadOnlyList<YokkoTimingPoint> timingPoints,
        IReadOnlyList<YokkoHitObject> hitObjects)
    {
        YokkoTimingPoint[] activePoints = timingPoints
                                          .Where(static point =>
                                              point.Uninherited
                                              && !double.IsNaN(
                                                  point.BeatLengthMilliseconds)
                                              && point.BeatLengthMilliseconds
                                              >= 0)
                                          .OrderBy(static point => point.TimeMilliseconds)
                                          .GroupBy(static point => point.TimeMilliseconds)
                                          .Select(static group => group.Last())
                                          .ToArray();

        if (activePoints.Length == 0)
            return YokkoTimingPoint.Default.BeatLengthMilliseconds;

        if (hitObjects.Count == 0)
            return activePoints[0].BeatLengthMilliseconds;

        double lastTime = hitObjects.Count == 0
            ? activePoints[^1].TimeMilliseconds
            : hitObjects.Max(static hitObject =>
                hitObject.EndTimeMilliseconds ?? hitObject.StartTimeMilliseconds);

        var durations =
            new Dictionary<
                double,
                (double Duration, double BeatLength)>();

        for (int i = 0; i < activePoints.Length; i++)
        {
            YokkoTimingPoint point = activePoints[i];

            if (point.TimeMilliseconds > lastTime)
                continue;

            double currentTime = i == 0 ? 0 : point.TimeMilliseconds;
            double nextTime = i == activePoints.Length - 1
                ? lastTime
                : Math.Min(lastTime, activePoints[i + 1].TimeMilliseconds);
            double roundedBeatLength =
                Math.Round(point.BeatLengthMilliseconds * 1000) / 1000;
            double duration = Math.Max(0, nextTime - currentTime);

            (double accumulatedDuration, double representativeBeatLength) =
                durations.GetValueOrDefault(
                    roundedBeatLength,
                    (0, point.BeatLengthMilliseconds));
            durations[roundedBeatLength] = (
                accumulatedDuration + duration,
                representativeBeatLength);
        }

        return durations.Count == 0
            ? activePoints[0].BeatLengthMilliseconds
            : durations.OrderByDescending(
                           static pair => pair.Value.Duration)
                       .First()
                       .Value.BeatLength;
    }

    private static double mostCommonQuaverBeatLength(
        IReadOnlyList<YokkoTimingPoint> timingPoints,
        IReadOnlyList<YokkoHitObject> hitObjects)
    {
        YokkoTimingPoint[] activePoints = timingPoints
                                          .Where(static point =>
                                              point.Uninherited
                                              && !double.IsNaN(
                                                  point.BeatLengthMilliseconds)
                                              && point.BeatLengthMilliseconds
                                              >= 0)
                                          .OrderBy(static point =>
                                              point.TimeMilliseconds)
                                          .ToArray();

        if (activePoints.Length == 0)
            return YokkoTimingPoint.Default.BeatLengthMilliseconds;

        if (hitObjects.Count == 0)
            return activePoints[0].BeatLengthMilliseconds;

        double lastTime = hitObjects.Max(static hitObject =>
            hitObject.EndTimeMilliseconds ?? hitObject.StartTimeMilliseconds);
        var durations = new Dictionary<double, int>();

        // Quaver walks timing points backwards. Dictionary insertion order is
        // therefore also its tie-breaker: the latest equally-long BPM wins.
        for (int i = activePoints.Length - 1; i >= 0; i--)
        {
            YokkoTimingPoint point = activePoints[i];

            if (point.TimeMilliseconds > lastTime)
                continue;

            int duration = (int)(
                lastTime - (i == 0 ? 0 : point.TimeMilliseconds));
            lastTime = point.TimeMilliseconds;
            durations[point.BeatLengthMilliseconds] =
                durations.GetValueOrDefault(
                    point.BeatLengthMilliseconds)
                + duration;
        }

        return durations.Count == 0
            ? activePoints[0].BeatLengthMilliseconds
            : durations.OrderByDescending(static pair => pair.Value)
                       .First()
                       .Key;
    }

    private static double adjustedQuaverMultiplier(
        double sliderVelocity,
        double baseBeatLength,
        double currentBeatLength)
    {
        // Quaver treats infinite BPM as an arbitrarily large 128x SV so the
        // normalized representation stays finite and round-trippable.
        if (currentBeatLength == 0)
            return 128;

        // A zero-BPM point stops the visual timeline.
        if (double.IsPositiveInfinity(currentBeatLength))
            return 0;

        // If infinite BPM is itself the common BPM, finite sections normalize
        // to zero just as currentBpm / positive-infinity does upstream.
        if (baseBeatLength == 0)
            return 0;

        double multiplier =
            sliderVelocity * baseBeatLength / currentBeatLength;

        // A zero common BPM makes finite BPM sections infinitely fast in
        // Quaver's normalized representation. Yokko's timing model is
        // intentionally finite, so retain direction and use the same 128x
        // sentinel Quaver uses for infinite-BPM sections.
        if (double.IsInfinity(multiplier))
            return sliderVelocity < 0 ? -128 : 128;

        if (double.IsNaN(multiplier))
            return 0;

        return multiplier;
    }
}

internal sealed record ScrollVelocityProfile(
    double InitialMultiplier,
    IReadOnlyList<YokkoScrollVelocity> Changes)
{
    public static ScrollVelocityProfile Default { get; } = new(1, []);
}
