using Yokko.Core.Beatmaps;

namespace Yokko.Core.Scoring;

/// <summary>
/// Deterministic, UI-independent osu!lazer mania judgement state.
/// Behaviour is ported from ppy/osu's DrawableNote, DrawableHoldNote,
/// DrawableHoldNoteTail and OrderedHitPolicy at
/// 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0 (MIT).
/// </summary>
public sealed class BeatmapJudgementState
{
    public const double HoldReleaseWindowLenience = 1.5;

    // StepMania 5.1 checks mines within the configured 0.09s window and also
    // triggers them when a lane remains held as the mine crosses the receptor.
    // Source: stepmania/stepmania Player.cpp
    // commit 21bb8dcd6c7e3782f23d5f4e01b6ee4c82cccc71
    // (StepMania permissive licence; see Docs/Licenses.txt).
    public const double MineWindowMilliseconds = 90;

    // Etterna keeps its mine window fixed at 75ms for every Judge/Justice.
    // Source: etternagame/etterna GameConstantsAndTypes.h and Player.cpp
    // commit 939a26ae042d3a689999a0dae630721c7701f187 (MIT).
    public const double EtternaMineWindowMilliseconds = 75;

    // Etterna hold life refills while pressed and takes a fixed 0.25 seconds
    // to drain while released. Judge/Justice does not scale this window.
    // Source: etternagame/etterna GameConstantsAndTypes.h and Player.cpp
    // commit b65660062ef2a23121e331c36e23c23a8f6eafaa (MIT).
    public const double EtternaHoldDropWindowMilliseconds = 250;

    // Etterna rolls drain continuously and every press refills their life.
    // Unlike Judge-scaled tap windows, this interval is fixed at 0.5 seconds.
    // Source: etternagame/etterna GameConstantsAndTypes.h and Player.cpp
    // commit b65660062ef2a23121e331c36e23c23a8f6eafaa (MIT).
    public const double EtternaRollDropWindowMilliseconds = 500;

    private readonly YokkoBeatmap beatmap;
    private readonly ObjectState[] states;
    private readonly int[][] laneObjectIndices;
    private readonly int[][] laneMineObjectIndices;
    private readonly int[][] laneEtternaObjectIndices;
    private readonly int[] nextLaneObjectIndices;
    private readonly int[] nextHeadPositions;
    private readonly int[] nextMinePositions;
    private readonly int[] nextEtternaPositions;
    private readonly int[] nextForceMissPositions;
    private readonly double?[] lanePressedSinceMilliseconds;
    private readonly HashSet<int>[] openHoldIndices;
    private readonly List<int> openHoldSnapshot = [];
    private readonly ExpirationEntry[] expirations;
    private readonly ManiaScoreProcessor scoreProcessor;
    private readonly int totalJudgementObjectCount;
    private readonly bool noRelease;
    private int nextExpiration;
    private int resolvedJudgementObjectCount;

    public BeatmapJudgementState(
        YokkoBeatmap beatmap,
        JudgementWindows? windows = null,
        bool noRelease = false,
        double scoreMultiplier = 1,
        bool minesEnabled = true)
    {
        this.beatmap = beatmap;
        this.noRelease = noRelease;
        Windows = windows ?? new JudgementWindows(beatmap.OverallDifficulty);
        states = beatmap.HitObjects
                        .Select(hitObject => new ObjectState(
                            hitObject,
                            minesEnabled))
                        .ToArray();
        scoreProcessor = new ManiaScoreProcessor(
            beatmap,
            scoreMultiplier,
            Windows.Configuration);
        totalJudgementObjectCount =
            beatmap.HitObjects.Count(hitObject =>
                isStandardJudgementObject(hitObject)
                || minesEnabled && hitObject.Kind == HitObjectKind.Mine);

        laneObjectIndices = Enumerable.Range(0, (int)beatmap.KeyMode)
                                      .Select(lane => beatmap.HitObjects
                                                             .Select((hitObject, index) => (hitObject, index))
                                                             .Where(item => item.hitObject.Lane == lane
                                                                            && isStandardJudgementObject(item.hitObject))
                                                             .OrderBy(item => item.hitObject.StartTimeMilliseconds)
                                                             .ThenBy(item => item.index)
                                                             .Select(item => item.index)
                                                             .ToArray())
                                      .ToArray();
        laneMineObjectIndices = Enumerable.Range(0, (int)beatmap.KeyMode)
                                          .Select(lane => minesEnabled
                                              ? beatmap.HitObjects
                                                       .Select((hitObject, index) => (hitObject, index))
                                                       .Where(item =>
                                                           item.hitObject.Lane == lane
                                                           && item.hitObject.Kind == HitObjectKind.Mine)
                                                       .OrderBy(item =>
                                                           item.hitObject.StartTimeMilliseconds)
                                                       .ThenBy(item => item.index)
                                                       .Select(item => item.index)
                                                       .ToArray()
                                               : [])
                                          .ToArray();
        laneEtternaObjectIndices = Enumerable.Range(0, (int)beatmap.KeyMode)
                                             .Select(lane => beatmap.HitObjects
                                                                    .Select((hitObject, index) => (hitObject, index))
                                                                    .Where(item =>
                                                                        item.hitObject.Lane == lane
                                                                        && (isStandardJudgementObject(item.hitObject)
                                                                            || minesEnabled
                                                                            && item.hitObject.Kind
                                                                            == HitObjectKind.Mine))
                                                                    .OrderBy(item =>
                                                                        item.hitObject.StartTimeMilliseconds)
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
        nextMinePositions = new int[laneObjectIndices.Length];
        nextEtternaPositions = new int[laneObjectIndices.Length];
        nextForceMissPositions = new int[laneObjectIndices.Length];
        lanePressedSinceMilliseconds =
            new double?[laneObjectIndices.Length];
        openHoldIndices = Enumerable.Range(0, laneObjectIndices.Length)
                                    .Select(_ => new HashSet<int>())
                                    .ToArray();

        expirations = beatmap.HitObjects
                             .SelectMany((hitObject, index) =>
                             {
                                 if (hitObject.Kind == HitObjectKind.Mine)
                                 {
                                     return minesEnabled
                                         ?
                                         [
                                              new ExpirationEntry(
                                                  hitObject.StartTimeMilliseconds
                                                  + ActiveMineAvoidWindowMilliseconds,
                                                  index,
                                                  JudgementPhase.Mine),
                                         ]
                                         : [];
                                 }

                                 if (!isStandardJudgementObject(hitObject))
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

    // Yokko works in rate-adjusted chart time. Etterna divides note offsets by
    // the music rate before applying its fixed 75ms real-time window, so the
    // equivalent chart-time window scales with the fixed playback rate.
    public double ActiveMineWindowMilliseconds =>
        Windows.Configuration.Mode == JudgementMode.Etterna
            ? EtternaMineWindowMilliseconds * Windows.SpeedMultiplier
            : Windows.Configuration.Mode == JudgementMode.Quaver
                ? Windows.PerfectMilliseconds
            : MineWindowMilliseconds;

    // Etterna turns an untouched mine into AvoidMine at the same rate-adjusted
    // 180ms outer boundary used by UpdateTapNotesMissedOlderThan().
    public double ActiveMineAvoidWindowMilliseconds =>
        Windows.Configuration.Mode == JudgementMode.Etterna
            ? Windows.MissMilliseconds
            : ActiveMineWindowMilliseconds;

    public double ActiveEtternaHoldDropWindowMilliseconds =>
        EtternaHoldDropWindowMilliseconds * Windows.SpeedMultiplier;

    public double ActiveEtternaRollDropWindowMilliseconds =>
        EtternaRollDropWindowMilliseconds * Windows.SpeedMultiplier;

    public JudgementCounter Counts => scoreProcessor.Counts;

    public int Combo => scoreProcessor.Combo;

    public int MaxCombo => scoreProcessor.MaxCombo;

    public int ComboBreaks => scoreProcessor.ComboBreaks;

    public int MissCombo => scoreProcessor.MissCombo;

    public int MaxMissCombo => scoreProcessor.MaxMissCombo;

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
        Counts.Miss,
        ComboBreaks,
        MaxMissCombo);

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
        JudgeLanePress(lane, gameplayTimeMilliseconds, events);
        return events;
    }

    public void JudgeLanePress(
        int lane,
        double gameplayTimeMilliseconds,
        List<JudgementEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if ((uint)lane >= laneObjectIndices.Length)
            return;

        int[] laneIndices = laneObjectIndices[lane];
        lanePressedSinceMilliseconds[lane] ??=
            gameplayTimeMilliseconds;

        if (Windows.Configuration.Mode == JudgementMode.Etterna)
        {
            handleEtternaOpenRollPress(
                lane,
                gameplayTimeMilliseconds,
                events);
            handleEtternaOpenHoldPress(
                lane,
                gameplayTimeMilliseconds,
                events);
            judgeEtternaPress(
                lane,
                gameplayTimeMilliseconds,
                events);
            return;
        }

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

        judgeMinePress(lane, gameplayTimeMilliseconds, events);
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
    }

    public IReadOnlyList<JudgementEvent> CollectMineJudgements(
        double gameplayTimeMilliseconds,
        IReadOnlyList<bool> pressedLanes)
    {
        var events = new List<JudgementEvent>();
        CollectMineJudgements(
            gameplayTimeMilliseconds,
            pressedLanes,
            events);
        return events;
    }

    public void CollectMineJudgements(
        double gameplayTimeMilliseconds,
        IReadOnlyList<bool> pressedLanes,
        List<JudgementEvent> events)
    {
        ArgumentNullException.ThrowIfNull(pressedLanes);
        ArgumentNullException.ThrowIfNull(events);

        int laneCount = Math.Min(
            laneMineObjectIndices.Length,
            pressedLanes.Count);
        for (int lane = 0; lane < laneCount; lane++)
        {
            if (!pressedLanes[lane])
            {
                lanePressedSinceMilliseconds[lane] = null;
                continue;
            }

            if (lanePressedSinceMilliseconds[lane] is not double pressedSince
                || pressedSince > gameplayTimeMilliseconds)
            {
                pressedSince = gameplayTimeMilliseconds;
                lanePressedSinceMilliseconds[lane] = pressedSince;
            }

            collectCrossedMines(
                lane,
                pressedSince,
                gameplayTimeMilliseconds,
                events);
        }
    }

    /// <summary>
    /// Etterna searches both sides of the current row and judges the closest
    /// ungraded note. Equal distances select the future note.
    /// Source: etternagame/etterna Player::GetClosestNote
    /// commit 939a26ae042d3a689999a0dae630721c7701f187 (MIT).
    /// </summary>
    private int closestEtternaInputObject(
        int lane,
        double gameplayTimeMilliseconds)
    {
        advanceEtternaPosition(lane);

        int[] laneIndices = laneEtternaObjectIndices[lane];
        int closestIndex = -1;
        double closestDistance = double.PositiveInfinity;
        double closestTime = double.NegativeInfinity;
        double outerWindow = Windows.MissMilliseconds;

        for (int position = nextEtternaPositions[lane];
             position < laneIndices.Length;
             position++)
        {
            int index = laneIndices[position];
            if (states[index].HeadResolved)
                continue;

            double objectTime =
                beatmap.HitObjects[index].StartTimeMilliseconds;
            if (objectTime > gameplayTimeMilliseconds + outerWindow)
                break;

            // Player::GetClosestNote deliberately rejects mines that have
            // already crossed the receptor. A late press may still select an
            // earlier tap, but never retroactively explode an old mine.
            if (beatmap.HitObjects[index].Kind == HitObjectKind.Mine
                && objectTime < gameplayTimeMilliseconds)
            {
                continue;
            }

            double distance =
                Math.Abs(gameplayTimeMilliseconds - objectTime);
            if (distance > outerWindow)
                continue;

            if (distance < closestDistance
                || distance == closestDistance
                && objectTime > closestTime)
            {
                closestIndex = index;
                closestDistance = distance;
                closestTime = objectTime;
            }
        }

        return closestIndex;
    }

    private void judgeEtternaPress(
        int lane,
        double gameplayTimeMilliseconds,
        List<JudgementEvent> events)
    {
        int index = closestEtternaInputObject(
            lane,
            gameplayTimeMilliseconds);
        if (index < 0)
            return;

        YokkoHitObject hitObject = beatmap.HitObjects[index];
        double error =
            gameplayTimeMilliseconds
            - hitObject.StartTimeMilliseconds;

        if (hitObject.Kind == HitObjectKind.Mine)
        {
            if (Math.Abs(error) > ActiveMineWindowMilliseconds)
                return;

            events.Add(resolveMine(
                index,
                gameplayTimeMilliseconds,
                error,
                wasHit: true));
            advanceMinePosition(lane);
            advanceEtternaPosition(lane);
            return;
        }

        JudgementRating rating = Windows.Judge(error);
        if (rating == JudgementRating.None)
            return;

        if (hitObject.Kind == HitObjectKind.Hold
            && error >= -Windows.MissMilliseconds)
        {
            states[index].Holding = true;
        }

        events.Add(resolveBasic(
            index,
            hitObject.StartTimeMilliseconds,
            gameplayTimeMilliseconds,
            error,
            rating,
            hitObject.Kind == HitObjectKind.Hold
                ? JudgementPhase.HoldHead
                : JudgementPhase.Tap));
        advanceHeadPosition(lane);
        advanceEtternaPosition(lane);
    }

    private void handleEtternaOpenHoldPress(
        int lane,
        double gameplayTimeMilliseconds,
        List<JudgementEvent> events)
    {
        openHoldSnapshot.Clear();
        openHoldSnapshot.AddRange(openHoldIndices[lane]);
        foreach (int index in openHoldSnapshot)
        {
            YokkoHitObject hitObject = beatmap.HitObjects[index];
            ObjectState state = states[index];
            if (hitObject.Kind != HitObjectKind.Hold
                || hitObject.HoldType == HoldNoteType.Roll
                || !state.HeadResolved
                || !state.HeadRating.IsHit()
                || state.TailResolved
                || state.Holding
                || state.EtternaReleasedAtMilliseconds is not double releasedAt
                || hitObject.EndTimeMilliseconds is not double endTime)
            {
                continue;
            }

            double dropTime =
                releasedAt + ActiveEtternaHoldDropWindowMilliseconds;
            if (dropTime <= Math.Min(gameplayTimeMilliseconds, endTime))
            {
                resolveEtternaHold(
                    index,
                    dropTime,
                    wasHeld: false,
                    events);
                continue;
            }

            if (gameplayTimeMilliseconds >= endTime)
            {
                resolveEtternaHold(
                    index,
                    endTime,
                    wasHeld: true,
                    events);
                continue;
            }

            state.Holding = true;
            state.EtternaReleasedAtMilliseconds = null;
        }
    }

    private void handleEtternaOpenRollPress(
        int lane,
        double gameplayTimeMilliseconds,
        List<JudgementEvent> events)
    {
        openHoldSnapshot.Clear();
        openHoldSnapshot.AddRange(openHoldIndices[lane]);
        foreach (int index in openHoldSnapshot)
        {
            YokkoHitObject hitObject = beatmap.HitObjects[index];
            ObjectState state = states[index];
            if (hitObject.Kind != HitObjectKind.Hold
                || hitObject.HoldType != HoldNoteType.Roll
                || !state.HeadResolved
                || !state.HeadRating.IsHit()
                || state.TailResolved
                || hitObject.EndTimeMilliseconds is not double endTime)
            {
                continue;
            }

            double expiry =
                state.EtternaRollLifeExpiresAtMilliseconds
                ?? hitObject.StartTimeMilliseconds
                + ActiveEtternaRollDropWindowMilliseconds;
            if (expiry <= Math.Min(
                    gameplayTimeMilliseconds,
                    endTime))
            {
                resolveEtternaHold(
                    index,
                    expiry,
                    wasHeld: false,
                    events);
                continue;
            }

            if (gameplayTimeMilliseconds >= endTime)
            {
                resolveEtternaHold(
                    index,
                    endTime,
                    wasHeld: true,
                    events);
                continue;
            }

            state.Holding = true;
            state.EtternaRollLifeExpiresAtMilliseconds =
                gameplayTimeMilliseconds
                + ActiveEtternaRollDropWindowMilliseconds;
        }
    }

    public IReadOnlyList<JudgementEvent> JudgeLaneRelease(
        int lane,
        double gameplayTimeMilliseconds)
    {
        if ((uint)lane >= laneObjectIndices.Length)
            return [];

        var events = new List<JudgementEvent>();
        JudgeLaneRelease(lane, gameplayTimeMilliseconds, events);
        return events;
    }

    public void JudgeLaneRelease(
        int lane,
        double gameplayTimeMilliseconds,
        List<JudgementEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if ((uint)lane >= laneObjectIndices.Length)
            return;

        if (lanePressedSinceMilliseconds[lane] is double pressedSince)
        {
            collectCrossedMines(
                lane,
                pressedSince,
                gameplayTimeMilliseconds,
                events);
        }

        lanePressedSinceMilliseconds[lane] = null;

        openHoldSnapshot.Clear();
        openHoldSnapshot.AddRange(openHoldIndices[lane]);
        foreach (int index in openHoldSnapshot)
        {
            YokkoHitObject hitObject = beatmap.HitObjects[index];
            ObjectState state = states[index];

            if (hitObject.Kind != HitObjectKind.Hold
                || !state.Holding
                || state.TailResolved
                || hitObject.EndTimeMilliseconds is not double endTime)
                continue;

            if (Windows.Configuration.Mode == JudgementMode.Etterna
                && hitObject.HoldType == HoldNoteType.Roll)
            {
                continue;
            }

            if (Windows.Configuration.Mode == JudgementMode.Etterna)
            {
                if (gameplayTimeMilliseconds >= endTime)
                {
                    resolveEtternaHold(
                        index,
                        endTime,
                        wasHeld: true,
                        events);
                }
                else
                {
                    state.Holding = false;
                    state.EtternaReleasedAtMilliseconds =
                        gameplayTimeMilliseconds;
                }

                continue;
            }

            double rawError = gameplayTimeMilliseconds - endTime;
            JudgementRating rating = judgeHoldRelease(rawError);
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
    }

    private JudgementRating judgeHoldRelease(double rawError)
    {
        JudgementRating rating = Windows.Judge(
            rawError / HoldReleaseWindowLenience);

        if (Windows.Configuration.Mode != JudgementMode.Quaver)
            return rating;

        // Quaver's default key rules use 1.5x release windows, promote the
        // otherwise possible Okay release to Good, and leave releases beyond
        // the 127ms * 1.5 boundary unresolved instead of awarding a Miss.
        // Source: Quaver.API Maps/Processors/Scoring/ScoreProcessorKeys.cs
        // commit 1e4dc1d64a968cfeaee3e267603cd78a48979772 (MPL-2.0).
        return rating switch
        {
            JudgementRating.Meh => JudgementRating.Ok,
            JudgementRating.Miss => JudgementRating.None,
            _ => rating,
        };
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
        if (Windows.Configuration.Mode == JudgementMode.Etterna)
            resolveEtternaHeldTails(gameplayTimeMilliseconds, events);
        else
            resolveHeldNoReleaseTails(gameplayTimeMilliseconds, events);

        while (nextExpiration < expirations.Length
               && gameplayTimeMilliseconds >
               expirations[nextExpiration].TimeMilliseconds)
        {
            ExpirationEntry expiration = expirations[nextExpiration++];
            int i = expiration.HitObjectIndex;
            YokkoHitObject hitObject = beatmap.HitObjects[i];
            ObjectState state = states[i];

            if (expiration.Phase == JudgementPhase.Mine)
            {
                if (state.HeadResolved)
                    continue;

                events.Add(resolveMine(
                    i,
                    null,
                    gameplayTimeMilliseconds
                    - hitObject.StartTimeMilliseconds,
                    wasHit: false));
                advanceMinePosition(hitObject.Lane);
                advanceEtternaPosition(hitObject.Lane);
                continue;
            }

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
                advanceHeadPosition(hitObject.Lane);
                advanceEtternaPosition(hitObject.Lane);
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

    private void resolveEtternaHeldTails(
        double gameplayTimeMilliseconds,
        List<JudgementEvent> events)
    {
        foreach (HashSet<int> laneHolds in openHoldIndices)
        {
            foreach (int index in laneHolds.ToArray())
            {
                YokkoHitObject hitObject = beatmap.HitObjects[index];
                ObjectState state = states[index];
                if (state.TailResolved
                    || hitObject.EndTimeMilliseconds is not double endTime)
                {
                    continue;
                }

                if (hitObject.HoldType == HoldNoteType.Roll)
                {
                    resolveEtternaRoll(
                        index,
                        gameplayTimeMilliseconds,
                        endTime,
                        events);
                    continue;
                }

                bool wasHeld = state.HeadRating.IsHit()
                               && !state.BodyBroken
                               && (state.Holding
                                   || state.EtternaReleasedAtMilliseconds
                                   is double releasedAt
                                   && releasedAt
                                      + ActiveEtternaHoldDropWindowMilliseconds
                                      > endTime);
                double resolutionTime = endTime;
                if (!state.Holding
                    && state.HeadRating.IsHit()
                    && state.EtternaReleasedAtMilliseconds
                    is double dropStartedAt)
                {
                    double dropTime =
                        dropStartedAt
                        + ActiveEtternaHoldDropWindowMilliseconds;
                    if (dropTime <= Math.Min(
                            gameplayTimeMilliseconds,
                            endTime))
                    {
                        wasHeld = false;
                        resolutionTime = dropTime;
                    }
                    else if (gameplayTimeMilliseconds < endTime)
                    {
                        continue;
                    }
                }
                else if (gameplayTimeMilliseconds < endTime)
                {
                    continue;
                }

                resolveEtternaHold(
                    index,
                    resolutionTime,
                    wasHeld,
                    events);
            }
        }
    }

    private void resolveEtternaRoll(
        int hitObjectIndex,
        double gameplayTimeMilliseconds,
        double endTime,
        List<JudgementEvent> events)
    {
        ObjectState state = states[hitObjectIndex];
        if (!state.HeadRating.IsHit())
        {
            if (gameplayTimeMilliseconds >= endTime)
            {
                resolveEtternaHold(
                    hitObjectIndex,
                    endTime,
                    wasHeld: false,
                    events);
            }

            return;
        }

        double expiry =
            state.EtternaRollLifeExpiresAtMilliseconds
            ?? beatmap.HitObjects[hitObjectIndex]
                      .StartTimeMilliseconds
               + ActiveEtternaRollDropWindowMilliseconds;
        if (expiry <= Math.Min(
                gameplayTimeMilliseconds,
                endTime))
        {
            resolveEtternaHold(
                hitObjectIndex,
                expiry,
                wasHeld: false,
                events);
            return;
        }

        if (gameplayTimeMilliseconds >= endTime)
        {
            resolveEtternaHold(
                hitObjectIndex,
                endTime,
                wasHeld: true,
                events);
        }
    }

    private void resolveEtternaHold(
        int hitObjectIndex,
        double eventTimeMilliseconds,
        bool wasHeld,
        List<JudgementEvent> events)
    {
        YokkoHitObject hitObject = beatmap.HitObjects[hitObjectIndex];
        ObjectState state = states[hitObjectIndex];
        double endTime =
            hitObject.EndTimeMilliseconds
            ?? hitObject.StartTimeMilliseconds;
        JudgementRating passiveRating = wasHeld
            ? JudgementRating.IgnoreHit
            : JudgementRating.IgnoreMiss;

        if (!state.TailResolved)
        {
            events.Add(resolveBasic(
                hitObjectIndex,
                endTime,
                eventTimeMilliseconds,
                eventTimeMilliseconds - endTime,
                passiveRating,
                JudgementPhase.HoldTail));
        }

        if (!state.BodyResolved)
        {
            JudgementRating bodyRating = wasHeld
                ? JudgementRating.IgnoreHit
                : state.HeadRating.IsHit()
                    ? JudgementRating.ComboBreak
                    : JudgementRating.IgnoreMiss;
            events.Add(resolveBody(
                hitObjectIndex,
                eventTimeMilliseconds,
                bodyRating));
        }

        if (!state.ParentResolved)
        {
            events.Add(resolveParent(
                hitObjectIndex,
                eventTimeMilliseconds,
                passiveRating));
        }

        state.Holding = false;
        state.EtternaReleasedAtMilliseconds = null;
        state.EtternaRollLifeExpiresAtMilliseconds = null;
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

    private void advanceMinePosition(int lane)
    {
        int[] mineIndices = laneMineObjectIndices[lane];
        int position = nextMinePositions[lane];

        while (position < mineIndices.Length
               && states[mineIndices[position]].HeadResolved)
        {
            position++;
        }

        nextMinePositions[lane] = position;
    }

    private void advanceEtternaPosition(int lane)
    {
        int[] laneIndices = laneEtternaObjectIndices[lane];
        int position = nextEtternaPositions[lane];

        while (position < laneIndices.Length
               && states[laneIndices[position]].HeadResolved)
        {
            position++;
        }

        nextEtternaPositions[lane] = position;
    }

    private void collectCrossedMines(
        int lane,
        double pressedSinceMilliseconds,
        double gameplayTimeMilliseconds,
        List<JudgementEvent> events)
    {
        int[] mineIndices = laneMineObjectIndices[lane];
        for (int position = nextMinePositions[lane];
             position < mineIndices.Length;
             position++)
        {
            int index = mineIndices[position];
            if (states[index].HeadResolved)
                continue;

            YokkoHitObject mine = beatmap.HitObjects[index];
            if (mine.StartTimeMilliseconds < pressedSinceMilliseconds)
                continue;
            if (mine.StartTimeMilliseconds > gameplayTimeMilliseconds)
                break;

            events.Add(resolveMine(
                index,
                mine.StartTimeMilliseconds,
                0,
                wasHit: true));
        }

        advanceMinePosition(lane);
        advanceEtternaPosition(lane);
    }

    private void judgeMinePress(
        int lane,
        double gameplayTimeMilliseconds,
        List<JudgementEvent> events)
    {
        int[] mineIndices = laneMineObjectIndices[lane];
        int bestIndex = -1;
        double bestAbsoluteError = double.PositiveInfinity;

        for (int position = nextMinePositions[lane];
             position < mineIndices.Length;
             position++)
        {
            int index = mineIndices[position];
            if (states[index].HeadResolved)
                continue;

            YokkoHitObject mine = beatmap.HitObjects[index];
            double error = gameplayTimeMilliseconds
                           - mine.StartTimeMilliseconds;
            if (error < -ActiveMineWindowMilliseconds)
                break;
            if (error > ActiveMineWindowMilliseconds)
                continue;

            double absoluteError = Math.Abs(error);
            if (absoluteError < bestAbsoluteError)
            {
                bestIndex = index;
                bestAbsoluteError = absoluteError;
            }
        }

        if (bestIndex < 0)
            return;

        double hitError = gameplayTimeMilliseconds
                          - beatmap.HitObjects[bestIndex]
                                   .StartTimeMilliseconds;
        events.Add(resolveMine(
            bestIndex,
            gameplayTimeMilliseconds,
            hitError,
            wasHit: true));
        advanceMinePosition(lane);
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
                if (Windows.Configuration.Mode == JudgementMode.Etterna
                    && beatmap.HitObjects[hitObjectIndex].HoldType
                    == HoldNoteType.Roll
                    && rating.IsHit())
                {
                    state.EtternaRollLifeExpiresAtMilliseconds =
                        (hitTimeMilliseconds ?? objectTimeMilliseconds)
                        + ActiveEtternaRollDropWindowMilliseconds;
                }
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
        scoreProcessor.Apply(
            rating,
            hitErrorMilliseconds / Windows.SpeedMultiplier,
            phase,
            objectTimeMilliseconds);
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
        scoreProcessor.Apply(
            rating,
            0,
            JudgementPhase.HoldBody,
            eventTimeMilliseconds);

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
        scoreProcessor.Apply(
            rating,
            0,
            JudgementPhase.Hold,
            eventTimeMilliseconds);

        return createEvent(
            hitObjectIndex,
            eventTimeMilliseconds,
            eventTimeMilliseconds,
            0,
            rating,
            JudgementPhase.Hold);
    }

    private JudgementEvent resolveMine(
        int hitObjectIndex,
        double? hitTimeMilliseconds,
        double hitErrorMilliseconds,
        bool wasHit)
    {
        ObjectState state = states[hitObjectIndex];
        bool wasComplete = state.IsComplete;
        state.HeadResolved = true;
        state.HeadRating = wasHit
            ? JudgementRating.IgnoreMiss
            : JudgementRating.IgnoreHit;
        trackCompletion(state, wasComplete);
        scoreProcessor.ApplyMine(wasHit);

        return createEvent(
            hitObjectIndex,
            beatmap.HitObjects[hitObjectIndex]
                   .StartTimeMilliseconds,
            hitTimeMilliseconds,
            hitErrorMilliseconds,
            state.HeadRating,
            JudgementPhase.Mine);
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

    private static bool isStandardJudgementObject(
        YokkoHitObject hitObject)
        => hitObject.Kind is HitObjectKind.Tap or HitObjectKind.Hold;

    private readonly record struct ExpirationEntry(
        double TimeMilliseconds,
        int HitObjectIndex,
        JudgementPhase Phase);

    private sealed class ObjectState
    {
        public ObjectState(
            YokkoHitObject hitObject,
            bool minesEnabled)
        {
            if (hitObject.Kind == HitObjectKind.Tap)
            {
                ParentResolved = true;
                TailResolved = true;
                BodyResolved = true;
            }
            else if (hitObject.Kind == HitObjectKind.Mine
                     && minesEnabled)
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
        public double? EtternaReleasedAtMilliseconds { get; set; }
        public double? EtternaRollLifeExpiresAtMilliseconds { get; set; }
        public bool IsComplete =>
            ParentResolved && HeadResolved && TailResolved && BodyResolved;
    }
}
