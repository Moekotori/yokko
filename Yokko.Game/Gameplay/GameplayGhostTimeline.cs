using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;

namespace Yokko.Game.Gameplay;

internal readonly record struct GameplayGhostSnapshot(
    double TimeMilliseconds,
    long Score,
    double Accuracy,
    int Combo,
    int MissCount);

internal sealed class GameplayGhostTimeline
{
    private readonly GameplayGhostSnapshot[] snapshots;

    internal int SimulationStepCount { get; }

    public GameplayGhostSnapshot FinalSnapshot =>
        snapshots.Length == 0 ? default : snapshots[^1];

    private GameplayGhostTimeline(
        IReadOnlyList<GameplayGhostSnapshot> snapshots,
        int simulationStepCount)
    {
        this.snapshots = snapshots.ToArray();
        SimulationStepCount = simulationStepCount;
    }

    public static GameplayGhostTimeline Build(
        YokkoBeatmap beatmap,
        GameplayReplay replay,
        ManiaModSet mods,
        JudgementWindows windows,
        JudgementConfiguration judgementConfiguration,
        bool minesEnabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        ArgumentNullException.ThrowIfNull(replay);
        var simulator = new GameplayReplaySimulator(
            beatmap,
            replay,
            mods,
            windows,
            judgementConfiguration,
            minesEnabled);

        double firstObject = beatmap.HitObjects.Count == 0
            ? 0
            : beatmap.HitObjects.Min(static hitObject =>
                hitObject.StartTimeMilliseconds);
        double finalObject = beatmap.HitObjects.Count == 0
            ? 0
            : beatmap.HitObjects.Max(static hitObject =>
                hitObject.EndTimeMilliseconds
                ?? hitObject.StartTimeMilliseconds);
        double firstReplay = replay.Frames.Count == 0
            ? 0
            : replay.Frames[0].TimeMilliseconds;
        double finalReplay = replay.Frames.Count == 0
            ? 0
            : replay.Frames[^1].TimeMilliseconds;
        double startTime = Math.Min(0, Math.Min(firstObject - 1000, firstReplay));
        double endTime = Math.Max(finalObject, finalReplay) + 2000;
        var result = new List<GameplayGhostSnapshot>();

        simulator.AdvanceTo(startTime);
        int simulationStepCount = 1;
        addSnapshot(result, simulator, startTime, force: true);
        double simulationTime = startTime;
        while (simulationTime < endTime)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double nextReplay = simulator.NextReplayFrameTime
                                ?? double.PositiveInfinity;
            double nextPassive = simulator.NextPassiveJudgementTime
                                 ?? double.PositiveInfinity;
            double nextTime = Math.Min(
                endTime,
                Math.Min(nextReplay, nextPassive));
            if (nextTime <= simulationTime)
                nextTime = Math.BitIncrement(simulationTime);
            if (nextTime > endTime)
                nextTime = endTime;

            simulator.AdvanceTo(nextTime);
            simulationStepCount++;
            addSnapshot(result, simulator, nextTime, force: false);
            simulationTime = nextTime;
        }

        addSnapshot(result, simulator, endTime, force: true);
        return new GameplayGhostTimeline(result, simulationStepCount);
    }

    public bool TryQuery(
        double gameplayTimeMilliseconds,
        ref int cachedIndex,
        out GameplayGhostSnapshot snapshot)
    {
        if (snapshots.Length == 0)
        {
            cachedIndex = -1;
            snapshot = default;
            return false;
        }

        if (cachedIndex >= 0 && cachedIndex < snapshots.Length)
        {
            if (cachedIndex == snapshots.Length - 1
                && gameplayTimeMilliseconds
                   >= snapshots[cachedIndex].TimeMilliseconds)
            {
                snapshot = snapshots[cachedIndex];
                return true;
            }
            if (cachedIndex + 1 < snapshots.Length
                && gameplayTimeMilliseconds
                   >= snapshots[cachedIndex].TimeMilliseconds
                && gameplayTimeMilliseconds
                   < snapshots[cachedIndex + 1].TimeMilliseconds)
            {
                snapshot = snapshots[cachedIndex];
                return true;
            }
        }

        int low = 0;
        int high = snapshots.Length - 1;
        while (low < high)
        {
            int middle = low + (high - low + 1) / 2;
            if (snapshots[middle].TimeMilliseconds
                <= gameplayTimeMilliseconds)
            {
                low = middle;
            }
            else
                high = middle - 1;
        }

        cachedIndex = low;
        snapshot = snapshots[low];
        return true;
    }

    private static void addSnapshot(
        List<GameplayGhostSnapshot> snapshots,
        GameplayReplaySimulator simulator,
        double time,
        bool force)
    {
        var snapshot = new GameplayGhostSnapshot(
            time,
            simulator.JudgementState.Score,
            simulator.JudgementState.Accuracy,
            simulator.JudgementState.Combo,
            simulator.JudgementState.Counts.Miss);
        if (!force && snapshots.Count > 0)
        {
            GameplayGhostSnapshot previous = snapshots[^1];
            if (previous.Score == snapshot.Score
                && previous.Accuracy == snapshot.Accuracy
                && previous.Combo == snapshot.Combo
                && previous.MissCount == snapshot.MissCount)
            {
                return;
            }
        }

        if (snapshots.Count > 0
            && snapshots[^1].TimeMilliseconds == snapshot.TimeMilliseconds)
        {
            snapshots[^1] = snapshot;
        }
        else
            snapshots.Add(snapshot);
    }
}
