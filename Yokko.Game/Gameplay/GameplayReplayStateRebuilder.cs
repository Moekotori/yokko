using System;
using System.Collections.Generic;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;

namespace Yokko.Game.Gameplay;

internal sealed record GameplayReplayRestoredState(
    GameplayReplayTimeline Timeline,
    BeatmapJudgementState JudgementState,
    ManiaHealthState HealthState,
    ManiaAdaptiveSpeedState AdaptiveSpeedState,
    bool[] PressedLanes);

/// <summary>
/// Rebuilds Yokko's deterministic gameplay model before exposing the seek
/// controls found in ppy/osu's ReplayPlayer.cs at commit
/// 83b8a64bec19e1463353645c2d6d10c75e275b43 (MIT). Yokko uses a local
/// reset-and-fast-forward pass to keep judgement state independent from UI.
/// </summary>
internal static class GameplayReplayStateRebuilder
{
    public static GameplayReplayRestoredState Rebuild(
        YokkoBeatmap beatmap,
        GameplayReplay replay,
        ManiaModSet mods,
        JudgementWindows windows,
        JudgementConfiguration judgementConfiguration,
        bool minesEnabled,
        double targetGameplayTimeMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        ArgumentNullException.ThrowIfNull(replay);
        ArgumentNullException.ThrowIfNull(mods);
        ArgumentNullException.ThrowIfNull(windows);
        if (!double.IsFinite(targetGameplayTimeMilliseconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetGameplayTimeMilliseconds));
        }

        var judgementState = new BeatmapJudgementState(
            beatmap,
            windows,
            mods.Contains(ManiaModId.NoRelease),
            mods.ScoreMultiplier,
            minesEnabled);
        var healthState = new ManiaHealthState(
            beatmap,
            mods,
            judgementConfiguration);
        ManiaAdaptiveSpeedState adaptiveSpeedState = mods.HasAdaptiveSpeed
            ? new ManiaAdaptiveSpeedState(
                beatmap,
                mods.AdaptiveInitialRate)
            : null;
        var timeline = new GameplayReplayTimeline(replay.Frames);
        var pressedLanes = new bool[(int)beatmap.KeyMode];
        var events = new List<JudgementEvent>(16);
        double previousSimulationTime = double.NaN;

        while (timeline.MoveNext(
                   targetGameplayTimeMilliseconds,
                   out GameplayReplayFrame frame))
        {
            advanceAdaptiveSpeed(
                adaptiveSpeedState,
                ref previousSimulationTime,
                Math.BitDecrement(frame.TimeMilliseconds));
            collectPassiveJudgements(
                judgementState,
                healthState,
                adaptiveSpeedState,
                pressedLanes,
                Math.BitDecrement(frame.TimeMilliseconds),
                events);
            advanceAdaptiveSpeed(
                adaptiveSpeedState,
                ref previousSimulationTime,
                frame.TimeMilliseconds);

            ulong previousLanes = pressedMask(pressedLanes);
            ulong changedLanes = previousLanes ^ frame.PressedLanes;
            for (int lane = 0; lane < pressedLanes.Length; lane++)
            {
                ulong laneMask = 1UL << lane;
                if ((changedLanes & laneMask) == 0)
                    continue;

                events.Clear();
                bool pressed = (frame.PressedLanes & laneMask) != 0;
                pressedLanes[lane] = pressed;
                if (pressed)
                {
                    judgementState.JudgeLanePress(
                        lane,
                        frame.TimeMilliseconds,
                        events);
                }
                else
                {
                    judgementState.JudgeLaneRelease(
                        lane,
                        frame.TimeMilliseconds,
                        events);
                }

                applyEvents(
                    events,
                    judgementState,
                    healthState,
                    adaptiveSpeedState);
            }
        }

        advanceAdaptiveSpeed(
            adaptiveSpeedState,
            ref previousSimulationTime,
            targetGameplayTimeMilliseconds);
        collectPassiveJudgements(
            judgementState,
            healthState,
            adaptiveSpeedState,
            pressedLanes,
            targetGameplayTimeMilliseconds,
            events);

        return new GameplayReplayRestoredState(
            timeline,
            judgementState,
            healthState,
            adaptiveSpeedState,
            pressedLanes);
    }

    private static void collectPassiveJudgements(
        BeatmapJudgementState judgementState,
        ManiaHealthState healthState,
        ManiaAdaptiveSpeedState adaptiveSpeedState,
        IReadOnlyList<bool> pressedLanes,
        double gameplayTimeMilliseconds,
        List<JudgementEvent> events)
    {
        events.Clear();
        judgementState.CollectMineJudgements(
            gameplayTimeMilliseconds,
            pressedLanes,
            events);
        judgementState.CollectExpiredMisses(
            gameplayTimeMilliseconds,
            events);
        applyEvents(
            events,
            judgementState,
            healthState,
            adaptiveSpeedState);
    }

    private static void applyEvents(
        IReadOnlyList<JudgementEvent> events,
        BeatmapJudgementState judgementState,
        ManiaHealthState healthState,
        ManiaAdaptiveSpeedState adaptiveSpeedState)
    {
        foreach (JudgementEvent judgement in events)
        {
            adaptiveSpeedState?.Apply(judgement);
            healthState.Apply(
                judgement,
                judgementState.Accuracy,
                judgementState.MaximumAchievableAccuracy);
        }
    }

    private static void advanceAdaptiveSpeed(
        ManiaAdaptiveSpeedState adaptiveSpeedState,
        ref double previousGameplayTime,
        double gameplayTime)
    {
        if (adaptiveSpeedState != null
            && double.IsFinite(previousGameplayTime)
            && gameplayTime > previousGameplayTime)
        {
            adaptiveSpeedState.AdvanceByGameplayTime(
                gameplayTime - previousGameplayTime);
        }

        previousGameplayTime = gameplayTime;
    }

    private static ulong pressedMask(IReadOnlyList<bool> pressedLanes)
    {
        ulong mask = 0;
        for (int lane = 0; lane < pressedLanes.Count; lane++)
        {
            if (pressedLanes[lane])
                mask |= 1UL << lane;
        }

        return mask;
    }
}
