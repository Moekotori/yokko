using Yokko.Core.Beatmaps;

namespace Yokko.Core.Scoring;

public sealed class BeatmapJudgementState
{
    private readonly YokkoBeatmap beatmap;
    private readonly bool[] resolvedHitObjects;

    public BeatmapJudgementState(YokkoBeatmap beatmap, JudgementWindows windows)
    {
        this.beatmap = beatmap;
        Windows = windows;
        resolvedHitObjects = new bool[beatmap.HitObjects.Count];
    }

    public JudgementWindows Windows { get; }

    public JudgementCounter Counts { get; } = new();

    public int Combo { get; private set; }

    public int MaxCombo { get; private set; }

    public int ResolvedObjectCount { get; private set; }

    public int TotalJudgementObjectCount => beatmap.HitObjects.Count(static hitObject => hitObject.Kind is HitObjectKind.Tap or HitObjectKind.Hold);

    public bool IsComplete => ResolvedObjectCount >= TotalJudgementObjectCount;

    public double Accuracy => Counts.Total == 0 ? 1 : Counts.WeightedAccuracy;

    public bool IsResolved(int hitObjectIndex) => resolvedHitObjects[hitObjectIndex];

    public JudgementEvent? TryJudgeLanePress(int lane, double gameplayTimeMilliseconds)
    {
        int bestIndex = -1;
        double bestAbsoluteError = double.MaxValue;

        for (int i = 0; i < beatmap.HitObjects.Count; i++)
        {
            YokkoHitObject hitObject = beatmap.HitObjects[i];

            if (resolvedHitObjects[i] || hitObject.Lane != lane || !isJudgementObject(hitObject))
                continue;

            double hitError = gameplayTimeMilliseconds - hitObject.StartTimeMilliseconds;
            double absoluteError = Math.Abs(hitError);

            if (absoluteError <= Windows.BadMilliseconds && absoluteError < bestAbsoluteError)
            {
                bestIndex = i;
                bestAbsoluteError = absoluteError;
            }
        }

        if (bestIndex < 0)
            return null;

        YokkoHitObject best = beatmap.HitObjects[bestIndex];
        double error = gameplayTimeMilliseconds - best.StartTimeMilliseconds;
        return resolve(bestIndex, gameplayTimeMilliseconds, error, Windows.Judge(error));
    }

    public IReadOnlyList<JudgementEvent> CollectExpiredMisses(double gameplayTimeMilliseconds)
    {
        List<JudgementEvent>? misses = null;

        for (int i = 0; i < beatmap.HitObjects.Count; i++)
        {
            YokkoHitObject hitObject = beatmap.HitObjects[i];

            if (resolvedHitObjects[i] || !isJudgementObject(hitObject))
                continue;

            if (gameplayTimeMilliseconds <= hitObject.StartTimeMilliseconds + Windows.BadMilliseconds)
                continue;

            misses ??= new List<JudgementEvent>();
            misses.Add(resolve(i, null, 0, JudgementRating.Miss));
        }

        return misses ?? [];
    }

    private JudgementEvent resolve(int hitObjectIndex, double? hitTimeMilliseconds, double hitErrorMilliseconds, JudgementRating rating)
    {
        YokkoHitObject hitObject = beatmap.HitObjects[hitObjectIndex];
        resolvedHitObjects[hitObjectIndex] = true;
        ResolvedObjectCount++;
        Counts.Add(rating);

        if (rating == JudgementRating.Miss)
        {
            Combo = 0;
        }
        else
        {
            Combo++;
            MaxCombo = Math.Max(MaxCombo, Combo);
        }

        return new JudgementEvent(
            hitObjectIndex,
            hitObject.Lane,
            hitObject.StartTimeMilliseconds,
            hitTimeMilliseconds,
            hitErrorMilliseconds,
            rating);
    }

    private static bool isJudgementObject(YokkoHitObject hitObject)
        => hitObject.Kind is HitObjectKind.Tap or HitObjectKind.Hold;
}
