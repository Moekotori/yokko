using System;
using System.Collections.Generic;
using System.Linq;
using Yokko.Core.Gameplay;
using Yokko.Core.Scoring;

namespace Yokko.Game.Gameplay;

internal sealed class GameplayPracticeSession
{
    private readonly List<ManiaScoreResult> results = new();

    public GameplayPracticePlan Plan { get; }
    public int CompletedIterations => results.Count;
    public int TotalIterations => Plan.Repetitions;
    public bool HasRemainingIterations =>
        CompletedIterations < TotalIterations;
    public IReadOnlyList<ManiaScoreResult> Results => results;

    public GameplayPracticeSession(GameplayPracticePlan plan)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
    }

    public void Record(ManiaScoreResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!HasRemainingIterations)
            return;
        results.Add(result);
    }

    public string Summary => results.Count == 0
        ? $"PRACTICE 0/{TotalIterations}"
        : $"PRACTICE {results.Count}/{TotalIterations}  ·  "
          + $"AVG ACC {results.Average(static result => result.Accuracy):P2}  ·  "
          + $"BEST {results.Max(static result => result.Accuracy):P2}";
}
