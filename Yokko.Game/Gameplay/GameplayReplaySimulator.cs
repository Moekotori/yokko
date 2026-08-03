using System;
using System.Collections.Generic;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;

namespace Yokko.Game.Gameplay;

/// <summary>
/// Incrementally advances Yokko's deterministic replay model. The seek
/// rebuilder and local ghost timeline share this implementation so score
/// comparison cannot drift from replay playback semantics.
/// </summary>
internal sealed class GameplayReplaySimulator
{
    private readonly List<JudgementEvent> events = new(16);
    private double previousSimulationTime = double.NaN;
    private double currentTime = double.NegativeInfinity;

    public GameplayReplayTimeline Timeline { get; }
    public BeatmapJudgementState JudgementState { get; }
    public ManiaHealthState HealthState { get; }
    public ManiaAdaptiveSpeedState AdaptiveSpeedState { get; }
    public bool[] PressedLanes { get; }
    public double? NextReplayFrameTime => Timeline.NextFrame?.TimeMilliseconds;

    public GameplayReplaySimulator(
        YokkoBeatmap beatmap,
        GameplayReplay replay,
        ManiaModSet mods,
        JudgementWindows windows,
        JudgementConfiguration judgementConfiguration,
        bool minesEnabled)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        ArgumentNullException.ThrowIfNull(replay);
        ArgumentNullException.ThrowIfNull(mods);
        ArgumentNullException.ThrowIfNull(windows);

        OsuStableScoreV1ModMultipliers stableScoreMods =
            judgementConfiguration.Mode == JudgementMode.OsuStable
                ? OsuStableScoreV1Mods.Calculate(beatmap, mods)
                : new OsuStableScoreV1ModMultipliers(
                    mods.ScoreMultiplier,
                    1);
        JudgementState = new BeatmapJudgementState(
            beatmap,
            windows,
            mods.Contains(ManiaModId.NoRelease),
            stableScoreMods.ScoreMultiplier,
            minesEnabled,
            stableScoreMods.BonusPunishmentDivider);
        HealthState = new ManiaHealthState(
            beatmap,
            mods,
            judgementConfiguration);
        AdaptiveSpeedState = mods.HasAdaptiveSpeed
            ? new ManiaAdaptiveSpeedState(beatmap, mods.AdaptiveInitialRate)
            : null;
        Timeline = new GameplayReplayTimeline(replay.Frames);
        PressedLanes = new bool[(int)beatmap.KeyMode];
    }

    public void AdvanceTo(double targetGameplayTimeMilliseconds)
    {
        if (!double.IsFinite(targetGameplayTimeMilliseconds))
            throw new ArgumentOutOfRangeException(
                nameof(targetGameplayTimeMilliseconds));
        if (targetGameplayTimeMilliseconds < currentTime)
        {
            throw new InvalidOperationException(
                "Replay simulation cannot move backwards.");
        }

        while (Timeline.MoveNext(
                   targetGameplayTimeMilliseconds,
                   out GameplayReplayFrame frame))
        {
            advanceAdaptiveSpeed(Math.BitDecrement(frame.TimeMilliseconds));
            collectPassiveJudgements(Math.BitDecrement(frame.TimeMilliseconds));
            advanceAdaptiveSpeed(frame.TimeMilliseconds);

            ulong previousLanes = pressedMask(PressedLanes);
            ulong changedLanes = previousLanes ^ frame.PressedLanes;
            for (int lane = 0; lane < PressedLanes.Length; lane++)
            {
                ulong laneMask = 1UL << lane;
                if ((changedLanes & laneMask) == 0)
                    continue;

                events.Clear();
                bool pressed = (frame.PressedLanes & laneMask) != 0;
                PressedLanes[lane] = pressed;
                if (pressed)
                {
                    JudgementState.JudgeLanePress(
                        lane,
                        frame.TimeMilliseconds,
                        events);
                }
                else
                {
                    JudgementState.JudgeLaneRelease(
                        lane,
                        frame.TimeMilliseconds,
                        events);
                }

                applyEvents();
            }
        }

        advanceAdaptiveSpeed(targetGameplayTimeMilliseconds);
        collectPassiveJudgements(targetGameplayTimeMilliseconds);
        currentTime = targetGameplayTimeMilliseconds;
    }

    private void collectPassiveJudgements(double gameplayTimeMilliseconds)
    {
        events.Clear();
        JudgementState.CollectMineJudgements(
            gameplayTimeMilliseconds,
            PressedLanes,
            events);
        JudgementState.CollectExpiredMisses(
            gameplayTimeMilliseconds,
            events);
        applyEvents();
    }

    private void applyEvents()
    {
        foreach (JudgementEvent judgement in events)
        {
            AdaptiveSpeedState?.Apply(judgement);
            HealthState.Apply(
                judgement,
                JudgementState.Accuracy,
                JudgementState.MaximumAchievableAccuracy);
        }
    }

    private void advanceAdaptiveSpeed(double gameplayTime)
    {
        if (AdaptiveSpeedState != null
            && double.IsFinite(previousSimulationTime)
            && gameplayTime > previousSimulationTime)
        {
            AdaptiveSpeedState.AdvanceByGameplayTime(
                gameplayTime - previousSimulationTime);
        }

        previousSimulationTime = gameplayTime;
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
