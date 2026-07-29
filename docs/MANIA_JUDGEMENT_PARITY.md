# Yokko Mania Judgement Parity

## Contract

Before Yokko introduces custom judgement rules, the default ruleset must match
osu!lazer Mania for the same beatmap, active Mods, and ordered chart-time input
edges.

Parity means the same:

- hit-window boundaries;
- tap, hold-head, hold-tail, hold-body, and parent results;
- note-lock and forced-miss behaviour;
- combo, accuracy, rank, and unmodified 1,000,000-point score.

Yokko's Raw Input, SDL fallback, native audio clock, and rendering lifecycle do
not need to share lazer's implementation. They must produce the chart-time input
edges consumed by the parity-tested Core rules without changing those rules.

## Pinned upstream baseline

- repository: `ppy/osu`
- commit: `9f227ed28b6c8ba46dfea1f000f778d8b2827ad0`
- licence: MIT
- hit windows:
  `osu.Game.Rulesets.Mania/Scoring/ManiaHitWindows.cs`
- tap and hold behaviour:
  `osu.Game.Rulesets.Mania/Objects/Drawables/DrawableNote.cs`,
  `DrawableHoldNote.cs`, and `DrawableHoldNoteTail.cs`
- note lock: `osu.Game.Rulesets.Mania/UI/OrderedHitPolicy.cs`
- scoring:
  `osu.Game.Rulesets.Mania/Scoring/ManiaScoreProcessor.cs` and
  `osu.Game/Rulesets/Scoring/ScoreProcessor.cs`
- primary hold golden cases:
  `osu.Game.Rulesets.Mania.Tests/TestSceneHoldNoteInput.cs`

The previous judgement code reference
`cb3d5da8b441afd8d2cf3e03ceebc6b027e2074d` has no changes in the listed
judgement/scoring paths relative to the pinned baseline.

## Current parity evidence

| Area | Evidence | Status |
| --- | --- | --- |
| Modern OD windows | `JudgementStateTest.LazerWindowsFollowOverallDifficulty` | Covered |
| Inclusive window boundaries | `JudgementStateTest.EveryLazerWindowBoundaryIsInclusive` | Covered |
| Classic native Mania windows | `JudgementStateTest.ClassicUsesStableManiaWindowsUnlessScoreV2IsPresent` | Covered |
| Classic converted-map windows | Core window test plus `TestClassicConvertedChartUsesLazerConvertWindows` | Covered |
| Fixed and dynamic rate window policy | `ManiaModSetTest.OnlyLazerManiaFixedRateModsScaleHitWindows` plus Wind Up gameplay test | Covered |
| Hold baseline scenarios | `LazerManiaJudgementParityTest` mirrors the upstream input and nearby-note golden cases | Covered |
| Hold release lenience | `JudgementStateTest.HoldReleaseUsesOnePointFiveTimesWindow` | Covered |
| Nearby-note forced miss | `JudgementStateTest.NearbyNoteCanForceEarlierHoldToMissLikeOrderedHitPolicy` | Covered |
| Zero-length hold | `JudgementStateTest.ZeroLengthHoldCanHitHeadAndTail` | Covered |
| Same-lane stack ordering | `LazerManiaJudgementParityTest.SameLaneStackOnlyHitsMostRecentObjectLikeLazer` | Covered |
| Simultaneous hold/note maximum score | Two `LazerManiaJudgementParityTest` maximum-score cases | Covered |
| Default Mania score curve | `JudgementStateTest.MixedResultsUseLazerAccuracyWeightsAndComboCurve` | Covered |
| Mania SS rule | `JudgementStateTest.AllGreatsReceiveLazerSsRank` | Covered |

## Remaining proof work

The following must be closed before claiming complete ruleset parity:

1. Add overlapping same-lane hold cases beyond the upstream nearby-note
   fixtures to prove deterministic event ordering.
2. Expand score golden cases to cover all upstream Mania rank inputs and
   simultaneous maximum-score ordering.
3. Audit judgement-driven health and fail conditions separately; they consume
   judgement events but are not part of the raw judgement-state equivalence
   proven above.
4. Run the complete focused Core parity group and the related headless gameplay
   integration cases after each parity change.

Custom Yokko judgement behaviour must eventually live behind an explicit
ruleset or option. It must not silently modify the default lazer-parity path.
