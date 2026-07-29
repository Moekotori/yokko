using Yokko.Core.Beatmaps;

namespace Yokko.Core.Scoring;

/// <summary>
/// Deterministic, UI-independent osu!lazer mania judgement state.
/// Behaviour is ported from ppy/osu's DrawableNote, DrawableHoldNote,
/// DrawableHoldNoteTail and OrderedHitPolicy at
/// cb3d5da8b441afd8d2cf3e03ceebc6b027e2074d (MIT).
/// </summary>
public sealed class BeatmapJudgementState
{
    public const double HoldReleaseWindowLenience = 1.5;

    private readonly YokkoBeatmap beatmap;
    private readonly ObjectState[] states;
    private readonly int[][] laneObjectIndices;
    private readonly int[] nextLaneObjectIndices;
    private readonly int[] nextHeadPositions;
    private readonly int[] nextForceMissPositions;
    private readonly HashSet<int>[] openHoldIndices;
    private readonly ExpirationEntry[] expirations;
    private readonly ManiaScoreProcessor scoreProcessor;
    private readonly int totalJudgementObjectCount;
    private readonly bool noRelease;
    private int nextExpiration;
    private int resolvedJudgementObjectCount;

    public BeatmapJudgementState(
        YokkoBeatmap beatmap,
        JudgementWindows? windows = null,
        bool noRelease = false)
    {
        this.beatmap = beatmap;
        this.noRelease = noRelease;
        Windows = windows ?? new JudgementWindows(beatmap.OverallDifficulty);
        states = beatmap.HitObjects.Select(static hitObject => new ObjectState(hitObject)).ToArray();
        scoreProcessor = new ManiaScoreProcessor(beatmap);
        totalJudgementObjectCount =
            beatmap.HitObjects.Count(isJudgementObject);

        laneObjectIndices = Enumerable.Range(0, (int)beatmap.KeyMode)
                                      .Select(lane => beatmap.HitObjects
                                                             .Select((hitObject, index) => (hitObject, index))
                                                             .Where(item => item.hitObject.Lane == lane
                                                                            && isJudgementObject(item.hitObject))
                                                             .OrderBy(item => item.hitObject.StartTimeMilliseconds)
                                                             .ThenBy(item => item.index)
                                                             .Select(item => item.index)
                                                             .ToArray())
                                      .ToArray();

        nextLaneObjectIndices = Enumerable.Repeat(-1, beatmap.HitObjects.Count).ToArray();
        foreach (int[] laneIndices in laneObjectIndices)
        {
            for (int i = 0; i < laneIndices.Length - 1; i++)
                nextLaneObjectIndices[laneIndices[i]] = laneIndices[i + 1];
        }

        nextHeadPositions = new int[laneObjectIndices.Length];
        nextForceMissPositions = new int[laneObjectIndices.Length];
        openHoldIndices = Enumerable.Range(0, laneObjectIndices.Length)
                                    .Select(_ => new HashSet<int>())
                                    .ToArray();

        expirations = beatmap.HitObjects
                             .SelectMany((hitObject, index) =>
                             {
                                 if (!isJudgementObject(hitObject))
                                     return [];

                                 var entries = new List<ExpirationEntry>
                                 {
                                     new(
                                         hitObject.StartTimeMilliseconds
                                         + Windows.MehMilliseconds,
                                         index,
                                         hitObject.Kind == HitObjectKind.Hold
                                             ? JudgementPhase.HoldHead
                                             : JudgementPhase.Tap),
                                 };

                                 if (hitObject.Kind == HitObjectKind.Hold
                                     && hitObject.EndTimeMilliseconds is double endTime)
                                 {
                                     entries.Add(new ExpirationEntry(
                                         endTime
                                         + Windows.MehMilliseconds
                                         * HoldReleaseWindowLenience,
                                         index,
                                         JudgementPhase.HoldTail));
                                 }

                                 return entries;
                             })
                             .OrderBy(entry => entry.TimeMilliseconds)
                             .ThenBy(entry =>
                                 beatmap.HitObjects[entry.HitObjectIndex].Lane)
                             .ThenBy(entry => entry.Phase)
                             .ToArray();
    }

    public JudgementWindows Windows { get; }

    public JudgementCounter Counts => scoreProcessor.Counts;

    public int Combo => scoreProcessor.Combo;

    public int MaxCombo => scoreProcessor.MaxCombo;

    public double Accuracy => scoreProcessor.Accuracy;

    public double MaximumAchievableAccuracy =>
        scoreProcessor.MaximumAchievableAccuracy;

    public long Score => scoreProcessor.TotalScore;

    public ScoreRank Rank => scoreProcessor.Rank;

    public ManiaScoreResult CreateResult() => new(
        Score,
        Accuracy,
        MaxCombo,
        Rank,
        Counts.Perfect,
        Counts.Great,
        Counts.Good,
        Counts.Ok,
        Counts.Meh,
        Counts.Miss);

    public int ResolvedObjectCount => resolvedJudgementObjectCount;

    public int TotalJudgementObjectCount =>
        totalJudgementObjectCount;

    public bool IsComplete =>
        ResolvedObjectCount == TotalJudgementObjectCount;

    public bool IsResolved(int hitObjectIndex) => states[hitObjectIndex].IsComplete;

    public bool IsHeadResolved(int hitObjectIndex) => states[hitObjectIndex].HeadResolved;

    public bool IsHoldActive(int hitObjectIndex) => states[hitObjectIndex].Holding;

    public JudgementEvent? TryJudgeLanePress(int lane, double gameplayTimeMilliseconds)
        => JudgeLanePress(lane, gameplayTimeMilliseconds).FirstOrDefault();

    public JudgementEvent? TryJudgeLaneRelease(int lane, double gameplayTimeMilliseconds)
        => JudgeLaneRelease(lane, gameplayTimeMilliseconds).FirstOrDefault();

    public IReadOnlyList<JudgementEvent> JudgeLanePress(
        int lane,
        double gameplayTimeMilliseconds)
    {
        if ((uint)lane >= laneObjectIndices.Length)
            return [];

        var events = new List<JudgementEvent>();
        int[] laneIndices = laneObjectIndices[lane];

        // A dropped hold may be picked back up. In lazer, a judged head's
        // handler reports holding and then allows the press to continue.
        foreach (int index in openHoldIndices[lane])
        {
            YokkoHitObject hitObject = beatmap.HitObjects[index];
            ObjectState state = states[index];

            if (hitObject.Kind != HitObjectKind.Hold
                || !state.HeadResolved
                || state.TailResolved
                || state.Holding
                || !isHittableByOrderedPolicy(index, gameplayTimeMilliseconds)
                || hitObject.EndTimeMilliseconds is not double endTime)
                continue;

            double tailError = gameplayTimeMilliseconds - endTime;
            // lazer delays the tail's automatic miss using release lenience,
            // but does not allow a hold to begin in that extra late interval.
            if (tailError > Windows.MehMilliseconds)
                continue;

            double headError = gameplayTimeMilliseconds - hitObject.StartTimeMilliseconds;
            if (headError >= -Windows.MissMilliseconds)
                state.Holding = true;
        }

        advanceHeadPosition(lane);

        for (int position = nextHeadPositions[lane];
             position < laneIndices.Length;
             position++)
        {
            int index = laneIndices[position];
            YokkoHitObject hitObject = beatmap.HitObjects[index];
            ObjectState state = states[index];

            if (state.HeadResolved)
                continue;

            if (!isHittableByOrderedPolicy(index, gameplayTimeMilliseconds))
                continue;

            double error = gameplayTimeMilliseconds - hitObject.StartTimeMilliseconds;
            JudgementRating rating = Windows.Judge(error);

            if (rating == JudgementRating.None)
                break;

            if (hitObject.Kind == HitObjectKind.Hold
                && error >= -Windows.MissMilliseconds)
            {
                state.Holding = true;
            }

            JudgementPhase phase = hitObject.Kind == HitObjectKind.Hold
                ? JudgementPhase.HoldHead
                : JudgementPhase.Tap;
            events.Add(resolveBasic(
                index,
                hitObject.StartTimeMilliseconds,
                gameplayTimeMilliseconds,
                error,
                rating,
                phase));

            if (rating.IsHit())
                forceMissEarlierObjects(index, events);

            advanceHeadPosition(lane);
            break;
        }

        return events;
    }

    public IReadOnlyList<JudgementEvent> JudgeLaneRelease(
        int lane,
        double gameplayTimeMilliseconds)
    {
        if ((uint)lane >= laneObjectIndices.Length)
            return [];

        var events = new List<JudgementEvent>();

        foreach (int index in openHoldIndices[lane].ToArray())
        {
            YokkoHitObject hitObject = beatmap.HitObjects[index];
            ObjectState state = states[index];

            if (hitObject.Kind != HitObjectKind.Hold
                || !state.Holding
                || state.TailResolved
                || hitObject.EndTimeMilliseconds is not double endTime)
                continue;

            double rawError = gameplayTimeMilliseconds - endTime;
            JudgementRating rating = Windows.Judge(
                rawError / HoldReleaseWindowLenience);
            bool tailResolvedNow = false;

            if (rating != JudgementRating.None)
            {
                if ((!state.HeadRating.IsHit() || state.BodyBroken)
                    && rating > JudgementRating.Meh)
                {
                    rating = JudgementRating.Meh;
                }

                events.Add(resolveBasic(
                    index,
                    endTime,
                    gameplayTimeMilliseconds,
                    rawError,
                    rating,
                    JudgementPhase.HoldTail));
                tailResolvedNow = true;
            }

            if (!state.BodyResolved)
            {
                events.Add(resolveBody(
                    index,
                    gameplayTimeMilliseconds,
                    state.TailResolved && state.TailRating.IsHit()
                        ? JudgementRating.IgnoreHit
                        : JudgementRating.ComboBreak));
            }

            if (tailResolvedNow && !state.ParentResolved)
            {
                events.Add(resolveParent(
                    index,
                    gameplayTimeMilliseconds,
                    state.TailRating.IsHit()
                        ? JudgementRating.IgnoreHit
                        : JudgementRating.IgnoreMiss));
            }

            state.Holding = false;
        }

        return events;
    }

    public IReadOnlyList<JudgementEvent> CollectExpiredMisses(
        double gameplayTimeMilliseconds)
    {
        var events = new List<JudgementEvent>();
        CollectExpiredMisses(gameplayTimeMilliseconds, events);
        return events;
    }

    public void CollectExpiredMisses(
        double gameplayTimeMilliseconds,
        List<JudgementEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        resolveHeldNoReleaseTails(gameplayTimeMilliseconds, events);

        while (nextExpiration < expirations.Length
               && gameplayTimeMilliseconds >
               expirations[nextExpiration].TimeMilliseconds)
        {
            ExpirationEntry expiration = expirations[nextExpiration++];
            int i = expiration.HitObjectIndex;
            YokkoHitObject hitObject = beatmap.HitObjects[i];
            ObjectState state = states[i];

            if (expiration.Phase is JudgementPhase.Tap
                or JudgementPhase.HoldHead)
            {
                if (state.HeadResolved)
                    continue;

                events.Add(resolveBasic(
                    i,
                    hitObject.StartTimeMilliseconds,
                    null,
                    gameplayTimeMilliseconds - hitObject.StartTimeMilliseconds,
                    JudgementRating.Miss,
                    expiration.Phase));
                continue;
            }

            if (state.TailResolved
                || hitObject.EndTimeMilliseconds is not double endTime)
                continue;

            events.Add(resolveBasic(
                i,
                endTime,
                null,
                gameplayTimeMilliseconds - endTime,
                JudgementRating.Miss,
                JudgementPhase.HoldTail));

            events.Add(resolveParent(
                i,
                gameplayTimeMilliseconds,
                JudgementRating.IgnoreMiss));

            if (!state.BodyResolved)
            {
                events.Add(resolveBody(
                    i,
                    gameplayTimeMilliseconds,
                    JudgementRating.ComboBreak));
            }

            state.Holding = false;
        }
    }

    private void resolveHeldNoReleaseTails(
        double gameplayTimeMilliseconds,
        List<JudgementEvent> events)
    {
        if (!noRelease)
            return;

        foreach (HashSet<int> laneHolds in openHoldIndices)
        {
            foreach (int index in laneHolds.ToArray())
            {
                YokkoHitObject hitObject = beatmap.HitObjects[index];
                ObjectState state = states[index];
                if (!state.Holding
                    || state.TailResolved
                    || hitObject.EndTimeMilliseconds is not double endTime
                    || gameplayTimeMilliseconds < endTime)
                {
                    continue;
                }

                events.Add(resolveBasic(
                    index,
                    endTime,
                    endTime,
                    0,
                    JudgementRating.Perfect,
                    JudgementPhase.HoldTail));

                if (!state.BodyResolved)
                {
                    events.Add(resolveBody(
                        index,
                        endTime,
                        JudgementRating.IgnoreHit));
                }

                if (!state.ParentResolved)
                {
                    events.Add(resolveParent(
                        index,
                        endTime,
                        JudgementRating.IgnoreHit));
                }

                state.Holding = false;
            }
        }
    }

    private bool isHittableByOrderedPolicy(
        int hitObjectIndex,
        double gameplayTimeMilliseconds)
    {
        int nextIndex = nextLaneObjectIndices[hitObjectIndex];
        if (nextIndex < 0)
            return true;

        YokkoHitObject next = beatmap.HitObjects[nextIndex];
        return gameplayTimeMilliseconds < next.StartTimeMilliseconds;
    }

    private void advanceHeadPosition(int lane)
    {
        int[] laneIndices = laneObjectIndices[lane];
        int position = nextHeadPositions[lane];

        while (position < laneIndices.Length
               && states[laneIndices[position]].HeadResolved)
        {
            position++;
        }

        nextHeadPositions[lane] = position;
    }

    private void forceMissEarlierObjects(
        int hitObjectIndex,
        List<JudgementEvent> events)
    {
        YokkoHitObject target = beatmap.HitObjects[hitObjectIndex];
        int[] laneIndices = laneObjectIndices[target.Lane];
        int position = nextForceMissPositions[target.Lane];

        for (; position < laneIndices.Length; position++)
        {
            int earlierIndex = laneIndices[position];

            if (earlierIndex == hitObjectIndex)
                break;

            YokkoHitObject earlier = beatmap.HitObjects[earlierIndex];
            double earlierEnd =
                earlier.EndTimeMilliseconds ?? earlier.StartTimeMilliseconds;

            // OrderedHitPolicy stops at the first object whose end reaches the
            // target, which preserves overlapping-hold behaviour.
            if (earlierEnd >= target.StartTimeMilliseconds)
                break;

            ObjectState state = states[earlierIndex];

            if (earlier.Kind == HitObjectKind.Hold)
            {
                if (!state.ParentResolved)
                {
                    events.Add(resolveParent(
                        earlierIndex,
                        target.StartTimeMilliseconds,
                        JudgementRating.IgnoreMiss));
                }

                if (!state.HeadResolved)
                {
                    events.Add(resolveBasic(
                        earlierIndex,
                        earlier.StartTimeMilliseconds,
                        null,
                        target.StartTimeMilliseconds
                        - earlier.StartTimeMilliseconds,
                        JudgementRating.Miss,
                        JudgementPhase.HoldHead));
                }

                if (!state.TailResolved && earlier.EndTimeMilliseconds is double endTime)
                {
                    events.Add(resolveBasic(
                        earlierIndex,
                        endTime,
                        null,
                        target.StartTimeMilliseconds - endTime,
                        JudgementRating.Miss,
                        JudgementPhase.HoldTail));
                }

                if (!state.BodyResolved)
                {
                    events.Add(resolveBody(
                        earlierIndex,
                        target.StartTimeMilliseconds,
                        JudgementRating.ComboBreak));
                }

                state.Holding = false;
                continue;
            }

            if (!state.HeadResolved)
            {
                events.Add(resolveBasic(
                    earlierIndex,
                    earlier.StartTimeMilliseconds,
                    null,
                    target.StartTimeMilliseconds - earlier.StartTimeMilliseconds,
                    JudgementRating.Miss,
                    JudgementPhase.Tap));
            }
        }

        nextForceMissPositions[target.Lane] = position;
    }

    private JudgementEvent resolveBasic(
        int hitObjectIndex,
        double objectTimeMilliseconds,
        double? hitTimeMilliseconds,
        double hitErrorMilliseconds,
        JudgementRating rating,
        JudgementPhase phase)
    {
        ObjectState state = states[hitObjectIndex];
        bool wasComplete = state.IsComplete;

        switch (phase)
        {
            case JudgementPhase.Tap:
                state.HeadResolved = true;
                state.HeadRating = rating;
                break;

            case JudgementPhase.HoldHead:
                state.HeadResolved = true;
                state.HeadRating = rating;
                openHoldIndices[
                    beatmap.HitObjects[hitObjectIndex].Lane].Add(
                    hitObjectIndex);
                break;

            case JudgementPhase.HoldTail:
                state.TailResolved = true;
                state.TailRating = rating;
                openHoldIndices[
                    beatmap.HitObjects[hitObjectIndex].Lane].Remove(
                    hitObjectIndex);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(phase));
        }

        trackCompletion(state, wasComplete);
        scoreProcessor.Apply(rating);
        return createEvent(
            hitObjectIndex,
            objectTimeMilliseconds,
            hitTimeMilliseconds,
            hitErrorMilliseconds,
            rating,
            phase);
    }

    private JudgementEvent resolveBody(
        int hitObjectIndex,
        double eventTimeMilliseconds,
        JudgementRating rating)
    {
        ObjectState state = states[hitObjectIndex];
        bool wasComplete = state.IsComplete;
        state.BodyResolved = true;
        state.BodyBroken = rating == JudgementRating.ComboBreak;
        trackCompletion(state, wasComplete);
        scoreProcessor.Apply(rating);

        return createEvent(
            hitObjectIndex,
            eventTimeMilliseconds,
            eventTimeMilliseconds,
            0,
            rating,
            JudgementPhase.HoldBody);
    }

    private JudgementEvent resolveParent(
        int hitObjectIndex,
        double eventTimeMilliseconds,
        JudgementRating rating)
    {
        ObjectState state = states[hitObjectIndex];
        bool wasComplete = state.IsComplete;
        state.ParentResolved = true;
        trackCompletion(state, wasComplete);
        scoreProcessor.Apply(rating);

        return createEvent(
            hitObjectIndex,
            eventTimeMilliseconds,
            eventTimeMilliseconds,
            0,
            rating,
            JudgementPhase.Hold);
    }

    private void trackCompletion(ObjectState state, bool wasComplete)
    {
        if (!wasComplete && state.IsComplete)
            resolvedJudgementObjectCount++;
    }

    private JudgementEvent createEvent(
        int hitObjectIndex,
        double objectTimeMilliseconds,
        double? hitTimeMilliseconds,
        double hitErrorMilliseconds,
        JudgementRating rating,
        JudgementPhase phase)
        => new(
            hitObjectIndex,
            beatmap.HitObjects[hitObjectIndex].Lane,
            objectTimeMilliseconds,
            hitTimeMilliseconds,
            hitErrorMilliseconds,
            rating,
            phase);

    private static bool isJudgementObject(YokkoHitObject hitObject)
        => hitObject.Kind is HitObjectKind.Tap or HitObjectKind.Hold;

    private readonly record struct ExpirationEntry(
        double TimeMilliseconds,
        int HitObjectIndex,
        JudgementPhase Phase);

    private sealed class ObjectState
    {
        public ObjectState(YokkoHitObject hitObject)
        {
            if (hitObject.Kind == HitObjectKind.Tap)
            {
                ParentResolved = true;
                TailResolved = true;
                BodyResolved = true;
            }
            else if (hitObject.Kind is not HitObjectKind.Hold)
            {
                ParentResolved = true;
                HeadResolved = true;
                TailResolved = true;
                BodyResolved = true;
            }
        }

        public bool HeadResolved { get; set; }
        public JudgementRating HeadRating { get; set; }
        public bool ParentResolved { get; set; }
        public bool TailResolved { get; set; }
        public JudgementRating TailRating { get; set; }
        public bool BodyResolved { get; set; }
        public bool BodyBroken { get; set; }
        public bool Holding { get; set; }
        public bool IsComplete =>
            ParentResolved && HeadResolved && TailResolved && BodyResolved;
    }
}
