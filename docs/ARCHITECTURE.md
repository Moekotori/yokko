# Yokko Architecture

## Goals

- 4K and 7K vertical-scrolling rhythm gameplay.
- Frame-rate independent judgement using audio-clock time.
- Renderer designed for very high frame rates with no per-frame gameplay allocations.
- Import boundaries for osu!mania, Malody, BMS, and LR2-style BMS folders.
- Yokko-owned audio backends: event-driven WASAPI exclusive with shared fallback,
  followed by ASIO.

## Layering

`Yokko.Game` is allowed to know about osu!framework. It should not parse chart files or talk directly to low-level audio devices.

## Runtime Threading

Desktop builds explicitly use osu!framework's multi-threaded execution mode:
input, scene update, draw, and audio work remain on framework-owned threads.
Update publishes draw-node snapshots instead of making gameplay wait for
presentation.

Active draw and update rates follow the selected refresh-rate multiplier, with
both capped at 1000 Hz. The user-facing unlimited mode uses this same safe
ceiling; a genuinely uncapped loop is reserved for dedicated benchmarking.
Judgement remains driven by timestamped input and the audio clock, so raising
the update rate beyond the input ceiling cannot improve scoring accuracy.

Gameplay follows lazer's lifetime-managed hit object direction. Note models
remain available for judgement and seeking, while preloaded note drawables are
attached to the live scene graph only inside the current visibility window.
Forward seeks and rewinds reuse those loaded drawables without loading assets
on the gameplay hot path.
This is adapted from lazer's
`PooledDrawableWithLifetimeContainer` at osu commit
`9f227ed28b6c8ba46dfea1f000f778d8b2827ad0` and osu!framework's
`LifetimeManagementContainer` at framework commit
`21e409540e8a7c9cd6739c05aa6c1e6257bd2b29`; Yokko currently reuses
preloaded drawables but does not yet pool and reset them.

Tap notes are selected with a binary search over their integrated scroll
positions. Hold-note paths use an immutable interval index with cached subtree
maximums, allowing each frame to prune both past and future ranges instead of
walking every hold before the current time. This preserves reversed-SV path
extrema and follows osu!framework's `LifetimeManagementContainer` goal of
making typical update cost track the alive set rather than the complete chart.

`Yokko.Core` contains portable gameplay data and timing rules. This layer should stay free of rendering and platform dependencies.

`Yokko.Audio` owns device discovery, playback start/stop, latency reporting, and the authoritative playback clock.
It may use codec libraries on decoder worker threads, but output, buffering,
device access, and clock truth must not depend on osu!framework.

The production low-latency path is implemented behind a stable native C ABI.
The native output callback owns the hardware-facing clock and never calls
managed code. See [Native Audio](NATIVE_AUDIO.md) for real-time invariants,
clock truth, fallback policy, and backend rollout gates.

`Yokko.Import` converts outside formats into `Yokko.Core` beatmaps. Format-specific quirks stay in this layer.

## Difficulty Rating

`Yokko.Core.Difficulty.ManiaMsdCalculator` adapts the canonical
`YokkoBeatmap` model to Etterna MinaCalc v515. Yokko vendors Etterna's official
`src/Etterna/MinaCalc` source from commit
`b65660062ef2a23121e331c36e23c23a8f6eafaa` and compiles it behind a small
native C ABI. The algorithm and its parameters remain Etterna-owned; Yokko only
converts its format-independent lane chart into Etterna `NoteInfo` rows.

The adapter mirrors Etterna `NoteData::SerializeNoteData2`: tap and hold heads
sharing the same timestamp become one row bitmask, the first row is normalized
to zero seconds, and hold tails, mines and sample-only objects do not enter
MinaCalc. Imported osu!mania, Quaver, Malody, Etterna and BMS charts therefore
share one MSD path after conversion.

Each successful result contains Overall plus Stream, Jumpstream, Handstream,
Stamina, JackSpeed, Chordjack and Technical values. Song select displays
Overall as `ETTERNA MSD` and names the dominant non-Overall skillset; gameplay
rate readouts display the recalculated Overall MSD. The calculator accepts an
explicit playback rate, so `1.5` and `0.75` use MinaCalc's own rate scaling.
Dynamic-rate gameplay samples the same calculator at the rounded live rate on a
worker task.

Import-time results persist at
`Beatmaps/.yokko-cache/etterna-msd.json`. Cache keys include the exact float row
times, row masks, key count, playback rate, adapter revision, MinaCalc version
and pinned upstream commit. Missing, stale or corrupt entries are recalculated.
Unsupported input and unavailable/incompatible native libraries return a
structured failure and display `--`; Yokko never substitutes Overall
Difficulty or another rating while labelling it MSD.

Users can choose the displayed difficulty system under Display settings.
Etterna MSD remains the default; Rebirth stars use the retained
StarRatingRebirth adapter and its separate
`Beatmaps/.yokko-cache/star-ratings.json` cache. Song selection, difficulty
sorting, details, gameplay rate overlays and the live HUD all read the same
persisted selection.

## Beat Timing

`YokkoTimingPoint` preserves the timing-point fields required for osu!mania round trips, including inherited points. `BeatTimingMap` uses uninherited positive beat lengths as the authoritative beat grid and converts editor rows to song time through the active timing segment.

The editor must not assume a fixed number of milliseconds per row. New notes are placed using the active timing point and the selected beat divisor.

## Scroll Velocity

`YokkoScrollVelocity` is separate from beat timing: it maps song time onto a
piecewise-linear visual distance axis through `ScrollVelocityMap`. Multipliers
may be positive, zero, or negative, so gameplay must position notes from
integrated distance rather than the active multiplier alone.

osu!mania import combines the inherited timing-point multiplier with the
current BPM relative to the chart's duration-weighted common BPM. Quaver
import normalizes both legacy BPM-relative SV and
`BPMDoesNotAffectScrollVelocity` charts before they enter `Yokko.Core`.
Quaver scroll-speed factors remain a separate, linearly interpolated current
speed scale. `$Global` signals are merged into the default and every custom
timing group, while each hit object selects its named `YokkoScrollProfile`.

Gameplay keeps one integrated map per profile and supports positive, stopped,
and reversed travel. A zero multiplier freezes the visual-distance axis while
retaining the last non-zero direction; that direction also controls the
orientation of a long-note tail. Hold-note bounds include every
direction-change extremum, so a long note crossing zero or negative SV is not
culled from only its two endpoints. Position extrema use an indexed range
query rather than scanning every SV on every hold update. The editor signal
strip exposes the default SV track and reports SV, SSF, and timing-group
counts; importing and editing a chart preserves all profiles even though Yokko
does not yet provide authoring controls for them.

osu!mania export is lossless when its original timing points still describe
the current profile. Yokko can synthesize positive inherited timing points,
but fails closed for zero/negative velocity, Quaver SSF, and per-note timing
groups because the osu!mania timing-point format cannot represent those
features faithfully.

The integration model and format conversion were adapted from:

- `ppy/osu`, `osu.Game/Rulesets/UI/Scrolling/Algorithms/SequentialScrollAlgorithm.cs`
  and `osu.Game/Rulesets/Timing/MultiplierControlPoint.cs`, commit
  `cb3d5da8b441afd8d2cf3e03ceebc6b027e2074d`;
- `Quaver/Quaver`,
  `Quaver.Shared/Screens/Gameplay/Rulesets/Keys/HitObjects/ScrollGroupControllerKeys.cs`
  and `ScrollNoteController.cs`, commit
  `bcc32673ca86349993cd7cf37a062ebc668972ea`;
- `Quaver/Quaver.API`, `Quaver.API/Maps/Qua.cs` (`NormalizeSVs`), commit
  `a921d561b2ece7f6bf3682446696c06c17b81649`.

## Hold Judgement

Hold notes have two scored phases:

- the head is judged on key press;
- the tail is judged on key release.

The hold remains active between those phases. Releasing outside the tail window or holding beyond it resolves the tail as a miss. A hold is complete only after both phases resolve.

## Timing Contract

The gameplay path should calculate hit error as:

```text
input gameplay time = presented audio time - input event age + user offset
hit error = input gameplay time - object time
```

Keyboard edges are timestamped from the SDL window callback before
osu!framework drains them on the update thread. When gameplay consumes an
edge, its monotonic timestamp is correlated with the presented audio clock at
the time of observation. Update-thread delay is therefore removed from the
judgement time instead of being mistaken for player error.

On Windows, both the managed monotonic timestamp and native WASAPI clock
correlation use QPC. A missing timestamp falls back to the observed gameplay
clock for that edge; it never changes the authoritative audio clock mid-song.
The WASAPI clock is already the presented audio position. Reported device
latency remains telemetry and is not subtracted from judgement time again.

The desktop host registers a foreground-only Raw Input keyboard target on the
osu!framework window and records QPC at `WM_INPUT`. Raw Input does not disable
legacy keyboard messages, so framework navigation and text input remain
unchanged. Gameplay consumes the earlier Raw Input timestamp by resolved key;
the ordered Raw Input edge queue is drained before expired misses are
collected. If Raw Input cannot attach when the host starts, the whole gameplay
session uses SDL callback timestamps instead, avoiding two timestamp queues
that could drift out of alignment.
When the host loses focus, gameplay enters pause and ends timestamp capture
before a foreground-only key release can be lost. Resuming starts a fresh
capture session, preventing a held lane from becoming stuck after Alt+Tab.

The gameplay diagnostic reports input-event age as rolling p50/p95/p99 values
and identifies whether samples came from Raw Input or the SDL fallback.
Captured, pending, and dropped edge counts are retained for the whole session,
so queue loss cannot fail silently. This is queue age from platform capture to
gameplay consumption, not total keyboard-to-photon latency.

Frame time is only for presentation. A dropped frame may look bad, but it must not shift judgement.
