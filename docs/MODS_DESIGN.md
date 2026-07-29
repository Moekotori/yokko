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

The target catalogue contains 40 selectable entries:

| Category | Target entries |
| --- | --- |
| Difficulty reduction | EZ, NF, HT, DC, NR |
| Difficulty increase | HR, SD, PF, DT, NC, FI, HD, CO, FL, AC |
| Conversion | RD, DS, MR, DA, CL, IN, CS, HO, 1K through 10K |
| Automation | AT, CN |
| Fun | WU, WD, MU, AD |
| System | SV2 |

`OsuManiaModParityCatalog` records this target in `Yokko.Core`. It is
deliberately not a runtime registry. An entry in the target catalogue must not
appear selectable until its complete behaviour is registered and tested.

## Implemented runtime entries

The currently selectable, behaviour-complete first slices are:

- fixed rate: HT, DC, DT and NC at their upstream default rates;
- conversion: MR, seeded RD and HO;
- difficulty reduction: NR;
- automation: AT.

RD stores its seed in the canonical Mod fingerprint. Retry and in-memory replay
playback therefore reproduce the same global lane permutation.

## What "implemented" means

A Mod is complete only when all affected contracts are covered:

1. selection, configuration, incompatibilities, and deterministic defaults;
2. beatmap, judgement, audio, presentation, or automation behaviour;
3. adjusted star rating and score multiplier where applicable;
4. replay identity, including random seed and configurable values;
5. score identity and result-screen display;
6. retry and replay playback preserving the exact same configuration;
7. focused Core tests and at least one gameplay integration test.

Showing an acronym in Song Select is not implementation.

## Current Yokko gaps

The current codebase already has strong foundations:

- osu!lazer-style Mania hit windows;
- default non-classic 1,000,000-point Mania scoring;
- audio-clock-driven judgement;
- deterministic tap and hold judgement;
- playback-rate-aware star-rating calculation;
- a canonical, format-independent `YokkoBeatmap`.

The remaining parity blockers are structural:

- `KeyMode` and gameplay bindings only support 4K and 7K;
- gameplay has no health, fail, extra-life, Sudden Death, or Perfect state;
- persisted replay files do not yet store a canonical Mod configuration;
- the playfield has no visibility-effect pipeline;
- Song Select still needs a full configuration overlay for adjustable Mods.

These blockers mean the 40 entries must not be enabled at once.

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

A future configuration envelope should be versioned and use stable string
keys, for example:

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
- variable rate: WU or WD, with AD handled by its upstream rate rules;
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

Start with Mods that do not require audio-rate or health infrastructure:

- MR and seeded RD;
- HO and NR;
- CS and supported visibility behaviour;
- DA where it only adjusts already-supported Mania accuracy rules.

Each transformation operates on a copied beatmap and has pure Core tests.

### Stage 2: visibility and playfield behaviour

- FI, HD, CO, FL;
- IN after hold semantics are verified against upstream;
- configurable cover and flashlight values in the session fingerprint.

Visual tests must cover both built-in and imported osu!mania skins.

### Stage 3: health and fail contract

Introduce one Core-owned Mania health state, then implement:

- NF;
- EZ, including its extra-life behaviour rather than only wider windows;
- HR;
- SD and PF;
- AC.

Health must be frame-rate independent and driven by judgement events.

### Stage 4: fixed and variable audio rate

Extend `IAudioEngine` with explicit capabilities and an authoritative rated
clock, then implement:

- HT and DC;
- DT and NC;
- WU and WD;
- AD;
- MU.

The visual timeline, judgement time, star rating, replay, and audio clock must
all agree on the same effective rate. Pitch-changing and pitch-preserving
variants stay explicit.

### Stage 5: scoring variants and structural expansion

- CL and SV2;
- generalise Core and gameplay from the current 4K/7K enum to 1K through 10K;
- add 1K through 10K conversion;
- add DS after single-stage key modes are stable.

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
