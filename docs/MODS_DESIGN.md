# Yokko Mania Mods Design

## Goal and parity baseline

Yokko's first Mods target is behavioural parity with the osu!lazer
`osu!mania` ruleset, not parity with every osu! ruleset and not a visual clone
of osu!'s selection screen.

The baseline is pinned so "parity" does not move during implementation:

- upstream: `ppy/osu`
- commit: `9f227ed28b6c8ba46dfea1f000f778d8b2827ad0`
- source of the category list:
  `osu.Game.Rulesets.Mania/ManiaRuleset.cs`
- upstream licence: MIT

The target catalogue contains 39 user-selectable entries plus the import-only
SV2 system marker:

| Category | Target entries |
| --- | --- |
| Difficulty reduction | EZ, NF, HT, DC, NR |
| Difficulty increase | HR, SD, PF, DT, NC, FI, HD, CO, FL, AC |
| Conversion | RD, DS, MR, DA, CL, IN, CS, HO, 1K through 10K |
| Automation | AT, CN |
| Fun | WU, WD, MU, AS |
| System | SV2 |

`OsuManiaModParityCatalog` records this target in `Yokko.Core`. It is
deliberately not a runtime registry. An entry in the target catalogue must not
appear selectable until its complete behaviour is registered and tested.

## Selectable runtime entries

The currently selectable implementation slices are:

- fixed rate: configurable HT, DC, DT and NC across their upstream ranges;
- conversion: MR, seeded RD, HO, IN, CL, CS, configurable DA, DS and
  1K through 10K;
- difficulty reduction: EZ, NF and NR;
- difficulty increase: HR, SD, PF and configurable AC;
- visibility: FI and HD plus configurable CO and FL across their upstream
  ranges and options;
- automation: AT and CN;
- audio: configurable MU;
- dynamic rate: configurable WU, WD and AS.

RD exposes lazer's signed 32-bit custom seed plus an explicit reroll action and
stores the resolved seed in the canonical Mod fingerprint. Retry and persisted
replay playback therefore reproduce the same global lane permutation.
FI/HD use the lazer-style dynamic 160–400/768 note cover. CO exposes lazer's
20%–80% coverage and both expansion directions, while FL exposes its 0.5×–3×
window multiplier and optional combo-based shrinking; their defaults remain
50% and a full-width 50-unit rectangular window respectively. These Mods also
adjust top ranks to SSH/SH.
The gameplay health state follows Mania HP drain values per judgement. EZ
halves HP difficulty, widens hit windows by 1.4x and restores full health for
two extra lives; NF suppresses failure, while SD and PF use judgement-driven
fail conditions. PF defaults to accepting Great or better and exposes lazer's
optional `Require perfect hits` setting; the setting is included in canonical
score identity and survives compatible Mod changes.
The final displayed and persisted score applies lazer's current Mania Mod
multiplier matrix after rounding the score-without-Mods. This includes 0.5x
for EZ/NF/WU/WD/AS, lazer's rate-rounded 0.1x–0.5x matrix for HT/DC, and 0.9x
for NR/CS/HO/key Mods, with multiplicative combinations.
HR multiplies Mania hit-window difficulty by 1.4 and raises HP drain by 1.4
up to 10. AC defaults to a 90% maximum-achievable-accuracy target and stores
its 60.0%–99.9% threshold and accuracy mode in the canonical fingerprint.
DA independently overrides HP and OD, supports lazer's extended HP 0–11 and
Mania OD -15–15 limits, and remains incompatible with EZ/HR. CS switches the
playfield to a constant time-based scroll and ignores BPM, SV, timing-group,
and scroll-factor visual speed changes without changing judgement timing.
IN replaces each lane's consecutive object locations with shortened holds,
matching lazer's beat-length-aware gap rule. CN uses the same deterministic
auto replay as AT while hiding the playfield, HUD, and judgement presentation.
MU independently fades music, keysounds, and its generated metronome over
500 ms. It supports normal or inverse muting, a 0–500 combo fade length,
optional metronome, and optional keysound muting; all settings are included in
the canonical Mod fingerprint.
CL switches native Mania charts to the stable-era timing windows (including
the fixed 16.5 ms MAX window). When the import-only SV2 marker is also present,
modern lazer windows take precedence, matching upstream. SV2 is intentionally
not exposed in Song Select because upstream marks it as non-user-playable.
WU and WD linearly ramp between configurable 0.5×–2.0× endpoints and reach
the final rate at 75% of the playable timeline. Music tempo/pitch, callback-side
keysounds, frame/audio clocks, BPM/length display, and Etterna MSD input all
follow the live rate. The canonical fingerprint stores both endpoints and the
pitch mode.
AS starts at a configurable 0.5×–2.0× rate, derives its next target from the
last eight accuracy-affecting results, slows misses by 5%, and damps toward the
target with lazer's 50 ms half-time. Its live rate drives the same authoritative
audio, keysound, input, frame-clock and HUD paths as WU/WD. Initial rate and
pitch mode are part of the canonical fingerprint.
HT/DC expose lazer's 0.50×–0.99× range and DT/NC expose 1.01×–2.00×, all at
0.01× precision. HT/DT optionally scale pitch with the configured rate. DC/NC
instead keep lazer's fixed 0.75×/1.50× frequency and vary tempo independently,
including in Song Select preview and gameplay; keysounds follow the configured
total speed. Non-default speed and pitch settings are part of score identity.
Gameplay replays now own their exact `ManiaModSet` rather than relying on the
caller to reconstruct it. Native `.ykr` replays persist the versioned Mod
configuration, exact beatmap fingerprint, applied key count, and ordered input
edges; completed live plays are saved atomically and can be opened by drag and
drop after process restart. Imported legacy `.osr` flags continue to follow
lazer's Mania mapping and NC/DT, PF/SD, and CN/AT precedence before gameplay
starts.
Configurable Mod preferences are persisted globally by stable Mod key and are
restored when that Mod is next enabled. Active Mod selection remains
session-owned, so a new play still starts at NM unless the player selects Mods.
Mode 0 osu!standard imports retain their original positioned circles, sliders,
and spinners as a conversion source. The default target column count follows
lazer's special-object-density, CS and OD rules. 1K–10K regenerate from that
source, remain mutually exclusive, and do not alter native Mania charts. DS
regenerates two stages and supports 2–20 total lanes; dual-stage input uses
lazer's QWER/IOP and SDFG/JKL families, including the special 10K centre
expansion. Song Select exposes these controls in a dedicated KEY page and
states when the selected chart is already Mania-native.

## What "implemented" means

A Mod is complete only when all affected contracts are covered:

1. selection, configuration, incompatibilities, and deterministic defaults;
2. beatmap, judgement, audio, presentation, or automation behaviour;
3. adjusted Etterna MSD and score multiplier where applicable;
4. replay identity, including random seed and configurable values;
5. score identity and result-screen display;
6. retry and replay playback preserving the exact same configuration;
7. focused Core tests and at least one gameplay integration test.

Showing an acronym in Song Select is not implementation.

## Current Yokko gaps

The current codebase already has strong foundations:

- exact pinned-osu!lazer Mania hit windows, including lazer's optional Classic
  Mod path;
- default non-classic 1,000,000-point Mania scoring;
- audio-clock-driven judgement;
- deterministic tap and hold judgement;
- playback-rate-aware Etterna MSD calculation;
- a canonical, format-independent `YokkoBeatmap`.

The previously explicit persistence blockers are now closed:

- native `.ykr` replay persistence restores the exact configurable Mod state
  after process restart and rejects mismatched beatmaps or key counts;
- configurable Mod UI preferences are stored globally without silently
  restoring the previous active Mod selection;
- standard and legacy-Mania object shape matches 2,564 objects across 11
  pinned-lazer golden beatmaps, including full-map, complex/repeated slider,
  old Mania slider, and old Mania spinner cases;
- hit-sample and slider-node payloads, lazer lookup precedence, object volume,
  native-Mania layered-normal suppression, hold head/tail samples, and
  converted-slider looping samples are now implemented and focused-tested.
  Default fallback waveforms remain skin-owned rather than ruleset-owned.

No selectable entry should be described as fully parity-proven until its
affected contracts and configuration variants have focused upstream-derived
evidence.

## Ownership and module boundaries

### `Yokko.Core`

Owns all deterministic rules:

- stable Mod identifiers and target metadata;
- immutable Mod configuration values;
- compatibility validation and canonical ordering;
- resolved gameplay rules;
- pure beatmap transformations;
- hit-window, health, fail, and scoring policies;
- canonical configuration fingerprint used by scores and replays.

Core must not know about osu!framework drawables, platform audio devices, or
Song Select controls.

### `Yokko.Audio`

Owns rate and pitch capabilities:

- fixed and changing playback rate;
- pitch-preserving and pitch-shifting modes;
- presented audio clock after rate changes;
- rate-transition safety and capability reporting.

Gameplay time remains audio-owned. Mods must not create a second clock.

### `Yokko.Game`

Owns application composition and presentation:

- the Mod selection overlay;
- the runtime implementation registry;
- visibility effects and playfield covers;
- autoplay input production;
- passing one immutable session configuration through play, retry, replay,
  results, and score persistence.

### `Yokko.Import`

Owns legacy interoperability only:

- translating legacy osu! replay bit flags into Yokko Mod identifiers;
- reporting Mods Yokko cannot faithfully replay yet;
- exporting compatible legacy metadata where representable.

Import must not execute gameplay Mod behaviour.

## Session model

Gameplay should receive one immutable session value instead of independent
booleans:

```text
GameplaySession
├── original beatmap identity
├── applied beatmap
├── canonical Mod configuration
├── resolved rules
├── deterministic seed set
└── replay or live-input source
```

The original beatmap is never mutated. Conversion Mods produce an applied
beatmap for that session.

The current configuration envelope is versioned and uses stable string keys,
for example:

```json
{
  "schemaVersion": 1,
  "mods": [
    { "key": "random", "settings": { "seed": 123456 } },
    { "key": "cover", "settings": { "coverage": 0.5, "direction": "closing-in" } }
  ]
}
```

Numeric enum values must not be persisted because enum order can change.
Unknown schemas, duplicate entries, and unsupported keys fail closed rather
than being interpreted as a different Mod configuration.

## Deterministic application order

Mod effects are resolved once before gameplay in this order:

1. key-count and stage conversion;
2. structural conversion such as Hold Off or Invert;
3. lane mapping such as Random or Mirror;
4. fixed or variable track-rate policy;
5. judgement, health, fail, and scoring rules;
6. scroll and visibility presentation;
7. automation or replay input source.

This order is part of the replay contract. A later code refactor must not
silently change it.

Random uses one stored seed and one global column permutation for the complete
chart, matching the pinned upstream Mania implementation. Retry and replay use
the same seed.

## Compatibility

Compatibility is a Core rule, not a UI convention. The UI asks Core whether a
combination is valid and displays the returned reason.

At minimum the resolver must model these alternative families and the exact
upstream incompatibilities:

- slow rate: HT or DC;
- fast rate: DT or NC;
- fail condition: NF, SD, or PF;
- main visibility mode: FI, HD, or CO;
- automation: AT or CN;
- variable rate: WU, WD or AS;
- key conversion: exactly one of 1K through 10K;
- NR, HO, and IN restrictions around hold-note semantics.

The implementation should port incompatibility behaviour from the pinned
upstream types and add pairwise tests. It should not infer conflicts from
acronyms or category names.

## Delivery plan

### Stage 0: session and identity foundation

- add canonical Mod configuration and validation in `Yokko.Core`;
- add a runtime registry which only exposes fully implemented Mods;
- pass the session through play, retry, results, and replay playback;
- include Mod configuration in score storage and result display;
- replace the hard-coded Song Select HD/DT footer with registry-owned state.

This stage ships with no fake selectable Mods.

### Stage 1: deterministic rules and transformations

The deterministic conversion layer currently includes:

- MR and seeded RD;
- HO and NR;
- CS and supported visibility behaviour;
- DA for HP and Mania OD, including extended limits.

Each transformation operates on a copied beatmap and has pure Core tests.
Invert also clears break periods after replacing the chart with continuous
holds, matching lazer's `ManiaModInvert`.

### Stage 2: visibility and playfield behaviour

- FI, HD, CO, FL;
- IN after hold semantics are verified against upstream;
- configurable cover and flashlight values in the session fingerprint.

Visual tests must cover both built-in and imported osu!mania skins.

### Stage 3: health and fail contract

The Core-owned, judgement-driven Mania health state implements:

- NF;
- EZ, including its extra-life behaviour rather than only wider windows;
- SD and PF;
- HR;
- AC.

Health must be frame-rate independent and driven by judgement events.

### Stage 4: fixed and variable audio rate

The audio engine now carries an authoritative rated clock and explicit
tempo/frequency policy for:

- HT and DC;
- DT and NC;
- WU and WD;
- AS;
- MU.

The visual timeline, judgement time, Etterna MSD, replay, and audio clock must
all agree on the same effective rate. Pitch-changing and pitch-preserving
variants stay explicit.

### Stage 5: scoring variants and structural expansion

- CL and SV2;
- generalise Core and gameplay from the current 4K/7K enum to 1K through 10K;
- add 1K through 10K conversion;
- add DS after single-stage key modes are stable.

These structural items are now implemented. Converter object shape is covered
by the pinned lazer corpus. The hit-sample, slider-node, and sliding-sample
payload model is also implemented through Core, import, gameplay resolution,
and native callback-owned looping playback.

This is intentionally later because key-count conversion affects bindings,
skins, imports, playfield layout, replays, and score identity together.

### Stage 6: automation

- AT produces deterministic synthetic input through the normal judgement path;
- CN reuses the same automation while hiding gameplay UI as upstream defines;
- replay and autoplay remain distinct input-source types.

## First implementation slice

The first vertical slice should be:

1. Stage 0 session/identity foundation;
2. Mirror;
3. seeded Random;
4. Mod selection UI showing only MR and RD as available;
5. score and replay persistence of the selected Mod and RD seed.

MR and RD are the best first proof because they exercise the whole contract
without waiting for health or audio-rate infrastructure. They also force
deterministic retry/replay behaviour early, before more Mods depend on it.

## Validation gates

Use focused tests during each slice:

- catalogue and configuration tests;
- exhaustive incompatibility-pair tests;
- pure beatmap-transform tests for 4K and 7K;
- retry/replay seed preservation;
- score identity separating NM, MR, and seeded RD;
- one gameplay visual test per presentation Mod family.

Run the full desktop build and related gameplay tests when a slice changes
cross-module session, audio, scoring, or key-mode contracts.
