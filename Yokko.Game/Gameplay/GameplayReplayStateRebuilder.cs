using System;
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

        var simulator = new GameplayReplaySimulator(
            beatmap,
            replay,
            mods,
            windows,
            judgementConfiguration,
            minesEnabled);
        simulator.AdvanceTo(targetGameplayTimeMilliseconds);

        return new GameplayReplayRestoredState(
            simulator.Timeline,
            simulator.JudgementState,
            simulator.HealthState,
            simulator.AdaptiveSpeedState,
            simulator.PressedLanes);
    }
}
