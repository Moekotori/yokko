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
- Mod score multipliers:
  `osu.Game.Rulesets.Mania/Scoring/ManiaScoreMultiplierCalculator.cs`
- fixed-rate Mod configuration and audio:
  `osu.Game/Rulesets/Mods/ModHalfTime.cs`, `ModDaycore.cs`,
  `ModDoubleTime.cs`, `ModNightcore.cs`, and `ModRateAdjust.cs`
- health:
  `osu.Game.Rulesets.Mania/Scoring/ManiaHealthProcessor.cs` and
  `osu.Game/Rulesets/Scoring/LegacyDrainingHealthProcessor.cs`
- fail conditions:
  `osu.Game.Rulesets.Mania/Mods/ManiaModPerfect.cs` and
  `osu.Game/Rulesets/Mods/ModSuddenDeath.cs`
- primary hold golden cases:
  `osu.Game.Rulesets.Mania.Tests/TestSceneHoldNoteInput.cs`
- legacy replay Mod conversion:
  `osu.Game.Rulesets.Mania/ManiaRuleset.cs` and
  `osu.Game.Rulesets.Mania.Tests/ManiaLegacyModConversionTest.cs`
- standard and legacy-Mania object conversion:
  `osu.Game.Rulesets.Mania/Beatmaps/ManiaBeatmapConverter.cs`,
  `Beatmaps/Patterns/Legacy/HitCirclePatternGenerator.cs`,
  `SliderPatternGenerator.cs`, `SpinnerPatternGenerator.cs`, and
  `PassThroughPatternGenerator.cs`
- hit-sample selection and hold sliding playback:
  `osu.Game/Rulesets/Objects/HitObject.cs`,
  `osu.Game.Rulesets.Mania/Objects/HoldNote.cs`, and
  `osu.Game.Rulesets.Mania/Objects/Drawables/DrawableHoldNote.cs`

Every path above is read from the pinned `ppy/osu` **lazer** repository and
commit. `LegacyDrainingHealthProcessor` is a class used by lazer's current
`ManiaHealthProcessor`; the word `Legacy` in its type name does not mean Yokko
is using the osu!stable client as its baseline.

The previous judgement code reference
`cb3d5da8b441afd8d2cf3e03ceebc6b027e2074d` has no changes in the listed
judgement/scoring paths relative to the pinned baseline.

## Current parity evidence

| Area | Evidence | Status |
| --- | --- | --- |
| Modern OD windows | `JudgementStateTest.LazerWindowsFollowOverallDifficulty` | Covered |
| Inclusive window boundaries | `JudgementStateTest.EveryLazerWindowBoundaryIsInclusive` | Covered |
| lazer Classic Mod, native Mania windows | `JudgementStateTest.ClassicUsesStableManiaWindowsUnlessScoreV2IsPresent` | Covered |
| lazer Classic Mod, converted-map windows | Core window test plus `TestClassicConvertedChartUsesLazerConvertWindows` | Covered |
| Fixed and dynamic rate window policy | `ManiaModSetTest.OnlyLazerManiaFixedRateModsScaleHitWindows` plus Wind Up gameplay test | Covered |
| Hold baseline scenarios | `LazerManiaJudgementParityTest` mirrors the upstream input and nearby-note golden cases | Covered |
| Hold release lenience | `JudgementStateTest.HoldReleaseUsesOnePointFiveTimesWindow` | Covered |
| Nearby-note forced miss | `JudgementStateTest.NearbyNoteCanForceEarlierHoldToMissLikeOrderedHitPolicy` | Covered |
| Zero-length hold | `JudgementStateTest.ZeroLengthHoldCanHitHeadAndTail` | Covered |
| Same-lane stack ordering | `LazerManiaJudgementParityTest.SameLaneStackOnlyHitsMostRecentObjectLikeLazer` | Covered |
| Overlapping same-lane holds | `LazerManiaJudgementParityTest.OverlappingSameLaneHoldsRespectLazerNoteLock` | Covered |
| Simultaneous hold/note maximum score | Two `LazerManiaJudgementParityTest` maximum-score cases | Covered |
| Default Mania score curve | `JudgementStateTest.MixedResultsUseLazerAccuracyWeightsAndComboCurve` | Covered |
| Mania SS rule | `JudgementStateTest.AllGreatsReceiveLazerSsRank` | Covered |
| Complete Mania rank matrix | Thirteen `LazerManiaJudgementParityTest` rank cases mirror `ManiaScoreProcessorTest` | Covered |
| Current Mod score multipliers | `ManiaScoreMultiplierParityTest` mirrors the complete current-score matrix from lazer's `ManiaScoreMultiplierTest` | Covered |
| Configurable HT/DC/DT/NC | Complete pinned-lazer speed/multiplier matrix, decoded frequency-duration test, Song Select/headless gameplay request tests | Covered |
| Health recovery multiplier | `ManiaHealthStateTest.ManiaHealthUsesLazerDrainAndRecoveryValues` ports lazer's iterative recovery calculation | Covered |
| Break-aware health simulation | `ManiaHealthStateTest.BreakPeriodsMatchLazerRecoverySimulation` plus editable osu round-trip coverage | Covered |
| Sudden Death | `ManiaHealthStateTest.SuddenDeathOnlyFailsOnComboBreakingResult` | Covered |
| Mania Perfect default and strict setting | Core default/strict Great-boundary tests plus `TestPerfectStrictSettingMatchesLazerGreatBoundary` | Covered |
| Easy lives, No Fail, Accuracy Challenge | Focused `ManiaHealthStateTest` cases | Covered |
| Standard and legacy-Mania object shape | 11 pinned-lazer golden beatmaps cover complex/repeated/zero-length sliders, spinners, legacy Mania sliders/spinners, and four full conversion maps; 2,564 objects match lane/start/end shape | Covered |
| Hit-sample payload and playback | Pinned-lazer conversion golden assertions plus `GameplayHitSampleResolverTest` cover banks, custom suffixes, object/node volumes, native-Mania layered-normal suppression, hold head/tail samples, and `sliderslide`/`sliderwhistle`; native callback tests cover looping, gain, wrap, and stop | Covered |
| Legacy `.osr` Mania Mods | Complete lazer legacy flag mapping/precedence corpus plus replay-owned `ManiaModSet` gameplay request test | Covered |
| Native `.ykr` replay identity | Versioned stable-key Mod configuration, exact beatmap fingerprint/key-count validation, atomic completed-play persistence, drag-and-drop restore, and focused Core/gameplay tests | Covered |
| Configurable Mod preferences | Per-Mod settings persist independently of active selection and survive config restart; corrupt/unknown data falls back safely | Covered |

## Remaining proof work

The following must still be closed before claiming complete ruleset parity:

1. Keep auditing chart conversion and Mod-specific gameplay outside the
   judgement/scoring/health surface covered by this document. A passing
   judgement parity gate is not yet a claim that every lazer Mania Mod and
   conversion detail is complete.

Latest change-focused gate: 81 judgement, conversion, and hit-sample cases pass
against pinned-lazer-derived expectations. A separate 20-case configuration,
preference, replay-identity, beatmap-fingerprint, and visibility-settings gate
also passes. The converter probe additionally matches all 2,564 objects from
11 pinned-lazer golden beatmaps. Native callback audio passes 1/1, and the
managed native-audio subset passes two cases with one hardware-only
exclusive-output case skipped when no compatible endpoint is available.

Default fallback sample waveforms are Yokko skin resources. The parity contract
here covers lazer's beatmap-owned sample identity, lookup, layering, timing,
volume, and looping behaviour; it does not require every skin to use lazer's
exact waveform assets.

Custom Yokko judgement behaviour must eventually live behind an explicit
ruleset or option. It must not silently modify the default lazer-parity path.
