using Yokko.Core.Beatmaps;

namespace Yokko.Core.Scoring;

public sealed class BeatmapJudgementState
{
    private readonly YokkoBeatmap beatmap;
    private readonly bool[] resolvedHeads;
    private readonly bool[] resolvedTails;
    private readonly bool[] activeHolds;

    public BeatmapJudgementState(YokkoBeatmap beatmap, JudgementWindows windows)
    {
        this.beatmap = beatmap;
        Windows = windows;
        resolvedHeads = new bool[beatmap.HitObjects.Count];
        resolvedTails = new bool[beatmap.HitObjects.Count];
        activeHolds = new bool[beatmap.HitObjects.Count];

        for (int i = 0; i < beatmap.HitObjects.Count; i++)
        {
            if (beatmap.HitObjects[i].Kind == HitObjectKind.Tap)
                resolvedTails[i] = true;
        }
    }

    public JudgementWindows Windows { get; }

    public JudgementCounter Counts { get; } = new();

    public int Combo { get; private set; }

    public int MaxCombo { get; private set; }

    public int ResolvedObjectCount { get; private set; }

    public int TotalJudgementObjectCount => beatmap.HitObjects.Sum(static hitObject => hitObject.Kind switch
    {
        HitObjectKind.Tap => 1,
        HitObjectKind.Hold => 2,
        _ => 0,
    });

    public bool IsComplete => ResolvedObjectCount >= TotalJudgementObjectCount;

    public double Accuracy => Counts.Total == 0 ? 1 : Counts.WeightedAccuracy;

    public bool IsResolved(int hitObjectIndex)
        => resolvedHeads[hitObjectIndex] && resolvedTails[hitObjectIndex];

    public bool IsHeadResolved(int hitObjectIndex) => resolvedHeads[hitObjectIndex];

    public bool IsHoldActive(int hitObjectIndex) => activeHolds[hitObjectIndex];

    public JudgementEvent? TryJudgeLanePress(int lane, double gameplayTimeMilliseconds)
    {
        int bestIndex = -1;
        double bestAbsoluteError = double.MaxValue;

        for (int i = 0; i < beatmap.HitObjects.Count; i++)
        {
            YokkoHitObject hitObject = beatmap.HitObjects[i];

            if (resolvedHeads[i] || hitObject.Lane != lane || !isJudgementObject(hitObject))
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
        JudgementPhase phase = best.Kind == HitObjectKind.Hold
            ? JudgementPhase.HoldHead
            : JudgementPhase.Tap;
        return resolve(bestIndex, best.StartTimeMilliseconds, gameplayTimeMilliseconds, error, Windows.Judge(error), phase);
    }

    public JudgementEvent? TryJudgeLaneRelease(int lane, double gameplayTimeMilliseconds)
    {
        int bestIndex = -1;
        double earliestEndTime = double.MaxValue;

        for (int i = 0; i < beatmap.HitObjects.Count; i++)
        {
            YokkoHitObject hitObject = beatmap.HitObjects[i];

            if (hitObject.Kind != HitObjectKind.Hold
                || hitObject.Lane != lane
                || !activeHolds[i]
                || resolvedTails[i]
                || hitObject.EndTimeMilliseconds == null)
                continue;

            if (hitObject.EndTimeMilliseconds.Value < earliestEndTime)
            {
                bestIndex = i;
                earliestEndTime = hitObject.EndTimeMilliseconds.Value;
            }
        }

        if (bestIndex < 0)
            return null;

        double error = gameplayTimeMilliseconds - earliestEndTime;
        return resolve(
            bestIndex,
            earliestEndTime,
            gameplayTimeMilliseconds,
            error,
            Windows.Judge(error),
            JudgementPhase.HoldTail);
    }

    public IReadOnlyList<JudgementEvent> CollectExpiredMisses(double gameplayTimeMilliseconds)
    {
        List<JudgementEvent>? misses = null;

        for (int i = 0; i < beatmap.HitObjects.Count; i++)
        {
            YokkoHitObject hitObject = beatmap.HitObjects[i];

            if (!isJudgementObject(hitObject))
                continue;

            if (!resolvedHeads[i] && gameplayTimeMilliseconds > hitObject.StartTimeMilliseconds + Windows.BadMilliseconds)
            {
                misses ??= new List<JudgementEvent>();
                misses.Add(resolve(
                    i,
                    hitObject.StartTimeMilliseconds,
                    null,
                    gameplayTimeMilliseconds - hitObject.StartTimeMilliseconds,
                    JudgementRating.Miss,
                    hitObject.Kind == HitObjectKind.Hold ? JudgementPhase.HoldHead : JudgementPhase.Tap));
            }

            if (hitObject.Kind == HitObjectKind.Hold
                && !resolvedTails[i]
                && hitObject.EndTimeMilliseconds is double endTime
                && gameplayTimeMilliseconds > endTime + Windows.BadMilliseconds)
            {
                misses ??= new List<JudgementEvent>();
                misses.Add(resolve(
                    i,
                    endTime,
                    null,
                    gameplayTimeMilliseconds - endTime,
                    JudgementRating.Miss,
                    JudgementPhase.HoldTail));
            }
        }

        return misses ?? [];
    }

    private JudgementEvent resolve(
        int hitObjectIndex,
        double objectTimeMilliseconds,
        double? hitTimeMilliseconds,
        double hitErrorMilliseconds,
        JudgementRating rating,
        JudgementPhase phase)
    {
        YokkoHitObject hitObject = beatmap.HitObjects[hitObjectIndex];

        switch (phase)
        {
            case JudgementPhase.Tap:
                resolvedHeads[hitObjectIndex] = true;
                break;

            case JudgementPhase.HoldHead:
                resolvedHeads[hitObjectIndex] = true;
                activeHolds[hitObjectIndex] = rating != JudgementRating.Miss;
                break;

            case JudgementPhase.HoldTail:
                resolvedTails[hitObjectIndex] = true;
                activeHolds[hitObjectIndex] = false;
                break;
        }

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
            objectTimeMilliseconds,
            hitTimeMilliseconds,
            hitErrorMilliseconds,
            rating,
            phase);
    }

    private static bool isJudgementObject(YokkoHitObject hitObject)
        => hitObject.Kind is HitObjectKind.Tap or HitObjectKind.Hold;
}
