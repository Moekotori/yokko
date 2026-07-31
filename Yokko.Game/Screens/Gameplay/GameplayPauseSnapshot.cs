using System;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;

namespace Yokko.Game.Screens.Gameplay;

internal sealed record GameplayPauseSnapshot(
    double GameplayTimeMilliseconds,
    double TotalTimeMilliseconds,
    long Score,
    double Accuracy,
    int Combo,
    int MaxCombo,
    string Rank,
    int Perfect,
    int Great,
    int Good,
    int Ok,
    int Meh,
    int Miss,
    string DisplayedMods,
    int MissCombo = 0,
    int ComboBreaks = 0,
    int MaxMissCombo = 0,
    int PauseCount = 0)
{
    public JudgementConfiguration JudgementConfiguration { get; init; } =
        JudgementConfiguration.YokkoDefault;

    public static GameplayPauseSnapshot Capture(
        BeatmapJudgementState state,
        ManiaModSet mods,
        double gameplayTimeMilliseconds,
        double totalTimeMilliseconds,
        int pauseCount = 0)
    {
        ArgumentNullException.ThrowIfNull(state);
        mods ??= ManiaModSet.Empty;

        double total = Math.Max(
            Math.Max(0, totalTimeMilliseconds),
            Math.Max(0, gameplayTimeMilliseconds));
        double current = Math.Clamp(
            gameplayTimeMilliseconds,
            0,
            total);
        string displayedMods = mods.IsEmpty
            ? "NM"
            : string.Join("  ", mods.DisplayLabels);
        if (state.Windows.Configuration.Mode == JudgementMode.Etterna)
        {
            displayedMods +=
                $"  ·  ETTERNA "
                + state.Windows.Configuration.EtternaJusticeLabel
                    .ToUpperInvariant();
        }

        return new GameplayPauseSnapshot(
            current,
            total,
            state.Score,
            state.Accuracy,
            state.Combo,
            state.MaxCombo,
            state.Windows.Configuration.Mode == JudgementMode.Etterna
                ? EtternaScoringRules.GradeLabel(state.Accuracy)
                : mods.AdjustRank(state.Rank).ToDisplayLabel(),
            state.Counts.Perfect,
            state.Counts.Great,
            state.Counts.Good,
            state.Counts.Ok,
            state.Counts.Meh,
            state.Counts.Miss,
            displayedMods,
            state.MissCombo,
            state.ComboBreaks,
            state.MaxMissCombo,
            Math.Max(0, pauseCount))
        {
            JudgementConfiguration = state.Windows.Configuration,
        };
    }
}
