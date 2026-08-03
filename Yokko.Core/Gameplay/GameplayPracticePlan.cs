using Yokko.Core.Beatmaps;

namespace Yokko.Core.Gameplay;

public sealed record GameplayPracticePlan
{
    public double StartTimeMilliseconds { get; }
    public double EndTimeMilliseconds { get; }
    public int Repetitions { get; }

    public GameplayPracticePlan(
        double startTimeMilliseconds,
        double endTimeMilliseconds,
        int repetitions = 5)
    {
        if (!double.IsFinite(startTimeMilliseconds)
            || !double.IsFinite(endTimeMilliseconds)
            || startTimeMilliseconds < 0
            || endTimeMilliseconds - startTimeMilliseconds < 500)
        {
            throw new ArgumentOutOfRangeException(nameof(endTimeMilliseconds));
        }
        if (repetitions is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(repetitions));

        StartTimeMilliseconds = startTimeMilliseconds;
        EndTimeMilliseconds = endTimeMilliseconds;
        Repetitions = repetitions;
    }

    public YokkoBeatmap Slice(YokkoBeatmap beatmap)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        YokkoHitObject[] objects = beatmap.HitObjects
            .Where(hitObject =>
                hitObject.StartTimeMilliseconds >= StartTimeMilliseconds
                && hitObject.StartTimeMilliseconds < EndTimeMilliseconds)
            .ToArray();
        YokkoScheduledSample[] samples = beatmap.ScheduledSamples
            .Where(sample =>
                sample.TimeMilliseconds >= StartTimeMilliseconds
                && sample.TimeMilliseconds <= EndTimeMilliseconds)
            .ToArray();
        YokkoBreakPeriod[] breaks = beatmap.BreakPeriods
            .Where(period =>
                period.EndTimeMilliseconds >= StartTimeMilliseconds
                && period.StartTimeMilliseconds <= EndTimeMilliseconds)
            .ToArray();
        return beatmap with
        {
            DifficultyName = beatmap.DifficultyName + " · PRACTICE",
            HitObjects = objects,
            ScheduledSamples = samples,
            BreakPeriods = breaks,
        };
    }
}
